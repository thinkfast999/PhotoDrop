using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Win32;

namespace PhotoDrop;

sealed class AppConfig
{
    /// <summary>Blank means "the Pictures folder on this PC", resolved at load time.</summary>
    public string SaveFolder { get; set; } = "";
    public int Port { get; set; } = 8080;
    public string Pin { get; set; } = "";
    public bool OrganizeByDate { get; set; }      // off by default: everything in one folder
    public bool Introduced { get; set; }          // has the first-run setup page been shown?

    public static string DefaultSaveFolder =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoDrop");

    /// <summary>~/.photodrop - the same place on Windows, macOS and Linux, so the settings
    /// survive moving, replacing or reinstalling the executable.</summary>
    public static string Folder { get; } = System.IO.Path.Combine(HomeDirectory(), ".photodrop");

    /// <summary>One file, where you can see it.</summary>
    public static string Path_ { get; } = System.IO.Path.Combine(Folder, "config.json");

    static string HomeDirectory()
    {
        // UserProfile is %USERPROFILE% on Windows and $HOME everywhere else.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home)) home = Environment.GetEnvironmentVariable("HOME") ?? "";
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        // No home at all (a service account, say) - beside the exe beats nowhere.
        return string.IsNullOrWhiteSpace(home) ? AppContext.BaseDirectory : home;
    }

    // Source-generated metadata: reflection-based JSON does not survive trimming.
    static readonly JsonSerializerOptions ReadOptions = new()
    {
        TypeInfoResolver = ConfigJson.Default,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,   // tolerate hand-edited files
        AllowTrailingCommas = true
    };

    static readonly JsonSerializerOptions WriteOptions = new()
    {
        TypeInfoResolver = ConfigJson.Default,
        WriteIndented = true
    };

    static JsonTypeInfo<AppConfig> ReadInfo =>
        (JsonTypeInfo<AppConfig>)ReadOptions.GetTypeInfo(typeof(AppConfig));

    static JsonTypeInfo<AppConfig> WriteInfo =>
        (JsonTypeInfo<AppConfig>)WriteOptions.GetTypeInfo(typeof(AppConfig));

    public static AppConfig Load()
    {
        AppConfig config;
        try
        {
            config = File.Exists(Path_)
                ? JsonSerializer.Deserialize(File.ReadAllText(Path_), ReadInfo) ?? new AppConfig()
                : new AppConfig();
        }
        catch
        {
            config = new AppConfig();   // unreadable or corrupt - fall back to defaults
        }

        // Lets a hand-written config say %USERPROFILE%\Pictures and stay portable.
        config.SaveFolder = Environment.ExpandEnvironmentVariables(config.SaveFolder ?? "");
        if (string.IsNullOrWhiteSpace(config.SaveFolder)) config.SaveFolder = DefaultSaveFolder;
        if (config.Port is < 1 or > 65535) config.Port = 8080;

        // Materialise the file on first run, but never rewrite a hand-edited one on startup.
        if (!File.Exists(Path_)) config.Save();
        return config;
    }

    static bool _warnedAboutSaving;

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path_, JsonSerializer.Serialize(this, WriteInfo));
        }
        catch (Exception ex)
        {
            // Say so rather than losing settings quietly - but only once per run.
            if (_warnedAboutSaving) return;
            _warnedAboutSaving = true;
            Dialogs.Warn($"PhotoDrop can't write its settings file:\n\n{Path_}\n\n{ex.Message}"
                         + "\n\nSettings won't be remembered. Check that you can "
                         + $"write to {Folder}.");
        }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool RunsAtLogin
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(Program.AppName) is not null;
            }
            catch { return false; }
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (key is null) return;
                if (value) key.SetValue(Program.AppName, $"\"{Environment.ProcessPath}\"");
                else key.DeleteValue(Program.AppName, throwOnMissingValue: false);
            }
            catch { /* locked-down machine; not worth bothering the user about */ }
        }
    }
}

[JsonSerializable(typeof(AppConfig))]
partial class ConfigJson : JsonSerializerContext;
