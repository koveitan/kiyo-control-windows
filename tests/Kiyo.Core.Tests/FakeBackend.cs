using Kiyo.Core;

namespace Kiyo.Core.Tests;

/// <summary>In-memory camera: stores a value per control and records every write.</summary>
internal sealed class FakeBackend(KiyoDevice? device = null, string id = "fake-camera") : ICameraBackend
{
    public KiyoDevice Device { get; } = device ?? KiyoDevice.ProUltra;
    public string Id { get; } = id;
    public string? FirmwareVersion => "8.21";

    public Dictionary<string, int> Current = [];
    public Dictionary<string, ControlRange> Ranges = [];
    public HashSet<string> Failing = [];
    public bool Unplugged;
    public List<string> SetLog = [];
    public List<byte[]> RazerWrites = [];

    public void Stub(CameraControl c, int cur, int min = 0, int max = 255, int def = 0)
    {
        Current[c.Id] = cur;
        Ranges[c.Id] = new ControlRange(min, max, 1, def);
    }

    private void Check(CameraControl c)
    {
        if (Unplugged) throw new CameraException("unplugged", isDisconnected: true);
        if (Failing.Contains(c.Id) || !Ranges.ContainsKey(c.Id)) throw new CameraException("fake");
    }

    public ControlRange GetRange(CameraControl c) { Check(c); return Ranges[c.Id]; }

    public int Get(CameraControl c) { Check(c); return Current[c.Id]; }

    public void Set(CameraControl c, int value)
    {
        Check(c);
        Current[c.Id] = value;
        SetLog.Add(c.Id);
    }

    public byte[] RazerExchange(byte[] command)
    {
        if (Unplugged) throw new CameraException("unplugged", isDisconnected: true);
        RazerWrites.Add(command);
        return new byte[8];
    }

    public void Dispose() { }
}

internal static class TestStore
{
    public static ProfileStore Create(int maxBackups = 20) =>
        new(Path.Combine(Path.GetTempPath(), "kiyo-tests-" + Guid.NewGuid().ToString("N")), maxBackups);
}
