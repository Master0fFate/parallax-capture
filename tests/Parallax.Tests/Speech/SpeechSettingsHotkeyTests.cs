using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Shell;
using Parallax.Core.Speech;
using static Parallax.Tests.Speech.SpeechTestSupport;

namespace Parallax.Tests.Speech;

public sealed class SpeechSettingsHotkeyTests
{
    [Fact]
    public void Settings_persist_and_transcription_hotkey_registers_with_tray_fallback()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-speech-settings", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = TestLocations(root);
            var settings = ParallaxSettings.CreateDefaults(Path.Combine(root, "captures"));
            settings.SpeechToTextEnabled = true;
            settings.SpeechShortcut = "Ctrl+Shift+D";
            settings.SpeechShortcutMode = SpeechShortcutMode.Toggle;
            settings.SpeechProvider = SpeechProviderKind.OpenAiCompatible;
            settings.SpeechApiBaseUrl = "https://api.groq.com/openai/v1";
            settings.SpeechApiKey = "gsk_test";
            settings.SpeechModel = "whisper-large-v3-turbo";
            settings.SpeechLanguage = "auto";
            settings.SpeechPasteMethod = SpeechPasteMethod.ShiftInsert;
            settings.SpeechCopyToClipboard = true;
            settings.SpeechAutoSubmit = true;
            settings.SpeechCustomWords = "ParallaxCapture";
            settings.SpeechAppendTrailingSpace = true;
            settings.SpeechMaxHistoryEntries = 25;
            settings.SpeechHistoryRetentionDays = 30;

            var store = new JsonSettingsStore(locations);
            store.Save(settings);
            var loaded = store.Load();
            var hotkeys = HotkeyPlanner.Plan(loaded, CapabilityResult.Supported("hotkeys"));
            var surface = TraySurfaceBuilder.Build(
                new PlatformInfo(PlatformKind.Windows, "Windows"),
                CapabilitySet(),
                new ShellRuntimeState(IsRecording: false, TrayAvailable: false, IsTranscribing: false),
                hotkeys);

            Assert.Equal("Ctrl+Shift+D", loaded.SpeechShortcut);
            Assert.Equal(SpeechProviderKind.OpenAiCompatible, loaded.SpeechProvider);
            Assert.Equal(SpeechPasteMethod.ShiftInsert, loaded.SpeechPasteMethod);
            Assert.Equal(PlannedHotkeyState.Registered, hotkeys.Single(item => item.Action == HotkeyAction.SpeechToText).State);
            Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.StartSpeechToText && item.IsVisible);
            Assert.Contains("fallback control surface", surface.FallbackMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
