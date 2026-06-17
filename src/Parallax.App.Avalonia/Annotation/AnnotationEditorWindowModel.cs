using Parallax.Core.Annotation;
using Parallax.Core.Capture;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.App.Avalonia.Annotation;

public sealed class AnnotationEditorWindowModel
{
    private readonly IClipboardService _clipboard;
    private readonly CollisionSafeImageSaver _saver;
    private readonly IPlatformLocations _locations;

    public AnnotationEditorWindowModel(
        CaptureImage source,
        IClipboardService clipboard,
        CollisionSafeImageSaver saver,
        IPlatformLocations locations,
        string? sourcePath = null)
    {
        Document = new AnnotationDocument(source);
        _clipboard = clipboard;
        _saver = saver;
        _locations = locations;
        SourcePath = sourcePath;
    }

    public AnnotationDocument Document { get; }

    public string? SourcePath { get; }

    public AnnotationTool SelectedTool { get; private set; } = AnnotationTool.Pen;

    public RgbaColor SelectedColor { get; private set; } = RgbaColor.Red;

    public int StrokeThickness { get; private set; } = 2;

    public IReadOnlyList<AnnotationToolbarCommand> ToolbarCommands => AnnotationToolbarCatalog.Commands;

    public string StatusMessage { get; private set; } = "Ready.";

    public void SelectTool(AnnotationTool tool)
    {
        SelectedTool = tool;
        StatusMessage = $"{tool} selected.";
    }

    public void SetStyle(RgbaColor color, int strokeThickness)
    {
        SelectedColor = color;
        StrokeThickness = Math.Max(1, strokeThickness);
        StatusMessage = "Annotation style updated.";
    }

    public void AddPenStroke(IReadOnlyList<CapturePoint> points)
    {
        Document.Add(AnnotationMark.Pen(points, SelectedColor, StrokeThickness));
        StatusMessage = "Pen stroke added.";
    }

    public void AddShape(CaptureRectangle bounds)
    {
        Document.Add(SelectedTool switch
        {
            AnnotationTool.Arrow => AnnotationMark.Arrow(new CapturePoint(bounds.X, bounds.Y), new CapturePoint(bounds.X + bounds.Width, bounds.Y + bounds.Height), SelectedColor, StrokeThickness),
            AnnotationTool.Rectangle => AnnotationMark.Rectangle(bounds, SelectedColor, StrokeThickness),
            AnnotationTool.Ellipse => AnnotationMark.Ellipse(bounds, SelectedColor, StrokeThickness),
            AnnotationTool.Highlighter => AnnotationMark.Highlighter(bounds, SelectedColor, StrokeThickness),
            AnnotationTool.Blur => AnnotationMark.Blur(bounds, StrokeThickness),
            _ => AnnotationMark.Rectangle(bounds, SelectedColor, StrokeThickness)
        });
        StatusMessage = $"{SelectedTool} annotation added.";
    }

    public void AddText(CapturePoint origin, string text)
    {
        Document.Add(AnnotationMark.TextMark(origin, text, SelectedColor, StrokeThickness));
        StatusMessage = "Text annotation added.";
    }

    public bool Undo()
    {
        bool undone = Document.Undo();
        StatusMessage = undone ? "Last annotation removed." : "No annotations to undo.";
        return undone;
    }

    public void Clear()
    {
        Document.Clear();
        StatusMessage = "All annotations cleared.";
    }

    public ImageSaveResult Save(ParallaxSettings settings)
    {
        var result = AnnotationExportWorkflow.Save(Document, settings, _locations, _saver, SourcePath);
        StatusMessage = result.Message;
        return result;
    }

    public ClipboardImageResult Copy()
    {
        var result = AnnotationExportWorkflow.Copy(Document, _clipboard);
        StatusMessage = result.Message;
        return result;
    }
}
