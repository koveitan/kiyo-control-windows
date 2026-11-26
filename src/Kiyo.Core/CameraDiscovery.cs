using System.Globalization;
using System.Text.RegularExpressions;

namespace Kiyo.Core;

/// <summary>
/// A USB camera found on the PC, supported or not. Lets the app explain why it can't connect
/// (no camera, a model it doesn't know yet, another brand) and pick one of several Kiyos.
/// </summary>
/// <param name="Id">Identifies this camera on its USB port (on Windows, the device path), so two cameras of
/// the same model can be told apart.</param>
public sealed record UsbCamera(string Name, ushort VendorId, ushort ProductId, string Id)
{
    public enum Kind { Supported, UnsupportedRazer, Other }

    public KiyoDevice? Device => KiyoDevice.Find(VendorId, ProductId);

    public Kind Status => Device is not null ? Kind.Supported
        : VendorId == KiyoDevice.RazerVendorId ? Kind.UnsupportedRazer
        : Kind.Other;

    /// <summary>USB ids as people write them, e.g. "1532:0E0C".</summary>
    public string UsbId => $"{VendorId:X4}:{ProductId:X4}";

    /// <summary>
    /// A camera from a Windows device path or instance id such as
    /// <c>\\?\usb#vid_1532&amp;pid_0e08&amp;mi_00#…</c>; null when the path names no USB vendor and product.
    /// </summary>
    public static UsbCamera? FromDevicePath(string name, string devicePath)
    {
        var m = Regex.Match(devicePath, @"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        return new UsbCamera(name,
            ushort.Parse(m.Groups[1].Value, NumberStyles.HexNumber),
            ushort.Parse(m.Groups[2].Value, NumberStyles.HexNumber),
            devicePath);
    }
}

public static class CameraDiscovery
{
    public const string ProjectUrl = "https://github.com/koveitan/kiyo-control-windows";

    /// <summary>The camera to connect to: the preferred one if it's plugged in and supported, otherwise the first supported one.</summary>
    public static UsbCamera? Choose(IEnumerable<UsbCamera> cameras, string? preferredId)
    {
        var supported = cameras.Where(c => c.Device is not null).ToList();
        return supported.FirstOrDefault(c => SameDevice(c.Id, preferredId)) ?? supported.FirstOrDefault();
    }

    /// <summary>
    /// True if two device paths name the same device. Windows lists the same camera under different interface
    /// classes (DirectShow vs. Media Foundation), so only the part before the interface GUID is compared.
    /// </summary>
    public static bool SameDevice(string? a, string? b) =>
        a is not null && b is not null && string.Equals(InstanceKey(a), InstanceKey(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>"\\?\usb#vid_1532&amp;pid_0e08&amp;mi_00#9&amp;1931aa6c&amp;0&amp;0000#{guid}\global" → "usb#vid_1532&amp;pid_0e08&amp;mi_00#9&amp;1931aa6c&amp;0&amp;0000".</summary>
    public static string InstanceKey(string devicePath)
    {
        var p = devicePath.StartsWith(@"\\?\", StringComparison.Ordinal) ? devicePath[4..] : devicePath;
        var end = p.IndexOf("#{", StringComparison.Ordinal);
        return (end < 0 ? p : p[..end]).Replace('\\', '#').ToLowerInvariant();
    }

    /// <summary>A prefilled GitHub issue asking for support of a camera, with the information needed to add it.</summary>
    public static string SupportRequestUrl(UsbCamera camera, int listNumber)
    {
        var title = $"Support {camera.Name} ({camera.UsbId})";
        var body = $"""
            Camera: {camera.Name}
            USB id: {camera.UsbId}

            Output of `kiyoctl cameras` and `kiyoctl probe {listNumber}` (kiyoctl.exe is in the app's folder,
            %LOCALAPPDATA%\KiyoControl\current):

            ```
            paste here
            ```
            """;
        return $"{ProjectUrl}/issues/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
    }
}
