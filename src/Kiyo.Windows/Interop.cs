using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Kiyo.Windows;

// Minimal DirectShow / kernel-streaming COM declarations (strmif.h, vidcap.h, ks.h, ksmedia.h).
// Method order matters: it is the vtable order from the Windows SDK headers.

internal static class Guids
{
    public static readonly Guid CLSID_SystemDeviceEnum = new("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
    public static readonly Guid CLSID_VideoInputDeviceCategory = new("860BB310-5D01-11d0-BD3B-00A0C911CE86");
    public static readonly Guid IID_IBaseFilter = new("56a86895-0ad4-11ce-b03a-0020af0ba770");
    public static readonly Guid IID_IPropertyBag = new("55272A00-42CB-11CE-8135-00AA004BB851");
    public static readonly Guid KSNODETYPE_DEV_SPECIFIC = new("941C7AC0-C559-11D0-8A2B-00A0C9255AC1");
    public static readonly Guid PROPSETID_VIDCAP_VIDEOPROCAMP = new("C6E13360-30AC-11d0-A18C-00A0C9118956");
    public static readonly Guid PROPSETID_VIDCAP_CAMERACONTROL = new("C6E13370-30AC-11d0-A18C-00A0C9118956");
}

[ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICreateDevEnum
{
    [PreserveSig]
    int CreateClassEnumerator([In] ref Guid clsidDeviceClass, out IEnumMoniker? ppEnumMoniker, int dwFlags);
}

[ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyBag
{
    [PreserveSig]
    int Read([MarshalAs(UnmanagedType.LPWStr)] string pszPropName, out object? pVar, IntPtr pErrorLog);

    [PreserveSig]
    int Write([MarshalAs(UnmanagedType.LPWStr)] string pszPropName, ref object pVar);
}

internal enum CameraControlProperty { Pan = 0, Tilt = 1, Roll = 2, Zoom = 3, Exposure = 4, Iris = 5, Focus = 6 }

internal enum VideoProcAmpProperty
{
    Brightness = 0, Contrast = 1, Hue = 2, Saturation = 3, Sharpness = 4, Gamma = 5, ColorEnable = 6,
    WhiteBalance = 7, BacklightCompensation = 8, Gain = 9,
    /// <summary>KSPROPERTY_VIDEOPROCAMP_POWERLINE_FREQUENCY: 0 off, 1 50 Hz, 2 60 Hz, 3 auto.</summary>
    PowerlineFrequency = 13,
}

[Flags]
internal enum ControlFlags { None = 0, Auto = 1, Manual = 2 }

[ComImport, Guid("C6E13370-30AC-11d0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAMCameraControl
{
    [PreserveSig] int GetRange(int property, out int min, out int max, out int step, out int defaultValue, out int capsFlags);
    [PreserveSig] int Set(int property, int value, int flags);
    [PreserveSig] int Get(int property, out int value, out int flags);
}

[ComImport, Guid("C6E13360-30AC-11d0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAMVideoProcAmp
{
    [PreserveSig] int GetRange(int property, out int min, out int max, out int step, out int defaultValue, out int capsFlags);
    [PreserveSig] int Set(int property, int value, int flags);
    [PreserveSig] int Get(int property, out int value, out int flags);
}

[ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    [PreserveSig] int KsProperty(IntPtr property, int propertyLength, IntPtr propertyData, int dataLength, out int bytesReturned);
    [PreserveSig] int KsMethod(IntPtr method, int methodLength, IntPtr methodData, int dataLength, out int bytesReturned);
    [PreserveSig] int KsEvent(IntPtr evt, int eventLength, IntPtr eventData, int dataLength, out int bytesReturned);
}

[ComImport, Guid("720D4AC0-7533-11D0-A5D6-28DB04C10000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsTopologyInfo
{
    [PreserveSig] int get_NumCategories(out int count);
    [PreserveSig] int get_Category(int index, out Guid category);
    [PreserveSig] int get_NumConnections(out int count);
    [PreserveSig] int get_ConnectionInfo(int index, IntPtr connectionInfo);
    [PreserveSig] int get_NodeName(int nodeId, IntPtr name, int bufferSize, out int nameLength);
    [PreserveSig] int get_NumNodes(out int count);
    [PreserveSig] int get_NodeType(int nodeId, out Guid nodeType);
    [PreserveSig] int CreateNodeInstance(int nodeId, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
}

/// <summary>KSPROPERTY_TYPE_* flags.</summary>
internal static class KsFlags
{
    public const int Get = 0x00000001;
    public const int Set = 0x00000002;
    public const int Topology = 0x10000000;
}

/// <summary>KSP_NODE: a KSPROPERTY addressed to one node of the filter's topology.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KsPropertyNode
{
    public Guid Set;
    public int Id;
    public int Flags;
    public int NodeId;
    public int Reserved;
}

/// <summary>KSPROPERTY_VIDEOPROCAMP_S.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KsVideoProcAmp
{
    public Guid Set;
    public int Id;
    public int Flags;
    public int Value;
    public int ControlFlags;
    public int Capabilities;
}

internal static class CfgMgr
{
    public const int CR_SUCCESS = 0;
    public const int CM_DRP_HARDWAREID = 0x00000002;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Locate_DevNodeW(out int devInst, string deviceId, int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_DevNode_Registry_PropertyW(int devInst, int property, out int regType,
        [Out] byte[]? buffer, ref int length, int flags);
}
