# Architecture

Kiyo Control for Windows is a C#/.NET port of [kiyo-control](https://github.com/f0ns/kiyo-control) for macOS. The
camera logic is kept separate from the UI so it can be tested without hardware and reused by other front ends.

## Projects

| Project | What it is |
|---|---|
| `src/Kiyo.Core` | Platform-independent logic: control map, Razer commands, profiles, backups, zoom presets and `CameraController`. Talks to hardware only through `ICameraBackend`. |
| `src/Kiyo.Windows` | `ICameraBackend` for Windows, through the in-box camera driver (`usbvideo.sys`), so other apps keep streaming. |
| `src/KiyoControl` | WPF app: settings panel, live preview (`Windows.Media.Capture` frame reader), tray icon. |
| `src/kiyoctl` | Command-line tool, including a read-only `probe` that dumps what the driver reports. |
| `tests/Kiyo.Core.Tests` | xUnit tests for `Kiyo.Core`, against an in-memory fake camera. |

## Build and test

Requires the .NET 10 SDK.

```powershell
dotnet test
dotnet run --project src\KiyoControl
```

CI runs the tests and builds the app and CLI on every push (`.github/workflows/build.yml`).

## How the Windows backend reaches the camera

- **Standard controls** (zoom, pan, tilt, focus, exposure, brightness, white balance…) go through DirectShow's
  `IAMCameraControl` and `IAMVideoProcAmp` on the camera's capture filter.
- **Anti-flicker** is the video proc-amp power-line-frequency property (id 13), which DirectShow has no enum value for.
- **Razer settings** are 8-byte commands written to the camera's vendor extension unit
  (GUID `23e49ed0-1178-4f31-ae52-d2fb8a8d3b48`, selector 1) through `IKsControl` with a `KSP_NODE` request; the camera
  answers on selector 2. Command layouts come from USB captures of Synapse documented in
  [cameractrls issue #19](https://github.com/soyersoyer/cameractrls/issues/19).
- **Firmware version** is read from the `REV_xxxx` part of the device's hardware id.

## Differences from the macOS version

The macOS app sends raw UVC requests over USB. Windows doesn't allow that while its camera driver owns the device,
so some things are expressed differently:

| | macOS | Windows |
|---|---|---|
| Razer extension unit | UVC unit 6 | A kernel-streaming topology node, found on connect by probing the device-specific nodes (node 2 on the tested camera). |
| Auto exposure / focus / white balance | Separate UVC controls | An *auto* flag on the value property. `Kiyo.Core` keeps the UVC values (auto exposure 8 = auto, 1 = manual) so profiles read the same. |
| Pan / tilt | Arc-seconds (±36000) | Degrees (±10) |
| Anti-flicker | Off / 50 Hz / 60 Hz | The Kiyo Pro Ultra reports 50 Hz and 60 Hz only, so the app offers only those. |
| Firmware version | `bcdDevice` from IOKit | `REV_xxxx` from the hardware id |

## Rules for changes

- Never send guessed commands to the Razer extension unit. Add only command layouts confirmed from USB captures of
  Synapse, with a byte-layout test like those in `RazerCommandTests`.
- The app never sends the save-to-camera command on its own; it does so only when the user presses SAVE after changing
  a Razer setting, after taking a backup.
- Changes to the shared camera logic (Razer commands, control map, profile format) are worth proposing upstream in
  [f0ns/kiyo-control](https://github.com/f0ns/kiyo-control) too.
