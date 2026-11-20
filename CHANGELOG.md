# Changelog

## 0.1.0 — 2026-11-20

First Windows release, ported from [kiyo-control](https://github.com/f0ns/kiyo-control) for macOS.

- Camera logic (`Kiyo.Core`) split from the UI: control map, Razer commands, profiles, backups, zoom presets.
- Windows backend through the in-box camera driver: DirectShow camera / proc-amp properties and `IKsControl` for the
  Razer extension unit, so other apps keep streaming.
- WPF app with live preview, Camera / Processing / Image tabs, Save / Revert, profiles, backups, tray icon and
  Launch at Login.
- `kiyoctl` command-line tool, including a read-only `probe`.
- Tested on a Kiyo Pro Ultra with firmware 8.21.
