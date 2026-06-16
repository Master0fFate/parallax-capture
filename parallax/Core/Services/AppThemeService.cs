using System.Windows;
using System.Windows.Media;
using parallax.Core.Models;
using MediaColor = System.Windows.Media.Color;

namespace parallax.Core.Services
{
    public static class AppThemeService
    {
        public const string ModeLight = "Light";
        public const string ModeDark = "Dark";
        public const string FamilyMaterial = "Material 3";
        public const string FamilyCatppuccin = "Catppuccin";
        public const string FamilyShadCn = "shadCN";
        public const string FamilyGitHub = "GitHub";

        public static IReadOnlyList<string> ThemeFamilies { get; } =
        [
            FamilyMaterial,
            FamilyCatppuccin,
            FamilyShadCn,
            FamilyGitHub
        ];

        public static IReadOnlyList<string> ThemeModes { get; } =
        [
            ModeLight,
            ModeDark
        ];

        public sealed record ThemePalette(string Family, string Mode, string DisplayName, IReadOnlyDictionary<string, MediaColor> Brushes);
        public sealed record ThemePreset(string Id, string Family, string Mode, string DisplayName);

        public static IReadOnlyList<ThemePreset> ThemePresets { get; } =
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

        public static void Apply(AppSettings settings)
        {
            Apply(settings.ThemeFamily, settings.ThemeMode);
        }

        public static void Apply(string? family, string? mode)
        {
            var app = Application.Current;
            if (app == null)
                return;

            if (!app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(() => ApplyTo(app.Resources, family, mode));
                return;
            }

            ApplyTo(app.Resources, family, mode);
        }

        public static void ApplyTo(ResourceDictionary resources, string? family, string? mode)
        {
            var palette = GetPalette(family, mode);
            ApplyPalette(resources, palette);
        }

        public static ThemePalette GetPalette(string? family, string? mode)
        {
            var preset = ResolveThemePreset(family, mode);

            return preset.Family switch
            {
                FamilyCatppuccin => BuildPalette(preset, preset.Mode == ModeDark ? CatppuccinMocha : CatppuccinLatte),
                FamilyShadCn => BuildPalette(preset, preset.Mode == ModeDark ? ShadCnDark : ShadCnLight),
                FamilyGitHub => BuildPalette(preset, preset.Mode == ModeDark ? GitHubDark : GitHubLight),
                _ => BuildPalette(preset, preset.Mode == ModeDark ? MaterialDark : MaterialLight)
            };
        }

        public static ThemePreset ResolveThemePreset(string? familyOrPreset, string? mode)
        {
            if (TryFindThemePreset(familyOrPreset, out var preset) || TryFindThemePreset(mode, out preset))
                return preset;

            string family = NormalizeThemeFamily(familyOrPreset);
            string resolvedMode = ResolveThemeMode(familyOrPreset, mode);
            return ThemePresets.First(item => item.Family == family && item.Mode == resolvedMode);
        }

        public static bool TryFindThemePreset(string? value, out ThemePreset preset)
        {
            preset = ThemePresets[0];
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string theme = value.Trim();
            foreach (var item in ThemePresets)
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

        public static string NormalizeThemeFamily(string? family)
        {
            if (string.IsNullOrWhiteSpace(family))
                return FamilyMaterial;

            string value = family.Trim();
            if (value.Equals(FamilyMaterial, StringComparison.OrdinalIgnoreCase) || value.Contains("material", StringComparison.OrdinalIgnoreCase))
                return FamilyMaterial;
            if (value.Equals(FamilyCatppuccin, StringComparison.OrdinalIgnoreCase)
                || value.Contains("catppuccin", StringComparison.OrdinalIgnoreCase)
                || value.Contains("catpuchin", StringComparison.OrdinalIgnoreCase)
                || value.Contains("mocha", StringComparison.OrdinalIgnoreCase)
                || value.Contains("latte", StringComparison.OrdinalIgnoreCase))
                return FamilyCatppuccin;
            if (value.Equals(FamilyShadCn, StringComparison.OrdinalIgnoreCase) || value.Contains("shad", StringComparison.OrdinalIgnoreCase))
                return FamilyShadCn;
            if (value.Equals(FamilyGitHub, StringComparison.OrdinalIgnoreCase) || value.Contains("github", StringComparison.OrdinalIgnoreCase))
                return FamilyGitHub;

            return FamilyMaterial;
        }

        public static string NormalizeThemeMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return ModeDark;

            string value = mode.Trim();
            if (value.Equals(ModeLight, StringComparison.OrdinalIgnoreCase) || value.Contains("light", StringComparison.OrdinalIgnoreCase) || value.Contains("latte", StringComparison.OrdinalIgnoreCase))
                return ModeLight;
            if (value.Equals(ModeDark, StringComparison.OrdinalIgnoreCase) || value.Contains("dark", StringComparison.OrdinalIgnoreCase) || value.Contains("mocha", StringComparison.OrdinalIgnoreCase))
                return ModeDark;

            return ModeDark;
        }

        private static string ResolveThemeMode(string? familyOrPreset, string? mode)
        {
            if (ContainsLightVariant(familyOrPreset))
                return ModeLight;
            if (ContainsDarkVariant(familyOrPreset))
                return ModeDark;

            return NormalizeThemeMode(mode);
        }

        private static bool ContainsLightVariant(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && (value.Contains("light", StringComparison.OrdinalIgnoreCase)
                       || value.Contains("latte", StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsDarkVariant(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && (value.Contains("dark", StringComparison.OrdinalIgnoreCase)
                       || value.Contains("mocha", StringComparison.OrdinalIgnoreCase));
        }

        private static ThemePalette BuildPalette(ThemePreset preset, IReadOnlyDictionary<string, string> hexColors)
        {
            return new ThemePalette(
                preset.Family,
                preset.Mode,
                preset.DisplayName,
                hexColors.ToDictionary(pair => pair.Key, pair => ParseColor(pair.Value)));
        }

        private static void ApplyPalette(ResourceDictionary resources, ThemePalette palette)
        {
            foreach (var pair in palette.Brushes)
            {
                resources[pair.Key] = new SolidColorBrush(pair.Value);
            }
        }

        private static MediaColor ParseColor(string hex)
        {
            return (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        }

        private static readonly IReadOnlyDictionary<string, string> MaterialDark = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FF111318",
            ["ProductChromeBrush"] = "#FF1B1B20",
            ["ProductSurfaceBrush"] = "#FF211F26",
            ["ProductSurfaceRaisedBrush"] = "#FF2B2930",
            ["ProductSurfaceHoverBrush"] = "#FF36333B",
            ["ProductSurfacePressedBrush"] = "#FF141218",
            ["ProductButtonBrush"] = "#FF2F2D35",
            ["ProductButtonHoverBrush"] = "#FF3B3842",
            ["ProductButtonPressedBrush"] = "#FF25232B",
            ["ProductInputBrush"] = "#FF17151B",
            ["ProductInputHoverBrush"] = "#FF211F26",
            ["ProductBorderBrush"] = "#FF49454F",
            ["ProductBorderStrongBrush"] = "#FF938F99",
            ["ProductSeparatorBrush"] = "#FF322F37",
            ["ProductTextBrush"] = "#FFE6E1E5",
            ["ProductTextMutedBrush"] = "#FFCAC4D0",
            ["ProductTextSubtleBrush"] = "#FF938F99",
            ["ProductAccentBrush"] = "#FFD0BCFF",
            ["ProductAccentHoverBrush"] = "#FFE9DDFF",
            ["ProductAccentPressedBrush"] = "#FFB69DF8",
            ["ProductAccentTextBrush"] = "#FF381E72",
            ["ProductFocusBrush"] = "#FFD0BCFF",
            ["ProductDangerBrush"] = "#FFF2B8B5",
            ["ProductDangerHoverBrush"] = "#FFFFDAD6",
            ["ProductDangerPressedBrush"] = "#FFEFB8C8",
            ["ProductWarningBrush"] = "#FFFFDDB3",
            ["ProductWarningSurfaceBrush"] = "#FF332716",
            ["ProductSuccessBrush"] = "#FF8CDFAF",
            ["ProductDisabledBrush"] = "#FF24232A",
            ["ProductDisabledTextBrush"] = "#FF79747E",
            ["ProductOverlayBrush"] = "#CC000000"
        };

        private static readonly IReadOnlyDictionary<string, string> MaterialLight = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FFFFFBFE",
            ["ProductChromeBrush"] = "#FFF7F2FA",
            ["ProductSurfaceBrush"] = "#FFF3EDF7",
            ["ProductSurfaceRaisedBrush"] = "#FFECE6F0",
            ["ProductSurfaceHoverBrush"] = "#FFE8DEF8",
            ["ProductSurfacePressedBrush"] = "#FFEADDFF",
            ["ProductButtonBrush"] = "#FFF3EDF7",
            ["ProductButtonHoverBrush"] = "#FFE8DEF8",
            ["ProductButtonPressedBrush"] = "#FFEADDFF",
            ["ProductInputBrush"] = "#FFFFFBFE",
            ["ProductInputHoverBrush"] = "#FFF7F2FA",
            ["ProductBorderBrush"] = "#FFCAC4D0",
            ["ProductBorderStrongBrush"] = "#FF79747E",
            ["ProductSeparatorBrush"] = "#FFE7E0EC",
            ["ProductTextBrush"] = "#FF1D1B20",
            ["ProductTextMutedBrush"] = "#FF49454F",
            ["ProductTextSubtleBrush"] = "#FF6750A4",
            ["ProductAccentBrush"] = "#FF6750A4",
            ["ProductAccentHoverBrush"] = "#FF7F67BE",
            ["ProductAccentPressedBrush"] = "#FF4F378B",
            ["ProductAccentTextBrush"] = "#FFFFFFFF",
            ["ProductFocusBrush"] = "#FF6750A4",
            ["ProductDangerBrush"] = "#FFBA1A1A",
            ["ProductDangerHoverBrush"] = "#FFDE3730",
            ["ProductDangerPressedBrush"] = "#FF93000A",
            ["ProductWarningBrush"] = "#FF7D5700",
            ["ProductWarningSurfaceBrush"] = "#FFFFF0D5",
            ["ProductSuccessBrush"] = "#FF006D3B",
            ["ProductDisabledBrush"] = "#FFE6E0E9",
            ["ProductDisabledTextBrush"] = "#FF79747E",
            ["ProductOverlayBrush"] = "#CC000000"
        };

        private static readonly IReadOnlyDictionary<string, string> CatppuccinMocha = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FF1E1E2E",
            ["ProductChromeBrush"] = "#FF181825",
            ["ProductSurfaceBrush"] = "#FF242438",
            ["ProductSurfaceRaisedBrush"] = "#FF313244",
            ["ProductSurfaceHoverBrush"] = "#FF45475A",
            ["ProductSurfacePressedBrush"] = "#FF11111B",
            ["ProductButtonBrush"] = "#FF313244",
            ["ProductButtonHoverBrush"] = "#FF45475A",
            ["ProductButtonPressedBrush"] = "#FF1E1E2E",
            ["ProductInputBrush"] = "#FF181825",
            ["ProductInputHoverBrush"] = "#FF242438",
            ["ProductBorderBrush"] = "#FF45475A",
            ["ProductBorderStrongBrush"] = "#FF6C7086",
            ["ProductSeparatorBrush"] = "#FF313244",
            ["ProductTextBrush"] = "#FFCDD6F4",
            ["ProductTextMutedBrush"] = "#FFBAC2DE",
            ["ProductTextSubtleBrush"] = "#FFA6ADC8",
            ["ProductAccentBrush"] = "#FF89B4FA",
            ["ProductAccentHoverBrush"] = "#FFB4BEFE",
            ["ProductAccentPressedBrush"] = "#FF74C7EC",
            ["ProductAccentTextBrush"] = "#FF11111B",
            ["ProductFocusBrush"] = "#FF89DCEB",
            ["ProductDangerBrush"] = "#FFF38BA8",
            ["ProductDangerHoverBrush"] = "#FFFFB3C7",
            ["ProductDangerPressedBrush"] = "#FFE64570",
            ["ProductWarningBrush"] = "#FFF9E2AF",
            ["ProductWarningSurfaceBrush"] = "#FF322817",
            ["ProductSuccessBrush"] = "#FFA6E3A1",
            ["ProductDisabledBrush"] = "#FF313244",
            ["ProductDisabledTextBrush"] = "#FF6C7086",
            ["ProductOverlayBrush"] = "#CC000000"
        };

        private static readonly IReadOnlyDictionary<string, string> CatppuccinLatte = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FFEFF1F5",
            ["ProductChromeBrush"] = "#FFE6E9EF",
            ["ProductSurfaceBrush"] = "#FFDCE0E8",
            ["ProductSurfaceRaisedBrush"] = "#FFCCD0DA",
            ["ProductSurfaceHoverBrush"] = "#FFBCC0CC",
            ["ProductSurfacePressedBrush"] = "#FFE6E9EF",
            ["ProductButtonBrush"] = "#FFDCE0E8",
            ["ProductButtonHoverBrush"] = "#FFCCD0DA",
            ["ProductButtonPressedBrush"] = "#FFBCC0CC",
            ["ProductInputBrush"] = "#FFEFF1F5",
            ["ProductInputHoverBrush"] = "#FFE6E9EF",
            ["ProductBorderBrush"] = "#FFBCC0CC",
            ["ProductBorderStrongBrush"] = "#FF8C8FA1",
            ["ProductSeparatorBrush"] = "#FFCCD0DA",
            ["ProductTextBrush"] = "#FF4C4F69",
            ["ProductTextMutedBrush"] = "#FF5C5F77",
            ["ProductTextSubtleBrush"] = "#FF6C6F85",
            ["ProductAccentBrush"] = "#FF1E66F5",
            ["ProductAccentHoverBrush"] = "#FF7287FD",
            ["ProductAccentPressedBrush"] = "#FF0B52D6",
            ["ProductAccentTextBrush"] = "#FFFFFFFF",
            ["ProductFocusBrush"] = "#FF04A5E5",
            ["ProductDangerBrush"] = "#FFD20F39",
            ["ProductDangerHoverBrush"] = "#FFE64553",
            ["ProductDangerPressedBrush"] = "#FFB0002A",
            ["ProductWarningBrush"] = "#FFDF8E1D",
            ["ProductWarningSurfaceBrush"] = "#FFFFF1D2",
            ["ProductSuccessBrush"] = "#FF40A02B",
            ["ProductDisabledBrush"] = "#FFE6E9EF",
            ["ProductDisabledTextBrush"] = "#FF8C8FA1",
            ["ProductOverlayBrush"] = "#CC000000"
        };

        private static readonly IReadOnlyDictionary<string, string> ShadCnDark = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FF020817",
            ["ProductChromeBrush"] = "#FF0F172A",
            ["ProductSurfaceBrush"] = "#FF111827",
            ["ProductSurfaceRaisedBrush"] = "#FF1E293B",
            ["ProductSurfaceHoverBrush"] = "#FF334155",
            ["ProductSurfacePressedBrush"] = "#FF020817",
            ["ProductButtonBrush"] = "#FF1E293B",
            ["ProductButtonHoverBrush"] = "#FF334155",
            ["ProductButtonPressedBrush"] = "#FF0F172A",
            ["ProductInputBrush"] = "#FF020817",
            ["ProductInputHoverBrush"] = "#FF0F172A",
            ["ProductBorderBrush"] = "#FF334155",
            ["ProductBorderStrongBrush"] = "#FF64748B",
            ["ProductSeparatorBrush"] = "#FF1E293B",
            ["ProductTextBrush"] = "#FFF8FAFC",
            ["ProductTextMutedBrush"] = "#FFCBD5E1",
            ["ProductTextSubtleBrush"] = "#FF94A3B8",
            ["ProductAccentBrush"] = "#FFF8FAFC",
            ["ProductAccentHoverBrush"] = "#FFE2E8F0",
            ["ProductAccentPressedBrush"] = "#FFCBD5E1",
            ["ProductAccentTextBrush"] = "#FF020817",
            ["ProductFocusBrush"] = "#FF38BDF8",
            ["ProductDangerBrush"] = "#FFEF4444",
            ["ProductDangerHoverBrush"] = "#FFF87171",
            ["ProductDangerPressedBrush"] = "#FFDC2626",
            ["ProductWarningBrush"] = "#FFF59E0B",
            ["ProductWarningSurfaceBrush"] = "#FF2B2111",
            ["ProductSuccessBrush"] = "#FF22C55E",
            ["ProductDisabledBrush"] = "#FF1E293B",
            ["ProductDisabledTextBrush"] = "#FF64748B",
            ["ProductOverlayBrush"] = "#CC000000"
        };

        private static readonly IReadOnlyDictionary<string, string> ShadCnLight = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FFFFFFFF",
            ["ProductChromeBrush"] = "#FFF8FAFC",
            ["ProductSurfaceBrush"] = "#FFF1F5F9",
            ["ProductSurfaceRaisedBrush"] = "#FFE2E8F0",
            ["ProductSurfaceHoverBrush"] = "#FFCBD5E1",
            ["ProductSurfacePressedBrush"] = "#FFE2E8F0",
            ["ProductButtonBrush"] = "#FFF8FAFC",
            ["ProductButtonHoverBrush"] = "#FFF1F5F9",
            ["ProductButtonPressedBrush"] = "#FFE2E8F0",
            ["ProductInputBrush"] = "#FFFFFFFF",
            ["ProductInputHoverBrush"] = "#FFF8FAFC",
            ["ProductBorderBrush"] = "#FFE2E8F0",
            ["ProductBorderStrongBrush"] = "#FF94A3B8",
            ["ProductSeparatorBrush"] = "#FFE2E8F0",
            ["ProductTextBrush"] = "#FF020817",
            ["ProductTextMutedBrush"] = "#FF334155",
            ["ProductTextSubtleBrush"] = "#FF64748B",
            ["ProductAccentBrush"] = "#FF0F172A",
            ["ProductAccentHoverBrush"] = "#FF1E293B",
            ["ProductAccentPressedBrush"] = "#FF020817",
            ["ProductAccentTextBrush"] = "#FFF8FAFC",
            ["ProductFocusBrush"] = "#FF0284C7",
            ["ProductDangerBrush"] = "#FFDC2626",
            ["ProductDangerHoverBrush"] = "#FFEF4444",
            ["ProductDangerPressedBrush"] = "#FFB91C1C",
            ["ProductWarningBrush"] = "#FFB45309",
            ["ProductWarningSurfaceBrush"] = "#FFFFF7ED",
            ["ProductSuccessBrush"] = "#FF15803D",
            ["ProductDisabledBrush"] = "#FFF1F5F9",
            ["ProductDisabledTextBrush"] = "#FF94A3B8",
            ["ProductOverlayBrush"] = "#CC000000"
        };

        private static readonly IReadOnlyDictionary<string, string> GitHubDark = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FF0D1117",
            ["ProductChromeBrush"] = "#FF161B22",
            ["ProductSurfaceBrush"] = "#FF161B22",
            ["ProductSurfaceRaisedBrush"] = "#FF21262D",
            ["ProductSurfaceHoverBrush"] = "#FF30363D",
            ["ProductSurfacePressedBrush"] = "#FF010409",
            ["ProductButtonBrush"] = "#FF21262D",
            ["ProductButtonHoverBrush"] = "#FF30363D",
            ["ProductButtonPressedBrush"] = "#FF161B22",
            ["ProductInputBrush"] = "#FF0D1117",
            ["ProductInputHoverBrush"] = "#FF161B22",
            ["ProductBorderBrush"] = "#FF30363D",
            ["ProductBorderStrongBrush"] = "#FF8B949E",
            ["ProductSeparatorBrush"] = "#FF21262D",
            ["ProductTextBrush"] = "#FFE6EDF3",
            ["ProductTextMutedBrush"] = "#FFC9D1D9",
            ["ProductTextSubtleBrush"] = "#FF8B949E",
            ["ProductAccentBrush"] = "#FF2F81F7",
            ["ProductAccentHoverBrush"] = "#FF58A6FF",
            ["ProductAccentPressedBrush"] = "#FF1F6FEB",
            ["ProductAccentTextBrush"] = "#FFFFFFFF",
            ["ProductFocusBrush"] = "#FF58A6FF",
            ["ProductDangerBrush"] = "#FFFF7B72",
            ["ProductDangerHoverBrush"] = "#FFFFA198",
            ["ProductDangerPressedBrush"] = "#FFDA3633",
            ["ProductWarningBrush"] = "#FFE3B341",
            ["ProductWarningSurfaceBrush"] = "#FF2B2111",
            ["ProductSuccessBrush"] = "#FF3FB950",
            ["ProductDisabledBrush"] = "#FF21262D",
            ["ProductDisabledTextBrush"] = "#FF6E7681",
            ["ProductOverlayBrush"] = "#CC000000"
        };

        private static readonly IReadOnlyDictionary<string, string> GitHubLight = new Dictionary<string, string>
        {
            ["ProductWindowBrush"] = "#FFFFFFFF",
            ["ProductChromeBrush"] = "#FFF6F8FA",
            ["ProductSurfaceBrush"] = "#FFF6F8FA",
            ["ProductSurfaceRaisedBrush"] = "#FFFFFFFF",
            ["ProductSurfaceHoverBrush"] = "#FFEAEFF4",
            ["ProductSurfacePressedBrush"] = "#FFD8DEE4",
            ["ProductButtonBrush"] = "#FFF6F8FA",
            ["ProductButtonHoverBrush"] = "#FFEAEFF4",
            ["ProductButtonPressedBrush"] = "#FFD8DEE4",
            ["ProductInputBrush"] = "#FFFFFFFF",
            ["ProductInputHoverBrush"] = "#FFF6F8FA",
            ["ProductBorderBrush"] = "#FFD0D7DE",
            ["ProductBorderStrongBrush"] = "#FF8C959F",
            ["ProductSeparatorBrush"] = "#FFD8DEE4",
            ["ProductTextBrush"] = "#FF24292F",
            ["ProductTextMutedBrush"] = "#FF57606A",
            ["ProductTextSubtleBrush"] = "#FF6E7781",
            ["ProductAccentBrush"] = "#FF0969DA",
            ["ProductAccentHoverBrush"] = "#FF218BFF",
            ["ProductAccentPressedBrush"] = "#FF0550AE",
            ["ProductAccentTextBrush"] = "#FFFFFFFF",
            ["ProductFocusBrush"] = "#FF0969DA",
            ["ProductDangerBrush"] = "#FFCF222E",
            ["ProductDangerHoverBrush"] = "#FFA40E26",
            ["ProductDangerPressedBrush"] = "#FF82071E",
            ["ProductWarningBrush"] = "#FF9A6700",
            ["ProductWarningSurfaceBrush"] = "#FFFFF8C5",
            ["ProductSuccessBrush"] = "#FF1A7F37",
            ["ProductDisabledBrush"] = "#FFF6F8FA",
            ["ProductDisabledTextBrush"] = "#FF8C959F",
            ["ProductOverlayBrush"] = "#CC000000"
        };
    }
}
