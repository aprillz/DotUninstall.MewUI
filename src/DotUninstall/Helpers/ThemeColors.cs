using Aprillz.MewUI;

namespace DotUninstall.Helpers;

public static class ThemeColors
{
    private static bool IsDark => Application.IsRunning ? Application.Current.Theme.IsDark : false;

    // Badge colors (Light, Dark from Colors.xaml)
    public static Color BadgeReleaseLabel => !IsDark ? Color.FromRgb(0x0B, 0x4F, 0x73) : Color.FromRgb(0x1A, 0x6E, 0x98);
    public static Color BadgeReleaseValue => !IsDark ? Color.FromRgb(0x13, 0x90, 0xD4) : Color.FromRgb(0x2D, 0x96, 0xD2);
    public static Color BadgeSupportLabel => !IsDark ? Color.FromRgb(0x39, 0x41, 0x48) : Color.FromRgb(0x55, 0x61, 0x6B);
    public static Color BadgeSupportValue => !IsDark ? Color.FromRgb(0x56, 0x61, 0x6B) : Color.FromRgb(0x72, 0x80, 0x8C);
    public static Color BadgeEolLabel => !IsDark ? Color.FromRgb(0x7B, 0x1F, 0x2B) : Color.FromRgb(0x9E, 0x36, 0x44);
    public static Color BadgeEolValue => !IsDark ? Color.FromRgb(0xC2, 0x3B, 0x4E) : Color.FromRgb(0xC8, 0x57, 0x68);
    public static Color BadgeMauiEolLabel => !IsDark ? Color.FromRgb(0x4B, 0x2D, 0x6B) : Color.FromRgb(0x6F, 0x4A, 0x9A);
    public static Color BadgeMauiEolValue => !IsDark ? Color.FromRgb(0x6E, 0x43, 0xA6) : Color.FromRgb(0x8D, 0x67, 0xBE);
    public static Color BadgeLatestLabel => !IsDark ? Color.FromRgb(0x0E, 0x5A, 0x3C) : Color.FromRgb(0x19, 0x80, 0x5A);
    public static Color BadgeLatestValue => !IsDark ? Color.FromRgb(0x1D, 0x8A, 0x5B) : Color.FromRgb(0x23, 0xA4, 0x70);
    public static Color BadgeStageLabel => !IsDark ? Color.FromRgb(0x28, 0x33, 0x42) : Color.FromRgb(0x3F, 0x4E, 0x62);
    public static Color BadgeStageValue => !IsDark ? Color.FromRgb(0x3C, 0x4C, 0x60) : Color.FromRgb(0x5C, 0x6F, 0x88);
    public static Color BadgeReleaseDateLabel => !IsDark ? Color.FromRgb(0x4A, 0x2E, 0x17) : Color.FromRgb(0x71, 0x52, 0x32);
    public static Color BadgeReleaseDateValue => !IsDark ? Color.FromRgb(0x7B, 0x4A, 0x25) : Color.FromRgb(0x97, 0x70, 0x4A);
    public static Color BadgeSecurityLabel => !IsDark ? Color.FromRgb(0x6B, 0x1F, 0x29) : Color.FromRgb(0x8B, 0x33, 0x41);
    public static Color BadgeSecurityValue => !IsDark ? Color.FromRgb(0xA1, 0x29, 0x3D) : Color.FromRgb(0xBF, 0x4A, 0x63);
    public static Color BadgeArchLabel => !IsDark ? Color.FromRgb(0x20, 0x32, 0x3D) : Color.FromRgb(0x32, 0x51, 0x67);
    public static Color BadgeArchValue => !IsDark ? Color.FromRgb(0x32, 0x50, 0x5F) : Color.FromRgb(0x49, 0x73, 0x92);
    public static Color BadgeSubTypeLabel => !IsDark ? Color.FromRgb(0x2B, 0x2F, 0x40) : Color.FromRgb(0x44, 0x4A, 0x62);
    public static Color BadgeSubTypeValue => !IsDark ? Color.FromRgb(0x41, 0x46, 0x5A) : Color.FromRgb(0x61, 0x69, 0x8A);
    public static Color BadgeStateLabel => !IsDark ? Color.FromRgb(0x0F, 0x4C, 0x37) : Color.FromRgb(0x22, 0x74, 0x58);
    public static Color BadgeStateValue => !IsDark ? Color.FromRgb(0x17, 0x75, 0x54) : Color.FromRgb(0x2D, 0x9C, 0x75);
    public static Color BadgeReasonBg => !IsDark ? Color.FromRgb(0x4E, 0x59, 0x63) : Color.FromRgb(0x5D, 0x68, 0x74);

    // Latest badge - security variant (theme-independent)
    public static readonly Color LatestSecurityLabel = Color.FromRgb(0x7F, 0x0E, 0x12);
    public static readonly Color LatestSecurityValue = Color.FromRgb(0xDA, 0x2A, 0x1B);

    // Security colors (theme-independent)
    public static readonly Color SecurityAlert = Color.FromRgb(0xB7, 0x1C, 0x1C);
    public static readonly Color SecurityWarning = Color.FromRgb(0xC1, 0x56, 0x00);

    // Banner colors (Light, Dark)
    public static Color BannerSnapshotBg => !IsDark ? Color.FromRgb(0xF2, 0xF2, 0xF2) : Color.FromRgb(0x2A, 0x2A, 0x2A);
    public static Color BannerSnapshotBorder => !IsDark ? Color.FromRgb(0xCC, 0xCC, 0xCC) : Color.FromRgb(0x4A, 0x4A, 0x4A);
    public static Color BannerSnapshotFg => !IsDark ? Color.FromRgb(0x44, 0x44, 0x44) : Color.FromRgb(0xE2, 0xE2, 0xE2);
    public static Color BannerCachedBg => !IsDark ? Color.FromRgb(0xEE, 0xF7, 0xFF) : Color.FromRgb(0x0F, 0x26, 0x35);
    public static Color BannerCachedBorder => !IsDark ? Color.FromRgb(0xB3, 0xDA, 0xF7) : Color.FromRgb(0x2D, 0x6F, 0x98);
    public static Color BannerCachedFg => !IsDark ? Color.FromRgb(0x0A, 0x46, 0x6E) : Color.FromRgb(0xBF, 0xE4, 0xFF);
    public static Color BannerUpdateBg => !IsDark ? Color.FromRgb(0xD1, 0xF0, 0xD1) : Color.FromRgb(0x11, 0x2A, 0x1B);
    public static Color BannerUpdateBorder => !IsDark ? Color.FromRgb(0x7B, 0xC4, 0x7B) : Color.FromRgb(0x2E, 0x85, 0x5B);
    public static Color BannerUpdateFg => !IsDark ? Color.FromRgb(0x0B, 0x3D, 0x0B) : Color.FromRgb(0xC7, 0xF2, 0xD7);
    public static Color BannerElevationBg => !IsDark ? Color.FromRgb(0xFF, 0xF3, 0xCD) : Color.FromRgb(0x39, 0x2A, 0x10);
    public static Color BannerElevationBorder => !IsDark ? Color.FromRgb(0xF8, 0xD4, 0x86) : Color.FromRgb(0x9A, 0x78, 0x34);
    public static Color BannerElevationFg => !IsDark ? Color.FromRgb(0x66, 0x4D, 0x03) : Color.FromRgb(0xF3, 0xDE, 0xAF);
    public static Color BannerNonElevatedBg => !IsDark ? Color.FromRgb(0xE8, 0xF4, 0xFF) : Color.FromRgb(0x10, 0x27, 0x36);
    public static Color BannerNonElevatedBorder => !IsDark ? Color.FromRgb(0xB3, 0xDA, 0xF7) : Color.FromRgb(0x2E, 0x7E, 0xAC);
    public static Color BannerNonElevatedFg => !IsDark ? Color.FromRgb(0x0A, 0x46, 0x6E) : Color.FromRgb(0xC5, 0xE9, 0xFF);

    // Lifecycle (theme-independent)
    public static readonly Color LifecycleEol = Color.FromRgb(0x60, 0x1F, 0x1F);
    public static readonly Color LifecycleExpiring = Color.FromRgb(0x7F, 0x5A, 0x15);
    public static readonly Color LifecycleSupported = Color.FromRgb(0x1F, 0x3A, 0x52);

    public static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);
}
