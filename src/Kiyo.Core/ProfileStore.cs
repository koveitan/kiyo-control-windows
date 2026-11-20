using System.Text.Json;

namespace Kiyo.Core;

/// <summary>Profiles and automatic backups on disk, under %APPDATA%\KiyoControl.</summary>
public sealed class ProfileStore
{
    public const string ConnectReason = "on-connect";

    public string Root { get; }
    public int MaxBackups { get; }
    public string ProfilesDir => Path.Combine(Root, "profiles");
    public string BackupsDir => Path.Combine(Root, "backups");
    private string StateFile => Path.Combine(Root, "state.json");

    public sealed record Backup(string Path, Profile Profile);

    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KiyoControl");

    public ProfileStore(string? root = null, int maxBackups = 20)
    {
        Root = root ?? DefaultRoot;
        MaxBackups = maxBackups;
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(BackupsDir);
    }

    // Profiles

    public List<string> ProfileNames() =>
        Directory.EnumerateFiles(ProfilesDir, "*.json")
            .Select(Load).OfType<Profile>().Select(p => p.Name)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public Profile? GetProfile(string name) => Load(ProfilePath(name));

    public void Save(Profile profile) => Write(profile, ProfilePath(profile.Name));

    public void DeleteProfile(string name) => File.Delete(ProfilePath(name));

    public string? ActiveProfileName
    {
        get
        {
            try
            {
                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(StateFile));
                return d?.GetValueOrDefault("activeProfile");
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
        }
        set => WriteAtomic(StateFile, JsonSerializer.Serialize(new Dictionary<string, string> { ["activeProfile"] = value ?? "" }));
    }

    // Backups

    /// <summary>
    /// Stores a timestamped snapshot. Connect backups identical to the previous one are skipped, and
    /// connect backups are pruned separately so frequent replugs never push out other backups.
    /// </summary>
    public string BackupValues(IReadOnlyDictionary<string, int> values, RazerSettings? razer, string reason)
    {
        razer ??= new RazerSettings();
        var existing = Backups();
        if (reason == ConnectReason)
        {
            var last = existing.FirstOrDefault(b => b.Profile.Name == reason);
            if (last is not null && SameValues(last.Profile.Values, values) && last.Profile.Razer == razer) return last.Path;
        }
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss.fffZ");
        var path = Path.Combine(BackupsDir, $"{stamp} {reason} {Guid.NewGuid().ToString("N")[..6]}.json");
        Write(new Profile { Name = reason, Values = new(values), Razer = razer }, path);

        var all = Backups();
        var connects = all.Where(b => b.Profile.Name == ConnectReason).Skip(MaxBackups);
        var others = all.Where(b => b.Profile.Name != ConnectReason).Skip(MaxBackups);
        foreach (var old in connects.Concat(others))
        {
            try { File.Delete(old.Path); } catch (IOException) { }
        }
        return path;
    }

    /// <summary>Backups, newest first.</summary>
    public List<Backup> Backups() =>
        Directory.EnumerateFiles(BackupsDir, "*.json")
            .Select(p => Load(p) is Profile pr ? new Backup(p, pr) : null).OfType<Backup>()
            .OrderByDescending(b => b.Profile.SavedAt).ThenByDescending(b => b.Path, StringComparer.Ordinal)
            .ToList();

    // Helpers

    /// <summary>File for a profile name. Characters Windows forbids in file names become "-".</summary>
    public string ProfilePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).TrimEnd('.', ' ');
        return Path.Combine(ProfilesDir, (safe.Length == 0 ? "-" : safe) + ".json");
    }

    private static bool SameValues(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    private static Profile? Load(string path)
    {
        try
        {
            var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Profile.Json);
            p?.Normalize();
            return p is { Name.Length: > 0 } ? p : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static void Write(Profile profile, string path) =>
        WriteAtomic(path, JsonSerializer.Serialize(profile, Profile.Json));

    private static void WriteAtomic(string path, string text)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, overwrite: true);
    }
}
