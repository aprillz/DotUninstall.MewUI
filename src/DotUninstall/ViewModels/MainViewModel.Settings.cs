using System.Text.Json;

using Octokit;

namespace DotUninstall.ViewModels;

partial class MainViewModel
{
    private const string UiSettingsFileName = "ui-settings.json";

    private static string GetUiSettingsPath() => Path.Combine(GetAppDataRootPath(), UiSettingsFileName);

    private static UiSettings LoadUiSettings()
    {
        try
        {
            var path = GetUiSettingsPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.UiSettings);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch { }
        return new UiSettings();
    }

    private static string NormalizeThemeMode(string? value)
    {
        if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return "Light";
        }

        if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return "Dark";
        }

        return "System";
    }

    private static int ThemeModeToIndex(string mode) => mode switch
    {
        "Light" => 1,
        "Dark" => 2,
        _ => 0
    };

    private void PersistUiSettings()
    {
        try
        {
            var settings = new UiSettings { ShowDotNetMetadata = ShowDotNetMetadata.Value, ThemeMode = ThemeMode.Value };
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.UiSettings);
            File.WriteAllText(GetUiSettingsPath(), json);
        }
        catch { }
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var stamp = GetUpdateStampPath();
            if (File.Exists(stamp))
            {
                try
                {
                    var txt = await File.ReadAllTextAsync(stamp);
                    if (DateTimeOffset.TryParse(txt, out var last) && (DateTimeOffset.UtcNow - last) < TimeSpan.FromDays(1))
                    {
                        return;
                    }
                }
                catch { }
            }

            var client = new GitHubClient(new ProductHeaderValue("dotuninstall"));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            Release? latestRelease = null;
            try { latestRelease = await client.Repository.Release.GetLatest("lextudio", "dotuninstall").WaitAsync(cts.Token); }
            catch { return; }
            var tag = latestRelease?.TagName;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            LatestReleaseTag.Value = tag;
            var current = GetCurrentVersion();
            if (TryParseVersion(tag, out var latest) && TryParseVersion(current, out var currentV))
            {
                if (latest > currentV)
                {
                    HasUpdate.Value = true;
                    UpdateMessage.Value = $"A newer release {latest} is available (current {currentV}).";
                }
            }
            try { await File.WriteAllTextAsync(stamp, DateTimeOffset.UtcNow.ToString("o")); } catch { }
        }
        catch { }
    }

    public async Task ForceCheckForUpdatesAsync()
    {
        try
        {
            var stamp = GetUpdateStampPath();
            if (File.Exists(stamp))
            {
                try { File.Delete(stamp); } catch { }
            }

            await CheckForUpdatesAsync();
            if (!HasUpdate.Value)
            {
                UpdateMessage.Value = $"You are running the latest version ({GetCurrentVersion()}).";
            }
        }
        catch { }
    }

    private sealed class UiSettings
    {
        public bool ShowDotNetMetadata { get; set; }

        public string ThemeMode { get; set; } = "System";
    }
}
