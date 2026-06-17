using Avalonia.Threading;
using Parallax.App.Avalonia.Annotation;
using Parallax.Core.Capture;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.App.Avalonia.Capture;

public interface IScreenshotWorkflowRunner
{
    ScreenshotWorkflowResult CaptureRegion(ParallaxSettings settings, IPlatformLocations locations);

    ScreenshotWorkflowResult CaptureFullScreen(ParallaxSettings settings, IPlatformLocations locations);
}

public sealed class AvaloniaScreenshotWorkflowRunner : IScreenshotWorkflowRunner
{
    private readonly ScreenshotWorkflow _workflow;

    public AvaloniaScreenshotWorkflowRunner(ScreenshotWorkflow workflow)
    {
        _workflow = workflow;
    }

    public ScreenshotWorkflowResult CaptureRegion(ParallaxSettings settings, IPlatformLocations locations)
    {
        return _workflow.CaptureRegion(settings, locations);
    }

    public ScreenshotWorkflowResult CaptureFullScreen(ParallaxSettings settings, IPlatformLocations locations)
    {
        return _workflow.CaptureFullScreen(settings, locations);
    }
}

public sealed class AvaloniaAnnotationEditorLauncher : IAnnotationEditorLauncher
{
    private readonly IClipboardService _clipboard;
    private readonly CollisionSafeImageSaver _saver;
    private readonly IPlatformLocations _locations;
    private readonly ParallaxSettings _settings;

    public AvaloniaAnnotationEditorLauncher(
        IClipboardService clipboard,
        CollisionSafeImageSaver saver,
        IPlatformLocations locations,
        ParallaxSettings settings)
    {
        _clipboard = clipboard;
        _saver = saver;
        _locations = locations;
        _settings = settings;
    }

    public AnnotationEditorLaunchResult Open(CaptureImage image, string? sourcePath)
    {
        try
        {
            void Show()
            {
                var model = new AnnotationEditorWindowModel(image, _clipboard, _saver, _locations, sourcePath);
                new AnnotationEditorWindow(model, _settings).Show();
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Show();
            }
            else
            {
                Dispatcher.UIThread.Post(Show);
            }

            return new AnnotationEditorLaunchResult(true, "Annotation editor opened.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return new AnnotationEditorLaunchResult(false, $"Could not open annotation editor: {ex.Message}");
        }
    }
}
