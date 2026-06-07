# Parallax Capture Product Register

This document describes the product in this local repository state. It is a source-of-truth brief for the consumer-ready overhaul and should not be read as a public release promise.

## Register

| Field | Value |
| --- | --- |
| Product name | Parallax Capture |
| Category | Windows screenshot and screen recording utility |
| Platform | Windows 10+, x64, WPF on .NET 10 |
| Distribution model | Per-user desktop app, no admin required |
| Operating model | Tray-first, hotkey-driven, local-first |
| Current core surfaces | Region screenshot, full screenshot, annotation editor, region video recording with audio, video trim/export, tray menu, settings |
| Current capture stack | WPF, Windows Forms interop, ScreenRecorderLib, FFMpegCore and direct FFmpeg CLI, Hardcodet.NotifyIcon.Wpf |
| Data model | Local files, local settings, clipboard integration |
| Account/cloud posture | No account system, sync service, or cloud storage in the local source |

## Purpose

Parallax Capture helps people capture, mark up, trim, and share visual proof quickly without leaving their current workflow. It should feel like a reliable desk tool that stays quiet until needed, then gets out of the user's way.

The app is optimized for fast local capture under pressure: support cases, bug reports, screen shares, creator clips, tutorials, and lightweight documentation.

## Users

- Developers capturing bugs, UI states, logs, and reproduction steps.
- Support teams recording issue evidence and sending annotated screenshots.
- Creators making short local clips, demos, and explainers.
- Consumers who need a simpler Windows capture tool than a full editor.
- Screen-sharing users who need visible recording state and safe stop paths.

## Core Jobs

- Capture an exact screen region quickly.
- Capture the full screen without choosing a region.
- Annotate screenshots with simple shapes, text, blur, highlight, and copy/save actions.
- Record a screen region with system audio.
- Stop recording predictably from the tray or recording hotkey.
- Preview, trim, and export a recording locally.
- Find saved captures without learning a library or account system.

## Current Local Behavior

- Region screenshot is triggered from `Print Screen` or the tray menu.
- Full screenshot is triggered from `Alt+Print Screen` or the tray menu.
- Region video recording is triggered from `Alt+R` or the tray menu.
- Recording stops from `Alt+R` again or from the tray menu.
- Screenshots can be copied to the clipboard after capture.
- Captures save to a configured local folder.
- The annotation editor currently opens after capture unless auto-save mode is selected.
- The video editor supports opening video files, preview, trim in/out, save original, save trimmed, and FFmpeg download/status handling.
- Settings currently cover save folder, image format, clipboard copy, auto-save, separate folders, and start with Windows.

## Selected Consumer-Ready Direction

The selected overhaul direction is a full consumer-ready pass over the local app, not a cloud product or account-based workflow.

- Keep the app tray-first and fast, but make every major action discoverable without reading the README.
- Add an optional hotkey suite with per-feature enable/disable controls instead of hard-coded-only behavior.
- Add a capture-aware on-screen stop HUD for recording, with tray stop and recording hotkey as fallback stop paths.
- Redesign and extend the video editor progressively while preserving current open, trim, save original, and save trimmed flows.
- Move the visual language to a dark professional creator-tool style.
- Broaden the test harness around settings, hotkeys, file paths, recording state, capture overlays, media editor logic, and failure states where headless testing is possible.

## Brand Personality

- Calm: low visual noise, no panic colors except true recording or destructive states.
- Precise: labels say exactly what will happen.
- Trustworthy: capture, save, and export state is visible and reversible where practical.
- Compact: controls are close to the work without crowding the capture or media area.
- Local-first: the product treats the user's filesystem and clipboard as primary destinations.
- Professional: it should sit comfortably beside creator, developer, and support tooling.

## Anti-References

- No 90s gray utility chrome.
- No generic AI dashboard look.
- No glassmorphism, decorative glow, gradient text, or novelty effects.
- No bright gamer UI or streamer overlay excess.
- No hidden destructive actions.
- No marketing copy that implies DRM, anti-leak security, or guaranteed capture exclusion.
- No cloud-first assumptions such as account login, automatic upload, or remote library unless explicitly added later.

## Design Principles

- Tray-first, not tray-only: power users can live from hotkeys and tray, while new users can discover controls in settings and editors.
- Local proof over polished publishing: the app should make useful evidence fast before it tries to be a full production suite.
- Status is part of the workflow: capture start, recording state, save path, FFmpeg availability, export progress, and failure states should be visible.
- Safe stop paths matter more than clever overlay behavior: tray stop and recording hotkey must remain reliable fallbacks.
- Preserve existing flows when improving screens: new editor features should not break current open, preview, trim, save original, save trimmed, copy, and save behaviors.
- Settings should reduce risk: hotkeys, startup, clipboard, auto-save, folders, and FFmpeg-related behavior need explicit states.
- Prefer boring data ownership: captures stay local unless a future feature explicitly says otherwise.

## Accessibility

- Use clear text labels for primary actions, not icons alone.
- Keep practical target sizes at 32 px minimum, with 44 px preferred for primary and destructive controls.
- Preserve keyboard paths for capture workflows, editor playback, close/cancel, and recording stop.
- Escape should cancel overlays or close transient capture UI when safe.
- Focus states must be visible on dark backgrounds.
- Text should meet WCAG contrast targets on dark surfaces.
- Recording state should not rely on red alone. Use label text such as `REC`, duration, and a clear `Stop` action.
- Avoid flashing or high-frequency animation.
- Status and error messages should be specific: say what failed, what still worked, and what the user can do next.
- Small windows must remain readable at common Windows display scaling levels.

## Capture-Proof Limitation

The recording stop HUD should be capture-resistant where Windows supports it, but this is best-effort only. `SetWindowDisplayAffinity` can request exclusion from capture on supported Windows versions and capture paths, but it is not a security guarantee and must not be described as one.

Known limitations to document in-product where relevant:

- Some capture APIs, remote sessions, GPUs, drivers, or third-party tools may still capture overlays.
- Region geometry, window ordering, DPI scaling, and timing can affect whether a HUD appears in a recording.
- The HUD is a convenience and safety feature, not DRM and not anti-leak protection.
- Tray stop and the configured recording hotkey remain required fallback stop paths.

## FFmpeg Trust Boundary

FFmpeg support is security-sensitive because the app downloads, stores, and runs local executable tools for trim, frame, and GIF export flows.

- Built-in download is explicit user action, not automatic background install.
- The app treats `gyan.dev` as the current download source and copies only expected FFmpeg binaries into `%LOCALAPPDATA%\parallax\tools`.
- The app does not currently verify FFmpeg signatures or hashes. Users who need stronger provenance should install FFmpeg manually from a source they trust.
- A globally installed FFmpeg discovered by the runtime is also treated as trusted user/system configuration.
- Export failures and timeouts must preserve the source recording and should clean up partial generated outputs where practical.
