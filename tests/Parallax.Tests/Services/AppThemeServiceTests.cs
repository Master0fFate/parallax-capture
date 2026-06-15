using parallax.Core.Services;

namespace Parallax.Tests.Services;

public class AppThemeServiceTests
{
    [Theory]
    [InlineData(null, "Material 3")]
    [InlineData("", "Material 3")]
    [InlineData("material", "Material 3")]
    [InlineData("Catpuchinno mocha", "Catppuccin")]
    [InlineData("shadcn", "shadCN")]
    [InlineData("github dark", "GitHub")]
    public void NormalizeThemeFamily_MapsKnownNamesAndAliases(string? input, string expected)
    {
        Assert.Equal(expected, AppThemeService.NormalizeThemeFamily(input));
    }

    [Theory]
    [InlineData(null, "Dark")]
    [InlineData("dark", "Dark")]
    [InlineData("mocha", "Dark")]
    [InlineData("light", "Light")]
    [InlineData("latte", "Light")]
    public void NormalizeThemeMode_MapsKnownModesAndAliases(string? input, string expected)
    {
        Assert.Equal(expected, AppThemeService.NormalizeThemeMode(input));
    }

    [Fact]
    public void GetPalette_ReturnsCompleteProductBrushSet()
    {
        var palette = AppThemeService.GetPalette("GitHub", "Light");

        Assert.Equal("GitHub", palette.Family);
        Assert.Equal("Light", palette.Mode);
        Assert.Contains("GitHub Light", palette.DisplayName);

        foreach (var key in new[]
        {
            "ProductWindowBrush",
            "ProductChromeBrush",
            "ProductSurfaceBrush",
            "ProductSurfaceRaisedBrush",
            "ProductButtonBrush",
            "ProductInputBrush",
            "ProductBorderBrush",
            "ProductTextBrush",
            "ProductAccentBrush",
            "ProductAccentTextBrush",
            "ProductDangerBrush",
            "ProductWarningBrush",
            "ProductWarningSurfaceBrush",
            "ProductSuccessBrush",
            "ProductOverlayBrush"
        })
        {
            Assert.True(palette.Brushes.ContainsKey(key), $"Missing {key}");
        }
    }
}
