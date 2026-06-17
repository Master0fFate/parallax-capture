namespace Parallax.Tests.KAMVerification;

public class ReleaseVerificationTests
{
    [Fact]
    public void RecordingHudAndBorder_RequestCaptureExclusionByContract()
    {
        string controls = ReadSource("RecordingControlsWindow.xaml.cs");
        string border = ReadSource("RecordingBorderWindow.xaml.cs");

        Assert.Contains("WDA_EXCLUDEFROMCAPTURE", controls);
        Assert.Contains("SetWindowDisplayAffinity", controls);
        Assert.Contains("IsCaptureExcluded", controls);
        Assert.Contains("SourceInitialized", controls);

        Assert.Contains("WDA_EXCLUDEFROMCAPTURE", border);
        Assert.Contains("SetWindowDisplayAffinity", border);
        Assert.Contains("IsCaptureExcluded", border);
        Assert.Contains("SourceInitialized", border);
    }

    [Fact]
    public void RecordingHudAndBorder_AreCleanedUpAcrossRecordingPaths()
    {
        string tray = ReadSource("TrayIconManager.cs");

        Assert.Contains("HideRecordingBorder();", tray);
        Assert.Contains("OnRecordingCompleted", tray);
        Assert.Contains("OnRecordingFailed", tray);
        Assert.Contains("_recordingControls?.Close()", tray);
        Assert.Contains("_recordingControls = null", tray);
        Assert.Contains("_recordingBorder?.Close()", tray);
        Assert.Contains("_recordingBorder = null", tray);
        Assert.Contains("Dispose()", tray);
    }

    [Fact]
    public void VideoEditor_FfmpegProcessExecutionRemainsHardened()
    {
        string editor = ReadSource("VideoEditorWindow.xaml.cs");

        Assert.Contains("ProcessStartInfo", editor);
        Assert.Contains("UseShellExecute = false", editor);
        Assert.Contains("CreateNoWindow = true", editor);
        Assert.Contains("RedirectStandardError = true", editor);
        Assert.Contains("ArgumentList.Add", editor);
        Assert.Contains("\"-n\"", editor);
        Assert.Contains("FFmpegProcessTimeout", editor);
        Assert.Contains("WaitForExitAsync(timeout.Token)", editor);
        Assert.Contains("Kill(entireProcessTree: true)", editor);
        Assert.Contains("MaxFFmpegErrorChars", editor);
        Assert.Contains("ValidateFFmpegOutput", editor);
        Assert.Contains("TryDeleteFile(outputPath)", editor);
    }

    [Fact]
    public void VideoEditor_TrimValidationCoversInvalidBoundaryStates()
    {
        string editor = ReadSource("VideoEditorWindow.xaml.cs");

        Assert.Contains("Invalid trim times", editor);
        Assert.Contains("Trim times cannot be negative", editor);
        Assert.Contains("Trim range is outside this video's duration", editor);
        Assert.Contains("Trim end must be after trim start", editor);
        Assert.Contains("TryGetValidatedTrimRange", editor);
    }

    [Fact]
    public void SettingsWindow_ValidatesDuplicateEnabledShortcutsBeforeSaving()
    {
        string settings = ReadSource("SettingsWindow.xaml.cs");

        Assert.Contains("TryValidateHotkeys", settings);
        Assert.Contains("new Dictionary<(uint Modifiers, uint VirtualKey), string>", settings);
        Assert.Contains("used.TryGetValue", settings);
        Assert.Contains("is already assigned", settings);
        Assert.Contains("HotkeyScreenshotEnabled", settings);
        Assert.Contains("HotkeyFullscreenEnabled", settings);
        Assert.Contains("HotkeyRegionVideoEnabled", settings);
    }

    [Fact]
    public void SettingsWindow_DoesNotExposeThemeControls()
    {
        string xaml = ReadSource("SettingsWindow.xaml");
        string code = ReadSource("SettingsWindow.xaml.cs");

        Assert.DoesNotContain("Header=\"Themes\"", xaml);
        Assert.DoesNotContain("Header=\"Appearance\"", xaml);
        Assert.DoesNotContain("ChkUseDarkMode", xaml);
        Assert.DoesNotContain("CmbThemePreset", xaml);
        Assert.DoesNotContain("TxtThemePreviewTitle", xaml);
        Assert.DoesNotContain("ThemeMode_Changed", code);
        Assert.DoesNotContain("ThemePreset_Changed", code);
        Assert.DoesNotContain("PreviewTheme", code);
        Assert.DoesNotContain("AppThemeService", code);
    }

    [Fact]
    public void ProductThemeBrushBindings_AreDynamicSoExistingWindowsUpdate()
    {
        string repoRoot = FindRepoRoot();
        string sourceRoot = Path.Combine(repoRoot, "parallax");

        string[] offenders = Directory
            .EnumerateFiles(sourceRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => new { path, line, index })
                .Where(item => item.line.Contains("{StaticResource Product") && item.line.Contains("Brush}"))
                .Select(item => $"{Path.GetRelativePath(repoRoot, item.path)}:{item.index + 1}: {item.line.Trim()}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TrayMenu_UsesThemeSafeMenuStylesAndInputGestures()
    {
        string styles = ReadSource("DefaultStyles.xaml");
        string tray = ReadSource("TrayIconManager.cs");

        Assert.Contains("ProductContextMenuStyle", styles);
        Assert.Contains("ProductMenuItemStyle", styles);
        Assert.Contains("InputGestureText", styles);
        Assert.Contains("ProductContextMenuStyle", tray);
        Assert.Contains("ProductMenuItemStyle", tray);
        Assert.Contains("InputGestureText", tray);
        Assert.Contains("FormatHotkeyGesture", tray);
        Assert.DoesNotContain("Capture region   ", tray);
        Assert.DoesNotContain("Capture full screen   ", tray);
        Assert.DoesNotContain("Record region   ", tray);
    }

    [Fact]
    public void AvaloniaShell_DoesNotExposePlaceholderModeMessages()
    {
        string repoRoot = FindRepoRoot();
        string shell = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "Parallax.App.Avalonia",
            "Shell",
            "AvaloniaShellCommandHandler.cs"));

        Assert.DoesNotContain("available through the platform screenshot workflow", shell);
        Assert.DoesNotContain("command is wired", shell);
        Assert.DoesNotContain("provided by the media milestone", shell);
        Assert.DoesNotContain("can start after selecting an area", shell);
        Assert.DoesNotContain("Stop recording command handled", shell);
    }

    [Fact]
    public void StartupShortcutConflicts_DoNotShowModalWarningOnRun()
    {
        string app = ReadSource("App.xaml.cs");

        Assert.Contains("RegisterConfiguredHotkeys(showWarnings: false)", app);
        Assert.Contains("Some enabled shortcuts could not be registered", app);
    }

    [Fact]
    public void Documentation_StatesCaptureExclusionAndFfmpegTrustBoundaries()
    {
        string readme = File.ReadAllText(Path.Combine(FindRepoRoot(), "README.md"));
        string product = File.ReadAllText(Path.Combine(FindRepoRoot(), "PRODUCT.md"));
        string design = File.ReadAllText(Path.Combine(FindRepoRoot(), "DESIGN.md"));

        Assert.Contains("best-effort only", readme);
        Assert.Contains("not DRM", readme);
        Assert.Contains("does not verify FFmpeg signatures or hashes", readme);
        Assert.Contains("not a security guarantee", product);
        Assert.Contains("does not currently verify FFmpeg signatures or hashes", product);
        Assert.Contains("best-effort only", design);
        Assert.Contains("Until signature or hash verification exists", design);
    }

    [Fact]
    public void Tray_UsesEditorAutoOpenSettingsAndFfmpegGate()
    {
        string tray = ReadSource("TrayIconManager.cs");

        Assert.Contains("OpenAnnotationEditorAfterScreenshot", tray);
        Assert.Contains("OpenVideoEditorAfterRecording", tray);
        Assert.Contains("EnsureFFmpegReadyWithConsentAsync", tray);
        Assert.Contains("SaveCompletedRecording", tray);
        Assert.Contains("IsVideoEditorOpen", tray);
        Assert.Contains("Save or close the current edit before starting another recording", tray);
    }

    [Fact]
    public void VideoEditor_UsesTimelineHandlesInsteadOfTextHeavyTrimButtons()
    {
        string xaml = ReadSource("VideoEditorWindow.xaml");
        string code = ReadSource("VideoEditorWindow.xaml.cs");

        Assert.Contains("TrimTimelineCanvas", xaml);
        Assert.Contains("TrimInHandle", xaml);
        Assert.Contains("TrimOutHandle", xaml);
        Assert.Contains("TimelinePlayhead", xaml);
        Assert.Contains("TrimTimelineCanvas_MouseLeftButtonDown", code);
        Assert.Contains("UpdateTimelineFromPoint", code);
        Assert.DoesNotContain("Mark In", xaml);
        Assert.DoesNotContain("Jump In", xaml);
        Assert.DoesNotContain("Mark Out", xaml);
        Assert.DoesNotContain("Jump Out", xaml);
        Assert.DoesNotContain("🔊", xaml + code);
        Assert.DoesNotContain("🔇", xaml + code);
    }

    [Fact]
    public void VideoEditor_TimelineUsesMillisecondPrecision()
    {
        string xaml = ReadSource("VideoEditorWindow.xaml");
        string code = ReadSource("VideoEditorWindow.xaml.cs");

        Assert.Contains("00:00.000", xaml);
        Assert.Contains("FormatTimelineTime", code);
        Assert.Contains("ParseTimelineTime", code);
        Assert.Contains("TimeSpan.FromTicks", code);
        Assert.Contains("MM:SS.fff", code);
    }

    [Fact]
    public void VideoEditor_GifExportUsesFullSelectedRangeAndPalettePipeline()
    {
        string code = ReadSource("VideoEditorWindow.xaml.cs");

        Assert.Contains("Export GIF", code);
        Assert.Contains("\"-i\", _videoPath", code);
        Assert.Contains("\"-ss\", start", code);
        Assert.Contains("\"-t\", duration", code);
        Assert.Contains("\"-filter_complex\"", code);
        Assert.Contains("palettegen", code);
        Assert.Contains("paletteuse", code);
        Assert.Contains("\"-loop\", \"0\"", code);
    }

    [Fact]
    public void AnnotationZoomControls_UseVisibleVectorContent()
    {
        string xaml = ReadSource("AnnotationWindow.xaml");

        Assert.Contains("BtnZoomOut", xaml);
        Assert.Contains("BtnZoomIn", xaml);
        Assert.Contains("M 2 8 L 14 8", xaml);
        Assert.Contains("M 8 2 L 8 14", xaml);
        Assert.Contains("ProductTextBrush", xaml);
    }

    [Fact]
    public void AnnotationBlur_UsesSnapshotPixelsInsteadOfOpacityErasing()
    {
        string code = ReadSource("AnnotationWindow.xaml.cs");

        Assert.Contains("var blurSnapshot = RenderFinalImage();", code);
        Assert.Contains("new ImageBrush(blurSnapshot)", code);
        Assert.Contains("Opacity = 1.0", code);
        Assert.DoesNotContain("Visual = ScreenshotImage", code);
    }

    [Fact]
    public void AnnotationTextToolbar_SupportsRichFormattingAndMoveHandle()
    {
        string xaml = ReadSource("AnnotationWindow.xaml");
        string code = ReadSource("AnnotationWindow.xaml.cs");

        Assert.Contains("FloatingTextToolbar", xaml);
        Assert.Contains("TextMoveHandle", xaml);
        Assert.Contains("TextFormatButton_Click", xaml + code);
        Assert.Contains("TextFontFamily_SelectionChanged", xaml + code);
        Assert.Contains("TextColorButton_Click", xaml + code);
        Assert.Contains("RichTextBox", code);
        Assert.Contains("ApplyTextSelectionValue", code);
        Assert.Contains("Inline.TextDecorationsProperty", code);
        Assert.Contains("TextMoveHandle_MouseMove", code);
        Assert.Contains("TextElement.FontSizeProperty", code);
    }

    [Fact]
    public void AnnotationStrokeSlider_UsesCenteredCustomTemplate()
    {
        string styles = ReadSource("DefaultStyles.xaml");

        Assert.Contains("CustomSliderThumbStyle", styles);
        Assert.Contains("CustomSliderFilledTrackStyle", styles);
        Assert.Contains("PART_Track", styles);
        Assert.Contains("VerticalAlignment=\"Center\"", styles);
        Assert.Contains("Command=\"Slider.DecreaseLarge\"", styles);
    }

    private static string ReadSource(string fileName)
    {
        string repoRoot = FindRepoRoot();
        string? found = Directory
            .EnumerateFiles(repoRoot, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(path => path.Contains($"{Path.DirectorySeparatorChar}parallax{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        if (found == null)
        {
            throw new FileNotFoundException($"Could not find source file {fileName} under {repoRoot}.");
        }

        return File.ReadAllText(found);
    }

    private static string FindRepoRoot()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "README.md"))
                && File.Exists(Path.Combine(dir, "parallax", "parallax.csproj")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
