using parallax.Core.Services;
using System.Windows;
using System.Windows.Media;

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

    [Theory]
    [InlineData("Material 3", "Dark", "Material 3 Dark")]
    [InlineData("Material 3", "Light", "Material 3 Light")]
    [InlineData("Catppuccin", "Dark", "Catppuccin Mocha")]
    [InlineData("Catppuccin", "Light", "Catppuccin Latte")]
    [InlineData("shadCN", "Dark", "shadCN Dark")]
    [InlineData("shadCN", "Light", "shadCN Light")]
    [InlineData("GitHub", "Dark", "GitHub Dark")]
    [InlineData("GitHub", "Light", "GitHub Light")]
    public void GetPalette_UsesConcreteLightAndDarkVariantNames(string family, string mode, string expectedDisplayName)
    {
        var palette = AppThemeService.GetPalette(family, mode);
        Assert.Equal(expectedDisplayName, palette.DisplayName);
    }

    [Fact]
    public void ApplyTo_ReplacesExistingBrushResourcesForDynamicResourceConsumers()
    {
        var resources = new ResourceDictionary();
        var accent = new SolidColorBrush(Colors.Red);
        resources["ProductAccentBrush"] = accent;

        AppThemeService.ApplyTo(resources, "GitHub", "Light");

        var expected = AppThemeService.GetPalette("GitHub", "Light").Brushes["ProductAccentBrush"];
        var actual = Assert.IsType<SolidColorBrush>(resources["ProductAccentBrush"]);
        Assert.NotSame(accent, actual);
        Assert.Equal(expected, actual.Color);
    }
}
