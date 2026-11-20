# Kiyo Control for Windows

Camera settings for the **Razer Kiyo Pro Ultra** (and the standard controls of the **Kiyo V2 Pro**) on Windows,
without Razer Synapse. A C#/.NET port of [kiyo-control](https://github.com/f0ns/kiyo-control), the macOS app,
with the camera logic split from the UI so it can be reused.

> **Unofficial.** Not affiliated with, endorsed by, or supported by Razer Inc.

## Run it

Needs Windows 10/11 and the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`).

```powershell
dotnet run --project src\KiyoControl
```

Or build a folder you can double-click from:

```powershell
dotnet publish src\KiyoControl -c Release -o dist\KiyoControl
.\dist\KiyoControl\KiyoControl.exe
```

If the preview stays black, turn on **Settings › Privacy & security › Camera › Let desktop apps access your camera**.

## What it does

- Live preview while you adjust; nothing goes into a profile until **SAVE** (Ctrl+S). **Revert** goes back.
- Camera tab: zoom, pan, tilt, 5 zoom presets (right-click to store), field of view presets, auto/manual focus,
  Razer focus mode / tracking / lighting, auto exposure with Razer metering and compensation, or manual ISO and shutter.
- Processing tab: 2D/3D noise reduction, HDR (backlight compensation), lens distortion compensation.
- Image tab: brightness, contrast, saturation, sharpness, white balance, mirror, anti-flicker.
- **SAVE** also stores changed Razer settings in the camera's memory (like Synapse), so they stick without the app.
- Profiles, re-applied when the camera is plugged in and after the PC wakes from sleep.
- Automatic backups before every reset/restore and on connect (`%APPDATA%\KiyoControl\backups`, last 20 kept).
- Closing the window keeps it in the **tray**: switch profiles, jump to zoom presets, Launch at Login, Quit.
- Works while other apps (Zoom, Teams, OBS…) use the camera; the preview then shares the stream read-only.

## Command line

```powershell
dotnet build src\kiyoctl
$k = "src\kiyoctl\bin\Debug\net10.0-windows\kiyoctl.exe"
& $k info                 # model, firmware, Razer extension unit node
& $k list                 # every control with value and range
& $k probe                # raw driver properties and topology (read-only)
& $k set zoom 200         # 2.0x
& $k razer mirror on      # Razer commands: mirror, nr3d, nr2d, metering, ev, iso, shutter, focus, tracking, lens
& $k backup
& $k restore <backup.json>
& $k save-to-camera --yes # store Razer settings in the camera's memory
```

## Tests

```powershell
dotnet test
```

55 unit tests for the core (Razer command bytes, controller, profiles, backups, presets) run against a fake camera.

## How it works

```
src/Kiyo.Core      Platform-independent: control map, Razer commands, profiles, backups, CameraController.
                   Talks to hardware only through ICameraBackend.
src/Kiyo.Windows   ICameraBackend for Windows, through the in-box camera driver (usbvideo.sys), so other apps keep streaming:
                     - standard controls via DirectShow IAMCameraControl / IAMVideoProcAmp
                     - anti-flicker via the video proc-amp power-line-frequency property
                     - Razer commands via IKsControl on the extension unit
                       (GUID 23e49ed0-1178-4f31-ae52-d2fb8a8d3b48, selector 1, reply on selector 2)
src/kiyoctl        Command-line tool.
src/KiyoControl    WPF app: settings panel, live preview (Windows.Media.Capture frame reader), tray icon.
tests/             xUnit tests for Kiyo.Core.
```

Differences from macOS, where the app sends raw UVC requests:

| | macOS | Windows |
|---|---|---|
| Razer extension unit | UVC unit 6 | kernel-streaming topology node (found by probing device-specific nodes; node 2 on the tested camera) |
| Auto exposure / focus / white balance | separate UVC controls | an *auto* flag on the value property; the app keeps the UVC values (auto exposure 8 = auto, 1 = manual) so profiles read the same |
| Pan / tilt | arc-seconds (±36000) | degrees (±10) |
| Anti-flicker | 0 / 1 / 2 | the Kiyo Pro Ultra reports 50 Hz and 60 Hz only |
| Firmware version | `bcdDevice` | `REV_xxxx` in the hardware id (e.g. 8.21) |

Razer command layouts come from USB captures of Synapse documented by the
[cameractrls](https://github.com/soyersoyer/cameractrls/issues/19) project. Never send guessed commands to the
extension unit. Firmware updates are not supported; use Razer's updater.

## License

MIT, see [LICENSE](LICENSE). Port of kiyo-control (MIT, Kiyo Control contributors).
