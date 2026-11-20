namespace Kiyo.Core;

/// <summary>A supported camera model.</summary>
public sealed record KiyoDevice(
    string Name,
    ushort VendorId,
    ushort ProductId,
    /// <summary>True if we know the Razer extension unit commands (<see cref="RazerCommand"/>) for this camera.</summary>
    bool RazerCommands,
    /// <summary>Razer's firmware updater page. The app itself never updates firmware.</summary>
    string? FirmwareUpdater)
{
    /// <summary>Kiyo Pro Ultra, USB 1532:0E08.</summary>
    public static readonly KiyoDevice ProUltra = new(
        "Kiyo Pro Ultra", 0x1532, 0x0E08, RazerCommands: true,
        FirmwareUpdater: "https://mysupport.razer.com/app/answers/detail/a_id/9777");

    /// <summary>Kiyo V2 Pro, USB 1532:0E0A. Its extension unit is undocumented: standard controls only.</summary>
    public static readonly KiyoDevice V2Pro = new(
        "Kiyo V2 Pro", 0x1532, 0x0E0A, RazerCommands: false, FirmwareUpdater: null);

    public static readonly IReadOnlyList<KiyoDevice> All = [ProUltra, V2Pro];

    /// <summary>The "vid_1532&amp;pid_0e08" fragment Windows uses in device paths and instance ids.</summary>
    public string UsbIdFragment => $"vid_{VendorId:x4}&pid_{ProductId:x4}";
}
