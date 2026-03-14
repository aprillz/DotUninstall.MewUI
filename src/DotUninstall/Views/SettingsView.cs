using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DotUninstall.ViewModels;

namespace DotUninstall;

public class SettingsView : UserControl
{
    private readonly MainViewModel _vm;
    public SettingsView(MainViewModel vm)
    {
        _vm = vm;
        Build();
    }

    protected override Element? OnBuild() =>
        new StackPanel()
            .Vertical()
            .Padding(12)
            .Spacing(16)
            .Children(
                new Label()
                    .Text("Application")
                    .FontSize(14)
                    .Bold(),

                new StackPanel()
                    .Horizontal()
                    .Spacing(8)
                    .CenterVertical()
                    .Children(
                        new Label().Text("Version:").Bold(),
                        new Label().Text(_vm.AppVersion)
                    ),

                new ToggleSwitch()
                    .Text("Use .NET metadata extras")
                    .BindIsChecked(_vm.ShowDotNetMetadata),

                new StackPanel()
                    .Vertical()
                    .Spacing(6)
                    .Children(
                        new Label().Text("Theme").Bold(),
                        new ComboBox()
                            .Left()
                            .Width(120)
                            .Items("System", "Light", "Dark")
                            .BindSelectedIndex(_vm.ThemeModeIndex),
                        new Label()
                            .Text("Theme changes apply immediately.")
                            .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText))
                    ),

                new Label()
                    .Text("Changes to metadata setting apply immediately and refresh installed items.")
                    .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText)),

                new Button()
                    .Content("Check for Updates Now")
                    .Left()
                    .OnClick(() => _ = _vm.ForceCheckForUpdatesAsync()),

                new Button()
                    .Content("Clear Metadata Cache")
                    .Left()
                    .OnClick(() => _ = _vm.ClearCacheAsync()),

                new Label()
                    .BindText(_vm.UpdateMessage, m => m ?? "")
                    .TextWrapping(TextWrapping.Wrap),

                new Label()
                    .Text("Manual check bypasses the once-per-day automatic delay.")
                    .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText))
    );
}
