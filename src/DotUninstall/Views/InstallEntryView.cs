using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.Helpers;
using DotUninstall.Models;
using DotUninstall.ViewModels;

namespace DotUninstall;

public class InstallEntryView : UserControl
{
    private readonly MainViewModel _vm;
    private Label _versionLabel = null!;
    private Label _displayNameLabel = null!;
    private StackPanel _badges = null!;
    private Button _uninstallButton = null!;

    public InstallEntryView(MainViewModel vm)
    {
        _vm = vm;
        Build();
    }

    protected override Element? OnBuild() =>
        new Grid()
            .Columns("*,Auto")
            .Children(
                new StackPanel()
                    .Vertical()
                    .Spacing(4)
                    .Padding(4)
                    .MinHeight(48)
                    .Margin(4)
                    .Children(
                        new StackPanel()
                            .Horizontal()
                            .Spacing(6)
                            .CenterVertical()
                            .Children(
                                new Label()
                                    .Ref(out _versionLabel)
                                    .Bold()
                                    .FontSize(18),
                                new Label()
                                    .Ref(out _displayNameLabel)
                                    .WithTheme((t, c) => t.Palette.WindowText.WithAlpha(150))
                                    .FontSize(10)
                                    .MaxWidth(360)
                            ),
                        new StackPanel().Horizontal().Spacing(6).CenterVertical().Ref(out _badges)
                    ),
                new Button()
                    .Ref(out _uninstallButton)
                    .Content("Uninstall")
                    .CenterVertical()
                    .Column(1)
                    .IsVisible(false)
            );

    public void Update(DotnetInstallEntry entry)
    {
        _versionLabel.Text = entry.Version;
        _displayNameLabel.Text = entry.DisplayName ?? "";

        _badges.Clear();

        if (entry.ReleaseDateValue != null)
            _badges.Add(BadgeBuilder.TwoPartBadge("Release", entry.ReleaseDateValue,
                () => ThemeColors.BadgeReleaseDateLabel, () => ThemeColors.BadgeReleaseDateValue,
                infoUrl: entry.ReleaseNotesUrl));

        if (entry.StageDisplay != null)
            _badges.Add(BadgeBuilder.TwoPartBadge("Stage", entry.StageDisplay,
                () => ThemeColors.BadgeStageLabel, () => ThemeColors.BadgeStageValue));

        if (_vm.IsMetadataEnabledInSession.Value && entry.SecurityStatus != SecurityStatus.None)
        {
            var secValue = entry.SecurityStatus switch
            {
                SecurityStatus.SecurityPatch or SecurityStatus.Patched => "Yes",
                _ => "No"
            };
            _badges.Add(BadgeBuilder.TwoPartBadge("Secure", secValue,
                () => ThemeColors.BadgeSecurityLabel, () => ThemeColors.BadgeSecurityValue));
        }

        if (entry.IsOutOfSupport)
            _badges.Add(BadgeBuilder.TwoPartBadge("EOL", "Build",
                () => ThemeColors.BadgeEolLabel, () => ThemeColors.BadgeEolValue));

        _badges.Add(BadgeBuilder.TwoPartBadge("Arch", entry.Architecture,
            () => ThemeColors.BadgeArchLabel, () => ThemeColors.BadgeArchValue));

        if (entry.SubType != null)
            _badges.Add(BadgeBuilder.TwoPartBadge("Type", entry.SubType,
                () => ThemeColors.BadgeSubTypeLabel, () => ThemeColors.BadgeSubTypeValue));

        if (!string.IsNullOrWhiteSpace(entry.Reason))
            _badges.Add(BadgeBuilder.SingleBadge(entry.Reason, () => ThemeColors.BadgeReasonBg));

        if (entry.CanUninstall)
            _badges.Add(BadgeBuilder.TwoPartBadge("State", "Removable",
                () => ThemeColors.BadgeStateLabel, () => ThemeColors.BadgeStateValue));

        _uninstallButton.IsVisible = entry.CanUninstall;
    }
}
