namespace Kiyo.Core;

/// <summary>
/// A resolution and frame rate the camera can stream. Each app that opens the camera picks its own,
/// so this only sets the app's preview (Synapse's resolution setting works the same way).
/// </summary>
public readonly record struct VideoMode(int Width, int Height, int Fps)
{
    public string Label => (Height >= 2160 ? "4K" : $"{Height}p") + $" {Fps}FPS";

    /// <summary>Each size and rate once, largest size first, then fastest.</summary>
    public static List<VideoMode> Options(IEnumerable<(int Width, int Height, double Fps)> formats) =>
        formats.Select(f => new VideoMode(f.Width, f.Height, (int)Math.Round(f.Fps)))
            .Distinct()
            .OrderByDescending(m => m.Width * m.Height).ThenByDescending(m => m.Fps)
            .ToList();

    /// <summary>The saved choice if the camera still offers it, else 1080p30 (or the largest size at 30 fps).</summary>
    public static VideoMode? Preferred(IReadOnlyList<VideoMode> options, VideoMode? saved)
    {
        if (saved is VideoMode s && options.Contains(s)) return s;
        // The preview is decoded on the CPU, so 1080p is a better default than 4K.
        var hd = options.Where(m => m.Fps == 30 && m.Height == 1080).Cast<VideoMode?>().FirstOrDefault();
        return hd ?? options.Where(m => m.Fps == 30).Cast<VideoMode?>().FirstOrDefault() ?? options.Cast<VideoMode?>().FirstOrDefault();
    }

    /// <summary>For settings: "1920x1080@30".</summary>
    public string Stored => $"{Width}x{Height}@{Fps}";

    public static VideoMode? Parse(string? stored)
    {
        if (stored is null) return null;
        var parts = stored.Split('x', '@');
        return parts.Length == 3 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && int.TryParse(parts[2], out var f)
            ? new VideoMode(w, h, f) : null;
    }
}
