namespace Kiyo.Core;

/// <summary>
/// Razer-specific settings of the Kiyo Pro Ultra: 8-byte commands written to selector 1 of the
/// extension unit with GUID 23e49ed0-1178-4f31-ae52-d2fb8a8d3b48; the camera answers on selector 2.
/// Byte layouts come from USB captures of Synapse documented in
/// https://github.com/soyersoyer/cameractrls/issues/19.
///
/// Never send guessed commands to this unit. Saving to the camera's memory is kept separate.
/// </summary>
public static class RazerCommand
{
    public static readonly Guid ExtensionUnit = new("23e49ed0-1178-4f31-ae52-d2fb8a8d3b48");
    public const int CommandSelector = 1;
    public const int ResultSelector = 2;
    public const int Length = 8;

    public enum Metering { Average = 0, Center = 1, Face = 5 }

    public static readonly IReadOnlyList<int> IsoSteps = [100, 200, 400, 800, 1600, 3200, 6400];

    public static byte[] Mirror(bool on) => Cmd(0xC0, 0x0E, 0x03, B(on));
    public static byte[] NoiseReduction3D(bool on) => Cmd(0xC0, 0x0E, 0x01, B(on));
    public static byte[] NoiseReduction2D(bool on) => Cmd(0xC0, 0x0E, 0x02, B(on));
    public static byte[] MeteringMode(Metering m) => Cmd(0xC0, 0x0E, 0x04, (byte)m);

    /// <summary>Exposure compensation in tenths of a stop, -30...30 (-3.0 to +3.0 EV).</summary>
    public static byte[] ExposureCompensation(int tenths) =>
        Cmd(0xC0, 0x0E, 0x05, (byte)(Math.Clamp(tenths, -30, 30) + 30));

    /// <summary>Manual ISO, one of <see cref="IsoSteps"/>.</summary>
    public static byte[] Iso(int value)
    {
        var index = Math.Max(0, IsoSteps.ToList().IndexOf(value));
        return Cmd(0xC0, 0x09, 0x01, (byte)(index + 1));
    }

    /// <summary>Manual shutter time in microseconds (500 = 1/2000 s ... 100000 = 1/10 s), big-endian in bytes 4-6.</summary>
    public static byte[] Shutter(int microseconds)
    {
        var v = Math.Clamp(microseconds, 500, 100_000);
        return [0xC0, 0x09, 0x05, 0x00, (byte)(v >> 16 & 0xFF), (byte)(v >> 8 & 0xFF), (byte)(v & 0xFF), 0x00];
    }

    /// <summary>Autofocus mode (Standard/Face) and lighting (Standard/Stylized) are one command.</summary>
    public static byte[] Focus(bool face, bool stylized) => [0xC0, 0x0A, 0x01, B(face), 0, 0, 0, B(stylized)];

    public static byte[] Tracking(bool responsive) => Cmd(0xFF, 0x06, (byte)(responsive ? 0 : 1));

    /// <summary>Lens distortion compensation. Takes effect when the video stream restarts.</summary>
    public static byte[] LensCorrection(bool on) => Cmd(0xFF, 0x01, B(on), 0x03);

    /// <summary>Stores the current settings in the camera's memory, exactly as Synapse's Save does.</summary>
    public static readonly IReadOnlyList<byte[]> SaveToCamera =
    [
        [0xC0, 0x09, 0x0A, 0, 0, 0, 0, 0],
        [0xC0, 0x03, 0xA8, 0, 0, 0, 0, 0],
    ];

    private static byte B(bool on) => (byte)(on ? 1 : 0);

    private static byte[] Cmd(params byte[] bytes)
    {
        var out8 = new byte[Length];
        bytes.CopyTo(out8, 0);
        return out8;
    }

    public static string Hex(IEnumerable<byte> bytes) => string.Concat(bytes.Select(b => b.ToString("x2")));
}
