namespace Parallax.Core.Settings;

public sealed class ThemeSettingsService
{
    private readonly IThemePreviewApplier _applier;

    public ThemeSettingsService(IThemePreviewApplier applier)
    {
        _applier = applier;
    }

    public ThemePreset Preview(string? familyOrPreset, string? mode)
    {
        var preset = ThemeCatalog.Resolve(familyOrPreset, mode);
        _applier.Apply(preset);
        return preset;
    }

    public void Persist(ParallaxSettings settings, string? familyOrPreset, string? mode)
    {
        var preset = Preview(familyOrPreset, mode);
        settings.ThemeFamily = preset.Family;
        settings.ThemeMode = preset.Mode;
    }
}
