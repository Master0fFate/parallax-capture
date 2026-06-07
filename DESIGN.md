# Parallax Capture Design System

This document defines the intended visual system and implementation constraints for the local WPF app. It is grounded in the current repository surfaces and the selected consumer-ready overhaul direction.

## Design Direction

Parallax Capture should look like a dark professional creator tool: compact, legible, calm, and built for fast capture/edit workflows. The interface should feel modern without becoming decorative.

The current local UI already uses dark WPF windows, tray-first behavior, an annotation editor, recording border, region overlay, settings, and a video editor. The overhaul should refine those surfaces into one coherent system rather than replace the app architecture wholesale.

## Visual Personality

- Dark professional creator-tool style.
- Restrained accent color for selected, primary, and active states.
- Neutral panels with visible hierarchy, not flat black everywhere.
- Tight, functional controls that still meet practical target sizes.
- Calm recording affordances with clear red status only where recording/destructive meaning exists.
- System-native enough to feel reliable on Windows, custom enough to avoid dated gray utility chrome.

## Palette

| Token | Purpose | Suggested value |
| --- | --- | --- |
| `SurfaceBase` | App background | `#111318` |
| `SurfacePanel` | Main panels and editor chrome | `#181B23` |
| `SurfaceRaised` | Toolbars, cards, menus | `#202532` |
| `SurfaceInset` | Canvas/video wells and inputs | `#0B0E13` |
| `BorderSubtle` | Dividers and inactive borders | `#2D3340` |
| `BorderStrong` | Focus and active outlines | `#566074` |
| `TextPrimary` | Primary text | `#F4F7FB` |
| `TextSecondary` | Labels and status text | `#AAB3C2` |
| `TextMuted` | Helper text and disabled labels | `#6F7888` |
| `Accent` | Primary actions and selection | `#3B82F6` |
| `AccentHover` | Hovered primary action | `#5B9BFF` |
| `AccentPressed` | Pressed primary action | `#2563EB` |
| `Success` | Completed save/export | `#22C55E` |
| `Warning` | FFmpeg missing, recoverable issue | `#F59E0B` |
| `Danger` | Recording, stop, destructive action | `#EF4444` |

Existing XAML colors can migrate gradually to these tokens through shared `ResourceDictionary` entries.

## Typography

- Use Segoe UI and system font fallbacks.
- Use 12-13 px for dense utility controls only when the target size remains usable.
- Use 14 px as the default body/control size for settings and editor panels.
- Use 16-18 px for section titles and editor headers.
- Use semibold sparingly for active tool, title, and primary status labels.
- Use monospace only for timestamps, dimensions, shortcuts, and file-like values.
- Avoid gradient text, oversized marketing headings, and decorative display fonts.

## Layout

- Use shared spacing steps: 4, 8, 12, 16, 24, 32.
- Keep primary editor actions near the content they affect.
- Preserve visible content hierarchy: header, working surface, contextual controls, status/action bar.
- Prefer resizable windows for editor surfaces where practical.
- Keep settings scannable with clear sections and direct labels.
- Do not allow dense toolbar rows to collapse into clipped controls at common Windows scaling levels.
- Keep capture overlays minimal because they appear while the user is focused on other content.

## Components

### Buttons

- Minimum height is 32 px for compact controls.
- Preferred height is 36-44 px for primary, stop, save, and destructive actions.
- Every button needs hover, pressed, disabled, and keyboard focus states.
- Icons may supplement labels, but primary actions should not be icon-only.
- Destructive or recording-stop actions use `Danger` intentionally and with clear text.

### Inputs

- Inputs use dark inset surfaces with visible borders.
- Combo boxes must not fall back to unreadable black-on-dark or white-on-white states.
- Validation errors should appear near the field and in any relevant status area.
- File/folder paths should support long text with clipping, tooltip, or copy affordance rather than layout breakage.

### Menus And Tray

- Tray remains a first-class control surface.
- Tray menu labels should include active hotkeys when enabled.
- Recording state should change both tray tooltip and menu actions.
- Settings should expose hotkey enabled/disabled state clearly once the optional hotkey suite is implemented.

### Capture Overlay

- Region selection overlay should keep dimming, crosshair, region border, size label, and Escape cancel behavior.
- Selection label should remain readable on varied screen content.
- The overlay must avoid heavy animation or ornamentation.
- Geometry must respect DPI scaling and multi-monitor coordinates where supported by the underlying capture services.

### Recording HUD

- The recording HUD should provide visible recording state, elapsed time where available, and a clear stop action.
- The HUD should be small, topmost, and movable or positioned to avoid the selected region where practical.
- It should request capture exclusion with `SetWindowDisplayAffinity` on supported Windows versions.
- It must clearly document that exclusion is best-effort only.
- Tray stop and the configured recording hotkey must remain fallback stop paths.

### Annotation Editor

- Preserve current tools: pen, arrow, rectangle, ellipse, text, highlighter, blur, undo, clear, save, and clipboard copy.
- Keep the image canvas dominant, with the toolbar compact but readable.
- Tool selection state must be explicit.
- Color and stroke controls should avoid tiny-only targets.
- Blur and redaction-like tools should avoid implying secure anonymization unless the pixels are actually changed in the exported bitmap.

### Video Editor

- Preserve current flows: open video, preview, play/pause, mute, mark trim in/out, save original, save trimmed, close.
- Make the preview area visually dominant.
- Keep trim state, current time, duration, FFmpeg availability, and export status visible.
- Missing FFmpeg must be a clear recoverable state, not a silent disabled action.
- Export failures should say what failed and leave the source recording intact.
- New features should be progressive additions rather than a wholesale rewrite.

## Motion

- Use minimal state-based motion only.
- Hover, pressed, focus, and status transitions may be subtle and fast.
- Avoid decorative reveals, glowing pulses, parallax effects, or animated gradients.
- Recording indicators can pulse gently only if they remain accessible and low-distraction.

## Accessibility Constraints

- Maintain WCAG-friendly contrast for text and focus states on dark backgrounds.
- Do not rely on color alone for recording, success, warning, or error states.
- Maintain keyboard access for overlays, editors, settings, and tray-triggered workflows where WPF permits it.
- Escape should close or cancel transient capture UI consistently.
- Ensure controls remain usable at 125 percent and 150 percent Windows scaling.
- Keep text short, specific, and plain. Avoid clever labels.

## WPF Implementation Constraints

- Keep lifecycle, HWND, dispatcher, topmost, interop, and tray behavior in code-behind or focused shell classes.
- Put parse, process, path, media, settings, and export decisions in services/helpers where feasible.
- Use shared `ResourceDictionary` styles and tokens for the visual system.
- Avoid a broad MVVM rewrite unless a later feature has a concrete reason.
- Prefer small, traceable changes over a full window rebuild.
- Keep .NET 10 Windows and x64 assumptions aligned with `parallax/parallax.csproj`.
- Preserve `ShutdownMode="OnExplicitShutdown"` because the app is tray-first.
- Preserve no-admin, per-user behavior.
- Be careful with `AllowsTransparency`, `Topmost`, and custom window chrome because they affect focus, capture, hit testing, and performance.

## Media And Security Constraints

- FFmpeg download and execution are security-sensitive.
- The UI should identify when FFmpeg is missing and when trim/export depends on it.
- Any downloader should use trusted sources, explicit user action, failure handling, and local verification where practical.
- Until signature or hash verification exists, FFmpeg binaries from the built-in download and globally installed FFmpeg should be described as trusted user/system inputs, not as app-verified binaries.
- FFmpeg export should use argument-list process startup, avoid overwriting generated outputs, bound surfaced logs, time out long-running work, and remove partial generated files after failures where practical.
- Export should never delete or overwrite the only source recording without a clear user choice.
- Capture-excluded windows should request `SetWindowDisplayAffinity` where supported, but UI and docs must call this best-effort only.
- Recording audio should continue using the system default output path unless the app adds explicit device selection.

## Testing Direction

- Keep broadening headless tests around settings persistence, file path behavior, image format handling, recording state, timer lifecycle, and service-level logic.
- Treat native recording, capture overlays, global hotkeys, tray behavior, and real FFmpeg execution as integration/manual-test-heavy areas when automation is not reliable.
- Add focused tests when moving hotkey configuration out of hard-coded registration.
- Add focused tests for editor time parsing, trim bounds, export command construction, and failure-state messaging.
- Capture-proof HUD behavior should have code-level tests for affinity calls where possible and manual verification notes for OS/capture-tool limitations.

## Non-Goals

- No cloud account system in this local product direction.
- No streaming suite or heavy nonlinear video editor.
- No DRM, anti-leak, or guaranteed capture prevention claims.
- No broad architecture rewrite just to restyle WPF windows.
- No decorative visual layer that competes with the captured content.
