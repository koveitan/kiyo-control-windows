using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using Kiyo.Core;
using K = Kiyo.Core.KiyoControls;

namespace Kiyo.Windows;

/// <summary>A video capture device as DirectShow lists it.</summary>
public sealed record VideoDeviceInfo(string FriendlyName, string DevicePath)
{
    /// <summary>The supported Kiyo model this device is, or null.</summary>
    public KiyoDevice? Kiyo => KiyoDevice.All.FirstOrDefault(d => DevicePath.Contains(d.UsbIdFragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>PnP instance id of the video interface, e.g. USB\VID_1532&amp;PID_0E08&amp;MI_00\9&amp;1931AA6C&amp;0&amp;0000.</summary>
    public string? InstanceId
    {
        get
        {
            // \\?\usb#vid_1532&pid_0e08&mi_00#9&1931aa6c&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\global
            var p = DevicePath.StartsWith(@"\\?\") ? DevicePath[4..] : DevicePath;
            var end = p.IndexOf("#{", StringComparison.Ordinal);
            return end < 0 ? null : p[..end].Replace('#', '\\');
        }
    }
}

/// <summary>
/// Talks to a Kiyo through the Windows camera driver (usbvideo.sys), so other apps keep streaming:
/// standard controls via DirectShow's IAMCameraControl / IAMVideoProcAmp, anti-flicker and the Razer
/// extension unit via IKsControl. Create and use it on one STA thread.
/// </summary>
public sealed class WindowsCameraBackend : ICameraBackend
{
    public KiyoDevice Device { get; }
    public string? FirmwareVersion { get; }
    public VideoDeviceInfo Info { get; }
    /// <summary>Topology node of the Razer extension unit, or null when not found / not supported.</summary>
    public int? RazerNode { get; }

    private object? _filter;
    private readonly IAMCameraControl? _cameraControl;
    private readonly IAMVideoProcAmp? _procAmp;
    private readonly IKsControl? _ks;

    private WindowsCameraBackend(VideoDeviceInfo info, KiyoDevice device, object filter)
    {
        Info = info;
        Device = device;
        _filter = filter;
        _cameraControl = filter as IAMCameraControl;
        _procAmp = filter as IAMVideoProcAmp;
        _ks = filter as IKsControl;
        FirmwareVersion = ReadFirmwareVersion(info);
        if (device.RazerCommands) RazerNode = FindRazerNode(filter);
    }

    // Discovery

    /// <summary>Every video capture device DirectShow knows, with its device path.</summary>
    public static List<VideoDeviceInfo> ListDevices()
    {
        var result = new List<VideoDeviceInfo>();
        foreach (var (moniker, info) in EnumerateMonikers())
        {
            result.Add(info);
            Marshal.ReleaseComObject(moniker);
        }
        return result;
    }

    /// <summary>Connects to the first supported Kiyo, or returns null when none is plugged in.</summary>
    public static WindowsCameraBackend? Connect()
    {
        WindowsCameraBackend? found = null;
        foreach (var (moniker, info) in EnumerateMonikers())
        {
            try
            {
                if (found is not null || info.Kiyo is not KiyoDevice device) continue;
                var iid = Guids.IID_IBaseFilter;
                moniker.BindToObject(null!, null, ref iid, out var filter);
                if (filter is null) continue;
                found = new WindowsCameraBackend(info, device, filter);
            }
            catch (COMException) { }
            finally { Marshal.ReleaseComObject(moniker); }
        }
        return found;
    }

    private static IEnumerable<(IMoniker, VideoDeviceInfo)> EnumerateMonikers()
    {
        var type = Type.GetTypeFromCLSID(Guids.CLSID_SystemDeviceEnum, throwOnError: true)!;
        var devEnum = (ICreateDevEnum)Activator.CreateInstance(type)!;
        try
        {
            var category = Guids.CLSID_VideoInputDeviceCategory;
            if (devEnum.CreateClassEnumerator(ref category, out var enumMoniker, 0) != 0 || enumMoniker is null) yield break;
            try
            {
                var monikers = new IMoniker[1];
                while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    var m = monikers[0];
                    var name = ReadProperty(m, "FriendlyName") ?? "";
                    var path = ReadProperty(m, "DevicePath") ?? "";
                    yield return (m, new VideoDeviceInfo(name, path));
                }
            }
            finally { Marshal.ReleaseComObject(enumMoniker); }
        }
        finally { Marshal.ReleaseComObject(devEnum); }
    }

    private static string? ReadProperty(IMoniker moniker, string name)
    {
        object? bagObj = null;
        try
        {
            var iid = Guids.IID_IPropertyBag;
            moniker.BindToStorage(null!, null, ref iid, out bagObj);
            if (bagObj is not IPropertyBag bag) return null;
            return bag.Read(name, out var value, IntPtr.Zero) == 0 ? value as string : null;
        }
        catch (COMException) { return null; }
        finally { if (bagObj is not null) Marshal.ReleaseComObject(bagObj); }
    }

    // ICameraBackend

    public ControlRange GetRange(CameraControl c)
    {
        var map = Map(c);
        if (map.Kind == Target.PowerLine)
        {
            // usbvideo answers range queries for this property through IAMVideoProcAmp on most cameras.
            if (ProcAmp().GetRange((int)VideoProcAmpProperty.PowerlineFrequency, out var lo, out var hi, out _, out var def, out _) == 0)
                return new ControlRange(Math.Max(0, lo), Math.Min(2, hi), 1, def);
            return new ControlRange(0, 2, 1, Get(c));
        }
        int hr, min, max, step, dflt, caps;
        if (map.Kind == Target.Camera)
            hr = CamControl().GetRange(map.Property, out min, out max, out step, out dflt, out caps);
        else
            hr = ProcAmp().GetRange(map.Property, out min, out max, out step, out dflt, out caps);
        Check(hr, $"range of {c.Id}");

        if (map.AutoFlag)
        {
            // DirectShow has no default for auto modes; cameras that can do auto start in auto.
            var canAuto = (caps & (int)ControlFlags.Auto) != 0;
            if (!canAuto) throw new CameraException($"{c.Id} has no auto mode");
            return c.Id == K.AutoExposure.Id
                ? new ControlRange(K.ExposureManual, K.ExposureAuto, 1, K.ExposureAuto)
                : new ControlRange(0, 1, 1, 1);
        }
        return new ControlRange(min, max, Math.Max(1, step), dflt);
    }

    public int Get(CameraControl c)
    {
        var map = Map(c);
        if (map.Kind == Target.PowerLine) return GetPowerLine();
        var (value, flags) = Read(map);
        if (!map.AutoFlag) return value;
        var auto = (flags & (int)ControlFlags.Auto) != 0;
        return c.Id == K.AutoExposure.Id ? (auto ? K.ExposureAuto : K.ExposureManual) : (auto ? 1 : 0);
    }

    public void Set(CameraControl c, int value)
    {
        var map = Map(c);
        if (map.Kind == Target.PowerLine) { SetPowerLine(value); return; }
        if (map.AutoFlag)
        {
            // An auto mode is a flag on its value property: keep the value, change the flag.
            var auto = KiyoControls.IsAuto(c.Id, value);
            var (current, _) = Read(map);
            Write(map, current, auto ? ControlFlags.Auto : ControlFlags.Manual, c.Id);
        }
        else
        {
            Write(map, value, ControlFlags.Manual, c.Id);
        }
    }

    public byte[] RazerExchange(byte[] command)
    {
        if (RazerNode is not int node) throw new CameraException("The Razer extension unit was not found on this camera.");
        if (command.Length != RazerCommand.Length) throw new ArgumentException("Razer commands are 8 bytes.", nameof(command));
        XuRequest(node, RazerCommand.CommandSelector, KsFlags.Set, command);
        var reply = new byte[RazerCommand.Length];
        try { XuRequest(node, RazerCommand.ResultSelector, KsFlags.Get, reply); }
        catch (CameraException) { return []; }
        return reply;
    }

    /// <summary>Reads the Razer extension unit's result register (read-only; used to check the path works).</summary>
    public byte[] ReadRazerResult()
    {
        if (RazerNode is not int node) throw new CameraException("The Razer extension unit was not found on this camera.");
        var reply = new byte[RazerCommand.Length];
        XuRequest(node, RazerCommand.ResultSelector, KsFlags.Get, reply);
        return reply;
    }

    public void Dispose()
    {
        if (_filter is not null) Marshal.FinalReleaseComObject(_filter);
        _filter = null;
    }

    // Control mapping

    private enum Target { Camera, ProcAmp, PowerLine }

    private readonly record struct Mapping(Target Kind, int Property, bool AutoFlag = false);

    private static Mapping Map(CameraControl c) => c.Id switch
    {
        "zoom" => new(Target.Camera, (int)CameraControlProperty.Zoom),
        "pan" => new(Target.Camera, (int)CameraControlProperty.Pan),
        "tilt" => new(Target.Camera, (int)CameraControlProperty.Tilt),
        "focus" => new(Target.Camera, (int)CameraControlProperty.Focus),
        "autoFocus" => new(Target.Camera, (int)CameraControlProperty.Focus, AutoFlag: true),
        "exposureTime" => new(Target.Camera, (int)CameraControlProperty.Exposure),
        "autoExposure" => new(Target.Camera, (int)CameraControlProperty.Exposure, AutoFlag: true),
        "brightness" => new(Target.ProcAmp, (int)VideoProcAmpProperty.Brightness),
        "contrast" => new(Target.ProcAmp, (int)VideoProcAmpProperty.Contrast),
        "saturation" => new(Target.ProcAmp, (int)VideoProcAmpProperty.Saturation),
        "sharpness" => new(Target.ProcAmp, (int)VideoProcAmpProperty.Sharpness),
        "whiteBalance" => new(Target.ProcAmp, (int)VideoProcAmpProperty.WhiteBalance),
        "autoWhiteBalance" => new(Target.ProcAmp, (int)VideoProcAmpProperty.WhiteBalance, AutoFlag: true),
        "backlight" => new(Target.ProcAmp, (int)VideoProcAmpProperty.BacklightCompensation),
        "gain" => new(Target.ProcAmp, (int)VideoProcAmpProperty.Gain),
        "antiFlicker" => new(Target.PowerLine, (int)VideoProcAmpProperty.PowerlineFrequency),
        _ => throw new CameraException($"Unknown control {c.Id}"),
    };

    private (int Value, int Flags) Read(Mapping m)
    {
        int hr, value, flags;
        if (m.Kind == Target.Camera) hr = CamControl().Get(m.Property, out value, out flags);
        else hr = ProcAmp().Get(m.Property, out value, out flags);
        Check(hr, $"get property {m.Kind}/{m.Property}");
        return (value, flags);
    }

    private void Write(Mapping m, int value, ControlFlags flags, string id)
    {
        var hr = m.Kind == Target.Camera
            ? CamControl().Set(m.Property, value, (int)flags)
            : ProcAmp().Set(m.Property, value, (int)flags);
        Check(hr, $"set {id}");
    }

    private IAMCameraControl CamControl() => _cameraControl ?? throw new CameraException("Camera controls are not available.");
    private IAMVideoProcAmp ProcAmp() => _procAmp ?? throw new CameraException("Image controls are not available.");
    private IKsControl Ks() => _ks ?? throw new CameraException("Kernel-streaming access is not available.");

    // Anti-flicker (power line frequency) has no DirectShow enum value, so it goes through IKsControl.

    private int GetPowerLine()
    {
        if (ProcAmp().Get((int)VideoProcAmpProperty.PowerlineFrequency, out var v, out _) == 0) return v;
        var s = PowerLineRequest(KsFlags.Get, 0);
        return s.Value;
    }

    private void SetPowerLine(int value)
    {
        if (ProcAmp().Set((int)VideoProcAmpProperty.PowerlineFrequency, value, (int)ControlFlags.Manual) == 0) return;
        PowerLineRequest(KsFlags.Set, value);
    }

    private unsafe KsVideoProcAmp PowerLineRequest(int kind, int value)
    {
        var s = new KsVideoProcAmp
        {
            Set = Guids.PROPSETID_VIDCAP_VIDEOPROCAMP,
            Id = (int)VideoProcAmpProperty.PowerlineFrequency,
            Flags = kind,
            Value = value,
            ControlFlags = (int)ControlFlags.Manual,
        };
        var size = sizeof(KsVideoProcAmp);
        var hr = Ks().KsProperty((IntPtr)(&s), size, (IntPtr)(&s), size, out _);
        Check(hr, "anti-flicker");
        return s;
    }

    // Razer extension unit

    private unsafe int XuRequestRaw(IKsControl ks, int node, int selector, int kind, byte[] data)
    {
        var prop = new KsPropertyNode
        {
            Set = RazerCommand.ExtensionUnit,
            Id = selector,
            Flags = kind | KsFlags.Topology,
            NodeId = node,
        };
        fixed (byte* p = data)
        {
            return ks.KsProperty((IntPtr)(&prop), sizeof(KsPropertyNode), (IntPtr)p, data.Length, out _);
        }
    }

    private void XuRequest(int node, int selector, int kind, byte[] data) =>
        Check(XuRequestRaw(Ks(), node, selector, kind, data), $"Razer extension unit selector {selector}");

    /// <summary>
    /// The Windows node id of the extension unit is not its UVC unit id (6). Extension units show up as
    /// device-specific nodes; the right one is the node that accepts a read of the Razer result register.
    /// </summary>
    private int? FindRazerNode(object filter)
    {
        if (filter is not IKsTopologyInfo topo || _ks is not IKsControl ks) return null;
        if (topo.get_NumNodes(out var count) != 0) return null;
        var candidates = new List<int>();
        for (var i = 0; i < count; i++)
        {
            if (topo.get_NodeType(i, out var type) != 0) continue;
            if (type == RazerCommand.ExtensionUnit) return i;
            if (type == Guids.KSNODETYPE_DEV_SPECIFIC) candidates.Add(i);
        }
        foreach (var node in candidates)
        {
            if (XuRequestRaw(ks, node, RazerCommand.ResultSelector, KsFlags.Get, new byte[RazerCommand.Length]) == 0) return node;
        }
        return null;
    }

    /// <summary>Every DirectShow camera / proc-amp property as the driver reports it (read-only), for diagnostics.</summary>
    public List<string> RawProperties()
    {
        var o = new List<string>();
        foreach (var p in Enum.GetValues<CameraControlProperty>())
        {
            if (_cameraControl is null) break;
            var r = _cameraControl.GetRange((int)p, out var min, out var max, out var step, out var def, out var caps);
            var g = _cameraControl.Get((int)p, out var v, out var f);
            o.Add(r == 0 ? $"camera  {p,-22} value={(g == 0 ? v.ToString() : "?"),-7} flags={f} range {min}..{max} step {step} default {def} caps {caps}"
                         : $"camera  {p,-22} not supported (0x{unchecked((uint)r):X8})");
        }
        foreach (var p in Enum.GetValues<VideoProcAmpProperty>())
        {
            if (_procAmp is null) break;
            var r = _procAmp.GetRange((int)p, out var min, out var max, out var step, out var def, out var caps);
            var g = _procAmp.Get((int)p, out var v, out var f);
            o.Add(r == 0 ? $"procamp {p,-22} value={(g == 0 ? v.ToString() : "?"),-7} flags={f} range {min}..{max} step {step} default {def} caps {caps}"
                         : $"procamp {p,-22} not supported (0x{unchecked((uint)r):X8})");
        }
        return o;
    }

    /// <summary>Node ids and types, for diagnostics.</summary>
    public List<(int Node, Guid Type, string Name)> Topology()
    {
        var o = new List<(int, Guid, string)>();
        if (_filter is not IKsTopologyInfo topo || topo.get_NumNodes(out var count) != 0) return o;
        for (var i = 0; i < count; i++)
        {
            topo.get_NodeType(i, out var type);
            var name = "";
            var buf = Marshal.AllocHGlobal(512);
            try
            {
                if (topo.get_NodeName(i, buf, 256, out _) == 0) name = Marshal.PtrToStringUni(buf) ?? "";
            }
            finally { Marshal.FreeHGlobal(buf); }
            o.Add((i, type, name));
        }
        return o;
    }

    // Errors

    private static readonly HashSet<uint> DisconnectedCodes =
    [
        0x80070651, // ERROR_DEVICE_REMOVED
        0x8007048F, // ERROR_DEVICE_NOT_CONNECTED
        0x80070015, // ERROR_NOT_READY
        0x800706BA, // RPC_S_SERVER_UNAVAILABLE
        0x8007001F, // ERROR_GEN_FAILURE (seen right after unplug)
    ];

    private static void Check(int hr, string context)
    {
        if (hr >= 0) return;
        var code = unchecked((uint)hr);
        throw new CameraException($"{context} failed (0x{code:X8})", DisconnectedCodes.Contains(code));
    }

    // Firmware

    /// <summary>The firmware version from the device's hardware id (…&amp;REV_0821 → "8.21").</summary>
    private static string? ReadFirmwareVersion(VideoDeviceInfo info)
    {
        if (info.InstanceId is not string id) return null;
        if (CfgMgr.CM_Locate_DevNodeW(out var devInst, id, 0) != CfgMgr.CR_SUCCESS) return null;
        var length = 0;
        CfgMgr.CM_Get_DevNode_Registry_PropertyW(devInst, CfgMgr.CM_DRP_HARDWAREID, out _, null, ref length, 0);
        if (length <= 0) return null;
        var buffer = new byte[length];
        if (CfgMgr.CM_Get_DevNode_Registry_PropertyW(devInst, CfgMgr.CM_DRP_HARDWAREID, out _, buffer, ref length, 0) != CfgMgr.CR_SUCCESS) return null;
        var text = Encoding.Unicode.GetString(buffer);
        var m = Regex.Match(text, "REV_([0-9A-Fa-f]{4})");
        if (!m.Success) return null;
        var bcd = Convert.ToInt32(m.Groups[1].Value, 16);
        return $"{(bcd >> 8) & 0xFF:X}.{bcd & 0xFF:X2}";
    }
}
