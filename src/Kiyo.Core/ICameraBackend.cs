namespace Kiyo.Core;

/// <summary>
/// The one place the core touches the operating system. Windows implements it with DirectShow /
/// kernel-streaming properties (Kiyo.Windows); tests use an in-memory fake.
/// </summary>
public interface ICameraBackend : IDisposable
{
    KiyoDevice Device { get; }

    /// <summary>Firmware version the camera reports over USB (e.g. "8.21"), or null.</summary>
    string? FirmwareVersion { get; }

    /// <summary>Range and default of a control. For toggles and choices only <see cref="ControlRange.Default"/> matters.</summary>
    ControlRange GetRange(CameraControl control);

    int Get(CameraControl control);

    void Set(CameraControl control, int value);

    /// <summary>Writes 8 bytes to the Razer extension unit's command selector and returns the reply from its result selector.</summary>
    byte[] RazerExchange(byte[] command);
}

/// <summary>Typed helpers over a backend (the Mac version's UVCCamera).</summary>
public static class CameraBackendExtensions
{
    /// <summary>
    /// Range of a control. Toggles get 0...1. Choices keep the range the camera reports when it gives one
    /// (e.g. anti-flicker 1...2 on a camera without "off"), else span their options.
    /// </summary>
    public static ControlRange Range(this ICameraBackend cam, CameraControl c)
    {
        var r = cam.GetRange(c);
        return c.Kind switch
        {
            ControlKind.Toggle => new ControlRange(0, 1, 1, r.Default),
            ControlKind.Choice when r.Max > r.Min => r with { Step = 1 },
            ControlKind.Choice => new ControlRange(c.Choices.Min(x => x.Value), c.Choices.Max(x => x.Value), 1, r.Default),
            _ => r with { Step = Math.Max(1, r.Step) },
        };
    }

    /// <summary>The options of a choice control that fall inside its range.</summary>
    public static IEnumerable<(int Value, string Label)> AvailableChoices(this CameraControl c, ControlRange? range) =>
        range is ControlRange r ? c.Choices.Where(x => x.Value >= r.Min && x.Value <= r.Max) : c.Choices;

    /// <summary>Reads every readable control. Unsupported ones are skipped.</summary>
    public static Dictionary<string, int> Snapshot(this ICameraBackend cam, IEnumerable<CameraControl> controls)
    {
        var o = new Dictionary<string, int>();
        foreach (var c in controls)
        {
            try { o[c.Id] = cam.Get(c); }
            catch (CameraException) { }
        }
        return o;
    }

    /// <summary>
    /// Writes values in safe order, clamped to each control's range. Values gated by an
    /// auto toggle are skipped while that toggle is on. Returns the controls that failed.
    /// </summary>
    public static Dictionary<string, Exception> Apply(this ICameraBackend cam, IReadOnlyDictionary<string, int> values,
        IReadOnlyDictionary<string, ControlRange> ranges, IEnumerable<CameraControl> controls)
    {
        var failed = new Dictionary<string, Exception>();
        foreach (var c in controls)
        {
            if (!values.TryGetValue(c.Id, out var v)) continue;
            if (c.ManualOnlyWhenOff is string gate && KiyoControls.IsAuto(gate, values.TryGetValue(gate, out var g) ? g : null)) continue;
            if (c.Kind == ControlKind.Range && ranges.TryGetValue(c.Id, out var r)) v = Math.Clamp(v, r.Min, Math.Max(r.Min, r.Max));
            try { cam.Set(c, v); }
            catch (CameraException e) { failed[c.Id] = e; }
        }
        return failed;
    }

    /// <summary>Sends a Razer command and returns the camera's reply.</summary>
    public static byte[] Send(this ICameraBackend cam, byte[] command) => cam.RazerExchange(command);

    /// <summary>
    /// Stores the camera's current settings in its memory, like Synapse's Save: the camera loads them
    /// at power-up, also on other computers. Sends exactly what Synapse sends.
    /// </summary>
    public static void SaveToCamera(this ICameraBackend cam)
    {
        foreach (var cmd in RazerCommand.SaveToCamera) cam.RazerExchange(cmd);
    }
}
