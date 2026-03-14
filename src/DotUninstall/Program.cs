using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall;
using DotUninstall.ViewModels;

#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Aprillz.MewUI.HotReload.MewUiMetadataUpdateHandler))]
#endif


if (OperatingSystem.IsWindows())
{
    Win32Platform.Register();
    Direct2DBackend.Register();
}
else if (OperatingSystem.IsMacOS()) 
{
    MacOSPlatform.Register();
    MewVGMacOSBackend.Register();
}

var vm = new MainViewModel();

var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
var icon = File.Exists(iconPath) ? IconSource.FromFile(iconPath) : null;

Application.Create()
    .UseAccent(Accent.Blue)
    .BuildMainWindow(() => new Window()
        .Resizable(1100, 700)
        .Icon(icon)
        .OnBuild(x => x
            .OnLoaded(() =>
            {
                vm.ApplyCurrentTheme();
                vm.WindowService.SetOwner(x);
                _ = vm.RefreshAsync();
                _ = Task.Run(vm.CheckForUpdatesAsync);
                vm.DetectElevation();
            })
            .Title("DotUninstall")
            .Padding(8)
            .Content(new MainView(vm))
        ))
    .Run();
