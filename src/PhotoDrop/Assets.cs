using System.Collections.Concurrent;

namespace PhotoDrop;

static class Assets
{
    public const string Ico = "PhotoDrop.icon.ico";
    public const string AppleTouch = "PhotoDrop.apple-touch-icon.png";
    public const string Icon192 = "PhotoDrop.icon-192.png";
    public const string Icon512 = "PhotoDrop.icon-512.png";

    static readonly ConcurrentDictionary<string, byte[]> Cache = new();

    /// <summary>Raw bytes of an embedded asset, read once and kept.</summary>
    public static byte[]? Bytes(string resource)
    {
        if (Cache.TryGetValue(resource, out var cached)) return cached;

        try
        {
            using var stream = typeof(Assets).Assembly.GetManifestResourceStream(resource);
            if (stream is null) return null;

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            Cache[resource] = bytes;
            return bytes;
        }
        catch { return null; }
    }
}
