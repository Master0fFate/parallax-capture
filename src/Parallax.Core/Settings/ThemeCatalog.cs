namespace Parallax.Core.Settings;

public static class ThemeCatalog
{
    public const string ModeLight = "Light";
    public const string ModeDark = "Dark";
    public const string FamilyMaterial = "Material 3";
    public const string FamilyCatppuccin = "Catppuccin";
    public const string FamilyShadCn = "shadCN";
    public const string FamilyGitHub = "GitHub";

    public static IReadOnlyList<ThemePreset> Presets { get; } =
    [
        new("material-3-dark", FamilyMaterial, ModeDark, "Material 3 Dark"),
        new("material-3-light", FamilyMaterial, ModeLight, "Material 3 Light"),
        new("catppuccin-mocha", FamilyCatppuccin, ModeDark, "Catppuccin Mocha"),
        new("catppuccin-latte", FamilyCatppuccin, ModeLight, "Catppuccin Latte"),
        new("shadcn-dark", FamilyShadCn, ModeDark, "shadCN Dark"),
        new("shadcn-light", FamilyShadCn, ModeLight, "shadCN Light"),
        new("github-dark", FamilyGitHub, ModeDark, "GitHub Dark"),
        new("github-light", FamilyGitHub, ModeLight, "GitHub Light")
    ];

    public static ThemePreset Resolve(string? familyOrPreset, string? mode)
    {
        if (TryFindPreset(familyOrPreset, out var preset) || TryFindPreset(mode, out preset))
        {
            return preset;
        }

        string family = NormalizeFamily(familyOrPreset);
        string resolvedMode = ResolveMode(familyOrPreset, mode);
        return Presets.First(item => item.Family == family && item.Mode == resolvedMode);
    }

    public static bool TryFindPreset(string? value, out ThemePreset preset)
    {
        preset = Presets[0];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string theme = value.Trim();
        foreach (var item in Presets)
        {
            if (theme.Equals(item.Id, StringComparison.OrdinalIgnoreCase)
                || theme.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase)
                || theme.Equals($"{item.Family}|{item.Mode}", StringComparison.OrdinalIgnoreCase)
                || theme.Equals($"{item.Family} {item.Mode}", StringComparison.OrdinalIgnoreCase))
            {
                preset = item;
                return true;
            }
        }

        return false;
    }

    public static string NormalizeFamily(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return FamilyMaterial;
        }

        string value = family.Trim();
        if (value.Equals(FamilyMaterial, StringComparison.OrdinalIgnoreCase) || value.Contains("material", StringComparison.OrdinalIgnoreCase))
        {
            return FamilyMaterial;
        }

        if (value.Equals(FamilyCatppuccin, StringComparison.OrdinalIgnoreCase)
            || value.Contains("catppuccin", StringComparison.OrdinalIgnoreCase)
            || value.Contains("catpuchin", StringComparison.OrdinalIgnoreCase)
            || value.Contains("mocha", StringComparison.OrdinalIgnoreCase)
            || value.Contains("latte", StringComparison.OrdinalIgnoreCase))
        {
            return FamilyCatppuccin;
        }

        if (value.Equals(FamilyShadCn, StringComparison.OrdinalIgnoreCase) || value.Contains("shad", StringComparison.OrdinalIgnoreCase))
        {
            return FamilyShadCn;
        }

        return value.Equals(FamilyGitHub, StringComparison.OrdinalIgnoreCase) || value.Contains("github", StringComparison.OrdinalIgnoreCase)
            ? FamilyGitHub
            : FamilyMaterial;
    }

    public static string NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return ModeDark;
        }

        string value = mode.Trim();
        if (value.Equals(ModeLight, StringComparison.OrdinalIgnoreCase)
            || value.Contains("light", StringComparison.OrdinalIgnoreCase)
            || value.Contains("latte", StringComparison.OrdinalIgnoreCase))
        {
            return ModeLight;
        }

        return ModeDark;
    }

    private static string ResolveMode(string? familyOrPreset, string? mode)
    {
        if (!string.IsNullOrWhiteSpace(familyOrPreset)
            && (familyOrPreset.Contains("light", StringComparison.OrdinalIgnoreCase)
                || familyOrPreset.Contains("latte", StringComparison.OrdinalIgnoreCase)))
        {
            return ModeLight;
        }

        return NormalizeMode(mode);
    }
}

public sealed record ThemePreset(string Id, string Family, string Mode, string DisplayName);

public interface IThemePreviewApplier
{
    void Apply(ThemePreset preset);
}
