<p align="center">
  <img src="src/KiyoControl/Assets/icon-256.png" width="112" alt="Kiyo Control icon">
</p>

<h1 align="center">Kiyo Control for Windows</h1>

<p align="center">
  Camera settings for the <b>Razer Kiyo Pro Ultra</b> on Windows, without Razer Synapse.
</p>

<p align="center">
  <a href="https://github.com/koveitan/kiyo-control-windows/actions/workflows/build.yml"><img src="https://github.com/koveitan/kiyo-control-windows/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

The Windows version of [Kiyo Control](https://github.com/f0ns/kiyo-control) for macOS.

> **Unofficial.** Kiyo Control for Windows is an independent, community-made project. It is not affiliated with,
> endorsed by, or supported by Razer Inc. See [Disclaimer](#disclaimer).

## Features

- Live preview while you adjust. Nothing is kept until you press **SAVE** (Ctrl+S); **Revert** undoes your changes.
- **Camera:** zoom, pan and tilt, five zoom presets, Wide / Medium / Narrow field of view, auto or manual focus with
  face focus, tracking and lighting modes, auto exposure with metering and compensation, or manual ISO and shutter speed.
- **Processing:** 2D and 3D noise reduction, HDR, lens distortion compensation.
- **Image:** brightness, contrast, saturation, sharpness, white balance, mirror, anti-flicker.
- **SAVE** also stores the Razer settings in the camera's own memory, so they stick without the app, also on other computers.
- Profiles, applied again whenever the camera is plugged in and after your PC wakes from sleep.
- Automatic backups of the camera's settings, so **Reset All Settings** and **Restore Backup** are always safe.
- Runs in the system tray, optionally at login: switch profiles and zoom presets without opening the window.
- Works while Zoom, Teams, OBS and other apps are using the camera.

### Supported cameras

| Camera | Support |
|---|---|
| Razer Kiyo Pro Ultra | All settings above. Tested with firmware 8.21. |
| Razer Kiyo Pro | Standard settings only (zoom, focus, exposure, image, anti-flicker). Not yet tested on Windows. |
| Razer Kiyo V2 Pro | Standard settings only. Not yet tested on Windows. |
| Razer Kiyo V2 X | Standard settings only. Not yet tested on Windows. |

The Razer-only settings (ISO, shutter, metering, noise reduction, mirror, lens correction…) are hidden on the
models other than the Pro Ultra, and the app never sends Razer commands to them.

If no camera connects, the settings panel lists the USB cameras plugged in and why each can or can't be used.
A Razer camera that isn't supported yet gets a **Help add it…** link that opens a prefilled GitHub issue. With
several Kiyos plugged in, pick one in the **⋯** menu or the tray menu; the preview follows.

Firmware updates are not supported. Use Razer's
[Kiyo Pro Ultra firmware updater](https://mysupport.razer.com/app/answers/detail/a_id/9777).

## Install

You need Windows 10 or 11 (64-bit) and a Razer Kiyo Pro Ultra or Kiyo V2 Pro.

1. Go to the [latest release](https://github.com/koveitan/kiyo-control-windows/releases/latest) and download
   **KiyoControl-win-Setup.exe**.
2. Run it. Kiyo Control installs for your user account (no administrator rights needed), adds itself to the Start
   menu, and opens.
3. **"Windows protected your PC"?** The app isn't code-signed yet, so Windows SmartScreen warns about it the first
   time. Click **More info**, then **Run anyway**.

Plug in the camera and the settings appear.

Prefer not to install? Download **KiyoControl-win-Portable.zip** instead, unzip it anywhere and run
**Kiyo Control.exe**.

If the preview stays black, turn on **Settings › Privacy & security › Camera › Let desktop apps access your camera**.

For 4K, plug the camera into a USB 3 port. Through a USB 2 hub (such as a keyboard's) it offers up to 1080p.

### Updates

Kiyo Control checks for new versions in the background. When one has been downloaded, the tray icon tells you; it
installs when you quit the app, or right away with **Restart to Update** in the tray menu.

## Everyday use

- Change settings and watch the preview. Nothing is kept until you click **SAVE**; **Revert** undoes your changes.
- To store a zoom preset, right-click one of the numbers 1–5. Click it later to jump back to that view.
- Closing the window keeps Kiyo Control running in the system tray, so your profile comes back whenever you plug the
  camera in. Right-click the tray icon to switch profiles, turn on **Launch at Login**, or **Quit**.
- Made a mess? Click **Reset All Settings**, or the **⋯** button › **Restore Backup**.
- The resolution menu at the bottom only changes this app's preview. Zoom, Teams and OBS choose their own resolution
  when they turn the camera on.

### Where your settings are kept

Profiles and backups are in `%APPDATA%\KiyoControl`. The last 20 backups are kept.

### Uninstall

Open **Settings › Apps › Installed apps**, find **Kiyo Control** and choose **Uninstall**. This also turns off
Launch at Login. Your profiles stay in `%APPDATA%\KiyoControl`; delete that folder to remove them too.

## Command line

`kiyoctl` changes settings without the app, for scripts or a Stream Deck button. It comes with the app:

```powershell
$kiyoctl = "$env:LOCALAPPDATA\KiyoControl\current\kiyoctl.exe"
& $kiyoctl cameras           # USB cameras plugged in and whether they are supported
& $kiyoctl info              # camera model and firmware version
& $kiyoctl list              # every setting with its value and range
& $kiyoctl set zoom 200      # 2.0x zoom
& $kiyoctl razer mirror on   # also: nr3d, nr2d, metering, ev, iso, shutter, focus, tracking, lens
& $kiyoctl backup
& $kiyoctl restore <backup.json>
```

Run `kiyoctl help` for everything it can do.

## Build from source

Install the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`), then in the repository folder:

```powershell
dotnet run --project src\KiyoControl
```

## Contributing

Issues and pull requests are welcome. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the code is organized
and how to run the tests.

## Credits

- [Kiyo Control](https://github.com/f0ns/kiyo-control) for macOS, by the Kiyo Control contributors, which this
  project is based on.
- The [cameractrls](https://github.com/soyersoyer/cameractrls) contributors, who documented the Razer camera commands
  ([issue #19](https://github.com/soyersoyer/cameractrls/issues/19)).

## Disclaimer

- **No warranty, use at your own risk.** This software is provided "as is", without warranty of any kind (see
  [LICENSE](LICENSE)). The authors are not liable for any damage to your camera, computer, data or anything else,
  including from settings saved to the camera.
- **Not affiliated with Razer.** Razer, Kiyo, Kiyo Pro Ultra and Synapse are trademarks or registered trademarks of
  Razer Inc. The names are used only to describe which hardware this software works with. This project is not
  affiliated with, endorsed by, sponsored by, or supported by Razer Inc. For official support, use Razer's own
  software and support channels.
- **Interoperability.** Kiyo Control for Windows talks to the camera through the standard Windows camera driver and
  documented camera commands, solely to make the camera's settings usable without Synapse. It contains no Razer
  firmware, software, code, images or other proprietary material.
- **Warranty of your device.** Changing camera settings with third-party software might affect Razer's support or
  warranty. Check Razer's terms if that matters to you.

## License

MIT, see [LICENSE](LICENSE). Based on [Kiyo Control](https://github.com/f0ns/kiyo-control), also MIT-licensed.
