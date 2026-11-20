using Kiyo.Core;
using static Kiyo.Core.RazerCommand;

namespace Kiyo.Core.Tests;

/// <summary>Byte layouts from USB captures of Synapse with a Kiyo Pro Ultra (cameractrls issue #19), same as the Mac tests.</summary>
public class RazerCommandTests
{
    [Fact]
    public void MirrorBytes()
    {
        Assert.Equal("c00e030100000000", Hex(Mirror(true)));
        Assert.Equal("c00e030000000000", Hex(Mirror(false)));
    }

    [Fact]
    public void NoiseReductionBytes()
    {
        Assert.Equal("c00e010100000000", Hex(NoiseReduction3D(true)));
        Assert.Equal("c00e020000000000", Hex(NoiseReduction2D(false)));
    }

    [Fact]
    public void MeteringBytes()
    {
        Assert.Equal("c00e040000000000", Hex(MeteringMode(Metering.Average)));
        Assert.Equal("c00e040100000000", Hex(MeteringMode(Metering.Center)));
        Assert.Equal("c00e040500000000", Hex(MeteringMode(Metering.Face)));
    }

    [Fact]
    public void ExposureCompensationInTenthsOfAStop()
    {
        Assert.Equal("c00e050000000000", Hex(ExposureCompensation(-30)));
        Assert.Equal("c00e051e00000000", Hex(ExposureCompensation(0)));
        Assert.Equal("c00e053c00000000", Hex(ExposureCompensation(30)));
        Assert.Equal("c00e053c00000000", Hex(ExposureCompensation(99)));
    }

    [Fact]
    public void IsoBytes()
    {
        Assert.Equal("c009010100000000", Hex(Iso(100)));
        Assert.Equal("c009010700000000", Hex(Iso(6400)));
        Assert.Equal([100, 200, 400, 800, 1600, 3200, 6400], IsoSteps);
    }

    [Fact]
    public void ShutterInMicroseconds()
    {
        Assert.Equal("c00905000001f400", Hex(Shutter(500)));
        Assert.Equal("c00905000186a000", Hex(Shutter(100_000)));
        Assert.Equal("c009050000411a00", Hex(Shutter(16_666)));
    }

    [Fact]
    public void FocusModeAndLightingShareOneCommand()
    {
        Assert.Equal("c00a010000000000", Hex(Focus(false, false)));
        Assert.Equal("c00a010100000000", Hex(Focus(true, false)));
        Assert.Equal("c00a010000000001", Hex(Focus(false, true)));
    }

    [Fact]
    public void TrackingAndLensCorrection()
    {
        Assert.Equal("ff06000000000000", Hex(Tracking(true)));
        Assert.Equal("ff06010000000000", Hex(Tracking(false)));
        Assert.Equal("ff01010300000000", Hex(LensCorrection(true)));
        Assert.Equal("ff01000300000000", Hex(LensCorrection(false)));
    }

    [Fact]
    public void SaveToCameraSendsTheSameSequenceAsSynapse()
    {
        var fake = new FakeBackend();
        fake.SaveToCamera();
        Assert.Equal(["c0090a0000000000", "c003a80000000000"], fake.RazerWrites.Select(Hex));
    }
}
