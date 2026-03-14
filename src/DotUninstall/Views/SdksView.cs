using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.Models;
using DotUninstall.ViewModels;

namespace DotUninstall;

public class SdksView : UserControl
{
    private readonly MainViewModel _vm;

    public SdksView(MainViewModel vm)
    {
        _vm = vm;
        Build();
    }

    protected override Element? OnBuild() =>
        new DockPanel()
            .Children(
                new Label()
                    .Margin(12, 0, 12, 8)
                    .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText))
                    .BindText(_vm.SdkCount, c => $"Count: {c}").DockTop(),
                new ItemsControl()
                    .CornerRadius(0)
                    .BorderThickness(0)
                    .Background(Color.Transparent)
                    .StackPresenter()
                    .ItemsSource(ItemsView.Create(_vm.GroupedSdkItems, x => x.Channel))
                    .ItemTemplate(new DelegateTemplate<ChannelGroup>(
                        build: _ => new ChannelGroupView(_vm),
                        bind: (view, group, _, _) => ((ChannelGroupView)view).Update(group)
                    ))
            );
}
