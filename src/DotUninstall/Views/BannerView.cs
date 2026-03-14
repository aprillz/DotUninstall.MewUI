using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.Helpers;
using DotUninstall.ViewModels;

namespace DotUninstall;

public class BannerView : UserControl
{
    private readonly MainViewModel _vm;

    public BannerView(MainViewModel vm)
    {
        _vm = vm;
        Build();
    }

    protected override Element? OnBuild() =>
        new StackPanel()
            .Vertical()
            .Margin(0, 4, 0, 4)
            .Spacing(4)
            .Children(
                // Snapshot banner
                MakeBanner(
                    _vm.IsUsingSnapshot,
                    () => ThemeColors.BannerSnapshotBg, () => ThemeColors.BannerSnapshotBorder, () => ThemeColors.BannerSnapshotFg,
                    "Using embedded metadata snapshot (offline)."),

                // Cached live banner
                MakeBanner(
                    _vm.IsUsingCachedLive,
                    () => ThemeColors.BannerCachedBg, () => ThemeColors.BannerCachedBorder, () => ThemeColors.BannerCachedFg,
                    "On cached live metadata."),

                // Update available banner
                new Border()
                    .Padding(8)
                    .CornerRadius(6)
                    .WithTheme((t, c) => c
                        .Background(ThemeColors.BannerUpdateBg)
                        .BorderBrush(ThemeColors.BannerUpdateBorder))
                    .BorderThickness(1)
                    .BindIsVisible(_vm.HasUpdate)
                    .Child(
                        new StackPanel()
                            .Horizontal()
                            .Spacing(12)
                            .CenterVertical()
                            .Children(
                                new Label()
                                    .BindText(_vm.UpdateMessage, m => $"Update available: {m ?? ""}")
                                    .WithTheme((t, c) => c.Foreground(ThemeColors.BannerUpdateFg))
                                    .TextWrapping(TextWrapping.NoWrap),
                                new Button()
                                    .Content("Open Release Page")
                                    .OnClick(() => UrlLauncher.Open(_vm.GetReleasePageUrl()))
                            )
                    ),

                // Elevation warning (macOS)
                new Border()
                    .Padding(8)
                    .CornerRadius(6)
                    .WithTheme((t, c) => c
                        .Background(ThemeColors.BannerElevationBg)
                        .BorderBrush(ThemeColors.BannerElevationBorder))
                    .BorderThickness(1)
                    .BindIsVisible(_vm.ShowElevationWarning)
                    .Child(
                        new StackPanel()
                            .Horizontal()
                            .Spacing(12)
                            .CenterVertical()
                            .Children(
                                new Label()
                                    .BindText(_vm.OriginalUser, u => $"Running with elevated privileges (root). Original user: {u ?? "unknown"}. Proceed with caution.")
                                    .WithTheme((t, c) => c.Foreground(ThemeColors.BannerElevationFg))
                                    .TextWrapping(TextWrapping.Wrap),
                                new Button()
                                    .Content("Dismiss")
                                    .OnClick(() => _vm.DismissElevationWarning())
                            )
                    ),

                // Non-elevated offer (macOS)
                new Border()
                    .Padding(8)
                    .CornerRadius(6)
                    .WithTheme((t, c) => c
                        .Background(ThemeColors.BannerNonElevatedBg)
                        .BorderBrush(ThemeColors.BannerNonElevatedBorder))
                    .BorderThickness(1)
                    .BindIsVisible(_vm.ShowElevationOffer)
                    .Child(
                        new StackPanel()
                            .Horizontal()
                            .Spacing(12)
                            .CenterVertical()
                            .Children(
                                new Label()
                                    .Text("Some operations may require administrator rights.")
                                    .WithTheme((t, c) => c.Foreground(ThemeColors.BannerNonElevatedFg))
                                    .TextWrapping(TextWrapping.Wrap),
                                new Button()
                                    .Content("Dismiss")
                                    .OnClick(() => _vm.DismissElevationOffer())
                            )
                    )
            );

    private static FrameworkElement MakeBanner(ObservableValue<bool> visible, Func<Color> bg, Func<Color> bd, Func<Color> fg, string text) =>
        new Border()
            .CornerRadius(6)
            .Padding(8)
            .WithTheme((t, c) => c
                .Background(bg())
                .BorderBrush(bd()))
            .BorderThickness(1)
            .BindIsVisible(visible)
            .Child(
                new Label()
                    .Text(text)
                    .FontSize(12)
                    .WithTheme((t, c) => c.Foreground(fg()))
            );
}
