using parallax.Core.Services;

namespace Parallax.Tests.Services;

public class ScreenshotServiceTests
{
    private readonly ScreenshotService _service = new();

    [Fact]
    public void CaptureRegion_WithZeroWidth_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _service.CaptureRegion(0, 0, 0, 100));
        Assert.Contains("Width", ex.Message);
    }

    [Fact]
    public void CaptureRegion_WithZeroHeight_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _service.CaptureRegion(0, 0, 100, 0));
        Assert.Contains("Height", ex.Message);
    }

    [Fact]
    public void CaptureRegion_WithNegativeWidth_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _service.CaptureRegion(0, 0, -1, 100));
        Assert.Contains("Width", ex.Message);
    }

    [Fact]
    public void CaptureRegion_WithNegativeHeight_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _service.CaptureRegion(0, 0, 100, -50));
        Assert.Contains("Height", ex.Message);
    }

    // KAM edge case from audit: what about negative coordinates?
    // CopyFromScreen accepts negative coordinates on multi-monitor setups
    // (the virtual screen can have negative origins). On modern .NET/Windows,
    // negative coords succeed and capture from the virtual desktop origin.
    // This test validates the contract: it should NOT crash.
    [Fact]
    public void CaptureRegion_WithNegativeCoordinates_ReturnsBitmap()
    {
        using var bmp = _service.CaptureRegion(-9999, -9999, 100, 100);
        Assert.NotNull(bmp);
    }

    [Fact]
    public void GetFullScreenBounds_ReturnsNonEmpty()
    {
        var bounds = _service.GetFullScreenBounds();
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void GetPrimaryScreenBounds_ReturnsNonEmpty()
    {
        var bounds = _service.GetPrimaryScreenBounds();
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void CaptureFullScreen_ReturnsBitmap()
    {
        using var bmp = _service.CaptureFullScreen();
        Assert.NotNull(bmp);
        Assert.True(bmp.Width > 0);
        Assert.True(bmp.Height > 0);
    }

    [Fact]
    public void CaptureRegion_With10x10_ReturnsBitmap()
    {
        using var bmp = _service.CaptureRegion(100, 100, 10, 10);
        Assert.NotNull(bmp);
        Assert.Equal(10, bmp.Width);
        Assert.Equal(10, bmp.Height);
    }
}
