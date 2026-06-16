using Avalonia;
using Avalonia.Styling;
using Parallax.Core.Settings;

namespace Parallax.App.Avalonia.Settings;

public sealed class AvaloniaThemePreviewApplier : IThemePreviewApplier
{
    private readonly Application _application;

    public AvaloniaThemePreviewApplier(Application application)
    {
        _application = application;
    }

    public void Apply(ThemePreset preset)
    {
        _application.RequestedThemeVariant = preset.Mode == ThemeCatalog.ModeLight
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }
}
