using Parallax.App.Avalonia.Annotation;
using Parallax.Core.Annotation;
using Parallax.Core.Capture;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Tests.CaptureEdit;

public sealed class ScreenshotAndAnnotationParityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "parallax-capture-edit", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void RegionScreenshotSavesCopiesAndOpensEditorWhenConfigured()
    {
        var platform = TestPlatform.Create(_root);
        var settings = ParallaxSettings.CreateDefaults(Path.Combine(_root, "captures"));
        settings.ImageFormat = "jpeg";
        settings.SaveAutomatically = true;
        settings.CopyToClipboardAfterCapture = true;
        settings.OpenAnnotationEditorAfterScreenshot = true;

        var screenshot = new FakeScreenshotService();
        screenshot.RegionImage = CaptureImage.CreateSolid(80, 40, RgbaColor.Red);
        var selector = new FakeRegionSelectionService(new RegionSelectionResult(true, new CaptureRectangle(10, 20, 80, 40), "Selected."));
        var clipboard = new FakeClipboardService();
        var editor = new FakeAnnotationEditorLauncher();
        var workflow = new ScreenshotWorkflow(screenshot, selector, clipboard, new CollisionSafeImageSaver(), editor);

        var result = workflow.CaptureRegion(settings, platform.Locations);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new CaptureRectangle(10, 20, 80, 40), screenshot.LastRegion);
        Assert.Equal(80, result.Image?.Width);
        Assert.Equal(40, result.Image?.Height);
        Assert.True(File.Exists(result.SavedPath));
        Assert.EndsWith(".jpg", result.SavedPath);
        Assert.True(result.ClipboardCopied);
        Assert.True(clipboard.WasCopied);
        Assert.True(result.EditorOpened);
        Assert.Single(editor.OpenedImages);
    }

    [Fact]
    public void RegionOverlayEscapeCancelDoesNotSaveCopyOrOpenEditor()
    {
        var platform = TestPlatform.Create(_root);
        var settings = ParallaxSettings.CreateDefaults(Path.Combine(_root, "captures"));
        settings.SaveAutomatically = true;
        settings.CopyToClipboardAfterCapture = true;
        settings.OpenAnnotationEditorAfterScreenshot = true;

        var screenshot = new FakeScreenshotService();
        var clipboard = new FakeClipboardService();
        var editor = new FakeAnnotationEditorLauncher();
        var workflow = new ScreenshotWorkflow(
            screenshot,
            new FakeRegionSelectionService(RegionSelectionResult.Cancelled("Escape cancelled selection.")),
            clipboard,
            new CollisionSafeImageSaver(),
            editor);

        var result = workflow.CaptureRegion(settings, platform.Locations);

        Assert.True(result.Cancelled);
        Assert.False(result.Success);
        Assert.Null(screenshot.LastRegion);
        Assert.False(clipboard.WasCopied);
        Assert.Empty(editor.OpenedImages);
        Assert.False(Directory.Exists(settings.SaveFolder));
    }

    [Fact]
    public void ScreenshotWorkflowMatrixRespectsSaveClipboardAndEditorSettings()
    {
        var platform = TestPlatform.Create(_root);
        foreach (bool save in new[] { false, true })
        foreach (bool copy in new[] { false, true })
        foreach (bool openEditor in new[] { false, true })
        {
            var folder = Path.Combine(_root, $"case-{save}-{copy}-{openEditor}");
            var settings = ParallaxSettings.CreateDefaults(folder);
            settings.SaveAutomatically = save;
            settings.CopyToClipboardAfterCapture = copy;
            settings.OpenAnnotationEditorAfterScreenshot = openEditor;
            var screenshot = new FakeScreenshotService { FullImage = CaptureImage.CreateSolid(12, 8, RgbaColor.Blue) };
            var clipboard = new FakeClipboardService();
            var editor = new FakeAnnotationEditorLauncher();

            var result = new ScreenshotWorkflow(
                screenshot,
                new FakeRegionSelectionService(RegionSelectionResult.Cancelled("Unused.")),
                clipboard,
                new CollisionSafeImageSaver(),
                editor).CaptureFullScreen(settings, platform.Locations);

            Assert.True(result.Success, result.Message);
            Assert.Equal(save, result.SavedPath is not null);
            Assert.Equal(copy, result.ClipboardCopied);
            Assert.Equal(copy, clipboard.WasCopied);
            Assert.Equal(openEditor, result.EditorOpened);
            Assert.Equal(openEditor, editor.OpenedImages.Count == 1);
        }
    }

    [Theory]
    [InlineData("png", ".png", "PNG")]
    [InlineData(".jpeg", ".jpg", "JPEG")]
    [InlineData("jpg", ".jpg", "JPEG")]
    [InlineData("bmp", ".bmp", "BMP")]
    public void ImageSavesHonorFormatsNormalizeExtensionsAndAvoidCollisions(string configuredFormat, string extension, string expectedFormat)
    {
        var platform = TestPlatform.Create(_root);
        var settings = ParallaxSettings.CreateDefaults(Path.Combine(_root, "captures"));
        settings.ImageFormat = configuredFormat;
        var saver = new CollisionSafeImageSaver(() => new DateTimeOffset(2026, 6, 17, 1, 2, 3, TimeSpan.Zero));
        var image = CaptureImage.CreateSolid(5, 4, RgbaColor.Green);

        var first = saver.Save(image, settings, platform.Locations);
        var second = saver.Save(image, settings, platform.Locations);

        Assert.True(first.Success, first.Message);
        Assert.True(second.Success, second.Message);
        Assert.EndsWith(extension, first.FilePath);
        Assert.EndsWith("_1" + extension, second.FilePath);
        Assert.Equal(expectedFormat, first.Format.DisplayName);
        Assert.True(File.Exists(first.FilePath));
        Assert.True(File.Exists(second.FilePath));
    }

    [Fact]
    public void LogicalOverlayCoordinatesMapToPhysicalMultiMonitorDpiBounds()
    {
        var mapped = CaptureGeometryMapper.MapLogicalToPhysical(
            new LogicalRectangle(-100.5, 20.25, 300.5, 120.5),
            new DpiScale(1.5, 2.0));

        Assert.Equal(new CaptureRectangle(-151, 41, 451, 241), mapped);
    }

    [Fact]
    public void PermissionDeniedAndLinuxPortalStatesAreExplicitAndRecoverable()
    {
        var platform = TestPlatform.Create(_root, PlatformKind.MacOS);
        var workflow = new ScreenshotWorkflow(
            new FakeScreenshotService { RegionResult = CaptureResult.PermissionDenied("Grant Screen Recording permission in System Settings, then retry capture.") },
            new FakeRegionSelectionService(new RegionSelectionResult(true, new CaptureRectangle(0, 0, 10, 10), "Selected.")),
            new FakeClipboardService(),
            new CollisionSafeImageSaver(),
            new FakeAnnotationEditorLauncher());

        var denied = workflow.CaptureRegion(ParallaxSettings.CreateDefaults(Path.Combine(_root, "captures")), platform.Locations);

        Assert.False(denied.Success);
        Assert.Equal(CaptureFailureKind.PermissionDenied, denied.FailureKind);
        Assert.Contains("Screen Recording", denied.Message);

        var linux = TestPlatform.Create(_root, PlatformKind.Linux, portalRequired: true);
        Assert.Equal(CapabilityState.RequiresUserMediation, linux.Capabilities.ScreenCapture.State);
        Assert.Contains("portal", linux.Capabilities.ScreenCapture.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("picker", linux.Capabilities.ScreenCapture.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnnotationToolbarExposesCurrentToolParity()
    {
        var commands = AnnotationToolbarCatalog.Commands.Select(command => command.Id).ToArray();

        Assert.Contains(AnnotationCommandId.Pen, commands);
        Assert.Contains(AnnotationCommandId.Arrow, commands);
        Assert.Contains(AnnotationCommandId.Rectangle, commands);
        Assert.Contains(AnnotationCommandId.Ellipse, commands);
        Assert.Contains(AnnotationCommandId.Text, commands);
        Assert.Contains(AnnotationCommandId.Highlighter, commands);
        Assert.Contains(AnnotationCommandId.Blur, commands);
        Assert.Contains(AnnotationCommandId.Undo, commands);
        Assert.Contains(AnnotationCommandId.Clear, commands);
        Assert.Contains(AnnotationCommandId.Save, commands);
        Assert.Contains(AnnotationCommandId.Copy, commands);
    }

    [Fact]
    public void AnnotationExportMutatesPixelsUndoAndClearPreserveSource()
    {
        var source = CaptureImage.CreateSolid(30, 30, RgbaColor.White);
        var document = new AnnotationDocument(source);
        document.Add(AnnotationMark.Rectangle(new CaptureRectangle(5, 5, 10, 10), RgbaColor.Red, strokeThickness: 2));
        document.Add(AnnotationMark.TextMark(new CapturePoint(15, 15), "Hi", RgbaColor.Blue, strokeThickness: 3));

        var rendered = document.Render();

        Assert.Equal(RgbaColor.White, source.GetPixel(5, 5));
        Assert.NotEqual(RgbaColor.White, rendered.GetPixel(5, 5));
        Assert.NotEqual(RgbaColor.White, rendered.GetPixel(15, 15));

        Assert.True(document.Undo());
        var afterUndo = document.Render();
        Assert.NotEqual(RgbaColor.White, afterUndo.GetPixel(5, 5));
        Assert.Equal(RgbaColor.White, afterUndo.GetPixel(15, 15));

        document.Clear();
        Assert.Equal(RgbaColor.White, document.Render().GetPixel(5, 5));
    }

    [Fact]
    public void AnnotationTextColorStrokeAndBlurApplyOnlyToNewMarks()
    {
        var source = CaptureImage.CreateSolid(12, 12, RgbaColor.White);
        var document = new AnnotationDocument(source);
        document.Add(AnnotationMark.Pen([new CapturePoint(1, 1), new CapturePoint(2, 1)], RgbaColor.Red, strokeThickness: 1));
        document.Add(AnnotationMark.Pen([new CapturePoint(1, 3), new CapturePoint(2, 3)], RgbaColor.Blue, strokeThickness: 3));
        document.Add(AnnotationMark.Blur(new CaptureRectangle(0, 0, 4, 4), radius: 2));

        Assert.Equal(RgbaColor.Red, document.Marks[0].Color);
        Assert.Equal(1, document.Marks[0].StrokeThickness);
        Assert.Equal(RgbaColor.Blue, document.Marks[1].Color);
        Assert.Equal(3, document.Marks[1].StrokeThickness);
        Assert.Equal(AnnotationTool.Blur, document.Marks[2].Tool);
    }

    [Fact]
    public void LargeAnnotationSaveCopyAndExistingImageOpenAreSafe()
    {
        var platform = TestPlatform.Create(_root);
        var settings = ParallaxSettings.CreateDefaults(Path.Combine(_root, "captures"));
        settings.ImageFormat = "bmp";
        var source = CaptureImage.CreateSolid(1024, 768, RgbaColor.White);
        var reader = new FakeImageFileReader(source);
        var open = new OpenExistingImageWorkflow(reader);
        string existingPath = Path.Combine(_root, "existing.png");
        Directory.CreateDirectory(_root);
        File.WriteAllText(existingPath, "source remains untouched");

        var opened = open.Open(existingPath);
        var document = new AnnotationDocument(opened.Image!);
        document.Add(AnnotationMark.Highlighter(new CaptureRectangle(100, 100, 200, 50), RgbaColor.Yellow, strokeThickness: 6));
        var saver = new CollisionSafeImageSaver(() => new DateTimeOffset(2026, 6, 17, 2, 0, 0, TimeSpan.Zero));
        var clipboard = new FakeClipboardService();
        var save = AnnotationExportWorkflow.Save(document, settings, platform.Locations, saver, sourcePath: existingPath);
        var copy = AnnotationExportWorkflow.Copy(document, clipboard);

        Assert.True(opened.Success, opened.Message);
        Assert.True(save.Success, save.Message);
        Assert.NotEqual(existingPath, save.FilePath);
        Assert.True(File.Exists(existingPath));
        Assert.Equal("source remains untouched", File.ReadAllText(existingPath));
        Assert.EndsWith(".bmp", save.FilePath);
        Assert.True(copy.Success);
        Assert.True(clipboard.WasCopied);

        var unsupported = open.Open(Path.Combine(_root, "notes.txt"));
        Assert.False(unsupported.Success);
        Assert.Contains("Unsupported image format", unsupported.Message);

        reader.ThrowOnRead = true;
        var unreadable = open.Open(existingPath);
        Assert.False(unreadable.Success);
        Assert.Contains("could not be opened", unreadable.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvaloniaAnnotationEditorModelExposesToolsAndExportsRenderedImage()
    {
        var platform = TestPlatform.Create(_root);
        var clipboard = new FakeClipboardService();
        var model = new AnnotationEditorWindowModel(
            CaptureImage.CreateSolid(32, 32, RgbaColor.White),
            clipboard,
            new CollisionSafeImageSaver(() => new DateTimeOffset(2026, 6, 17, 3, 0, 0, TimeSpan.Zero)),
            platform.Locations,
            sourcePath: Path.Combine(_root, "source.png"));
        var settings = ParallaxSettings.CreateDefaults(Path.Combine(_root, "captures"));

        model.SelectTool(AnnotationTool.Rectangle);
        model.SetStyle(RgbaColor.Red, 2);
        model.AddShape(new CaptureRectangle(2, 2, 10, 10));
        model.SelectTool(AnnotationTool.Text);
        model.SetStyle(RgbaColor.Blue, 3);
        model.AddText(new CapturePoint(12, 12), "Note");
        var saved = model.Save(settings);
        var copied = model.Copy();

        Assert.Contains(model.ToolbarCommands, command => command.Id == AnnotationCommandId.Blur);
        Assert.True(saved.Success, saved.Message);
        Assert.True(File.Exists(saved.FilePath));
        Assert.True(copied.Success);
        Assert.True(clipboard.WasCopied);
        Assert.Contains("Copied", model.StatusMessage);
    }

    private sealed class TestPlatform : IPlatformBackend
    {
        private TestPlatform(IPlatformInfo info, IPlatformLocations locations, PlatformCapabilitySet capabilities)
        {
            Info = info;
            Locations = locations;
            Capabilities = capabilities;
        }

        public IPlatformInfo Info { get; }

        public IPlatformLocations Locations { get; }

        public PlatformCapabilitySet Capabilities { get; }

        public static TestPlatform Create(string root, PlatformKind kind = PlatformKind.Windows, bool portalRequired = false)
        {
            var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
                kind,
                UserProfile: Path.Combine(root, "user"),
                RoamingAppData: Path.Combine(root, "roaming"),
                LocalAppData: Path.Combine(root, "local"),
                TempDirectory: Path.Combine(root, "temp"),
                XdgConfigHome: Path.Combine(root, "xdg-config"),
                XdgDataHome: Path.Combine(root, "xdg-data"),
                XdgStateHome: Path.Combine(root, "xdg-state"),
                PicturesDirectory: Path.Combine(root, "pictures"),
                VideosDirectory: Path.Combine(root, "videos")));

            var capture = kind switch
            {
                PlatformKind.MacOS => CapabilityResult.RequiresPermission("Grant Screen Recording permission in System Settings, then retry capture."),
                PlatformKind.Linux when portalRequired => CapabilityResult.RequiresUserMediation("Wayland capture uses xdg-desktop-portal and may require a portal picker before capture can continue."),
                _ => CapabilityResult.Supported("Capture available.")
            };

            return new TestPlatform(
                new PlatformInfo(kind, $"{kind} test"),
                locations,
                new PlatformCapabilitySet(
                    capture,
                    CapabilityResult.Supported("Recording available."),
                    CapabilityResult.Supported("Hotkeys available."),
                    CapabilityResult.Supported("Clipboard available."),
                    CapabilityResult.Supported("Startup available."),
                    CapabilityResult.Unsupported("Capture exclusion is best-effort only.")));
        }
    }

    private sealed class FakeScreenshotService : IScreenshotService
    {
        public CaptureImage RegionImage { get; set; } = CaptureImage.CreateSolid(10, 10, RgbaColor.Red);

        public CaptureImage FullImage { get; set; } = CaptureImage.CreateSolid(20, 20, RgbaColor.Blue);

        public CaptureResult? RegionResult { get; set; }

        public CaptureRectangle? LastRegion { get; private set; }

        public CaptureResult CaptureRegion(CaptureRectangle region)
        {
            LastRegion = region;
            return RegionResult ?? CaptureResult.FromImage(RegionImage);
        }

        public CaptureResult CaptureFullScreen()
        {
            return CaptureResult.FromImage(FullImage);
        }
    }

    private sealed class FakeRegionSelectionService : IRegionSelectionService
    {
        private readonly RegionSelectionResult _result;

        public FakeRegionSelectionService(RegionSelectionResult result)
        {
            _result = result;
        }

        public RegionSelectionResult SelectRegion()
        {
            return _result;
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool WasCopied { get; private set; }

        public CaptureImage? CopiedImage { get; private set; }

        public ClipboardImageResult CopyImage(CaptureImage image)
        {
            WasCopied = true;
            CopiedImage = image;
            return new ClipboardImageResult(true, "Copied.");
        }
    }

    private sealed class FakeAnnotationEditorLauncher : IAnnotationEditorLauncher
    {
        public List<CaptureImage> OpenedImages { get; } = [];

        public AnnotationEditorLaunchResult Open(CaptureImage image, string? sourcePath)
        {
            OpenedImages.Add(image);
            return new AnnotationEditorLaunchResult(true, "Opened.");
        }
    }

    private sealed class FakeImageFileReader : IImageFileReader
    {
        private readonly CaptureImage _image;

        public FakeImageFileReader(CaptureImage image)
        {
            _image = image;
        }

        public bool ThrowOnRead { get; set; }

        public CaptureImage Read(string path)
        {
            if (ThrowOnRead)
            {
                throw new IOException("Fake read failure.");
            }

            return _image.Clone();
        }
    }
}
