<p align="center">
  <img src="src/KiyoControl/Assets/icon-256.png" width="112" alt="Kiyo Control icon">
</p>

<h1 align="center">Kiyo Control for Windows</h1>

<p align="center">
  Camera settings for the <b>Razer Kiyo Pro Ultra</b> (and the standard controls of the <b>Kiyo V2 Pro</b>) on Windows, without Razer Synapse.
</p>

<p align="center">
  <a href="https://github.com/koveitan/kiyo-control-windows/actions/workflows/build.yml"><img src="https://github.com/koveitan/kiyo-control-windows/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

> **A Windows port of [f0ns/kiyo-control](https://github.com/f0ns/kiyo-control)**, the macOS app by the Kiyo Control
> contributors. The camera protocol, control map, profiles and behaviour come from that project; this repository
> rewrites them in C#/.NET with the camera logic split from the UI, and adds a Windows camera backend and app.
> Both are MIT-licensed.

> **Unofficial.** Kiyo Control for Windows is an independent, community-made project. It is not affiliated with,
> endorsed by, or supported by Razer Inc. See [Disclaimer](#disclaimer).

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

## Contributing

Issues and pull requests are welcome. Please run `dotnet test` before sending a change, and never add guessed
commands for the Razer extension unit: only layouts confirmed from USB captures of Synapse. Changes to the shared
camera logic (Razer commands, control map, profile format) are worth proposing upstream in
[f0ns/kiyo-control](https://github.com/f0ns/kiyo-control) too.

## Credits

- [kiyo-control](https://github.com/f0ns/kiyo-control) by the Kiyo Control contributors: the original macOS app
  this project is ported from (MIT).
- [cameractrls](https://github.com/soyersoyer/cameractrls) contributors, who documented the Razer command layouts
  from USB captures ([issue #19](https://github.com/soyersoyer/cameractrls/issues/19)). This project uses those facts
  about the camera's protocol; it contains no code from cameractrls, Razer Synapse or any other Razer software.

## Disclaimer

- **No warranty, use at your own risk.** This software is provided "as is", without warranty of any kind (see
  [LICENSE](LICENSE)). The authors are not liable for any damage to your camera, computer, data or anything else,
  including from settings saved to the camera.
- **Not affiliated with Razer.** Razer, Kiyo, Kiyo Pro Ultra and Synapse are trademarks or registered trademarks of
  Razer Inc. The names are used only to describe which hardware this software works with. This project is not
  affiliated with, endorsed by, sponsored by, or supported by Razer Inc. For official support, use Razer's own
  software and support channels.
- **Interoperability.** Kiyo Control for Windows talks to the camera through the standard Windows camera driver and
  documented vendor commands, solely to make the camera's settings usable without Synapse. It contains no Razer
  firmware, software, code, images or other proprietary material.
- **Warranty of your device.** Changing camera settings with third-party software might affect Razer's support or
  warranty. Check Razer's terms if that matters to you.

## License

MIT, see [LICENSE](LICENSE). The copyright notice of the original
[kiyo-control](https://github.com/f0ns/kiyo-control) is kept, as the MIT license requires.
