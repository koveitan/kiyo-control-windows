namespace Kiyo.Core;

/// <summary>
/// Field of view from the zoom level. The Kiyo Pro Ultra has no field-of-view command; Synapse's
/// FOV follows the digital zoom. The full width is calibrated so 2x zoom gives the 44° Synapse shows.
/// </summary>
public static class FieldOfView
{
    /// <summary>Horizontal field of view at 1x zoom, in degrees.</summary>
    public static readonly double FullWidth = 2 * Math.Atan(2 * Math.Tan(22 * Math.PI / 180)) * 180 / Math.PI;

    /// <summary>Field of view for a zoom value (100 = 1x).</summary>
    public static double Degrees(int zoom)
    {
        var half = FullWidth / 2 * Math.PI / 180;
        return 2 * Math.Atan(Math.Tan(half) / (Math.Max(zoom, 1) / 100.0)) * 180 / Math.PI;
    }

    public enum Preset { Wide, Medium, Narrow }

    public static readonly IReadOnlyList<Preset> Presets = [Preset.Wide, Preset.Medium, Preset.Narrow];

    public static int Zoom(this Preset p) => p switch { Preset.Wide => 100, Preset.Medium => 140, _ => 200 };

    public static Preset? PresetFor(int zoom) => Presets.Cast<Preset?>().FirstOrDefault(p => p!.Value.Zoom() == zoom);
}
