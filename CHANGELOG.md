# Changelog

## 0.1.1 — 2026-11-20

- Fixed: after closing the window to the tray and opening it again, the preview stayed black while the camera
  light was on. The same happened when turning the preview off and on.
- The last preview frame is released while the window is closed, so the app uses less memory in the tray.

## 0.1.0 — 2026-11-20

First release of the Windows version of [Kiyo Control](https://github.com/f0ns/kiyo-control).

- All camera settings of the Kiyo Pro Ultra, with a live preview: zoom, pan, tilt, field of view, focus, exposure,
  ISO and shutter, noise reduction, HDR, lens correction, image settings, mirror and anti-flicker.
- SAVE and Revert, profiles, zoom presets and automatic backups.
- Settings are applied again when the camera is plugged in and after the PC wakes from sleep.
- System tray icon with profiles, zoom presets and Launch at Login.
- Installer and portable download, with automatic updates from GitHub Releases.
- `kiyoctl` command-line tool, included with the app.
- Tested on a Kiyo Pro Ultra with firmware 8.21.
