using Parallax.Core.Capture;

namespace Parallax.Core.Annotation;

public enum AnnotationTool
{
    Pen,
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    Highlighter,
    Blur
}

public enum AnnotationCommandId
{
    Pen,
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    Highlighter,
    Blur,
    Undo,
    Clear,
    Save,
    Copy
}

public sealed record AnnotationToolbarCommand(AnnotationCommandId Id, string Label);

public static class AnnotationToolbarCatalog
{
    public static IReadOnlyList<AnnotationToolbarCommand> Commands { get; } =
    [
        new(AnnotationCommandId.Pen, "Pen"),
        new(AnnotationCommandId.Arrow, "Arrow"),
        new(AnnotationCommandId.Rectangle, "Rectangle"),
        new(AnnotationCommandId.Ellipse, "Ellipse"),
        new(AnnotationCommandId.Text, "Text"),
        new(AnnotationCommandId.Highlighter, "Highlighter"),
        new(AnnotationCommandId.Blur, "Blur"),
        new(AnnotationCommandId.Undo, "Undo"),
        new(AnnotationCommandId.Clear, "Clear"),
        new(AnnotationCommandId.Save, "Save"),
        new(AnnotationCommandId.Copy, "Copy")
    ];
}

public sealed record AnnotationMark(
    AnnotationTool Tool,
    IReadOnlyList<CapturePoint> Points,
    CaptureRectangle? Bounds,
    RgbaColor Color,
    int StrokeThickness,
    string? Text,
    int BlurRadius)
{
    public static AnnotationMark Pen(IReadOnlyList<CapturePoint> points, RgbaColor color, int strokeThickness)
    {
        return new AnnotationMark(AnnotationTool.Pen, points.ToArray(), null, color, Math.Max(1, strokeThickness), null, 0);
    }

    public static AnnotationMark Arrow(CapturePoint start, CapturePoint end, RgbaColor color, int strokeThickness)
    {
        return new AnnotationMark(AnnotationTool.Arrow, [start, end], null, color, Math.Max(1, strokeThickness), null, 0);
    }

    public static AnnotationMark Rectangle(CaptureRectangle bounds, RgbaColor color, int strokeThickness)
    {
        return new AnnotationMark(AnnotationTool.Rectangle, [], bounds, color, Math.Max(1, strokeThickness), null, 0);
    }

    public static AnnotationMark Ellipse(CaptureRectangle bounds, RgbaColor color, int strokeThickness)
    {
        return new AnnotationMark(AnnotationTool.Ellipse, [], bounds, color, Math.Max(1, strokeThickness), null, 0);
    }

    public static AnnotationMark TextMark(CapturePoint origin, string text, RgbaColor color, int strokeThickness)
    {
        return new AnnotationMark(AnnotationTool.Text, [origin], null, color, Math.Max(1, strokeThickness), text, 0);
    }

    public static AnnotationMark Highlighter(CaptureRectangle bounds, RgbaColor color, int strokeThickness)
    {
        var highlight = color.A == 255 ? new RgbaColor(color.R, color.G, color.B, 96) : color;
        return new AnnotationMark(AnnotationTool.Highlighter, [], bounds, highlight, Math.Max(1, strokeThickness), null, 0);
    }

    public static AnnotationMark Blur(CaptureRectangle bounds, int radius)
    {
        return new AnnotationMark(AnnotationTool.Blur, [], bounds, RgbaColor.Transparent, 1, null, Math.Max(1, radius));
    }
}

public sealed class AnnotationDocument
{
    private readonly List<AnnotationMark> _marks = [];

    public AnnotationDocument(CaptureImage source)
    {
        Source = source.Clone();
    }

    public CaptureImage Source { get; }

    public IReadOnlyList<AnnotationMark> Marks => _marks;

    public void Add(AnnotationMark mark)
    {
        _marks.Add(mark);
    }

    public bool Undo()
    {
        if (_marks.Count == 0)
        {
            return false;
        }

        _marks.RemoveAt(_marks.Count - 1);
        return true;
    }

    public void Clear()
    {
        _marks.Clear();
    }

    public CaptureImage Render()
    {
        var target = Source.Clone();
        foreach (var mark in _marks)
        {
            Apply(target, mark);
        }

        return target;
    }

    private static void Apply(CaptureImage target, AnnotationMark mark)
    {
        switch (mark.Tool)
        {
            case AnnotationTool.Pen:
                DrawPolyline(target, mark.Points, mark.Color, mark.StrokeThickness);
                break;
            case AnnotationTool.Arrow:
                DrawPolyline(target, mark.Points, mark.Color, mark.StrokeThickness);
                if (mark.Points.Count >= 2)
                {
                    DrawSquare(target, mark.Points[^1], mark.Color, mark.StrokeThickness + 1);
                }
                break;
            case AnnotationTool.Rectangle:
                DrawRectangle(target, mark.Bounds!.Value, mark.Color, mark.StrokeThickness);
                break;
            case AnnotationTool.Ellipse:
                DrawEllipse(target, mark.Bounds!.Value, mark.Color, mark.StrokeThickness);
                break;
            case AnnotationTool.Text:
                DrawTextBlock(target, mark.Points[0], mark.Text ?? string.Empty, mark.Color, mark.StrokeThickness);
                break;
            case AnnotationTool.Highlighter:
                FillRectangle(target, mark.Bounds!.Value, mark.Color);
                break;
            case AnnotationTool.Blur:
                BlurRectangle(target, mark.Bounds!.Value, mark.BlurRadius);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mark), mark.Tool, "Unsupported annotation tool.");
        }
    }

    private static void DrawPolyline(CaptureImage target, IReadOnlyList<CapturePoint> points, RgbaColor color, int thickness)
    {
        if (points.Count == 1)
        {
            DrawSquare(target, points[0], color, thickness);
            return;
        }

        for (int i = 1; i < points.Count; i++)
        {
            DrawLine(target, points[i - 1], points[i], color, thickness);
        }
    }

    private static void DrawLine(CaptureImage target, CapturePoint start, CapturePoint end, RgbaColor color, int thickness)
    {
        int dx = Math.Abs(end.X - start.X);
        int sx = start.X < end.X ? 1 : -1;
        int dy = -Math.Abs(end.Y - start.Y);
        int sy = start.Y < end.Y ? 1 : -1;
        int error = dx + dy;
        int x = start.X;
        int y = start.Y;

        while (true)
        {
            DrawSquare(target, new CapturePoint(x, y), color, thickness);
            if (x == end.X && y == end.Y)
            {
                break;
            }

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    private static void DrawRectangle(CaptureImage target, CaptureRectangle bounds, RgbaColor color, int thickness)
    {
        for (int t = 0; t < thickness; t++)
        {
            for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                Set(target, x, bounds.Y + t, color);
                Set(target, x, bounds.Y + bounds.Height - 1 - t, color);
            }

            for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            {
                Set(target, bounds.X + t, y, color);
                Set(target, bounds.X + bounds.Width - 1 - t, y, color);
            }
        }
    }

    private static void DrawEllipse(CaptureImage target, CaptureRectangle bounds, RgbaColor color, int thickness)
    {
        double rx = bounds.Width / 2.0;
        double ry = bounds.Height / 2.0;
        double cx = bounds.X + rx;
        double cy = bounds.Y + ry;
        double threshold = Math.Max(0.08, thickness / Math.Max(rx, ry));

        for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                double value = Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2);
                if (Math.Abs(value - 1) <= threshold)
                {
                    Set(target, x, y, color);
                }
            }
    }

    private static void DrawTextBlock(CaptureImage target, CapturePoint origin, string text, RgbaColor color, int strokeThickness)
    {
        int width = Math.Max(strokeThickness, text.Length * Math.Max(1, strokeThickness));
        int height = Math.Max(1, strokeThickness);
        FillRectangle(target, new CaptureRectangle(origin.X, origin.Y, width, height), color);
    }

    private static void FillRectangle(CaptureImage target, CaptureRectangle bounds, RgbaColor color)
    {
        for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                Set(target, x, y, color);
            }
    }

    private static void BlurRectangle(CaptureImage target, CaptureRectangle bounds, int radius)
    {
        var original = target.Clone();
        for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                int r = 0;
                int g = 0;
                int b = 0;
                int a = 0;
                int count = 0;
                for (int yy = y - radius; yy <= y + radius; yy++)
                    for (int xx = x - radius; xx <= x + radius; xx++)
                    {
                        if (!original.Contains(xx, yy))
                        {
                            continue;
                        }

                        var color = original.GetPixel(xx, yy);
                        r += color.R;
                        g += color.G;
                        b += color.B;
                        a += color.A;
                        count++;
                    }

                if (count > 0)
                {
                    target.SetPixel(x, y, new RgbaColor((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count)));
                }
            }
    }

    private static void DrawSquare(CaptureImage target, CapturePoint point, RgbaColor color, int thickness)
    {
        int radius = Math.Max(0, thickness / 2);
        for (int y = point.Y - radius; y <= point.Y + radius; y++)
            for (int x = point.X - radius; x <= point.X + radius; x++)
            {
                Set(target, x, y, color);
            }
    }

    private static void Set(CaptureImage target, int x, int y, RgbaColor color)
    {
        if (!target.Contains(x, y))
        {
            return;
        }

        target.SetPixel(x, y, color.BlendOver(target.GetPixel(x, y)));
    }
}
