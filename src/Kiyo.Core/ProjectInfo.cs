namespace Kiyo.Core;

/// <summary>Where this project lives and which version of the original macOS app it is based on.</summary>
public static class ProjectInfo
{
    public const string ProjectUrl = "https://github.com/koveitan/kiyo-control-windows";

    /// <summary>The original macOS app this project is ported from.</summary>
    public const string UpstreamUrl = "https://github.com/f0ns/kiyo-control";

    /// <summary>
    /// The macOS version whose features this release matches. Update it (and the README line) when porting a new
    /// upstream release.
    /// </summary>
    public const string UpstreamVersion = "0.6.0";

    /// <summary>"based on macOS 0.6.0", for version labels.</summary>
    public static string BasedOn => $"based on macOS {UpstreamVersion}";
}
