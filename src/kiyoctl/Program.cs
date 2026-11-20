using System.Text.Json;
using Kiyo.Core;
using Kiyo.Windows;
using K = Kiyo.Core.KiyoControls;

namespace KiyoCtl;

internal static class Program
{
    private const string Usage = """
        kiyoctl - Razer Kiyo camera settings from the command line

          kiyoctl devices                 video devices Windows knows
          kiyoctl info                    model, firmware, Razer extension unit
          kiyoctl list                    every control with value and range
          kiyoctl probe                   raw driver properties and topology (read-only)
          kiyoctl set <control> <value>   e.g. kiyoctl set zoom 200
          kiyoctl razer <setting> <value> e.g. kiyoctl razer mirror on
                  settings: mirror on|off, nr3d on|off, nr2d on|off, metering average|center|face,
                            ev <-30..30 tenths>, iso <100..6400>, shutter <microseconds>,
                            focus standard|face, tracking passive|responsive, lens on|off
          kiyoctl backup                  save the camera's current values as a backup
          kiyoctl restore <backup.json>   apply the values of a backup
          kiyoctl save-to-camera --yes    store the Razer settings in the camera's memory (like Synapse's Save)
        """;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        try
        {
            return args[0] switch
            {
                "devices" => Devices(),
                "info" => WithCamera(Info),
                "list" => WithCamera(List),
                "probe" => WithCamera(Probe),
                "set" when args.Length == 3 => WithCamera(c => Set(c, args[1], args[2])),
                "razer" when args.Length == 3 => WithCamera(c => Razer(c, args[1], args[2])),
                "backup" => WithCamera(Backup),
                "restore" when args.Length == 2 => WithCamera(c => Restore(c, args[1])),
                "save-to-camera" when args.Length == 2 && args[1] == "--yes" => WithCamera(SaveToCamera),
                "save-to-camera" => Fail("This writes the camera's memory. Run 'kiyoctl save-to-camera --yes' to confirm."),
                _ => Fail(Usage),
            };
        }
        catch (CameraException e)
        {
            return Fail(e.Message);
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static int WithCamera(Func<WindowsCameraBackend, int> action)
    {
        using var cam = WindowsCameraBackend.Connect();
        if (cam is null) return Fail("No Razer Kiyo Pro Ultra or Kiyo V2 Pro found. Is it plugged in?");
        return action(cam);
    }

    private static IEnumerable<CameraControl> AllControls(WindowsCameraBackend cam) =>
        K.All.Concat(cam.Device.RazerCommands ? [] : new[] { K.ExposureTime, K.Gain });

    private static int Devices()
    {
        var devices = WindowsCameraBackend.ListDevices();
        if (devices.Count == 0) Console.WriteLine("No video capture devices.");
        foreach (var d in devices)
            Console.WriteLine($"{d.FriendlyName}{(d.Kiyo is { } k ? $"  [supported: {k.Name}]" : "")}\n    {d.DevicePath}");
        return 0;
    }

    private static int Info(WindowsCameraBackend cam)
    {
        Console.WriteLine($"Model:     {cam.Device.Name} (USB {cam.Device.VendorId:X4}:{cam.Device.ProductId:X4})");
        Console.WriteLine($"Device:    {cam.Info.FriendlyName}");
        Console.WriteLine($"Instance:  {cam.Info.InstanceId}");
        Console.WriteLine($"Firmware:  {cam.FirmwareVersion ?? "unknown"}");
        Console.WriteLine(cam.Device.RazerCommands
            ? $"Razer XU:  {(cam.RazerNode is int n ? $"node {n}" : "NOT FOUND")}"
            : "Razer XU:  not supported on this model");
        return 0;
    }

    private static int List(WindowsCameraBackend cam)
    {
        foreach (var c in AllControls(cam))
        {
            try
            {
                var r = cam.Range(c);
                var v = cam.Get(c);
                var label = c.Kind == ControlKind.Choice ? $" ({c.Choices.FirstOrDefault(x => x.Value == v).Label})" : "";
                Console.WriteLine($"{c.Id,-18} {v,7}{label,-10} range {r.Min}..{r.Max} step {r.Step} default {r.Default}");
            }
            catch (CameraException e)
            {
                Console.WriteLine($"{c.Id,-18} unavailable: {e.Message}");
            }
        }
        return 0;
    }

    private static int Probe(WindowsCameraBackend cam)
    {
        Info(cam);
        Console.WriteLine("\nTopology nodes:");
        foreach (var (node, type, name) in cam.Topology()) Console.WriteLine($"  {node,2}  {type}  {name}");
        Console.WriteLine("\nDriver properties:");
        foreach (var line in cam.RawProperties()) Console.WriteLine("  " + line);
        if (cam.RazerNode is not null)
        {
            try { Console.WriteLine($"\nRazer result register (read): {RazerCommand.Hex(cam.ReadRazerResult())}"); }
            catch (CameraException e) { Console.WriteLine($"\nRazer result register: {e.Message}"); }
        }
        return 0;
    }

    private static int Set(WindowsCameraBackend cam, string id, string value)
    {
        if (K.ById(id) is not CameraControl c) return Fail($"Unknown control '{id}'. See 'kiyoctl list'.");
        if (!int.TryParse(value, out var v)) return Fail($"'{value}' is not a number.");
        var r = cam.Range(c);
        if (c.Kind == ControlKind.Range) v = Math.Clamp(v, r.Min, r.Max);
        cam.Set(c, v);
        Console.WriteLine($"{c.Id} = {cam.Get(c)}");
        return 0;
    }

    private static int Razer(WindowsCameraBackend cam, string setting, string value)
    {
        if (!cam.Device.RazerCommands) return Fail($"{cam.Device.Name} has no known Razer commands.");
        bool On() => value switch
        {
            "on" or "1" or "true" => true,
            "off" or "0" or "false" => false,
            _ => throw new CameraException($"Expected on or off, got '{value}'."),
        };
        int Num() => int.TryParse(value, out var n) ? n : throw new CameraException($"'{value}' is not a number.");
        byte[] cmd = setting switch
        {
            "mirror" => RazerCommand.Mirror(On()),
            "nr3d" => RazerCommand.NoiseReduction3D(On()),
            "nr2d" => RazerCommand.NoiseReduction2D(On()),
            "lens" => RazerCommand.LensCorrection(On()),
            "metering" => RazerCommand.MeteringMode(Enum.Parse<RazerCommand.Metering>(value, ignoreCase: true)),
            "ev" => RazerCommand.ExposureCompensation(Num()),
            "iso" when RazerCommand.IsoSteps.Contains(Num()) => RazerCommand.Iso(Num()),
            "shutter" => RazerCommand.Shutter(Num()),
            "focus" => RazerCommand.Focus(face: value == "face", stylized: false),
            "tracking" => RazerCommand.Tracking(responsive: value == "responsive"),
            _ => throw new CameraException($"Unknown Razer setting '{setting}' or value '{value}'. See 'kiyoctl help'."),
        };
        var reply = cam.Send(cmd);
        Console.WriteLine($"sent {RazerCommand.Hex(cmd)}, reply {(reply.Length > 0 ? RazerCommand.Hex(reply) : "none")}");
        return 0;
    }

    private static int Backup(WindowsCameraBackend cam)
    {
        var path = new ProfileStore().BackupValues(cam.Snapshot(AllControls(cam)), null, "kiyoctl");
        Console.WriteLine(path);
        return 0;
    }

    private static int Restore(WindowsCameraBackend cam, string file)
    {
        var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(file), Profile.Json)
                      ?? throw new CameraException("Not a backup file.");
        var controls = AllControls(cam).ToList();
        var ranges = new Dictionary<string, ControlRange>();
        foreach (var c in controls)
        {
            try { ranges[c.Id] = cam.Range(c); } catch (CameraException) { }
        }
        new ProfileStore().BackupValues(cam.Snapshot(controls), null, "before-restore");
        var failed = cam.Apply(profile.Values, ranges, controls.Where(c => ranges.ContainsKey(c.Id)));
        foreach (var cmd in profile.Razer?.Commands(cam.Get(K.AutoExposure) == K.ExposureManual) ?? [])
            if (cam.Device.RazerCommands) cam.Send(cmd);
        Console.WriteLine(failed.Count == 0 ? "Restored." : "Could not apply: " + string.Join(", ", failed.Keys));
        return failed.Count == 0 ? 0 : 1;
    }

    private static int SaveToCamera(WindowsCameraBackend cam)
    {
        if (!cam.Device.RazerCommands) return Fail($"{cam.Device.Name} has no known Razer commands.");
        cam.SaveToCamera();
        Console.WriteLine("Saved the camera's current Razer settings to its memory.");
        return 0;
    }
}
