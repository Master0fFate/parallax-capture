using parallax.Core.Services;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

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

    [Theory]
    [InlineData("material-3-dark", "Material 3 Dark")]
    [InlineData("material-3-light", "Material 3 Light")]
    [InlineData("catppuccin-mocha", "Catppuccin Mocha")]
    [InlineData("catppuccin-latte", "Catppuccin Latte")]
    [InlineData("shadcn-dark", "shadCN Dark")]
    [InlineData("shadcn-light", "shadCN Light")]
    [InlineData("github-dark", "GitHub Dark")]
    [InlineData("github-light", "GitHub Light")]
    public void ResolveThemePreset_MapsExactPresetIds(string presetId, string expectedDisplayName)
    {
        var preset = AppThemeService.ResolveThemePreset(presetId, null);
        Assert.Equal(expectedDisplayName, preset.DisplayName);
    }

    [Theory]
    [InlineData("Catppuccin Latte", null, "Catppuccin Latte")]
    [InlineData("Catppuccin Mocha", "Light", "Catppuccin Mocha")]
    [InlineData("GitHub Light", "Dark", "GitHub Light")]
    [InlineData("Material 3 Light", "Dark", "Material 3 Light")]
    [InlineData("shadCN Dark", "Light", "shadCN Dark")]
    public void GetPalette_PreservesExplicitVariantNamesOverSeparateMode(string familyOrPreset, string? mode, string expectedDisplayName)
    {
        var palette = AppThemeService.GetPalette(familyOrPreset, mode);
        Assert.Equal(expectedDisplayName, palette.DisplayName);
    }

    [Theory]
    [InlineData("Material 3", "Dark", "#FF111318", "#FFE6E1E5", "#FFD0BCFF")]
    [InlineData("Material 3", "Light", "#FFFFFBFE", "#FF1D1B20", "#FF6750A4")]
    [InlineData("Catppuccin", "Dark", "#FF1E1E2E", "#FFCDD6F4", "#FF89B4FA")]
    [InlineData("Catppuccin", "Light", "#FFEFF1F5", "#FF4C4F69", "#FF1E66F5")]
    [InlineData("shadCN", "Dark", "#FF020817", "#FFF8FAFC", "#FFF8FAFC")]
    [InlineData("shadCN", "Light", "#FFFFFFFF", "#FF020817", "#FF0F172A")]
    [InlineData("GitHub", "Dark", "#FF0D1117", "#FFE6EDF3", "#FF2F81F7")]
    [InlineData("GitHub", "Light", "#FFFFFFFF", "#FF24292F", "#FF0969DA")]
    public void GetPalette_ReturnsRepresentativeColorsForNamedVariant(string family, string mode, string window, string text, string accent)
    {
        var palette = AppThemeService.GetPalette(family, mode);

        Assert.Equal(ParseColor(window), palette.Brushes["ProductWindowBrush"]);
        Assert.Equal(ParseColor(text), palette.Brushes["ProductTextBrush"]);
        Assert.Equal(ParseColor(accent), palette.Brushes["ProductAccentBrush"]);
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

    private static MediaColor ParseColor(string hex)
    {
        return (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
    }
}
