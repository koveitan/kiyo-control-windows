using System.Text.Json;
using Kiyo.Core;
using K = Kiyo.Core.KiyoControls;

namespace Kiyo.Core.Tests;

public class RazerSettingsTests
{
    private readonly FakeBackend _fake = new();
    private readonly ProfileStore _store = TestStore.Create();
    private static readonly byte[] SaveCommand = [0xC0, 0x03, 0xA8, 0, 0, 0, 0, 0];

    public RazerSettingsTests()
    {
        _fake.Stub(K.Zoom, cur: 150, min: 100, max: 400, def: 110);
        _fake.Stub(K.AutoExposure, cur: 8, def: 8);
    }

    private CameraController Connected()
    {
        var c = new CameraController(_store, () => _fake, [K.Zoom, K.AutoExposure]);
        c.Poll();
        return c;
    }

    private List<string> Writes => _fake.RazerWrites.Select(RazerCommand.Hex).ToList();

    [Fact]
    public void UnknownSettingsProduceNoCommands() => Assert.Empty(new RazerSettings().Commands(true));

    [Fact]
    public void FocusCommandCombinesModeAndLighting() =>
        Assert.Equal(RazerCommand.Focus(true, false), new RazerSettings { FaceFocus = true }.Commands(false).Single());

    [Fact]
    public void IsoAndShutterOnlyInManualExposure()
    {
        var s = new RazerSettings { Iso = 400, ShutterMicroseconds = 16_666 };
        Assert.Empty(s.Commands(false));
        Assert.Equal([RazerCommand.Iso(400), RazerCommand.Shutter(16_666)], s.Commands(true));
    }

    [Fact]
    public void FaceMeteringClampsCompensationToItsRange()
    {
        var s = new RazerSettings { Metering = RazerCommand.Metering.Face, ExposureCompensation = -30 };
        Assert.Contains(s.Commands(false), c => c.SequenceEqual(RazerCommand.ExposureCompensation(-10)));
    }

    [Fact]
    public void ProfilesWithoutRazerSettingsOrPresetsStillLoad()
    {
        var dir = _store.ProfilesDir;
        File.WriteAllText(Path.Combine(dir, "Old.json"), """{"name":"Old","values":{"zoom":150},"savedAt":"2026-10-07T11:28:08Z"}""");
        var p = _store.GetProfile("Old")!;
        Assert.Equal(new RazerSettings(), p.Razer);
        Assert.Equal(5, p.ZoomPresets.Count);
        Assert.All(p.ZoomPresets, Assert.Null);
    }

    [Fact]
    public void FirstConnectSendsNoRazerCommands()
    {
        Connected();
        Assert.Empty(_fake.RazerWrites);
    }

    [Fact]
    public void ChangingASettingSendsOnlyThatCommand()
    {
        var c = Connected();
        c.SetRazer(r => r.Mirror = true);
        Assert.Equal([RazerCommand.Hex(RazerCommand.Mirror(true))], Writes);
        Assert.True(c.IsDirty);
    }

    [Fact]
    public void SavedRazerSettingsAreAppliedOnConnect()
    {
        _store.Save(new Profile
        {
            Name = "Default", Values = new() { ["zoom"] = 150 },
            Razer = new RazerSettings { Mirror = true, Metering = RazerCommand.Metering.Face },
        });
        Connected();
        Assert.Equal([RazerCommand.Hex(RazerCommand.Mirror(true)), RazerCommand.Hex(RazerCommand.MeteringMode(RazerCommand.Metering.Face))], Writes);
    }

    [Fact]
    public void SaveAndRevertCoverRazerSettings()
    {
        var c = Connected();
        c.SetRazer(r => r.Mirror = true);
        c.Save();
        Assert.True(_store.GetProfile("Default")!.Razer.Mirror);
        c.SetRazer(r => r.Mirror = false);
        c.Revert();
        Assert.Equal(RazerCommand.Mirror(true), _fake.RazerWrites.Last());
        Assert.False(c.IsDirty);
    }

    [Fact]
    public void SwitchingToManualExposureSendsIsoAndShutter()
    {
        var c = Connected();
        c.SetRazer(r => r.Iso = 800);
        Assert.Empty(_fake.RazerWrites); // auto exposure is on
        c.Set(K.AutoExposure, K.ExposureManual);
        Assert.Equal([RazerCommand.Hex(RazerCommand.Iso(800))], Writes);
    }

    [Fact]
    public void SaveWritesChangedRazerSettingsToTheCameraAfterABackup()
    {
        var c = Connected();
        c.SetRazer(r => r.Mirror = true);
        c.Save();
        Assert.Equal(SaveCommand, _fake.RazerWrites.Last());
        Assert.Contains(_store.Backups(), b => b.Profile.Name == "before-save-to-camera");
    }

    [Fact]
    public void SaveWithOnlyStandardChangesDoesNotWriteTheCamera()
    {
        var c = Connected();
        c.Set(K.Zoom, 300);
        c.Save();
        Assert.DoesNotContain(_fake.RazerWrites, w => w.SequenceEqual(SaveCommand));
    }

    [Fact]
    public void NoRazerCommandsForCamerasWithoutThem()
    {
        var v2 = new FakeBackend(KiyoDevice.V2Pro);
        v2.Stub(K.Zoom, 150, 100, 400, 110);
        var c = new CameraController(_store, () => v2, [K.Zoom]);
        c.Poll();
        c.SetRazer(r => r.Mirror = true);
        c.Save();
        Assert.Empty(v2.RazerWrites);
    }

    [Fact]
    public void BackupsIncludeKnownRazerSettings()
    {
        var c = Connected();
        c.SetRazer(r => r.Mirror = true);
        c.ResetAll();
        Assert.True(_store.Backups().First(b => b.Profile.Name == "before-reset").Profile.Razer.Mirror);
    }

    [Fact]
    public void JsonUsesTheMacKeyNames()
    {
        var json = JsonSerializer.Serialize(new RazerSettings { NoiseReduction3D = true, Metering = RazerCommand.Metering.Face }, Profile.Json);
        Assert.Contains("\"noiseReduction3D\": true", json);
        Assert.Contains("\"metering\": 5", json);
    }
}
