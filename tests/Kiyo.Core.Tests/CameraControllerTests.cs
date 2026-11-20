using Kiyo.Core;
using K = Kiyo.Core.KiyoControls;

namespace Kiyo.Core.Tests;

public class CameraControllerTests
{
    private readonly FakeBackend _fake = new();
    private readonly ProfileStore _store = TestStore.Create();

    public CameraControllerTests()
    {
        _fake.Stub(K.Zoom, cur: 150, min: 100, max: 400, def: 110);
        _fake.Stub(K.Brightness, cur: 90, min: 0, max: 255, def: 128);
        _fake.Stub(K.AutoExposure, cur: 8, def: 8);
    }

    private CameraController Controller(params CameraControl[] controls) =>
        new(_store, () => _fake, controls.Length > 0 ? controls : [K.Zoom, K.Brightness, K.AutoExposure]);

    private CameraController Connected(params CameraControl[] controls)
    {
        var c = Controller(controls);
        c.Poll();
        return c;
    }

    [Fact]
    public void FirstConnectAdoptsCameraValuesWithoutWriting()
    {
        var c = Connected();
        Assert.True(c.Connected);
        Assert.Equal(90, c.Values["brightness"]);
        Assert.Empty(_fake.SetLog);
        Assert.Equal(150, _store.GetProfile("Default")!.Values["zoom"]);
        Assert.False(c.IsDirty);
    }

    [Fact]
    public void ConnectBacksUpCameraFirst()
    {
        Connected();
        var b = _store.Backups().First();
        Assert.Equal(90, b.Profile.Values["brightness"]);
        Assert.Equal(ProfileStore.ConnectReason, b.Profile.Name);
    }

    [Fact]
    public void ConnectAppliesActiveProfile()
    {
        _store.Save(new Profile { Name = "Default", Values = new() { ["zoom"] = 300, ["brightness"] = 90 } });
        var c = Connected();
        Assert.Equal(300, c.Values["zoom"]);
        Assert.False(c.IsDirty);
    }

    [Fact]
    public void LiveChangeIsDirtyUntilSaved()
    {
        var c = Connected();
        c.Set(K.Brightness, 120);
        Assert.Equal(120, c.Values["brightness"]);
        Assert.True(c.IsDirty);
        c.Save();
        Assert.False(c.IsDirty);
        Assert.Equal(120, _store.GetProfile("Default")!.Values["brightness"]);
    }

    [Fact]
    public void RevertRestoresSavedValuesOnCamera()
    {
        var c = Connected();
        c.Set(K.Brightness, 200);
        c.Revert();
        Assert.Equal(90, _fake.Current["brightness"]);
        Assert.False(c.IsDirty);
    }

    [Fact]
    public void SetClampsToRange()
    {
        var c = Connected();
        c.Set(K.Zoom, 10);
        Assert.Equal(100, c.Values["zoom"]);
    }

    [Fact]
    public void AutoExposureDriftIsNotDirty()
    {
        var device = new FakeBackend(KiyoDevice.V2Pro);
        device.Stub(K.Zoom, 150, 100, 400, 110);
        device.Stub(K.AutoExposure, 8, def: 8);
        device.Stub(K.ExposureTime, 156, 3, 2047, 156);
        var c = new CameraController(_store, () => device, [K.Zoom, K.AutoExposure]);
        c.Poll();
        device.Current["exposureTime"] = 400;
        c.Refresh();
        Assert.False(c.IsDirty);
    }

    [Fact]
    public void ResetAllBacksUpThenAppliesDefaults()
    {
        var c = Connected();
        c.ResetAll();
        Assert.Equal(128, c.Values["brightness"]);
        Assert.Contains(_store.Backups(), b => b.Profile.Name == "before-reset" && b.Profile.Values["brightness"] == 90);
    }

    [Fact]
    public void RestoreBacksUpThenApplies()
    {
        var c = Connected();
        var path = _store.BackupValues(new Dictionary<string, int> { ["brightness"] = 10 }, null, "old");
        c.Restore(_store.Backups().Single(b => b.Path == path));
        Assert.Equal(10, c.Values["brightness"]);
        Assert.Contains(_store.Backups(), b => b.Profile.Name == "before-restore");
    }

    [Fact]
    public void SwitchingProfileAppliesIt()
    {
        _store.Save(new Profile { Name = "Streaming", Values = new() { ["zoom"] = 250 } });
        var c = Connected();
        c.SwitchProfile("Streaming");
        Assert.Equal("Streaming", c.ActiveProfile);
        Assert.Equal(250, c.Values["zoom"]);
        Assert.Equal("Streaming", _store.ActiveProfileName);
    }

    [Fact]
    public void CreateProfileSavesCurrentValues()
    {
        var c = Connected();
        c.Set(K.Zoom, 200);
        Assert.True(c.CreateProfile("Calls"));
        Assert.Equal("Calls", c.ActiveProfile);
        Assert.Equal(200, _store.GetProfile("Calls")!.Values["zoom"]);
        Assert.Contains("Calls", c.ProfileNames);
    }

    [Fact]
    public void NewProfileNeverOverwritesAnExistingFile()
    {
        var c = Connected(K.Zoom);
        c.Set(K.Zoom, 300);
        Assert.False(c.CreateProfile("default"));
        Assert.False(c.CreateProfile("Default"));
        Assert.Equal(150, _store.GetProfile("Default")!.Values["zoom"]);
        Assert.Equal("Default", c.ActiveProfile);
    }

    [Fact]
    public void ProfilesThatMapToTheSameFileAreRefused()
    {
        var c = Connected(K.Zoom);
        Assert.True(c.CreateProfile("a/b"));
        c.Set(K.Zoom, 300);
        Assert.False(c.CreateProfile("a-b"));
        Assert.Equal(150, _store.GetProfile("a/b")!.Values["zoom"]);
        Assert.Equal("a/b", c.ActiveProfile);
    }

    [Fact]
    public void DetectsUnplugAndReconnects()
    {
        var c = Connected();
        _fake.Unplugged = true;
        c.Poll();
        Assert.False(c.Connected);
        _fake.Unplugged = false;
        c.Poll();
        Assert.True(c.Connected);
    }

    [Fact]
    public void NotConnectedWhenCameraAbsent()
    {
        var c = new CameraController(_store, () => null, [K.Zoom]);
        c.Poll();
        Assert.False(c.Connected);
    }

    [Fact]
    public void UnsupportedControlsAreDropped()
    {
        var c = Connected(K.Zoom, K.Focus);
        Assert.True(c.Connected);
        Assert.DoesNotContain(K.Focus, c.Controls);
    }

    [Fact]
    public void ReapplyRestoresValuesTheCameraLost()
    {
        var c = Connected();
        c.Set(K.Zoom, 250);
        _fake.Current["zoom"] = 110; // camera resets itself, e.g. across sleep
        c.Reapply();
        Assert.Equal(250, _fake.Current["zoom"]);
        Assert.Equal(250, c.Values["zoom"]);
    }
}
