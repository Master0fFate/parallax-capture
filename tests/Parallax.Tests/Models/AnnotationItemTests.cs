using parallax.Core.Models;

namespace Parallax.Tests.Models;

public class AnnotationItemTests
{
    [Fact]
    public void DefaultType_IsPen()
    {
        var item = new AnnotationItem();
        Assert.Equal("Pen", item.Type);
    }

    [Fact]
    public void DefaultColor_IsRed()
    {
        var item = new AnnotationItem();
        Assert.Equal(System.Windows.Media.Colors.Red, item.Color);
    }

    [Fact]
    public void DefaultStrokeThickness_Is2()
    {
        var item = new AnnotationItem();
        Assert.Equal(2.0, item.StrokeThickness);
    }

    [Fact]
    public void Points_InitializedEmpty()
    {
        var item = new AnnotationItem();
        Assert.NotNull(item.Points);
        Assert.Empty(item.Points);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var item = new AnnotationItem
        {
            Type = "Rectangle",
            StartPoint = new System.Windows.Point(10, 20),
            EndPoint = new System.Windows.Point(100, 200),
            Color = System.Windows.Media.Colors.Blue,
            StrokeThickness = 5.0,
            Text = "Hello",
            Points = { new System.Windows.Point(1, 2) }
        };

        Assert.Equal("Rectangle", item.Type);
        Assert.Equal(10, item.StartPoint.X);
        Assert.Equal(20, item.StartPoint.Y);
        Assert.Equal(System.Windows.Media.Colors.Blue, item.Color);
        Assert.Equal(5.0, item.StrokeThickness);
        Assert.Equal("Hello", item.Text);
        Assert.Single(item.Points);
    }
}
