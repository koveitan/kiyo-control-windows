using Kiyo.Core;
using K = Kiyo.Core.KiyoControls;

namespace Kiyo.Core.Tests;

public class ProfileStoreTests
{
    [Fact]
    public void SavesAndLoadsProfiles()
    {
        var store = TestStore.Create();
        store.Save(new Profile { Name = "Streaming", Values = new() { ["zoom"] = 200 } });
        store.Save(new Profile { Name = "calls", Values = new() { ["zoom"] = 100 } });
        Assert.Equal(["calls", "Streaming"], store.ProfileNames());
        Assert.Equal(200, store.GetProfile("Streaming")!.Values["zoom"]);
    }

    [Fact]
    public void RemembersActiveProfile()
    {
        var root = TestStore.Create().Root;
        new ProfileStore(root).ActiveProfileName = "Streaming";
        Assert.Equal("Streaming", new ProfileStore(root).ActiveProfileName);
    }

    [Fact]
    public void ProfileNamesWithForbiddenCharactersAreSafe()
    {
        var store = TestStore.Create();
        store.Save(new Profile { Name = "a/b:c*?" });
        Assert.NotNull(store.GetProfile("a/b:c*?"));
    }

    [Fact]
    public void KeepsOnlyNewestBackups()
    {
        var store = TestStore.Create(maxBackups: 3);
        for (var i = 1; i <= 5; i++)
        {
            store.BackupValues(new Dictionary<string, int> { ["zoom"] = i }, null, $"test{i}");
            Thread.Sleep(20);
        }
        Assert.Equal([5, 4, 3], store.Backups().Select(b => b.Profile.Values["zoom"]));
    }

    [Fact]
    public void ConnectBackupsDoNotPushOutOtherBackups()
    {
        var store = TestStore.Create(maxBackups: 4);
        store.BackupValues(new Dictionary<string, int> { ["zoom"] = 1 }, null, "before-reset");
        for (var i = 0; i < 10; i++) store.BackupValues(new Dictionary<string, int> { ["zoom"] = 100 + i }, null, ProfileStore.ConnectReason);
        Assert.Contains(store.Backups(), b => b.Profile.Name == "before-reset");
    }

    [Fact]
    public void IdenticalConnectBackupsAreNotRepeated()
    {
        var store = TestStore.Create();
        for (var i = 0; i < 5; i++) store.BackupValues(new Dictionary<string, int> { ["zoom"] = 150 }, null, ProfileStore.ConnectReason);
        Assert.Single(store.Backups());
    }

    [Fact]
    public void BackupsInTheSameSecondDoNotOverwriteEachOther()
    {
        var store = TestStore.Create();
        store.BackupValues(new Dictionary<string, int> { ["zoom"] = 1 }, null, "before-reset");
        store.BackupValues(new Dictionary<string, int> { ["zoom"] = 2 }, null, "before-reset");
        Assert.Equal(2, store.Backups().Count);
    }
}

public class ZoomPresetTests
{
    private readonly FakeBackend _fake = new();
    private readonly ProfileStore _store = TestStore.Create();

    public ZoomPresetTests()
    {
        _fake.Stub(K.Zoom, cur: 150, min: 100, max: 400, def: 110);
        _fake.Stub(K.Pan, cur: 0, min: -10, max: 10, def: 0);
        _fake.Stub(K.Tilt, cur: 0, min: -10, max: 10, def: 0);
    }

    private CameraController Connected()
    {
        var c = new CameraController(_store, () => _fake, [K.Zoom, K.Pan, K.Tilt]);
        c.Poll();
        return c;
    }

    [Fact]
    public void StoresCurrentViewInASlot()
    {
        var c = Connected();
        c.Set(K.Zoom, 250);
        c.Set(K.Pan, 3);
        c.StoreZoomPreset(2);
        Assert.Equal(new ZoomPreset(250, 3, 0), c.ZoomPresets[2]);
        Assert.True(c.IsDirty);
    }

    [Fact]
    public void ApplyingAPresetMovesTheCamera()
    {
        var c = Connected();
        c.Set(K.Zoom, 300);
        c.Set(K.Tilt, -2);
        c.StoreZoomPreset(0);
        c.Set(K.Zoom, 100);
        c.Set(K.Tilt, 0);
        c.ApplyZoomPreset(0);
        Assert.Equal(300, _fake.Current["zoom"]);
        Assert.Equal(-2, _fake.Current["tilt"]);
    }

    [Fact]
    public void EmptyOrInvalidSlotsDoNothing()
    {
        var c = Connected();
        var writes = _fake.SetLog.Count;
        c.ApplyZoomPreset(4);
        c.ApplyZoomPreset(9);
        c.StoreZoomPreset(-1);
        Assert.Equal(writes, _fake.SetLog.Count);
        Assert.Equal(5, c.ZoomPresets.Count);
    }

    [Fact]
    public void PresetsAreSavedWithTheProfile()
    {
        var c = Connected();
        c.StoreZoomPreset(1);
        c.Save();
        Assert.Equal(new ZoomPreset(150, 0, 0), _store.GetProfile("Default")!.ZoomPresets[1]);
        Assert.False(c.IsDirty);
    }
}

public class FieldOfViewAndVideoModeTests
{
    [Fact]
    public void TwoTimesZoomIs44Degrees() => Assert.Equal(44, Math.Round(FieldOfView.Degrees(200)));

    [Fact]
    public void PresetsMatchSynapse()
    {
        Assert.Equal(78, Math.Round(FieldOfView.Degrees(FieldOfView.Preset.Wide.Zoom())));
        Assert.Equal(FieldOfView.Preset.Medium, FieldOfView.PresetFor(140));
        Assert.Null(FieldOfView.PresetFor(123));
    }

    [Fact]
    public void ModesAreDedupedAndSorted()
    {
        var modes = VideoMode.Options([(1920, 1080, 30.00003), (1920, 1080, 30), (3840, 2160, 30), (1920, 1080, 60)]);
        Assert.Equal([new(3840, 2160, 30), new(1920, 1080, 60), new(1920, 1080, 30)], modes);
        Assert.Equal(new VideoMode(1920, 1080, 30), VideoMode.Preferred(modes, null));
        Assert.Equal(new VideoMode(1920, 1080, 60), VideoMode.Parse("1920x1080@60"));
    }
}
