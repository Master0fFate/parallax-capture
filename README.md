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
- **Speech-to-text:** push-to-talk or toggle transcription with OpenAI-compatible APIs or local Whisper CLI models.
- **Tray or status controls:** runs quietly with global hotkeys where the OS allows them.
- **Clipboard auto-copy:** screenshots land on your clipboard instantly when enabled.
- **No admin required:** normal install, run, settings, and uninstall flows use per-user paths.

## Install

Release packages publish a `SHA256SUMS` manifest. The one-line installers download the matching release artifact, verify its SHA-256 checksum, and stop before installing if the checksum does not match. Installers use per-user locations and do not require admin privileges.

### Windows

**One-line install or update:**

```powershell
irm https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-windows.ps1 | iex
```

Install a specific release:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-windows.ps1))) -Version v1.1.0
```

**What it installs:** the release `ParallaxCapture-<version>-win-x64.zip` artifact, extracted to `%LOCALAPPDATA%\Programs\Parallax Capture`, plus a Start Menu shortcut named `Parallax Capture.lnk`.

**Uninstall:**

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-windows.ps1))) -Uninstall
```

**Manual install from a release:**

1. Download `ParallaxCapture-<version>-win-x64.zip` and `SHA256SUMS` from the GitHub release.
2. Verify the zip hash against `SHA256SUMS`.
3. Extract the zip to `%LOCALAPPDATA%\Programs\Parallax Capture`.
4. Run `Parallax Capture.exe`. Create your own shortcut if needed.

**Build a package from source:**

```powershell
pwsh ./scripts/package-windows.ps1 -RuntimeIdentifier win-x64 -Version v1.1.0
```

This publishes the Avalonia app, creates `artifacts/release/ParallaxCapture-v1.1.0-win-x64.zip`, and regenerates `artifacts/release/SHA256SUMS`. If Inno Setup `iscc.exe` is installed, the script also builds the optional legacy Inno Setup installer from `parallax/installer.iss`; that installer is configured with `PrivilegesRequired=lowest`.

**Prerequisites:** Windows 10 or later for the app, PowerShell for the installer, .NET 10 SDK for source packaging, and optional Inno Setup for the `.exe` installer.

**Limitations and permissions:** the implemented installer supports `win-x64` only, installs per-user, stops a running `Parallax Capture` process during updates or uninstall, and does not elevate. Screen capture and recording are local desktop operations. Recording HUD capture exclusion is best-effort only.

### macOS

**One-line install or update:**

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh
```

Install a specific release:

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh -s -- --version v1.1.0
```

**What it installs:** the release `ParallaxCapture-<version>-osx-x64-app.tar.gz` or `ParallaxCapture-<version>-osx-arm64-app.tar.gz` artifact, selected from `uname -m`, extracted as `~/Applications/Parallax Capture.app`.

**Uninstall:**

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh -s -- --uninstall
```

**Manual install from a release:**

1. Download the matching `ParallaxCapture-<version>-osx-x64-app.tar.gz` or `ParallaxCapture-<version>-osx-arm64-app.tar.gz` and `SHA256SUMS`.
2. Verify the archive hash with `shasum -a 256`.
3. Extract the archive and move `Parallax Capture.app` to `~/Applications`.
4. Launch the app from Finder or with `open "$HOME/Applications/Parallax Capture.app"`.

Release automation may also publish `ParallaxCapture-<version>-osx-x64.dmg` or `ParallaxCapture-<version>-osx-arm64.dmg` when `hdiutil` is available. The repository includes Homebrew Cask metadata in `packaging/macos/parallax-capture.rb`, but that is release-ready metadata with a placeholder checksum, not a published Homebrew Cask.

**Build a package from source:**

```sh
MACOS_ALLOW_UNSIGNED=true bash ./scripts/package-macos.sh osx-x64
MACOS_ALLOW_UNSIGNED=true bash ./scripts/package-macos.sh osx-arm64
```

The script publishes the Avalonia app, creates a `.app` bundle, writes `Info.plist`, includes entitlements metadata, creates the app `tar.gz`, optionally creates a DMG with `hdiutil`, and regenerates `SHA256SUMS`. Local unsigned packages require the explicit `MACOS_ALLOW_UNSIGNED=true` opt-in. Release builds set `RELEASE_BUILD=true` and fail unless `MACOS_CODESIGN_IDENTITY` and `MACOS_NOTARY_PROFILE` are configured for signing, hardened runtime, notarization, and stapling.

**Prerequisites:** macOS on Intel or Apple Silicon for native package generation, `curl`, `tar`, `shasum`, .NET 10 SDK for source packaging, Xcode command-line tooling for signing/notarization steps, and `hdiutil` for DMG creation.

**Limitations and permissions:** the shell installer supports only `osx-x64` and `osx-arm64`. macOS Screen Recording permission is required for capture. Microphone, Accessibility, Input Monitoring, and Documents folder usage metadata exists in the app bundle, but actual permission prompts depend on the OS and feature used. macOS packages are produced by scripts and CI; native runtime behavior is not fully validated from a Windows development host.

### Linux

**One-line install or update:**

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh
```

Install a specific release:

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh -s -- --version v1.1.0
```

**What it installs:** the release `ParallaxCapture-<version>-linux-x64.tar.gz` AppDir-style artifact, extracted to `~/.local/share/parallax-capture`, plus a `~/.local/bin/parallax-capture` launcher symlink and desktop metadata in `~/.local/share/applications/parallax-capture.desktop`.

**Uninstall:**

```sh
curl -fsSL https://raw.githubusercontent.com/Master0fFate/parallax-capture/master/scripts/install-unix.sh | sh -s -- --uninstall
```

**Manual install from a release:**

1. Download `ParallaxCapture-<version>-linux-x64.tar.gz` and `SHA256SUMS`.
2. Verify the archive hash with `sha256sum`.
3. Extract the archive to `~/.local/share/parallax-capture`.
4. Link `~/.local/share/parallax-capture/usr/bin/parallax-capture` into a directory on `PATH`, for example `~/.local/bin/parallax-capture`.
5. Optionally copy `usr/share/applications/parallax-capture.desktop` to `~/.local/share/applications`.

Release automation may also publish `.AppImage`, `.deb`, and `.rpm` artifacts when the Linux packaging host has `appimagetool`, `dpkg-deb`, or `rpmbuild`. The repository contains AppStream and desktop metadata under `packaging/linux`; it does not contain Flatpak, Snap, apt repository, dnf repository, or pacman package metadata.

**Build a package from source:**

```sh
bash ./scripts/package-linux.sh linux-x64
```

The script publishes the Avalonia app into an AppDir layout, copies desktop and AppStream metadata, creates `artifacts/release/ParallaxCapture-<version>-linux-x64.tar.gz`, optionally creates AppImage, deb, and rpm artifacts when their tools are installed, and regenerates `SHA256SUMS`.

**Prerequisites:** Linux x86_64 for native package generation, `curl`, `tar`, `sha256sum`, .NET 10 SDK for source packaging, and optional `appimagetool`, `dpkg-deb`, or `rpmbuild` for those extra artifact types.

**Limitations and permissions:** the shell installer supports only Linux x86_64 and does not use `sudo`. Wayland sessions may require `xdg-desktop-portal` and PipeWire user mediation for capture or recording. X11, tray, and global shortcut behavior depend on the desktop session. Linux packages are produced by scripts and CI; native runtime behavior is not fully validated from a Windows development host.

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

Build a local Windows test executable without creating a release package:

```powershell
dotnet publish src/Parallax.App.Avalonia/Parallax.App.Avalonia.csproj -c Release -r win-x64 --self-contained false -o artifacts/local-test/win-x64
```

Run `artifacts/local-test/win-x64/Parallax.App.Avalonia.exe` for a local smoke test. The `artifacts/` folder is ignored by Git.

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
MACOS_ALLOW_UNSIGNED=true bash ./scripts/package-macos.sh osx-x64
MACOS_ALLOW_UNSIGNED=true bash ./scripts/package-macos.sh osx-arm64
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

## Speech-to-text

Speech-to-text is configured from Settings and can be launched from the tray/status menu or the configured shortcut.

- **Shortcut:** defaults to `Ctrl` + `Shift` + `D`. Push-to-talk records while held; toggle mode starts on the first press and stops on the next.
- **API providers:** use any OpenAI-compatible `/audio/transcriptions` endpoint by setting the base URL, API key, model, and language. `auto` is the default language.
- **Local Whisper:** choose `LocalWhisper`, download a pinned tiny/base GGML model from Settings, and place `whisper-cli`, `main`, or `whisper` in the app tools folder or on `PATH`.
- **Output:** paste through clipboard, `Ctrl` + `V`, `Ctrl` + `Shift` + `V`, or `Shift` + `Insert`, with optional auto-submit.
- **Custom words:** add frequently misheard names or terms so transcription output is normalized before paste/history.
- **History:** transcripts and recordings are kept under the app transcription history folder, with configurable maximum entries and retention days.

## Usage

| Action | Default shortcut |
|---|---|
| Region screenshot | `Print Screen` where supported |
| Full screenshot | `Alt` + `Print Screen` where supported |
| Start or stop region recording | `Alt` + `R` where supported |
| Transcribe speech | `Ctrl` + `Shift` + `D` where supported |
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
