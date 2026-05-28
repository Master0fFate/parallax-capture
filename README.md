# Parallax Capture

> Screenshot &amp; screen recording tool for Windows — region capture, annotation, video trimming, all from the system tray.

![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue?logo=windows)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Region &amp; full-screen screenshots** — drag to select, capture exactly what you see
- **Annotation editor** — pen, arrow, rectangle, ellipse, text, highlighter, blur, undo
- **Region video recording** — record any screen area with audio, saved as MP4
- **Video editor** — trim, preview, save/export with built-in FFmpeg support
- **System tray** — runs quietly in the notification area, global hotkeys
- **Clipboard auto-copy** — screenshots land on your clipboard instantly
- **No admin required** — installs per-user, no elevation needed

## Screenshots

| Annotation Editor | Video Editor |
|---|---|
| Pen, shapes, text, blur tools with color picker | Trim with frame-accurate FFmpeg encoding |

## Install

### Option 1: Download installer

Grab the latest `ParallaxCapture-Setup-x.x.x.exe` from [Releases](https://github.com/your-org/parallax-screencapture/releases).  
The installer checks for .NET 10 and offers to download it if missing.

### Option 2: Build from source

```powershell
# Prerequisites: .NET 10 SDK
git clone https://github.com/your-org/parallax-screencapture.git
cd parallax-screencapture\parallax
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

Right-click the tray icon for the full menu — open video editor, open image editor, settings, etc.

## Tech Stack

- **.NET 10** — WPF + Windows Forms interop
- **FFMpegCore** + direct ffmpeg CLI — video trimming/encoding
- **ScreenRecorderLib** — hardware-accelerated screen capture
- **SharpAvi** — AVI fallback encoding
- **Hardcodet.NotifyIcon.Wpf** — system tray integration
- **Newtonsoft.Json** — settings persistence

## License

MIT — do whatever you want. See [LICENSE](LICENSE).
