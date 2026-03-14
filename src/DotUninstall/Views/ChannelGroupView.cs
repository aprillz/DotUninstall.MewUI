using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.Helpers;
using DotUninstall.Models;
using DotUninstall.ViewModels;

namespace DotUninstall;

public class ChannelGroupView : UserControl
{
    private readonly MainViewModel _vm;
    private readonly Label _channelLabel;
    private readonly WrapPanel _channelBadges;
    private readonly ItemsControl _entriesControl;

    public ChannelGroupView(MainViewModel vm)
    {
        _vm = vm;
        _channelLabel = new Label().FontSize(20).Bold();
        _channelBadges = new WrapPanel().Margin(0, 6, 0, 4).Spacing(8);

        _entriesControl = new ItemsControl()
            .CornerRadius(0)
            .BorderThickness(0)
            .Background(Color.Transparent)
            .StackPresenter()
            .ItemTemplate(new DelegateTemplate<DotnetInstallEntry>(
                build: _ => new InstallEntryView(vm),
                bind: (view, entry, _, _) => ((InstallEntryView)view).Update(entry)
            ));

        Content = new Expander()
            .Margin(8, 0, 8, 12)
            .Padding(8, 4)
            .IsExpanded(true)
            .Header(
                new StackPanel()
                    .Horizontal()
                    .Spacing(8)
                    .CenterVertical()
                    .Children(
                        new Label().Text(".NET").FontSize(20).Bold(),
                        _channelLabel
                    )
            )
            .Content(
                new StackPanel()
                    .Vertical()
                    .Margin(0, 4, 4, 0)
                    .Children(_channelBadges, _entriesControl)
            );
    }

    public void Update(ChannelGroup group)
    {
        // Channel display: collapse "X.0" to "X"
        var channelDisplay = group.Channel;
        if (channelDisplay.EndsWith(".0"))
            channelDisplay = channelDisplay[..^2];
        _channelLabel.Text = channelDisplay;

        // Channel badges
        _channelBadges.Clear();

        if (group.ReleaseType != null)
            _channelBadges.Add(BadgeBuilder.TwoPartBadge("Type", group.ReleaseType,
                () => ThemeColors.BadgeReleaseLabel, () => ThemeColors.BadgeReleaseValue,
                infoUrl: group.ReleaseTypeInfoUrl));

        if (group.SupportPhase != null)
            _channelBadges.Add(BadgeBuilder.TwoPartBadge("Support", group.SupportPhase,
                () => ThemeColors.BadgeSupportLabel, () => ThemeColors.BadgeSupportValue,
                infoUrl: group.SupportPhaseInfoUrl));

        if (group.EolDateValue != null)
            _channelBadges.Add(BadgeBuilder.TwoPartBadge("EOL", group.EolDateValue,
                () => ThemeColors.BadgeEolLabel, () => ThemeColors.BadgeEolValue,
                infoUrl: group.EolInfoUrl));

        if (group.MauiEolDateValue != null)
            _channelBadges.Add(BadgeBuilder.TwoPartBadge("MAUI EOL", group.MauiEolDateValue,
                () => ThemeColors.BadgeMauiEolLabel, () => ThemeColors.BadgeMauiEolValue,
                infoUrl: group.MauiEolInfoUrl));

        if (group.ShowLatestRelevantMissingNormal)
            _channelBadges.Add(BadgeBuilder.TwoPartBadge("Latest", group.LatestRelevantVersion!,
                () => ThemeColors.BadgeLatestLabel, () => ThemeColors.BadgeLatestValue,
                infoUrl: group.ChannelDownloadUrl));

        if (group.ShowLatestRelevantMissingSecurity)
            _channelBadges.Add(BadgeBuilder.TwoPartBadge("Latest", group.LatestRelevantVersion!,
                () => ThemeColors.LatestSecurityLabel, () => ThemeColors.LatestSecurityValue,
                infoUrl: group.ChannelDownloadUrl));

        // Entries
        _entriesControl.ItemsSource = ItemsView.Create(
            group.Items,
            textSelector: e => e.Version);
    }
}
