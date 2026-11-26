# Changelog

## 0.2.0 — unreleased

Based on [Kiyo Control 0.6.0 for macOS](https://github.com/f0ns/kiyo-control/releases/tag/v0.6.0).

- Kiyo Pro (1532:0E05) and Kiyo V2 X (1532:0E0C) support for the standard settings; Razer-only settings are hidden.
- Camera list: when no camera connects, the settings panel lists the USB cameras plugged in and why each can or
  can't be used. Unsupported Razer cameras link to a prefilled GitHub issue.
- With several Kiyos plugged in, pick one in the ⋯ menu or the tray menu; the choice is remembered and the
  preview follows it.
- The "USB 2?" hint only shows for cameras that can stream 4K.
- On models without Razer commands, the bottom bar shows the manual shutter speed and gain, and resetting Auto
  Exposure also resets them.
- `kiyoctl cameras` lists USB cameras and whether they are supported; `kiyoctl probe <n>` also works on cameras
  that aren't supported yet.

## 0.1.1 — 2026-11-20

Based on Kiyo Control 0.5.0 for macOS.

- Fixed: after closing the window to the tray and opening it again, the preview stayed black while the camera
  light was on. The same happened when turning the preview off and on.
- The last preview frame is released while the window is closed, so the app uses less memory in the tray.

## 0.1.0 — 2026-11-20

First release of the Windows version of [Kiyo Control](https://github.com/f0ns/kiyo-control). Based on
[Kiyo Control 0.5.0 for macOS](https://github.com/f0ns/kiyo-control/releases/tag/v0.5.0).

- All camera settings of the Kiyo Pro Ultra, with a live preview: zoom, pan, tilt, field of view, focus, exposure,
  ISO and shutter, noise reduction, HDR, lens correction, image settings, mirror and anti-flicker.
- SAVE and Revert, profiles, zoom presets and automatic backups.
- Settings are applied again when the camera is plugged in and after the PC wakes from sleep.
- System tray icon with profiles, zoom presets and Launch at Login.
- Installer and portable download, with automatic updates from GitHub Releases.
- `kiyoctl` command-line tool, included with the app.
- Tested on a Kiyo Pro Ultra with firmware 8.21.
