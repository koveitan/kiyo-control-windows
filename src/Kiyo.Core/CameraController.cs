namespace Kiyo.Core;

/// <summary>
/// Connection, live values, profiles and backups for one camera. UI-independent so it can be unit tested.
/// Not thread-safe: call it from one thread (the UI thread in the app).
/// </summary>
public sealed class CameraController
{
    public bool Connected { get; private set; }
    public Dictionary<string, int> Values { get; private set; } = [];
    public Dictionary<string, ControlRange> Ranges { get; private set; } = [];
    public Dictionary<string, int> SavedValues { get; private set; } = [];
    public RazerSettings Razer { get; private set; } = new();
    public RazerSettings SavedRazer { get; private set; } = new();
    public List<ZoomPreset?> ZoomPresets { get; private set; } = Profile.EmptyPresets();
    private List<ZoomPreset?> _savedZoomPresets = Profile.EmptyPresets();
    public string ActiveProfile { get; private set; }
    public List<string> ProfileNames { get; private set; } = [];
    public List<ProfileStore.Backup> Backups { get; private set; } = [];
    public string? LastError { get; set; }
    public KiyoDevice Device => _camera?.Device ?? _lastDevice;
    public bool SupportsRazer => Device.RazerCommands;
    public string? FirmwareVersion => _camera?.FirmwareVersion;
    public IReadOnlyList<CameraControl> Controls => _controls;

    public ProfileStore Store { get; }
    private KiyoDevice _lastDevice = KiyoDevice.ProUltra;
    private List<CameraControl> _controls;
    private readonly List<CameraControl> _baseControls;
    private readonly Func<ICameraBackend?> _connect;
    private ICameraBackend? _camera;

    public CameraController(ProfileStore store, Func<ICameraBackend?> connect, IEnumerable<CameraControl>? controls = null)
    {
        Store = store;
        _connect = connect;
        _baseControls = (controls ?? KiyoControls.All).ToList();
        _controls = [.. _baseControls];
        ActiveProfile = store.ActiveProfileName is { Length: > 0 } n ? n : "Default";
        ReloadLists();
    }

    /// <summary>Unsaved changes, ignoring values the camera drives itself while in auto mode.</summary>
    public bool IsDirty =>
        Razer != SavedRazer || !ZoomPresets.SequenceEqual(_savedZoomPresets) ||
        !SameValues(KiyoControls.Comparable(Values, _controls), KiyoControls.Comparable(SavedValues, _controls));

    private bool ManualExposure => Values.TryGetValue(KiyoControls.AutoExposure.Id, out var v) && v == KiyoControls.ExposureManual;

    public int Value(CameraControl c) =>
        Values.TryGetValue(c.Id, out var v) ? v : Ranges.TryGetValue(c.Id, out var r) ? r.Default : 0;

    public ControlRange? Range(CameraControl c) => Ranges.TryGetValue(c.Id, out var r) ? r : null;

    public bool IsAuto(CameraControl c) => KiyoControls.IsAuto(c.Id, Values.TryGetValue(c.Id, out var v) ? v : null);

    public bool Has(CameraControl c) => Ranges.ContainsKey(c.Id);

    // Connection

    /// <summary>Connects when the camera appears and notices when it goes away. Call periodically.</summary>
    public void Poll()
    {
        if (_camera is null) { ConnectIfPossible(); return; }
        try { _camera.Get(_controls[0]); }
        catch (CameraException) { Disconnect(); }
    }

    /// <summary>Re-reads values from the camera.</summary>
    public void Refresh()
    {
        if (_camera is not null) Values = _camera.Snapshot(_controls);
    }

    /// <summary>Writes the current values again, e.g. after the PC wakes and the camera lost them.</summary>
    public void Reapply() => Apply(Values, Razer);

    /// <summary>Releases the camera (e.g. on exit).</summary>
    public void Disconnect()
    {
        _camera?.Dispose();
        _camera = null;
        Connected = false;
    }

    private void ConnectIfPossible()
    {
        if (_baseControls.Count == 0) return;
        var cam = _connect();
        if (cam is null) return;
        var probe = _baseControls[0];
        // If the camera does not answer, every control would wait for a timeout and startup would freeze.
        if (!TryGet(cam, probe)) { cam.Dispose(); return; }
        // Without Razer's ISO and shutter commands, manual exposure uses the standard exposure time and gain.
        var controls = _baseControls.Concat(cam.Device.RazerCommands ? [] : new[] { KiyoControls.ExposureTime, KiyoControls.Gain }).ToList();
        var r = new Dictionary<string, ControlRange>();
        foreach (var c in controls)
        {
            try { r[c.Id] = cam.Range(c); }
            catch (CameraException)
            {
                // One control can fail normally. If the camera stops answering, stop here instead of freezing.
                if (!TryGet(cam, probe)) { cam.Dispose(); return; }
            }
        }
        if (r.Count == 0) { cam.Dispose(); return; }

        // Controls the camera does not support are dropped, so they are never read or written.
        _controls = controls.Where(c => r.ContainsKey(c.Id)).ToList();
        _camera = cam;
        _lastDevice = cam.Device;
        Ranges = r;
        Connected = true;
        var current = cam.Snapshot(_controls);
        // Always keep the camera's state from before we touch anything.
        TryBackup(current, Razer, ProfileStore.ConnectReason);

        if (Store.GetProfile(ActiveProfile) is Profile profile)
        {
            Load(profile);
        }
        else
        {
            // First run: adopt whatever the camera has now (e.g. settings made in Synapse).
            Values = current;
            Save();
        }
        ReloadLists();
    }

    private static bool TryGet(ICameraBackend cam, CameraControl c)
    {
        try { cam.Get(c); return true; } catch (CameraException) { return false; }
    }

    // Live changes

    public void Set(CameraControl c, int newValue)
    {
        if (_camera is null) return;
        var v = newValue;
        if (c.Kind == ControlKind.Range && Ranges.TryGetValue(c.Id, out var r)) v = Math.Clamp(v, r.Min, Math.Max(r.Min, r.Max));
        try
        {
            _camera.Set(c, v);
            Values[c.Id] = v;
            // Toggling an auto mode changes what the camera reports for the values it gates.
            if (c.Kind != ControlKind.Range) Refresh();
            if (c.Id == KiyoControls.AutoExposure.Id && ManualExposure) Send(Razer.ExposureCommands);
        }
        catch (CameraException e) { Report(e, $"Could not set {c.Name}"); }
    }

    /// <summary>Changes Razer settings and sends only the commands that changed.</summary>
    public void SetRazer(Action<RazerSettings> change)
    {
        var next = Razer with { };
        change(next);
        var before = Razer.Commands(ManualExposure);
        Razer = next;
        Send(next.Commands(ManualExposure).Where(cmd => !before.Any(b => b.SequenceEqual(cmd))).ToList());
    }

    /// <summary>Remembers the current zoom, pan and tilt in preset slot 0...4.</summary>
    public void StoreZoomPreset(int slot)
    {
        if (slot < 0 || slot >= ZoomPresets.Count) return;
        ZoomPresets[slot] = new ZoomPreset(Value(KiyoControls.Zoom), Value(KiyoControls.Pan), Value(KiyoControls.Tilt));
    }

    /// <summary>Moves the camera to the view stored in a preset slot.</summary>
    public void ApplyZoomPreset(int slot)
    {
        if (slot < 0 || slot >= ZoomPresets.Count || ZoomPresets[slot] is not ZoomPreset p) return;
        Set(KiyoControls.Zoom, p.Zoom);
        if (Has(KiyoControls.Pan)) Set(KiyoControls.Pan, p.Pan);
        if (Has(KiyoControls.Tilt)) Set(KiyoControls.Tilt, p.Tilt);
    }

    public void ResetToDefault(IEnumerable<CameraControl> list)
    {
        foreach (var c in list)
            if (Ranges.TryGetValue(c.Id, out var r)) Set(c, r.Default);
    }

    // Profiles

    /// <summary>
    /// Saves the profile on the PC. Changed Razer settings are also stored in the camera itself, so
    /// they stick without the app; standard settings can't be, the app re-applies those on connect.
    /// </summary>
    public void Save()
    {
        if (Razer != SavedRazer) SaveToCamera();
        try
        {
            Store.Save(new Profile { Name = ActiveProfile, Values = new(Values), Razer = Razer with { }, ZoomPresets = [.. ZoomPresets] });
            SavedValues = new(Values);
            SavedRazer = Razer with { };
            _savedZoomPresets = [.. ZoomPresets];
            ReloadLists();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Report(e, "Could not save profile"); }
    }

    public void Revert()
    {
        ZoomPresets = [.. _savedZoomPresets];
        Apply(SavedValues, SavedRazer);
    }

    public void SwitchProfile(string name)
    {
        if (Store.GetProfile(name) is not Profile p) return;
        ActiveProfile = name;
        Store.ActiveProfileName = name;
        Load(p);
        ReloadLists();
    }

    /// <summary>Saves the current settings as a new profile. Returns false if the name is empty or taken.</summary>
    public bool CreateProfile(string name)
    {
        name = name.Trim();
        // Refuse names that would land on an existing file (Windows file names ignore case; invalid characters map to "-").
        if (name.Length == 0 || Store.GetProfile(name) is not null ||
            ProfileNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) ||
            File.Exists(Store.ProfilePath(name))) return false;
        ActiveProfile = name;
        Store.ActiveProfileName = name;
        Save();
        return true;
    }

    public void DeleteActiveProfile()
    {
        if (ProfileNames.Count <= 1) return;
        try { Store.DeleteProfile(ActiveProfile); } catch (IOException) { }
        ReloadLists();
        var next = ProfileNames.FirstOrDefault(n => n != ActiveProfile);
        if (next is not null) SwitchProfile(next);
    }

    /// <summary>Stores the current settings in the camera's own memory, after backing them up.</summary>
    public void SaveToCamera()
    {
        if (_camera is null || !_camera.Device.RazerCommands) return;
        BackupCurrent("before-save-to-camera");
        try { _camera.SaveToCamera(); }
        catch (CameraException e) { Report(e, "Could not save to the camera"); }
    }

    // Backups

    public void ResetAll()
    {
        BackupCurrent("before-reset");
        var neutral = Razer with
        {
            Mirror = false,
            Metering = RazerCommand.Metering.Average,
            ExposureCompensation = 0,
            FaceFocus = false,
            StylizedLighting = false,
        };
        Apply(Ranges.ToDictionary(kv => kv.Key, kv => kv.Value.Default), neutral);
    }

    public void Restore(ProfileStore.Backup backup)
    {
        BackupCurrent("before-restore");
        Apply(backup.Profile.Values, backup.Profile.Razer);
    }

    // Helpers

    /// <summary>
    /// Applies a profile; what the camera ends up with becomes the saved baseline, so controls
    /// missing from older profiles don't count as unsaved changes.
    /// </summary>
    private void Load(Profile profile)
    {
        ZoomPresets = [.. profile.ZoomPresets];
        _savedZoomPresets = [.. profile.ZoomPresets];
        Apply(profile.Values, profile.Razer);
        SavedValues = new(Values);
        SavedRazer = Razer with { };
    }

    private void Apply(IReadOnlyDictionary<string, int> target, RazerSettings razer)
    {
        if (_camera is null) return;
        var failed = _camera.Apply(target, Ranges, _controls);
        Values = _camera.Snapshot(_controls);
        Razer = razer with { };
        Send(Razer.Commands(ManualExposure));
        if (failed.Count > 0)
            LastError = "Could not apply: " + string.Join(", ", failed.Keys.Select(id => _controls.FirstOrDefault(c => c.Id == id)?.Name ?? id));
    }

    private void BackupCurrent(string reason)
    {
        if (_camera is null) return;
        TryBackup(_camera.Snapshot(_controls), Razer, reason);
        ReloadLists();
    }

    private void TryBackup(Dictionary<string, int> values, RazerSettings razer, string reason)
    {
        try { Store.BackupValues(values, razer, reason); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Report(e, "Backup failed"); }
    }

    private void Send(List<byte[]> commands)
    {
        if (_camera is null || !_camera.Device.RazerCommands) return;
        foreach (var cmd in commands)
        {
            try { _camera.Send(cmd); }
            catch (CameraException e) { Report(e, "Could not apply a Razer setting"); }
        }
    }

    private void ReloadLists()
    {
        ProfileNames = Store.ProfileNames();
        if (!ProfileNames.Contains(ActiveProfile)) ProfileNames.Add(ActiveProfile);
        Backups = Store.Backups();
    }

    private void Report(Exception e, string context)
    {
        LastError = $"{context}: {e.Message}";
        if (e is CameraException { IsDisconnected: true }) Disconnect();
    }

    private static bool SameValues(Dictionary<string, int> a, Dictionary<string, int> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);
}
