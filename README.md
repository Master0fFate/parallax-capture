# Parallax Capture

> Screenshot & screen recording tool for Windows: region capture, annotation, video trimming, all from the system tray.

![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue?logo=windows)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Region & full-screen screenshots:** drag to select, capture exactly what you see
- **Annotation editor:** pen, arrow, rectangle, ellipse, text, highlighter, blur, undo
- **Region video recording:** record any screen area with audio, saved as MP4
- **Video editor:** trim, preview, save/export with built-in FFmpeg support
- **System tray:** runs quietly in the notification area, global hotkeys
- **Clipboard auto-copy:** screenshots land on your clipboard instantly
- **No admin required:** installs per-user, no elevation needed

## Version 1.1.0 Updates

- Fixed annotation blur to blur both the screenshot and existing annotation strokes
- Reworked text annotation into a rich-text flow: partial selection formatting, improved toolbar controls, and color/size controls that affect selected text
- Added a dedicated move handle for repositioning annotation text with improved interaction affordances
- Updated stroke slider visuals for better precision and centered thumb behavior
- Kept all capture/recording/video workflows unchanged to minimize regression risk

## Screenshots

| Annotation Editor | Video Editor |
|---|---|
| Pen, shapes, text, blur tools with color picker | Trim with frame-accurate FFmpeg encoding |

## Install

### Option 1: Download installer

Grab the latest `ParallaxCapture-Setup-x.x.x.exe` from [Releases](https://github.com/Master0fFate/parallax-capture/releases).  
The installer checks for .NET 10 and offers to download it if missing.

### Option 2: Build from source

```powershell
# Prerequisites: .NET 10 SDK
git clone https://github.com/Master0fFate/parallax-capture.git
cd parallax-capture\parallax
dotnet build -c Release -p:Platform=x64
dotnet run --project parallax.csproj
```

To create the installer:

```powershell
dotnet publish -c Release -p:Platform=x64 -o publish
# Open installer.iss in Inno Setup 7 -> Compile
```

## Usage

| Action | Hotkey |
|---|---|
| Region screenshot | `Print Screen` |
| Full screenshot | `Alt`+`Print Screen` |
| Start/stop region recording | `Alt`+`R` |
| Exit recording | `Alt`+`R` again, or tray menu |

Right-click the tray icon for the full menu: open video editor, open image editor, settings, etc.

## Security And Trust Boundaries

- Recording HUD and border exclusion use Windows `SetWindowDisplayAffinity` where supported. This is best-effort only and is not DRM, anti-leak protection, or a guarantee that other capture tools cannot see those windows.
- Tray stop and the configured recording hotkey remain fallback stop paths if the on-screen HUD is captured, hidden, or unavailable.
- FFmpeg download is optional and user-initiated. The app downloads the FFmpeg essentials archive from `gyan.dev`, extracts it in an isolated temp folder, copies only expected binaries to `%LOCALAPPDATA%\parallax\tools`, and cleans up temp files when it can.
- The app does not verify FFmpeg signatures or hashes. Use the built-in download only if you trust that source, or install FFmpeg manually from a source you trust.
- Video export runs FFmpeg locally with bounded error output and generated output filenames. Source recordings are kept if export fails or times out.

## Tech Stack

- **.NET 10:** WPF + Windows Forms interop
- **FFMpegCore** + direct ffmpeg CLI: video trimming/encoding
- **ScreenRecorderLib:** hardware-accelerated screen capture
- **Hardcodet.NotifyIcon.Wpf:** system tray integration
- **Newtonsoft.Json:** settings persistence

## License

MIT. Do whatever you want. See [LICENSE](LICENSE).
