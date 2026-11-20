using System.Text.Json.Serialization;

namespace Kiyo.Core;

/// <summary>
/// The Razer-specific settings. <c>null</c> means unknown: the camera can't be asked for these, so the app
/// leaves them as they are (e.g. as set in Synapse) until the user changes them.
/// </summary>
public sealed record RazerSettings
{
    public bool? Mirror { get; set; }
    [JsonPropertyName("noiseReduction3D")] public bool? NoiseReduction3D { get; set; }
    [JsonPropertyName("noiseReduction2D")] public bool? NoiseReduction2D { get; set; }
    public RazerCommand.Metering? Metering { get; set; }
    /// <summary>Tenths of a stop, -30...30.</summary>
    public int? ExposureCompensation { get; set; }
    public bool? FaceFocus { get; set; }
    public bool? StylizedLighting { get; set; }
    public bool? ResponsiveTracking { get; set; }
    public bool? LensCorrection { get; set; }
    public int? Iso { get; set; }
    public int? ShutterMicroseconds { get; set; }

    /// <summary>Commands for every known setting. ISO and shutter only apply in manual exposure.</summary>
    public List<byte[]> Commands(bool manualExposure)
    {
        var o = new List<byte[]>();
        if (Mirror is bool m) o.Add(RazerCommand.Mirror(m));
        if (NoiseReduction3D is bool n3) o.Add(RazerCommand.NoiseReduction3D(n3));
        if (NoiseReduction2D is bool n2) o.Add(RazerCommand.NoiseReduction2D(n2));
        if (Metering is RazerCommand.Metering me) o.Add(RazerCommand.MeteringMode(me));
        if (ExposureCompensation is int ec)
        {
            // Face metering only supports -1.0...+3.0 EV.
            var low = Metering == RazerCommand.Metering.Face ? -10 : -30;
            o.Add(RazerCommand.ExposureCompensation(Math.Max(ec, low)));
        }
        if (FaceFocus is not null || StylizedLighting is not null)
            o.Add(RazerCommand.Focus(FaceFocus ?? false, StylizedLighting ?? false));
        if (ResponsiveTracking is bool rt) o.Add(RazerCommand.Tracking(rt));
        if (LensCorrection is bool lc) o.Add(RazerCommand.LensCorrection(lc));
        if (manualExposure) o.AddRange(ExposureCommands);
        return o;
    }

    /// <summary>Manual ISO and shutter, for when the camera switches to manual exposure.</summary>
    [JsonIgnore]
    public List<byte[]> ExposureCommands
    {
        get
        {
            var o = new List<byte[]>();
            if (Iso is int iso) o.Add(RazerCommand.Iso(iso));
            if (ShutterMicroseconds is int us) o.Add(RazerCommand.Shutter(us));
            return o;
        }
    }
}
