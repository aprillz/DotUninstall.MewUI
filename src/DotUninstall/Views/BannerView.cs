using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.Helpers;
using DotUninstall.ViewModels;

namespace DotUninstall;

public class BannerView : UserControl
{
    public BannerView(MainViewModel vm) =>
        Content = new StackPanel()
            .Vertical()
            .Margin(0, 4, 0, 4)
            .Spacing(4)
            .Children(
                // Snapshot banner
                MakeBanner(
                    vm.IsUsingSnapshot,
                    () => ThemeColors.BannerSnapshotBg, () => ThemeColors.BannerSnapshotBorder, () => ThemeColors.BannerSnapshotFg,
                    "Using embedded metadata snapshot (offline)."),

                // Cached live banner
                MakeBanner(
                    vm.IsUsingCachedLive,
                    () => ThemeColors.BannerCachedBg, () => ThemeColors.BannerCachedBorder, () => ThemeColors.BannerCachedFg,
                    "On cached live metadata."),

                // Update available banner
                MakeBanner(
                    vm.HasUpdate,
                    () => ThemeColors.BannerUpdateBg, () => ThemeColors.BannerUpdateBorder, () => ThemeColors.BannerUpdateFg,
                    new Label()
                        .BindText(vm.UpdateMessage, m => $"Update available: {m ?? ""}")
                        .WithTheme((t, c) => c.Foreground(ThemeColors.BannerUpdateFg)),
                    new Button()
                        .Content("Open Release Page")
                        .OnClick(() => UrlLauncher.Open(vm.GetReleasePageUrl()))),

                // Elevation warning (macOS)
                MakeBanner(
                    vm.ShowElevationWarning,
                    () => ThemeColors.BannerElevationBg, () => ThemeColors.BannerElevationBorder, () => ThemeColors.BannerElevationFg,
                    new Label()
                        .BindText(vm.OriginalUser, u => $"Running with elevated privileges (root). Original user: {u ?? "unknown"}. Proceed with caution.")
                        .WithTheme((t, c) => c.Foreground(ThemeColors.BannerElevationFg))
                        .TextWrapping(TextWrapping.Wrap),
                    new Button()
                        .Content("Dismiss")
                        .OnClick(() => vm.DismissElevationWarning())),

                // Non-elevated offer (macOS)
                MakeBanner(
                    vm.ShowElevationOffer,
                    () => ThemeColors.BannerNonElevatedBg, () => ThemeColors.BannerNonElevatedBorder, () => ThemeColors.BannerNonElevatedFg,
                    "Some operations may require administrator rights.",
                    new Button()
                        .Content("Dismiss")
                        .OnClick(() => vm.DismissElevationOffer()))
            );

    private static FrameworkElement MakeBanner(ObservableValue<bool> visible, Func<Color> bg, Func<Color> bd, Func<Color> fg, string text, Button? button = null) =>
        MakeBanner(visible, bg, bd, fg,
            new Label()
                .Text(text)
                .WithTheme((t, c) => c.Foreground(fg()))
                .TextWrapping(TextWrapping.Wrap),
            button);

    private static FrameworkElement MakeBanner(ObservableValue<bool> visible, Func<Color> bg, Func<Color> bd, Func<Color> fg, Label label, Button? button = null) =>
        new Border()
            .Padding(8)
            .CornerRadius(6)
            .WithTheme((t, c) => c.Background(bg()).BorderBrush(bd()))
            .BorderThickness(1)
            .BindIsVisible(visible)
            .Child(button is null ? label : new StackPanel()
                .Horizontal()
                .Spacing(12)
                .CenterVertical()
                .Children(label, button));
}
