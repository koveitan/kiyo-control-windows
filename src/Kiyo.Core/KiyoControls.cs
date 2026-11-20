namespace Kiyo.Core;

/// <summary>
/// The standard camera controls. Values keep the camera's UVC meaning where Windows passes them
/// through; auto exposure keeps the UVC values 1 = manual, 8 = auto so profiles read the same.
/// </summary>
public static class KiyoControls
{
    public const int ExposureManual = 1;
    public const int ExposureAuto = 8;

    // Camera
    public static readonly CameraControl AutoExposure = new(
        "autoExposure", "Auto Exposure", ControlKind.Choice, [(ExposureManual, "Manual"), (ExposureAuto, "Auto")]);
    public static readonly CameraControl ExposureTime = new(
        "exposureTime", "Shutter Speed", manualOnlyWhenOff: "autoExposure");
    public static readonly CameraControl AutoFocus = new("autoFocus", "Auto Focus", ControlKind.Toggle);
    public static readonly CameraControl Focus = new("focus", "Focus", manualOnlyWhenOff: "autoFocus");
    public static readonly CameraControl Zoom = new("zoom", "Zoom");
    public static readonly CameraControl Pan = new("pan", "Pan");
    public static readonly CameraControl Tilt = new("tilt", "Tilt");

    // Image
    public static readonly CameraControl Brightness = new("brightness", "Brightness");
    public static readonly CameraControl Contrast = new("contrast", "Contrast");
    public static readonly CameraControl Saturation = new("saturation", "Saturation");
    public static readonly CameraControl Sharpness = new("sharpness", "Sharpness");
    public static readonly CameraControl AutoWhiteBalance = new("autoWhiteBalance", "Auto White Balance", ControlKind.Toggle);
    public static readonly CameraControl WhiteBalance = new(
        "whiteBalance", "White Balance", manualOnlyWhenOff: "autoWhiteBalance");
    public static readonly CameraControl Backlight = new("backlight", "Backlight Compensation");
    public static readonly CameraControl Gain = new("gain", "Gain", manualOnlyWhenOff: "autoExposure");
    public static readonly CameraControl AntiFlicker = new(
        "antiFlicker", "Anti-Flicker", ControlKind.Choice, [(0, "Off"), (1, "50 Hz"), (2, "60 Hz")]);

    /// <summary>
    /// All controls in a safe apply order: auto toggles come before the values they gate.
    /// Gain and exposure time are left out: Razer's ISO and shutter commands replace them.
    /// </summary>
    public static readonly IReadOnlyList<CameraControl> All =
    [
        AntiFlicker, AutoExposure, AutoFocus, Focus, Zoom, Pan, Tilt,
        Brightness, Contrast, Saturation, Sharpness, AutoWhiteBalance, WhiteBalance, Backlight,
    ];

    public static CameraControl? ById(string id) =>
        All.FirstOrDefault(c => c.Id == id) ?? (new[] { ExposureTime, Gain }).FirstOrDefault(c => c.Id == id);

    /// <summary>Whether an auto toggle's raw value means "auto on".</summary>
    public static bool IsAuto(string id, int? value) =>
        value is int v && (id == AutoExposure.Id ? v != ExposureManual : v != 0);

    /// <summary>Values to compare for "unsaved changes": drops values the camera drives itself while their auto mode is on.</summary>
    public static Dictionary<string, int> Comparable(IReadOnlyDictionary<string, int> values, IEnumerable<CameraControl> controls)
    {
        var list = controls.ToList();
        return values.Where(kv =>
        {
            var gate = list.FirstOrDefault(c => c.Id == kv.Key)?.ManualOnlyWhenOff;
            return gate is null || !IsAuto(gate, values.TryGetValue(gate, out var g) ? g : null);
        }).ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
