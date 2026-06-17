using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Capture;

public interface IAnnotationEditorLauncher
{
    AnnotationEditorLaunchResult Open(CaptureImage image, string? sourcePath);
}

public sealed record AnnotationEditorLaunchResult(bool Success, string Message);

public sealed record ScreenshotWorkflowResult(
    bool Success,
    bool Cancelled,
    CaptureFailureKind? FailureKind,
    CaptureImage? Image,
    string? SavedPath,
    bool ClipboardCopied,
    bool EditorOpened,
    string Message);

public sealed class ScreenshotWorkflow
{
    private readonly IScreenshotService _screenshots;
    private readonly IRegionSelectionService _regionSelection;
    private readonly IClipboardService _clipboard;
    private readonly CollisionSafeImageSaver _imageSaver;
    private readonly IAnnotationEditorLauncher _annotationEditor;

    public ScreenshotWorkflow(
        IScreenshotService screenshots,
        IRegionSelectionService regionSelection,
        IClipboardService clipboard,
        CollisionSafeImageSaver imageSaver,
        IAnnotationEditorLauncher annotationEditor)
    {
        _screenshots = screenshots;
        _regionSelection = regionSelection;
        _clipboard = clipboard;
        _imageSaver = imageSaver;
        _annotationEditor = annotationEditor;
    }

    public ScreenshotWorkflowResult CaptureRegion(ParallaxSettings settings, IPlatformLocations locations)
    {
        var selection = _regionSelection.SelectRegion();
        if (!selection.Selected || selection.Bounds is null)
        {
            return new ScreenshotWorkflowResult(
                Success: false,
                Cancelled: true,
                FailureKind: CaptureFailureKind.Cancelled,
                Image: null,
                SavedPath: null,
                ClipboardCopied: false,
                EditorOpened: false,
                Message: selection.Message);
        }

        if (selection.Bounds.Value.IsEmpty)
        {
            return new ScreenshotWorkflowResult(false, false, CaptureFailureKind.Failed, null, null, false, false, "Selected region must have positive size.");
        }

        return CompleteCapture(_screenshots.CaptureRegion(selection.Bounds.Value), settings, locations);
    }

    public ScreenshotWorkflowResult CaptureFullScreen(ParallaxSettings settings, IPlatformLocations locations)
    {
        return CompleteCapture(_screenshots.CaptureFullScreen(), settings, locations);
    }

    private ScreenshotWorkflowResult CompleteCapture(CaptureResult capture, ParallaxSettings settings, IPlatformLocations locations)
    {
        if (!capture.Success || capture.Image is null)
        {
            return new ScreenshotWorkflowResult(false, false, capture.FailureKind, null, null, false, false, capture.Message);
        }

        string? savedPath = null;
        if (settings.SaveAutomatically)
        {
            var save = _imageSaver.Save(capture.Image, settings, locations);
            if (!save.Success)
            {
                return new ScreenshotWorkflowResult(false, false, CaptureFailureKind.Failed, capture.Image, null, false, false, save.Message);
            }

            savedPath = save.FilePath;
        }

        bool copied = false;
        if (settings.CopyToClipboardAfterCapture)
        {
            var copy = _clipboard.CopyImage(capture.Image);
            copied = copy.Success;
            if (!copy.Success)
            {
                return new ScreenshotWorkflowResult(false, false, CaptureFailureKind.Failed, capture.Image, savedPath, false, false, copy.Message);
            }
        }

        bool opened = false;
        if (settings.OpenAnnotationEditorAfterScreenshot)
        {
            var launch = _annotationEditor.Open(capture.Image.Clone(), savedPath);
            opened = launch.Success;
            if (!launch.Success)
            {
                return new ScreenshotWorkflowResult(false, false, CaptureFailureKind.Failed, capture.Image, savedPath, copied, false, launch.Message);
            }
        }

        return new ScreenshotWorkflowResult(true, false, null, capture.Image, savedPath, copied, opened, "Screenshot captured.");
    }
}
