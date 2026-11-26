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

## Releases

Releases are built by `.github/workflows/release.yml` when a version tag is pushed:

1. Add a `## x.y.z — date` section to `CHANGELOG.md` and commit it. It becomes the release notes.
2. Tag and push:

   ```powershell
   git tag v0.2.0
   git push origin v0.2.0
   ```

The workflow runs the tests, publishes the app and `kiyoctl` self-contained for win-x64 (users need no .NET), packages
them with [Velopack](https://velopack.io) (`vpk`, pinned in `dotnet-tools.json`) and publishes a GitHub Release with
`KiyoControl-win-Setup.exe`, a portable zip and the update packages. Installed copies find new releases there.

`Program.Main` runs `VelopackApp.Build().Run()` before anything else, which handles install, update and uninstall
hooks. `Updater` checks GitHub 30 seconds after start and every 12 hours, downloads in the background, and applies
the update on quit or from **Restart to Update** in the tray. Copies run from source don't check for updates.

To try packaging locally:

```powershell
dotnet tool restore
dotnet publish src\KiyoControl -c Release -r win-x64 --self-contained -o publish
dotnet publish src\kiyoctl -c Release -r win-x64 --self-contained -o publish
dotnet vpk pack --packId KiyoControl --packVersion 0.0.1 --packDir publish --mainExe KiyoControl.exe --packTitle "Kiyo Control" --icon src\KiyoControl\Assets\app.ico --shortcuts StartMenuRoot
```

Releases are not code-signed yet, so SmartScreen warns on first run. `vpk pack` accepts `--signParams` (signtool)
or Azure Trusted Signing options once a certificate is available.

## How the Windows backend reaches the camera

- **Standard controls** (zoom, pan, tilt, focus, exposure, brightness, white balance…) go through DirectShow's
  `IAMCameraControl` and `IAMVideoProcAmp` on the camera's capture filter.
- **Anti-flicker** is the video proc-amp power-line-frequency property (id 13), which DirectShow has no enum value for.
- **Razer settings** are 8-byte commands written to the camera's vendor extension unit
  (GUID `23e49ed0-1178-4f31-ae52-d2fb8a8d3b48`, selector 1) through `IKsControl` with a `KSP_NODE` request; the camera
  answers on selector 2. Command layouts come from USB captures of Synapse documented in
  [cameractrls issue #19](https://github.com/soyersoyer/cameractrls/issues/19).
- **Firmware version** is read from the `REV_xxxx` part of the device's hardware id.

## Finding cameras

`WindowsCameraBackend.ListCameras()` reads DirectShow's video devices and keeps those whose device path names a
USB vendor and product id (virtual cameras such as OBS's have none). `CameraDiscovery` in the core classifies them
(supported, unsupported Razer, other) and chooses which one to connect to: the one the user picked last, else the
first supported one. A camera is identified by its device path; Media Foundation (used by the preview) lists the
same camera under a different interface GUID, so paths are compared up to that GUID (`CameraDiscovery.SameDevice`).

Adding a model means adding a `KiyoDevice` with its USB ids, whether it can stream 4K, and `RazerCommands: false`
unless its extension-unit commands are documented. On Windows the standard controls go through DirectShow, so no
UVC unit ids are needed.

## Keeping in sync with the macOS version

This project has its own version numbers and records which macOS version it is based on (currently 0.6.0).
When [f0ns/kiyo-control](https://github.com/f0ns/kiyo-control) changes the command list, the control map or the
profile format, port the change to `Kiyo.Core` with its tests. After porting a new upstream release, update:

- `ProjectInfo.UpstreamVersion` in `Kiyo.Core`, shown in the app's ⋯ menu and tray menu ("based on macOS 0.6.0")
- the "based on" sentence near the top of the README
- the "Based on …" line of the new changelog entry, which also becomes the release notes

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
