using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.ViewModels;

namespace DotUninstall;

public class MainView : UserControl
{
    public MainView(MainViewModel vm) =>
        Content = new DockPanel()
            .Spacing(8)
            .Children(
                new BannerView(vm)
                    .DockTop(),

                new Label()
                    .DockBottom()
                    .BindText(vm.StatusMessage, x => x ?? string.Empty),

                new TabControl()
                    .AutoVerticalScroll()
                    .TabItems(
                        new TabItem()
                            .Header("Runtimes")
                            .Content(new RuntimesView(vm)),
                        new TabItem()
                            .Header("SDKs")
                            .Content(new SdksView(vm)),
                        new TabItem()
                            .Header("Settings")
                            .Content(new SettingsView(vm))
                    )
            );
}
