using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.ViewModels;

namespace DotUninstall;

public class MainView : UserControl
{
    public MainView(MainViewModel vm)
    {
        Content = new Grid()
            .Rows("Auto,*")
            .Children(
                new BannerView(vm),

                new TabControl()
                    .Row(1)
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
}
