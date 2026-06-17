# Parallax Capture

> Screenshot and screen recording tool: region capture, annotation, video trimming, tray or status item controls, and cross-platform packaging.

![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Region and full-screen screenshots:** drag to select, capture exactly what you see.
- **Annotation editor:** pen, arrow, rectangle, ellipse, text, highlighter, blur, undo.
- **Region video recording:** record a screen area with platform capability and permission states.
- **Video editor:** trim, preview, save frames, export GIFs, and keep source recordings safe.
- **Tray or status controls:** runs quietly with global hotkeys where the OS allows them.
- **Clipboard auto-copy:** screenshots land on your clipboard instantly when enabled.
- **No admin required:** normal install, run, settings, and uninstall flows use per-user paths.

## Install

Release packages publish a `SHA256SUMS` manifest. The one-line installers download the matching release artifact, verify its SHA-256 checksum, and stop before installing if the checksum does not match.

### Windows

PowerShell one-line install or update, per-user under `%LOCALAPPDATA%\Programs\Parallax Capture`:

```powershell
irm https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-windows.ps1 | iex
```

Install a specific release:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-windows.ps1))) -Version v1.1.0
```

Uninstall:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-windows.ps1))) -Uninstall
```

Windows releases may also include `ParallaxCapture-Setup-x.x.x.exe`. That Inno Setup path installs per-user with `PrivilegesRequired=lowest`.

### macOS

Shell one-line install, per-user under `~/Applications/Parallax Capture.app`:

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh
```

For Apple Silicon or Intel, the script selects `osx-arm64` or `osx-x64`, verifies `SHA256SUMS`, and installs the `.app` bundle archive. Release automation can also create a DMG when `hdiutil` is available. Signing, hardened runtime, notarization, and stapling hooks are present in `scripts/package-macos.sh`; unsigned local packages are used when signing secrets are not configured.

Uninstall:

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh -s -- --uninstall
```

### Linux

Shell one-line install, per-user under `~/.local/share/parallax-capture` with a `~/.local/bin/parallax-capture` launcher:

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh
```

The Linux package script produces a portable AppDir tarball and creates AppImage, deb, and rpm artifacts when the host has `appimagetool`, `dpkg-deb`, or `rpmbuild`. The installer verifies `SHA256SUMS`, installs desktop metadata to `~/.local/share/applications`, and does not use `sudo`.

Uninstall:

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh -s -- --uninstall
```

## Build

Prerequisites:

- .NET 10 SDK.
- Windows full legacy build: Windows 10 or later with Windows Desktop workload.
- Optional package tools: Inno Setup on Windows, Xcode command-line tools on macOS, `appimagetool`, `dpkg-deb`, or `rpmbuild` on Linux.

Restore and build the full solution on Windows:

```powershell
dotnet restore ParallaxCapture.sln
dotnet build ParallaxCapture.sln -c Release --no-restore
```

Build the cross-platform Avalonia app on any target host:

```sh
dotnet restore ParallaxCapture.sln
dotnet build src/Parallax.App.Avalonia/Parallax.App.Avalonia.csproj -c Release --no-restore
```

## Test

Run the complete Windows-hosted suite:

```powershell
dotnet test ParallaxCapture.sln -c Release --logger "console;verbosity=minimal"
```

Run packaging metadata tests on macOS or Linux runners:

```sh
dotnet test tests/Parallax.Packaging.Tests/Parallax.Packaging.Tests.csproj -c Release --logger "console;verbosity=minimal"
```

## Packaging

Windows:

```powershell
pwsh ./scripts/package-windows.ps1 -RuntimeIdentifier win-x64 -Version v1.1.0
```

macOS:

```sh
bash ./scripts/package-macos.sh osx-x64
bash ./scripts/package-macos.sh osx-arm64
```

Linux:

```sh
bash ./scripts/package-linux.sh linux-x64
```

All package scripts write release artifacts to `artifacts/release` and generate `SHA256SUMS`. CI covers `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64` publish/package smoke paths.

## Permissions

- **Windows:** screen capture, recording, settings, install, and uninstall are per-user and should not prompt for elevation.
- **macOS:** Screen Recording permission is required for screenshots and video capture. Accessibility or Input Monitoring may be required for global shortcut behavior. The app should show recovery guidance rather than silently failing when permissions are denied.
- **Linux:** Wayland sessions may require `xdg-desktop-portal` and PipeWire user mediation. X11 and tray/global shortcut behavior depend on the desktop session. Unsupported states should be visible and actionable.

## FFmpeg

FFmpeg is used for video trim, frame export, GIF export, and related media operations.

- Windows app-local FFmpeg install is optional and user-initiated. The app downloads the FFmpeg essentials archive from `gyan.dev`, extracts it in an isolated temp folder, copies only expected binaries to `%LOCALAPPDATA%\parallax\tools`, and cleans up temp files when it can.
- macOS and Linux users can install FFmpeg with their trusted package manager, such as Homebrew, apt, dnf, pacman, or Flatpak tooling, or place a trusted binary in the app-local tools folder.
- The app does not verify FFmpeg signatures or hashes. Use built-in or manual FFmpeg install only if you trust the source.
- Video export runs FFmpeg locally with argument-list process startup, bounded error output, timeouts, generated output names, and source recording preservation.

## Usage

| Action | Default shortcut |
|---|---|
| Region screenshot | `Print Screen` where supported |
| Full screenshot | `Alt` + `Print Screen` where supported |
| Start or stop region recording | `Alt` + `R` where supported |
| Stop recording fallback | Tray/status menu or configured recording shortcut |

Right-click the tray icon or use the platform status item for the full menu: open video editor, open image editor, settings, save folder, and quit.

## Security And Trust Boundaries

- Recording HUD and border exclusion use Windows `SetWindowDisplayAffinity` where supported. This is best-effort only and is not DRM, anti-leak protection, or a guarantee that other capture tools cannot see those windows.
- macOS and Linux do not claim guaranteed capture exclusion. Permission and portal limits are surfaced as platform capability states.
- Tray stop and the configured recording hotkey remain fallback stop paths if the on-screen HUD is captured, hidden, or unavailable.
- Release installers verify release artifact checksums through `SHA256SUMS`, but installer transport still depends on HTTPS and the GitHub release source.
- Packaging assets must not contain signing certificates, private keys, tokens, or release credentials. CI references signing secrets only by secret name.

## Tech Stack

- **.NET 10:** WPF legacy Windows app plus Avalonia cross-platform shell.
- **Avalonia:** cross-platform desktop UI surface.
- **FFMpegCore** and direct FFmpeg CLI: video trimming and encoding.
- **ScreenRecorderLib:** Windows hardware-accelerated screen capture path.
- **Hardcodet.NotifyIcon.Wpf:** legacy Windows tray integration.
- **Newtonsoft.Json:** settings persistence.

## License

MIT. Do whatever you want. See [LICENSE](LICENSE).
