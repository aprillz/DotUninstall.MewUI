using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Aprillz.MewUI;

using DotUninstall.Models;
using DotUninstall.Services;

using NuGet.Versioning;

using Octokit;

using Application = Aprillz.MewUI.Application;

namespace DotUninstall.ViewModels;

public partial class MainViewModel
{
    public WindowHostService WindowService { get; } = new();

    // Observable properties
    public ObservableValue<bool> IsElevated { get; } = new(false);

    public ObservableValue<string?> OriginalUser { get; } = new(null);

    public ObservableValue<bool> ShowElevationWarning { get; } = new(false);

    public ObservableValue<bool> ShowElevationOffer { get; } = new(false);

    public ObservableValue<bool> IsLoading { get; } = new(false);

    public ObservableValue<string?> StatusMessage { get; } = new(null);

    public ObservableValue<string?> ErrorMessage { get; } = new(null);

    public ObservableValue<bool> HasUpdate { get; } = new(false);

    public ObservableValue<string?> UpdateMessage { get; } = new(null);

    public ObservableValue<string?> LatestReleaseTag { get; } = new(null);

    public ObservableValue<bool> IsUsingSnapshot { get; } = new(false);

    public ObservableValue<bool> IsUsingCachedLive { get; } = new(false);

    public ObservableValue<bool> ShowDotNetMetadata { get; } = new(false);

    public ObservableValue<string> ThemeMode { get; } = new("System");

    public ObservableValue<int> ThemeModeIndex { get; } = new(0);

    public ObservableValue<bool> IsMetadataEnabledInSession { get; } = new(false);

    public ObservableValue<int> SdkCount { get; } = new(0);

    public ObservableValue<int> RuntimeCount { get; } = new(0);

    public ObservableCollection<ChannelGroup> GroupedSdkItems { get; } = new();

    public ObservableCollection<ChannelGroup> GroupedRuntimeItems { get; } = new();

    private List<DotnetInstallEntry> _sdkItems = new();
    private List<DotnetInstallEntry> _runtimeItems = new();

    public string AppVersion => GetCurrentVersion();

    private readonly SemaphoreSlim _metadataModeChangeGate = new(1, 1);

    public MainViewModel()
    {
        var settings = LoadUiSettings();
        ShowDotNetMetadata.Value = settings.ShowDotNetMetadata;
        ThemeMode.Value = NormalizeThemeMode(settings.ThemeMode);
        ThemeModeIndex.Value = ThemeModeToIndex(ThemeMode.Value);
        IsMetadataEnabledInSession.Value = ShowDotNetMetadata.Value;

        ShowDotNetMetadata.Changed += () =>
        {
            PersistUiSettings();
            _ = ApplyMetadataModeChangeAsync(ShowDotNetMetadata.Value);
        };

        ThemeModeIndex.Changed += () =>
        {
            var mode = ThemeModeIndex.Value switch
            {
                1 => "Light",
                2 => "Dark",
                _ => "System"
            };
            if (ThemeMode.Value != mode)
                ThemeMode.Value = mode;
        };

        ThemeMode.Changed += () =>
        {
            var normalized = NormalizeThemeMode(ThemeMode.Value);
            if (normalized != ThemeMode.Value)
            {
                ThemeMode.Value = normalized;
                return;
            }
            var idx = ThemeModeToIndex(normalized);
            if (ThemeModeIndex.Value != idx)
                ThemeModeIndex.Value = idx;
            PersistUiSettings();
            ApplyTheme(ThemeMode.Value);
        };
    }

    private static void ApplyTheme(string mode)
    {
        var variant = mode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.System
        };
        Application.Current.SetTheme(variant);
    }

    public void ApplyCurrentTheme()
        => ApplyTheme(ThemeMode.Value);

    // --- Refresh / Listing ---

    public async Task RefreshAsync()
    {
        if (IsLoading.Value) return;

        IsLoading.Value = true;
        ErrorMessage.Value = null;
        StatusMessage.Value = "Refreshing...";
        _sdkItems.Clear();
        _runtimeItems.Clear();
        try
        {
            await ListFromCliAsync();
            BuildGroups();
            StatusMessage.Value = $"Loaded {_sdkItems.Count + _runtimeItems.Count} entries.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = ex.Message;
            StatusMessage.Value = "Failed.";
        }
        finally
        {
            IsLoading.Value = false;
            SdkCount.Value = _sdkItems.Count;
            RuntimeCount.Value = _runtimeItems.Count;
        }
    }

    private async Task ListFromCliAsync()
    {
        var parsed = new List<DotnetInstallEntry>();

        // Discover SDKs
        try
        {
            var (exitCode, stdout, _) = await RunProcessAsync("dotnet", "--list-sdks");
            if (exitCode == 0)
            {
                foreach (var line in SplitLines(stdout))
                {
                    // Format: "9.0.100 [C:\Program Files\dotnet\sdk]"
                    var match = Regex.Match(line, @"^(\S+)\s+\[(.+)\]$");
                    if (match.Success)
                    {
                        var version = match.Groups[1].Value;
                        var path = match.Groups[2].Value;
                        var arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
                        parsed.Add(new DotnetInstallEntry("sdk", "sdk", version, arch, false, null)
                        {
                            DisplayName = $".NET SDK {version}"
                        });
                    }
                }
            }
        }
        catch { }

        // Discover Runtimes
        try
        {
            var (exitCode, stdout, _) = await RunProcessAsync("dotnet", "--list-runtimes");
            if (exitCode == 0)
            {
                foreach (var line in SplitLines(stdout))
                {
                    // Format: "Microsoft.NETCore.App 9.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]"
                    var match = Regex.Match(line, @"^(\S+)\s+(\S+)\s+\[(.+)\]$");
                    if (match.Success)
                    {
                        var runtimeName = match.Groups[1].Value;
                        var version = match.Groups[2].Value;
                        var arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
                        var subType = runtimeName switch
                        {
                            "Microsoft.AspNetCore.App" => "aspnet-runtime",
                            "Microsoft.WindowsDesktop.App" => "desktop-runtime",
                            _ => "runtime"
                        };
                        parsed.Add(new DotnetInstallEntry("runtime", "runtime", version, arch, false, null)
                        {
                            DisplayName = $"{runtimeName} {version}",
                            SubType = subType
                        });
                    }
                }
            }
        }
        catch { }

        // Always load the releases-index for basic channel info (Type, Support, EOL)
        var neededChannels = new HashSet<string>(parsed.Select(p => DeriveChannel(p.Version)), StringComparer.OrdinalIgnoreCase);
        if (neededChannels.Count > 0)
        {
            try
            {
                await EnsureReleasesIndexAsync();
            }
            catch { }
        }

        if (!IsMetadataEnabledInSession.Value)
        {
            IsUsingSnapshot.Value = false;
            IsUsingCachedLive.Value = false;
        }

        // Detailed per-channel metadata (security, release dates) only when enabled
        if (IsMetadataEnabledInSession.Value)
        {
            try
            {
                if (neededChannels.Count > 0)
                    await EnsureMetadataAsync(neededChannels);
            }
            catch (Exception ex)
            {
                StatusMessage.Value = (StatusMessage.Value ?? "") + $" (Metadata fetch failed: {ex.Message})";
            }
        }

        foreach (var baseEntry in parsed)
        {
            var channel = DeriveChannel(baseEntry.Version);
            var (previewKind, previewNum) = DerivePreviewInfo(baseEntry.Version);
            bool isPreview = previewKind != "ga";

            // Basic channel info is always available from the releases-index
            ChannelIndexInfo? indexInfo = null;
            _channelIndexCache?.TryGetValue(channel, out indexInfo);

            bool outOfSupport = indexInfo != null && (indexInfo.SupportPhase == "eol" || (indexInfo.EolDate.HasValue && indexInfo.EolDate.Value < DateTime.UtcNow.Date));

            var finalEntry = baseEntry with
            {
                Channel = channel,
                ReleaseType = indexInfo?.ReleaseType?.ToUpperInvariant(),
                SupportPhase = indexInfo?.SupportPhase,
                IsOutOfSupport = outOfSupport,
                PreviewKind = previewKind,
                PreviewNumber = previewNum,
                IsPreview = isPreview,
                EolDate = indexInfo?.EolDate
            };

            // Detailed enrichment only when metadata is enabled
            if (IsMetadataEnabledInSession.Value)
            {
                try
                {
                    ChannelResolved? meta = null;
                    _channelCache?.TryGetValue(channel, out meta);
                    bool isSecurity = meta != null && meta.SecurityVersions.Contains(baseEntry.Version);
                    SecurityStatus securityStatus = SecurityStatus.None;
                    string? securityTooltip = null;
                    if (meta != null)
                    {
                        var latestSec = baseEntry.Type == "sdk" ? meta.LatestSecuritySdk : meta.LatestSecurityRuntime;
                        (securityStatus, securityTooltip) = SecurityClassificationHelper.Classify(baseEntry.Version, latestSec, isSecurity);
                    }
                    finalEntry = finalEntry with
                    {
                        IsSecurityUpdate = isSecurity,
                        SecurityStatus = securityStatus,
                        SecurityTooltip = securityTooltip,
                        ReleaseDate = ResolveReleaseDate(meta, baseEntry.Version),
                        ReleaseNotesUrl = ResolveReleaseNotes(meta, baseEntry.Version)
                    };
                }
                catch { }
            }

            if (finalEntry.Type == "sdk")
                _sdkItems.Add(finalEntry);
            else
                _runtimeItems.Add(finalEntry);
        }
    }

    // --- Groups ---

    private void BuildGroups()
    {
        static (int major, int minor) ParseChannel(string? channel)
        {
            if (string.IsNullOrWhiteSpace(channel)) return (-1, -1);
            var parts = channel.Split('.', StringSplitOptions.RemoveEmptyEntries);
            int major = -1, minor = -1;
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            return (major, minor);
        }
        IOrderedEnumerable<IGrouping<string?, DotnetInstallEntry>> OrderGroups(IEnumerable<IGrouping<string?, DotnetInstallEntry>> groups)
            => groups.OrderByDescending(g => ParseChannel(g.Key).major)
                     .ThenByDescending(g => ParseChannel(g.Key).minor);

        GroupedSdkItems.Clear();
        GroupedRuntimeItems.Clear();

        if (_sdkItems.Count > 0)
        {
            foreach (var grp in OrderGroups(_sdkItems.GroupBy(i => i.Channel ?? DeriveChannel(i.Version))))
            {
                // Always use index cache for channel-level info
                ChannelIndexInfo? idx = null;
                _channelIndexCache?.TryGetValue(grp.Key!, out idx);

                ChannelResolved? cr = null;
                if (IsMetadataEnabledInSession.Value)
                    _channelCache?.TryGetValue(grp.Key!, out cr);

                DateTime? mauiEol = null;
                EnsureMauiLifecycleLoaded();
                if (_mauiLifecycle != null && grp.Key != null && _mauiLifecycle.TryGetValue(grp.Key, out var mdt)) mauiEol = mdt;

                bool latestIsSec = cr?.LatestSecuritySdk != null && cr.LatestSdk == cr.LatestSecuritySdk;
                GroupedSdkItems.Add(new ChannelGroup(grp.Key!, grp,
                    idx?.ReleaseType?.ToUpperInvariant(), idx?.SupportPhase, idx?.EolDate,
                    mauiEol, cr?.LatestSdk, cr?.LatestRuntime, cr?.LatestSecuritySdk, cr?.LatestSecurityRuntime,
                    isSdkGroup: true, latestRelevantIsSecurity: latestIsSec));
            }
        }

        if (_runtimeItems.Count > 0)
        {
            foreach (var grp in OrderGroups(_runtimeItems.GroupBy(i => i.Channel ?? DeriveChannel(i.Version))))
            {
                ChannelIndexInfo? idx2 = null;
                _channelIndexCache?.TryGetValue(grp.Key!, out idx2);

                ChannelResolved? cr2 = null;
                if (IsMetadataEnabledInSession.Value)
                    _channelCache?.TryGetValue(grp.Key!, out cr2);

                DateTime? mauiEol2 = null;
                EnsureMauiLifecycleLoaded();
                if (_mauiLifecycle != null && grp.Key != null && _mauiLifecycle.TryGetValue(grp.Key, out var mdt2)) mauiEol2 = mdt2;

                bool latestIsSecRt = cr2?.LatestSecurityRuntime != null && cr2.LatestRuntime == cr2.LatestSecurityRuntime;
                GroupedRuntimeItems.Add(new ChannelGroup(grp.Key!, grp,
                    idx2?.ReleaseType?.ToUpperInvariant(), idx2?.SupportPhase, idx2?.EolDate,
                    mauiEol2, cr2?.LatestSdk, cr2?.LatestRuntime, cr2?.LatestSecuritySdk, cr2?.LatestSecurityRuntime,
                    isSdkGroup: false, latestRelevantIsSecurity: latestIsSecRt));
            }
        }
    }

    // --- Update Check ---

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
                        return;
                }
                catch { }
            }

            var client = new GitHubClient(new ProductHeaderValue("dotuninstall"));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            Release? latestRelease = null;
            try { latestRelease = await client.Repository.Release.GetLatest("lextudio", "dotuninstall").WaitAsync(cts.Token); }
            catch { return; }
            var tag = latestRelease?.TagName;
            if (string.IsNullOrWhiteSpace(tag)) return;
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
            if (File.Exists(stamp)) try { File.Delete(stamp); } catch { }
            await CheckForUpdatesAsync();
            if (!HasUpdate.Value)
                UpdateMessage.Value = $"You are running the latest version ({GetCurrentVersion()}).";
        }
        catch { }
    }

    public async Task ClearCacheAsync()
    {
        if (IsLoading.Value) return;
        try
        {
            var cacheDir = Path.Combine(GetAppDataRootPath(), "cache");
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
        catch { }
        _channelCache = null;
        _channelIndexCache = null;
        _metaCacheTime = DateTime.MinValue;
        IsUsingCachedLive.Value = false;
        IsUsingSnapshot.Value = false;
        StatusMessage.Value = "Cache cleared. Refreshing...";
        await RefreshAsync();
    }

    // --- Elevation ---

    public void DetectElevation()
    {
        try
        {
            if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;
            // Check if running as root via environment
            var user = Environment.UserName;
            var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
            if (user == "root" || !string.IsNullOrEmpty(sudoUser))
            {
                IsElevated.Value = true;
                OriginalUser.Value = sudoUser ?? user;
                ShowElevationWarning.Value = true;
            }
            else
            {
                ShowElevationOffer.Value = true;
            }
        }
        catch { }
    }

    public void DismissElevationWarning() => ShowElevationWarning.Value = false;

    public void DismissElevationOffer() => ShowElevationOffer.Value = false;

    public string GetReleasePageUrl()
    {
        var tag = LatestReleaseTag.Value;
        return string.IsNullOrWhiteSpace(tag)
            ? "https://github.com/lextudio/DotUninstall/releases/latest"
            : $"https://github.com/lextudio/DotUninstall/releases/tag/{tag}";
    }

    // --- Helpers ---

    private static string GetUpdateStampPath()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".dotuninstall");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "last_update_check.txt");
        }
        catch { return Path.Combine(Path.GetTempPath(), "dotuninstall_last_update_check.txt"); }
    }

    private static string GetCurrentVersion()
    {
        try
        {
            var asm = typeof(MainViewModel).Assembly;
            var info = asm.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
            return info?.InformationalVersion ?? "0.0";
        }
        catch { return "0.0"; }
    }

    private static bool TryParseVersion(string? text, out NuGetVersion version)
    {
        version = new NuGetVersion(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.StartsWith('v')) text = text[1..];
        if (!NuGetVersion.TryParse(text, out var parsed) || parsed is null) return false;
        version = parsed;
        return true;
    }

    private static string[] SplitLines(string text) => text
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(string fileName, string arguments)
    {
        var tcs = new TaskCompletionSource<(int, string, string)>();
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        string so = string.Empty, se = string.Empty;
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) so += e.Data + "\n"; };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) se += e.Data + "\n"; };
        proc.Exited += (_, _) => tcs.TrySetResult((proc.ExitCode, so, se));
        if (!proc.Start()) throw new InvalidOperationException($"Cannot start process {fileName}");
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return await tcs.Task.ConfigureAwait(false);
    }
}
