using Kiyo.Core;
using K = Kiyo.Core.KiyoControls;

namespace Kiyo.Core.Tests;

public class CameraDiscoveryTests
{
    private static UsbCamera Camera(ushort product, ushort vendor = 0x1532, string port = "1", string name = "Razer Kiyo") =>
        new(name, vendor, product, $@"\\?\usb#vid_{vendor:x4}&pid_{product:x4}&mi_00#9&1931aa6c&0&000{port}#{{65e8773d-8f56-11d0-a3b9-00a0c9223196}}\global");

    [Fact]
    public void ClassifiesCameras()
    {
        Assert.Equal(KiyoDevice.V2X, Camera(0x0E0C).Device);
        Assert.Equal(UsbCamera.Kind.Supported, Camera(0x0E08).Status);
        Assert.Equal(KiyoDevice.KiyoPro, Camera(0x0E05).Device);
        Assert.Equal(UsbCamera.Kind.UnsupportedRazer, Camera(0x0E03).Status);
        Assert.Null(Camera(0x0E03).Device);
        Assert.Equal(UsbCamera.Kind.Other, Camera(0x085C, vendor: 0x046D).Status);
    }

    [Fact]
    public void FormatsTheUsbId() => Assert.Equal("1532:0E0C", Camera(0x0E0C).UsbId);

    [Fact]
    public void ReadsIdsFromAWindowsDevicePath()
    {
        var c = UsbCamera.FromDevicePath("Razer Kiyo Pro Ultra",
            @"\\?\usb#vid_1532&pid_0e08&mi_00#9&1931aa6c&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\global")!;
        Assert.Equal(KiyoDevice.ProUltra, c.Device);
        Assert.Null(UsbCamera.FromDevicePath("OBS Virtual Camera", ""));
    }

    [Fact]
    public void ChoosesThePreferredSupportedCamera()
    {
        var cams = new[] { Camera(0x0E03, port: "1"), Camera(0x0E08, port: "2"), Camera(0x0E0C, port: "3") };
        Assert.Equal(cams[2], CameraDiscovery.Choose(cams, cams[2].Id));
        // Unplugged or unsupported preference: the first supported camera.
        Assert.Equal(cams[1], CameraDiscovery.Choose(cams, "gone"));
        Assert.Equal(cams[1], CameraDiscovery.Choose(cams, cams[0].Id));
        Assert.Equal(cams[1], CameraDiscovery.Choose(cams, null));
    }

    [Fact]
    public void ChoosesNothingWithoutASupportedCamera()
    {
        Assert.Null(CameraDiscovery.Choose([Camera(0x0E03)], Camera(0x0E03).Id));
        Assert.Null(CameraDiscovery.Choose([], null));
    }

    [Fact]
    public void MatchesTheSameCameraAcrossInterfaceClasses()
    {
        // DirectShow and Media Foundation list the same camera under different interface GUIDs.
        Assert.True(CameraDiscovery.SameDevice(
            @"\\?\usb#vid_1532&pid_0e08&mi_00#9&1931aa6c&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\global",
            @"\\?\USB#VID_1532&PID_0E08&MI_00#9&1931AA6C&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL"));
        Assert.False(CameraDiscovery.SameDevice(Camera(0x0E08, port: "1").Id, Camera(0x0E08, port: "2").Id));
        Assert.False(CameraDiscovery.SameDevice(null, Camera(0x0E08).Id));
    }

    [Fact]
    public void SupportRequestNamesTheCamera()
    {
        var url = new Uri(CameraDiscovery.SupportRequestUrl(Camera(0x0E03, name: "Razer Kiyo"), 2));
        var query = Uri.UnescapeDataString(url.Query);
        Assert.Equal("github.com", url.Host);
        Assert.Contains("title=Support Razer Kiyo (1532:0E03)", query);
        Assert.Contains("kiyoctl probe 2", query);
    }
}

public class DeviceTests
{
    [Fact]
    public void OnlyTheProUltraHasRazerCommandsAndFirmwareInfo()
    {
        Assert.Equal([KiyoDevice.ProUltra], KiyoDevice.All.Where(d => d.RazerCommands));
        Assert.Equal([KiyoDevice.ProUltra], KiyoDevice.All.Where(d => d.FirmwareUpdater is not null));
    }

    [Fact]
    public void OneEightyPModelsGetNoFourKHint()
    {
        Assert.True(KiyoDevice.ProUltra.SupportsFourK);
        Assert.False(KiyoDevice.KiyoPro.SupportsFourK);
        Assert.False(KiyoDevice.V2X.SupportsFourK);
    }

    [Fact]
    public void ModelsWithoutRazerCommandsUseStandardExposureTimeAndGain()
    {
        Assert.Empty(KiyoDevice.ProUltra.ExposureControls);
        Assert.Equal([K.ExposureTime, K.Gain], KiyoDevice.KiyoPro.ExposureControls);
        Assert.Contains(K.Gain, KiyoDevice.V2X.Controls);
        Assert.DoesNotContain(K.Gain, KiyoDevice.ProUltra.Controls);
    }
}

public class ReconnectTests
{
    [Fact]
    public void ReconnectSwitchesToTheNewlyChosenCamera()
    {
        var ultra = new FakeBackend(KiyoDevice.ProUltra, "cam-1");
        var v2x = new FakeBackend(KiyoDevice.V2X, "cam-2");
        foreach (var f in new[] { ultra, v2x }) f.Stub(K.Zoom, 100, 100, 400, 100);
        var next = ultra;
        var connects = 0;
        var c = new CameraController(TestStore.Create(), () => { connects++; return next; }, [K.Zoom]);
        c.Poll();
        Assert.True(c.Connected);
        Assert.Equal("cam-1", c.CameraId);
        Assert.True(c.SupportsRazer);

        next = v2x;
        c.Reconnect();
        Assert.Equal(2, connects);
        Assert.True(c.Connected);
        Assert.Equal("cam-2", c.CameraId);
        Assert.Equal(KiyoDevice.V2X, c.Device);
        Assert.False(c.SupportsRazer);
    }

    [Fact]
    public void ReconnectWithoutACameraStaysDisconnected()
    {
        var c = new CameraController(TestStore.Create(), () => null, [K.Zoom]);
        c.Reconnect();
        Assert.False(c.Connected);
        Assert.Null(c.CameraId);
    }

    [Fact]
    public void StandardModelsGetExposureTimeAndGainWhenTheCameraHasThem()
    {
        var pro = new FakeBackend(KiyoDevice.KiyoPro);
        pro.Stub(K.Zoom, 100, 100, 400, 100);
        pro.Stub(K.ExposureTime, -6, -11, -2, -6);
        pro.Stub(K.Gain, 0, 0, 255, 0);
        var c = new CameraController(TestStore.Create(), () => pro, [K.Zoom]);
        c.Poll();
        Assert.Contains(K.ExposureTime, c.Controls);
        Assert.Contains(K.Gain, c.Controls);
    }
}
