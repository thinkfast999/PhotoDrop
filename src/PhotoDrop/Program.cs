using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using QRCoder;

namespace PhotoDrop;

static class Program
{
    public const string AppName = "PhotoDrop";

    /// <summary>Raised (off the UI thread) each time a file finishes saving.</summary>
    public static event Action<string>? FileSaved;

    [STAThread]   // the folder picker is COM, and COM wants an STA
    static void Main()
    {
        // One tray icon only - a second launch just points at the first.
        using var single = new Mutex(true, @"Local\PhotoDrop.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Dialogs.Info("PhotoDrop is already running.\n\nLook for the blue arrow icon in the "
                         + "system tray, next to the clock. You may need to click the ^ to see "
                         + "hidden icons.");
            return;
        }

        Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        var config = AppConfig.Load();
        var app = TryStart(config);
        if (app is null) return;

        using (var tray = new TrayApp(config))
        {
            tray.Run();   // blocks on the Win32 message loop until Exit
        }

        try { app.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult(); } catch { }
    }

    /// <summary>Starts the listener, or explains the problem and returns null.</summary>
    static WebApplication? TryStart(AppConfig config)
    {
        if (!EnsureFolder(config)) return null;

        var app = Build(config);
        try
        {
            app.StartAsync().GetAwaiter().GetResult();
            return app;
        }
        catch (Exception ex) when (Flatten(ex) is AddressInUseException)
        {
            if (Dialogs.AskYesNo(
                    $"Another program on this PC is already using port {config.Port}, "
                    + "so PhotoDrop can't start.\n\nThe port can be changed in PhotoDrop's "
                    + "settings file. Open it now?"))
            {
                OpenConfigFile();
            }
            return null;
        }
        catch (Exception ex)
        {
            Dialogs.Warn($"PhotoDrop couldn't start.\n\n{Flatten(ex).Message}");
            return null;
        }
    }

    /// <summary>Makes sure the save folder exists, offering a picker if it can't be used.</summary>
    static bool EnsureFolder(AppConfig config)
    {
        while (true)
        {
            try
            {
                Directory.CreateDirectory(config.SaveFolder);
                return true;
            }
            catch (Exception ex)
            {
                if (!Dialogs.AskYesNo($"PhotoDrop can't save photos here:\n\n{config.SaveFolder}"
                                      + $"\n\n{ex.Message}\n\nWould you like to pick a different "
                                      + "folder?"))
                {
                    return false;
                }

                var picked = Dialogs.PickFolder(config.SaveFolder);
                if (picked is null) return false;
                config.SaveFolder = picked;
                config.Save();
            }
        }
    }

    public static void OpenConfigFile()
    {
        try
        {
            if (!File.Exists(AppConfig.Path_)) AppConfig.Load();
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{AppConfig.Path_}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Dialogs.Warn($"Couldn't open the settings file.\n\n{ex.Message}");
        }
    }

    static Exception Flatten(Exception ex)
    {
        while (ex.InnerException is not null) ex = ex.InnerException;
        return ex;
    }

    const string CacheFor7Days = "public, max-age=604800";

    // Colours come from Theme so the home-screen tile can't drift from the page itself.
    const string Manifest = $$"""
        {
          "name": "PhotoDrop",
          "short_name": "PhotoDrop",
          "start_url": "/",
          "scope": "/",
          "display": "standalone",
          "background_color": "{{Theme.BgDark}}",
          "theme_color": "{{Theme.Accent}}",
          "icons": [
            { "src": "/icon-192.png", "sizes": "192x192", "type": "image/png" },
            { "src": "/icon-512.png", "sizes": "512x512", "type": "image/png" }
          ]
        }
        """;

    static WebApplication Build(AppConfig config)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenAnyIP(config.Port);
            o.Limits.MaxRequestBodySize = null;      // phone videos can be big
            o.Limits.MinRequestBodyDataRate = null;  // don't kill slow wifi uploads
        });

        var app = builder.Build();

        // --- the phone page ---------------------------------------------------------

        // Rendered per request, so changing the folder from the tray shows up right away.
        app.MapGet("/", () => Results.Content(
            Html.Page
                .Replace("__PIN_REQUIRED__", string.IsNullOrEmpty(config.Pin) ? "false" : "true")
                .Replace("__FOLDER__", WebUtility.HtmlEncode(config.SaveFolder)),
            "text/html; charset=utf-8"));

        // Icons for the browser tab and, more to the point, for "Add to Home Screen" -
        // without these a home-screen shortcut shows a blank tile.
        app.MapGet("/favicon.ico", (HttpResponse res) => Asset(res, Assets.Ico, "image/x-icon"));
        app.MapGet("/icon-192.png", (HttpResponse res) => Asset(res, Assets.Icon192, "image/png"));
        app.MapGet("/icon-512.png", (HttpResponse res) => Asset(res, Assets.Icon512, "image/png"));
        app.MapGet("/apple-touch-icon.png",
                   (HttpResponse res) => Asset(res, Assets.AppleTouch, "image/png"));
        // Older iOS looks for the -precomposed name first.
        app.MapGet("/apple-touch-icon-precomposed.png",
                   (HttpResponse res) => Asset(res, Assets.AppleTouch, "image/png"));

        app.MapGet("/manifest.webmanifest", (HttpResponse res) =>
        {
            res.Headers.CacheControl = CacheFor7Days;
            return Results.Content(Manifest, "application/manifest+json");
        });

        app.MapPost("/upload", async (HttpRequest req) =>
        {
            if (!Auth(req)) return Results.StatusCode(401);

            var raw = req.Headers["X-File-Name"].ToString();
            if (string.IsNullOrWhiteSpace(raw)) return Results.BadRequest("Missing X-File-Name.");

            var saved = await Storage.SaveAsync(req.Body, Uri.UnescapeDataString(raw), config);
            FileSaved?.Invoke(saved);
            return Results.Text($"{{\"file\":{Json(Path.GetFileName(saved))}}}", "application/json");
        });

        // Fallback for a plain form post (works with JavaScript disabled).
        app.MapPost("/upload-form", async (HttpRequest req) =>
        {
            if (!req.HasFormContentType) return Results.BadRequest("Expected a multipart form.");

            var boundary = HeaderUtilities.RemoveQuotes(
                MediaTypeHeaderValue.Parse(req.ContentType!).Boundary).Value;
            if (string.IsNullOrEmpty(boundary)) return Results.BadRequest("Missing multipart boundary.");

            var reader = new MultipartReader(boundary, req.Body);
            string? pin = null;
            var count = 0;

            for (var section = await reader.ReadNextSectionAsync();
                 section is not null;
                 section = await reader.ReadNextSectionAsync())
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd)) continue;

                if (cd.IsFileDisposition())
                {
                    if (!string.IsNullOrEmpty(config.Pin) && pin != config.Pin) return Results.StatusCode(401);
                    var name = HeaderUtilities.RemoveQuotes(cd.FileName).Value;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var saved = await Storage.SaveAsync(section.Body, name!, config);
                    FileSaved?.Invoke(saved);
                    count++;
                }
                else if (cd.IsFormDisposition() && cd.Name.Value == "pin")
                {
                    pin = (await new StreamReader(section.Body).ReadToEndAsync()).Trim();
                }
            }

            return Results.Content(
                Html.Receipt.Replace("__MSG__", $"Saved {count} {(count == 1 ? "photo" : "photos")}"),
                "text/html; charset=utf-8");
        });

        // --- the setup screen, for this PC only -------------------------------------

        app.MapGet("/setup", (HttpContext ctx) =>
        {
            if (!IsLocal(ctx)) return Results.NotFound();

            var addresses = Net.LocalAddresses();
            var state = $"{{\"port\":{config.Port},"
                        + $"\"addresses\":[{string.Join(",", addresses.Select(Json))}]}}";

            return Results.Content(SetupHtml.Page.Replace("__STATE__", state),
                                   "text/html; charset=utf-8");
        });

        app.MapGet("/setup-qr.png", (HttpContext ctx, string u) =>
        {
            if (!IsLocal(ctx)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(u) || u.Length > 200) return Results.BadRequest();

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(u, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(
                10, new byte[] { 20, 22, 26 }, new byte[] { 255, 255, 255 });
            return Results.Bytes(png, "image/png");
        });

        app.MapPost("/api/firewall", (HttpContext ctx) =>
        {
            if (!IsLocal(ctx)) return Results.NotFound();
            return Results.Text($"{{\"ok\":{(Firewall.Allow() ? "true" : "false")}}}",
                                "application/json");
        });

        bool Auth(HttpRequest req) =>
            string.IsNullOrEmpty(config.Pin) || req.Headers["X-Pin"].ToString() == config.Pin;

        return app;
    }

    /// <summary>Setup and control endpoints are for the PC running PhotoDrop, not the LAN.</summary>
    static bool IsLocal(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;
        return ip is not null && IPAddress.IsLoopback(ip);
    }

    static IResult Asset(HttpResponse res, string resource, string mime)
    {
        var bytes = Assets.Bytes(resource);
        if (bytes is null) return Results.NotFound();

        res.Headers.CacheControl = CacheFor7Days;
        return Results.Bytes(bytes, mime);
    }

    static string Json(string value) => $"\"{JsonEncodedText.Encode(value)}\"";
}

static class Storage
{
    static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<string> SaveAsync(Stream body, string requestedName, AppConfig config)
    {
        // Read from config on every upload so a folder change from the tray takes effect at once.
        var dir = config.OrganizeByDate
            ? Path.Combine(config.SaveFolder, DateTime.Now.ToString("yyyy-MM-dd"))
            : config.SaveFolder;
        Directory.CreateDirectory(dir);

        var final = await ReservePathAsync(dir, SafeName(requestedName));
        var temp = final + ".part";

        try
        {
            await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write,
                                                 FileShare.None, 1 << 16, useAsync: true))
            {
                await body.CopyToAsync(fs);
            }
            File.Move(temp, final, overwrite: true);
            return final;
        }
        catch
        {
            TryDelete(temp);
            TryDelete(final);   // drop the zero-byte reservation
            throw;
        }
    }

    // Creates a zero-byte placeholder so two phones uploading IMG_0001.jpg can't collide.
    static async Task<string> ReservePathAsync(string dir, string name)
    {
        await Gate.WaitAsync();
        try
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (var i = 0; ; i++)
            {
                var candidate = Path.Combine(dir, i == 0 ? name : $"{stem} ({i}){ext}");
                try
                {
                    new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None).Dispose();
                    return candidate;
                }
                catch (IOException) when (File.Exists(candidate)) { /* taken, try the next one */ }
            }
        }
        finally { Gate.Release(); }
    }

    static string SafeName(string raw)
    {
        var name = Path.GetFileName(raw.Replace('\\', '/').Trim());
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim('.', ' ');
        if (string.IsNullOrWhiteSpace(name)) name = $"upload-{DateTime.Now:HHmmss}";
        if (name.Length > 150) name = name[..150];
        return name;
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}

static class Net
{
    /// <summary>LAN addresses a phone could reach, best guess first.</summary>
    public static List<string> LocalAddresses()
    {
        var found = new List<string>();
        try
        {
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            probe.Connect("8.8.8.8", 65530);   // sends nothing; just picks the outbound interface
            if (probe.LocalEndPoint is IPEndPoint ep) found.Add(ep.Address.ToString());
        }
        catch { }

        try
        {
            foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    if (!found.Contains(ip.ToString())) found.Add(ip.ToString());
        }
        catch { }

        if (found.Count == 0) found.Add("localhost");
        return found;
    }
}
