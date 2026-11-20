using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kiyo.Core;

/// <summary>A saved camera view for one of the zoom preset slots.</summary>
public sealed record ZoomPreset(int Zoom, int Pan, int Tilt)
{
    public const int Slots = 5;
}

/// <summary>A saved set of control values, keyed by control id.</summary>
public sealed class Profile
{
    public string Name { get; set; } = "";
    public Dictionary<string, int> Values { get; set; } = [];
    public RazerSettings Razer { get; set; } = new();
    public List<ZoomPreset?> ZoomPresets { get; set; } = EmptyPresets();
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;

    public static List<ZoomPreset?> EmptyPresets() => Enumerable.Repeat<ZoomPreset?>(null, ZoomPreset.Slots).ToList();

    /// <summary>Profiles saved before presets existed (or with fewer slots) get exactly five slots.</summary>
    internal void Normalize()
    {
        Razer ??= new RazerSettings();
        Values ??= [];
        var presets = ZoomPresets ?? [];
        ZoomPresets = Enumerable.Range(0, ZoomPreset.Slots).Select(i => i < presets.Count ? presets[i] : null).ToList();
    }

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
