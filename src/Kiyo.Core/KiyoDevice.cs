namespace Kiyo.Core;

/// <summary>A supported camera model. All models use the same controls; only the USB ids and capabilities differ.</summary>
public sealed record KiyoDevice(
    string Name,
    ushort VendorId,
    ushort ProductId,
    /// <summary>True if we know the Razer extension unit commands (<see cref="RazerCommand"/>) for this camera.</summary>
    bool RazerCommands,
    /// <summary>Razer's firmware updater page. Null hides the firmware version and link; only the Pro Ultra's is supported.
    /// The app itself never updates firmware.</summary>
    string? FirmwareUpdater,
    /// <summary>True if the camera can stream 4K. The "plug into USB 3 for 4K" hint only makes sense for these:
    /// the 1080p models lose nothing on USB 2, and the V2 X is a USB 2 device anyway.</summary>
    bool SupportsFourK = true)
{
    /// <summary>Kiyo Pro Ultra, USB 1532:0E08.</summary>
    public static readonly KiyoDevice ProUltra = new(
        "Kiyo Pro Ultra", 0x1532, 0x0E08, RazerCommands: true,
        FirmwareUpdater: "https://mysupport.razer.com/app/answers/detail/a_id/9777");

    /// <summary>Kiyo V2 Pro, USB 1532:0E0A. Its extension unit is undocumented: standard controls only.</summary>
    public static readonly KiyoDevice V2Pro = new(
        "Kiyo V2 Pro", 0x1532, 0x0E0A, RazerCommands: false, FirmwareUpdater: null);

    /// <summary>Kiyo V2 X, USB 1532:0E0C. A 1080p USB 2 camera; its extension units are undocumented: standard controls only.</summary>
    public static readonly KiyoDevice V2X = new(
        "Kiyo V2 X", 0x1532, 0x0E0C, RazerCommands: false, FirmwareUpdater: null, SupportsFourK: false);

    /// <summary>
    /// Kiyo Pro (the original 1080p model), USB 1532:0E05. Its extension units differ from the Pro Ultra's and are
    /// undocumented, so only the standard controls are supported, and Razer commands are never sent to it.
    /// </summary>
    public static readonly KiyoDevice KiyoPro = new(
        "Kiyo Pro", 0x1532, 0x0E05, RazerCommands: false, FirmwareUpdater: null, SupportsFourK: false);

    public static readonly IReadOnlyList<KiyoDevice> All = [ProUltra, V2Pro, V2X, KiyoPro];

    /// <summary>Razer's USB vendor id. A Razer camera that is not in <see cref="All"/> is one the app does not support yet.</summary>
    public const ushort RazerVendorId = 0x1532;

    /// <summary>Without Razer's ISO and shutter commands, manual exposure uses the standard exposure time and gain.</summary>
    public IReadOnlyList<CameraControl> ExposureControls =>
        RazerCommands ? [] : [KiyoControls.ExposureTime, KiyoControls.Gain];

    /// <summary>Every control used with this model.</summary>
    public IReadOnlyList<CameraControl> Controls => [.. KiyoControls.All, .. ExposureControls];

    /// <summary>The "vid_1532&amp;pid_0e08" fragment Windows uses in device paths and instance ids.</summary>
    public string UsbIdFragment => $"vid_{VendorId:x4}&pid_{ProductId:x4}";

    /// <summary>The model with these USB ids, or null.</summary>
    public static KiyoDevice? Find(ushort vendorId, ushort productId) =>
        All.FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId);
}
