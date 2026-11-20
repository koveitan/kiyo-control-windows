namespace Kiyo.Core;

public enum ControlKind { Range, Toggle, Choice }

/// <summary>
/// A camera setting the app can read and write. The backend decides how it reaches the camera
/// (on Windows: DirectShow camera control / video proc amp properties).
/// </summary>
public sealed class CameraControl : IEquatable<CameraControl>
{
    public string Id { get; }
    public string Name { get; }
    public ControlKind Kind { get; }
    /// <summary>For <see cref="ControlKind.Choice"/>: raw value and label, in display order.</summary>
    public IReadOnlyList<(int Value, string Label)> Choices { get; }
    /// <summary>Id of an auto toggle that must be off before this value can be set.</summary>
    public string? ManualOnlyWhenOff { get; }

    public CameraControl(string id, string name, ControlKind kind = ControlKind.Range,
                         IReadOnlyList<(int, string)>? choices = null, string? manualOnlyWhenOff = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Choices = choices ?? [];
        ManualOnlyWhenOff = manualOnlyWhenOff;
    }

    public bool Equals(CameraControl? other) => other is not null && other.Id == Id;
    public override bool Equals(object? obj) => Equals(obj as CameraControl);
    public override int GetHashCode() => Id.GetHashCode();
    public override string ToString() => Id;
}

public readonly record struct ControlRange(int Min, int Max, int Step, int Default);

/// <summary>A failed camera request. <see cref="IsDisconnected"/> means the camera is gone.</summary>
public class CameraException(string message, bool isDisconnected = false, Exception? inner = null)
    : Exception(message, inner)
{
    public bool IsDisconnected { get; } = isDisconnected;
}
