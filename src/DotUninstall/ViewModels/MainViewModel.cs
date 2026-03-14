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

    // --- Metadata ---

    private static readonly Uri ReleaseMetadataIndex = new("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json");
    private const string AppDataDirectoryName = "dotnet-uninstall-ui";
    private const string UiSettingsFileName = "ui-settings.json";

    private sealed class UiSettings
    {
        public bool ShowDotNetMetadata { get; set; }

        public string ThemeMode { get; set; } = "System";
    }

    private sealed class ReleasesIndexRoot
    {
        [JsonPropertyName("releases-index")] public List<ChannelInfo>? ReleasesIndex { get; set; }
    }

    private sealed class ChannelInfo
    {
        [JsonPropertyName("channel-version")] public string? ChannelVersion { get; set; }

        [JsonPropertyName("release-type")] public string? ReleaseType { get; set; }

        [JsonPropertyName("support-phase")] public string? SupportPhase { get; set; }

        [JsonPropertyName("eol-date")] public DateTime? EolDate { get; set; }

        [JsonPropertyName("releases.json")] public string? ReleasesJson { get; set; }
    }

    private sealed class ChannelReleases { public List<ChannelRelease>? Releases { get; set; } }

    private sealed class ChannelRelease
    {
        [JsonPropertyName("release-version")] public string? ReleaseVersion { get; set; }

        [JsonPropertyName("release-date")] public DateTime? ReleaseDate { get; set; }

        [JsonPropertyName("release-notes")] public string? ReleaseNotes { get; set; }

        public bool? Security { get; set; }

        public SdkRelease? Sdk { get; set; }

        public List<SdkRelease>? Sdks { get; set; }

        public RuntimeRelease? Runtime { get; set; }
    }

    private sealed class SdkRelease { public string? Version { get; set; } }

    private sealed class RuntimeRelease { public string? Version { get; set; } }

    [JsonSerializable(typeof(UiSettings))]
    [JsonSerializable(typeof(ReleasesIndexRoot))]
    [JsonSerializable(typeof(ChannelReleases))]
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    private sealed partial class AppJsonContext : JsonSerializerContext;

    private static Dictionary<string, ChannelIndexInfo>? _channelIndexCache;

    private sealed class ChannelIndexInfo
    {
        public string? ReleaseType { get; set; }

        public string? SupportPhase { get; set; }

        public DateTime? EolDate { get; set; }
    }

    private static DateTime _metaCacheTime = DateTime.MinValue;
    private static Dictionary<string, ChannelResolved>? _channelCache;

    private sealed class ChannelResolved
    {
        public string? ReleaseType { get; set; }

        public string? SupportPhase { get; set; }

        public DateTime? EolDate { get; set; }

        public HashSet<string> SdkVersions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> RuntimeVersions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> SecurityVersions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? LatestSdk { get; set; }

        public string? LatestRuntime { get; set; }

        public DateTime? MauiEolDate { get; set; }

        public string? LatestSecuritySdk { get; set; }

        public string? LatestSecurityRuntime { get; set; }

        public Dictionary<string, DateTime?> ReleaseDates { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string?> ReleaseNotes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, DateTime?>? _mauiLifecycle;
    private static bool _mauiLifecycleLoaded;

    private static string GetAppDataRootPath()
    {
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var dir = Path.Combine(baseDir, AppDataDirectoryName);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
        catch { }
        try
        {
            var fallback = Path.Combine(Path.GetTempPath(), AppDataDirectoryName);
            Directory.CreateDirectory(fallback);
            return fallback;
        }
        catch { return Path.GetTempPath(); }
    }

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
                if (settings != null) return settings;
            }
        }
        catch { }
        return new UiSettings();
    }

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

    private async Task ApplyMetadataModeChangeAsync(bool enabled)
    {
        await _metadataModeChangeGate.WaitAsync();
        try
        {
            if (IsMetadataEnabledInSession.Value == enabled) return;
            IsMetadataEnabledInSession.Value = enabled;
            StatusMessage.Value = enabled ? "Metadata mode enabled. Refreshing..." : "Metadata mode disabled. Refreshing...";
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage.Value = ex.Message; }
        finally { _metadataModeChangeGate.Release(); }
    }

    private static string NormalizeThemeMode(string? value)
    {
        if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase)) return "Light";
        if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
        return "System";
    }

    private static int ThemeModeToIndex(string mode) => mode switch
    {
        "Light" => 1,
        "Dark" => 2,
        _ => 0
    };

    private static void EnsureMauiLifecycleLoaded()
    {
        if (_mauiLifecycleLoaded) return;
        _mauiLifecycleLoaded = true;
        try
        {
            var asm = typeof(MainViewModel).Assembly;
            var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("MetadataSnapshot.maui-lifecycle.json", StringComparison.OrdinalIgnoreCase));
            if (res == null) return;
            using var s = asm.GetManifestResourceStream(res);
            if (s == null) return;
            using var doc = JsonDocument.Parse(s);
            var dict = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in entries.EnumerateArray())
                {
                    var channel = e.TryGetProperty("channel", out var chEl) ? chEl.GetString() : null;
                    DateTime? eol = null;
                    if (e.TryGetProperty("eolDate", out var eolEl) && eolEl.ValueKind == JsonValueKind.String)
                        if (DateTime.TryParse(eolEl.GetString(), out var parsed)) eol = parsed.Date;
                    if (!string.IsNullOrWhiteSpace(channel)) dict[channel!] = eol;
                }
            }
            _mauiLifecycle = dict;
        }
        catch { }
    }

    internal static string DeriveChannel(string version)
    {
        if (NuGetVersion.TryParse(version, out var nv))
            return nv.Major + "." + nv.Minor;
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _)) return parts[0] + "." + parts[1];
        if (parts.Length >= 1) return parts[0];
        return version;
    }

    private static (string kind, int? number) DerivePreviewInfo(string version)
    {
        if (NuGetVersion.TryParse(version, out var nv))
        {
            if (!nv.IsPrerelease) return ("ga", null);
            var labels = nv.ReleaseLabels?.ToArray() ?? Array.Empty<string>();
            if (labels.Length == 0) return ("ga", null);
            var first = labels[0].ToLowerInvariant();
            if (first is "preview" or "rc")
            {
                int? num = null;
                if (labels.Length > 1 && int.TryParse(labels[1], out var n)) num = n;
                return (first, num);
            }
            return ("ga", null);
        }
        var m = Regex.Match(version.ToLowerInvariant(), "-(preview|rc)(?:\\.(?<n>[0-9]+))?");
        if (!m.Success) return ("ga", null);
        int? num2 = null;
        if (m.Groups["n"].Success && int.TryParse(m.Groups["n"].Value, out var p2)) num2 = p2;
        return (m.Groups[1].Value, num2);
    }

    private static DateTime? ResolveReleaseDate(ChannelResolved? meta, string version)
    {
        if (meta == null) return null;
        return meta.ReleaseDates.TryGetValue(version, out var dt) ? dt : null;
    }

    private static string? ResolveReleaseNotes(ChannelResolved? meta, string version)
    {
        if (meta == null) return null;
        return meta.ReleaseNotes.TryGetValue(version, out var rn) ? rn : null;
    }

    private async Task EnsureReleasesIndexAsync()
    {
        if (_channelIndexCache != null) return;

        ReleasesIndexRoot? indexRoot = null;

        // Try disk cache
        string? cacheDir = null;
        try { cacheDir = Path.Combine(GetAppDataRootPath(), "cache"); Directory.CreateDirectory(cacheDir); }
        catch { cacheDir = null; }
        var indexCachePath = cacheDir is null ? null : Path.Combine(cacheDir, "releases-index.json");

        if (indexCachePath != null && File.Exists(indexCachePath))
        {
            try
            {
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(indexCachePath);
                if (age < TimeSpan.FromDays(1))
                {
                    var json = await File.ReadAllTextAsync(indexCachePath);
                    indexRoot = JsonSerializer.Deserialize(json, AppJsonContext.Default.ReleasesIndexRoot);
                }
            }
            catch { }
        }

        // Try network
        if (indexRoot == null)
        {
            try
            {
                using var http = new HttpClient();
                using var s = await http.GetStreamAsync(ReleaseMetadataIndex);
                indexRoot = await JsonSerializer.DeserializeAsync(s, AppJsonContext.Default.ReleasesIndexRoot);
                if (indexRoot != null && indexCachePath != null)
                    try { await File.WriteAllTextAsync(indexCachePath, JsonSerializer.Serialize(indexRoot, AppJsonContext.Default.ReleasesIndexRoot)); } catch { }
            }
            catch { }
        }

        // Embedded snapshot fallback
        if (indexRoot == null)
        {
            try
            {
                var asm = typeof(MainViewModel).Assembly;
                var snapshotName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("MetadataSnapshot.releases-index.json", StringComparison.OrdinalIgnoreCase));
                if (snapshotName != null)
                {
                    using var rs = asm.GetManifestResourceStream(snapshotName);
                    if (rs != null)
                        indexRoot = await JsonSerializer.DeserializeAsync(rs, AppJsonContext.Default.ReleasesIndexRoot);
                }
            }
            catch { }
        }

        if (indexRoot?.ReleasesIndex == null) return;

        _channelIndexCache = new(StringComparer.OrdinalIgnoreCase);
        foreach (var ci in indexRoot.ReleasesIndex)
        {
            if (string.IsNullOrWhiteSpace(ci.ChannelVersion)) continue;
            _channelIndexCache[ci.ChannelVersion] = new ChannelIndexInfo
            {
                ReleaseType = ci.ReleaseType?.ToLowerInvariant(),
                SupportPhase = ci.SupportPhase?.ToLowerInvariant(),
                EolDate = ci.EolDate?.Date
            };
        }
    }

    private async Task EnsureMetadataAsync(HashSet<string> neededChannels)
    {
        if (_channelCache != null && (DateTime.UtcNow - _metaCacheTime) < TimeSpan.FromHours(12))
            if (neededChannels.All(c => _channelCache.ContainsKey(c))) return;

        _channelCache ??= new();
        ReleasesIndexRoot? indexRoot = null;
        bool usedSnapshot = false;
        bool usedDiskCache = false;

        string? cacheDir = null;
        try { cacheDir = Path.Combine(GetAppDataRootPath(), "cache"); Directory.CreateDirectory(cacheDir); }
        catch { cacheDir = null; }

        var indexCachePath = cacheDir is null ? null : Path.Combine(cacheDir, "releases-index.json");
        TimeSpan diskTtl = TimeSpan.FromDays(1);

        // Disk cache
        if (indexCachePath != null && File.Exists(indexCachePath))
        {
            try
            {
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(indexCachePath);
                if (age < diskTtl)
                {
                    var json = await File.ReadAllTextAsync(indexCachePath);
                    indexRoot = JsonSerializer.Deserialize(json, AppJsonContext.Default.ReleasesIndexRoot);
                    if (indexRoot != null) usedDiskCache = true;
                }
            }
            catch { }
        }

        // Live network
        if (indexRoot == null)
        {
            try
            {
                using var http = new HttpClient();
                using var s = await http.GetStreamAsync(ReleaseMetadataIndex);
                indexRoot = await JsonSerializer.DeserializeAsync(s, AppJsonContext.Default.ReleasesIndexRoot);
                if (indexRoot != null && indexCachePath != null)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(indexRoot, AppJsonContext.Default.ReleasesIndexRoot);
                        await File.WriteAllTextAsync(indexCachePath, json);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Embedded snapshot fallback
        if (indexRoot == null)
        {
            try
            {
                var asm = typeof(MainViewModel).Assembly;
                var snapshotName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("MetadataSnapshot.releases-index.json", StringComparison.OrdinalIgnoreCase));
                if (snapshotName != null)
                {
                    using var rs = asm.GetManifestResourceStream(snapshotName);
                    if (rs != null)
                    {
                        indexRoot = await JsonSerializer.DeserializeAsync(rs, AppJsonContext.Default.ReleasesIndexRoot);
                        usedSnapshot = indexRoot != null;
                    }
                }
            }
            catch { }
        }

        if (indexRoot?.ReleasesIndex == null) return;

        var indexLookup = indexRoot.ReleasesIndex
            .Where(c => !string.IsNullOrWhiteSpace(c.ChannelVersion))
            .ToDictionary(c => c.ChannelVersion!, StringComparer.OrdinalIgnoreCase);

        foreach (var ch in neededChannels)
        {
            if (_channelCache.ContainsKey(ch)) continue;
            if (!indexLookup.TryGetValue(ch, out var ci) || string.IsNullOrWhiteSpace(ci.ReleasesJson)) continue;
            try
            {
                ChannelReleases? rels = null;
                if (!usedSnapshot)
                {
                    try
                    {
                        ChannelReleases? diskCached = null;
                        string? channelCachePath = null;
                        if (cacheDir != null)
                        {
                            channelCachePath = Path.Combine(cacheDir, $"channel-{ch}.json");
                            if (File.Exists(channelCachePath))
                            {
                                try
                                {
                                    var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(channelCachePath);
                                    if (age < diskTtl)
                                    {
                                        var cachedJson = await File.ReadAllTextAsync(channelCachePath);
                                        diskCached = JsonSerializer.Deserialize(cachedJson, AppJsonContext.Default.ChannelReleases);
                                        if (diskCached != null) usedDiskCache = true;
                                    }
                                }
                                catch { }
                            }
                        }
                        if (diskCached != null)
                        {
                            rels = diskCached;
                        }
                        else
                        {
                            using var http = new HttpClient();
                            using var rs = await http.GetStreamAsync(ci.ReleasesJson);
                            rels = await JsonSerializer.DeserializeAsync(rs, AppJsonContext.Default.ChannelReleases);
                            if (rels != null && channelCachePath != null)
                            {
                                try
                                {
                                    var json = JsonSerializer.Serialize(rels, AppJsonContext.Default.ChannelReleases);
                                    await File.WriteAllTextAsync(channelCachePath, json);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
                if (rels == null)
                {
                    var asm = typeof(MainViewModel).Assembly;
                    var channelToken = ch.Replace('.', '_');
                    var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.Contains($"MetadataSnapshot.{channelToken}", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                    if (resourceName != null)
                    {
                        try
                        {
                            using var rs2 = asm.GetManifestResourceStream(resourceName);
                            if (rs2 != null)
                                rels = await JsonSerializer.DeserializeAsync(rs2, AppJsonContext.Default.ChannelReleases);
                        }
                        catch { }
                    }
                }

                var resolved = new ChannelResolved
                {
                    ReleaseType = ci.ReleaseType?.ToLowerInvariant(),
                    SupportPhase = ci.SupportPhase?.ToLowerInvariant(),
                    EolDate = ci.EolDate?.Date
                };

                if (rels?.Releases != null)
                {
                    NuGetVersion? maxStableSdk = null, maxStableRuntime = null;
                    NuGetVersion? maxStableSecSdk = null, maxStableSecRuntime = null;
                    NuGetVersion? maxAnySdk = null, maxAnyRuntime = null;
                    NuGetVersion? maxAnySecSdk = null, maxAnySecRuntime = null;

                    foreach (var r in rels.Releases)
                    {
                        bool sec = r.Security == true;
                        void AddSdk(string? v) { if (!string.IsNullOrWhiteSpace(v)) { resolved.SdkVersions.Add(v); if (sec) resolved.SecurityVersions.Add(v); } }
                        void AddRuntime(string? v) { if (!string.IsNullOrWhiteSpace(v)) { resolved.RuntimeVersions.Add(v); if (sec) resolved.SecurityVersions.Add(v); } }

                        if (r.Sdk?.Version is { } sv && !string.IsNullOrWhiteSpace(sv)) AddSdk(sv);
                        if (r.Sdks != null) foreach (var srel in r.Sdks) if (!string.IsNullOrWhiteSpace(srel.Version)) AddSdk(srel.Version);
                        if (r.Runtime?.Version is { } rv && !string.IsNullOrWhiteSpace(rv)) AddRuntime(rv);

                        if (!string.IsNullOrWhiteSpace(r.Sdk?.Version) && NuGetVersion.TryParse(r.Sdk!.Version, out var sdkNv))
                        {
                            if (maxAnySdk == null || sdkNv > maxAnySdk) maxAnySdk = sdkNv;
                            if (!sdkNv.IsPrerelease && (maxStableSdk == null || sdkNv > maxStableSdk)) maxStableSdk = sdkNv;
                            if (sec)
                            {
                                if (maxAnySecSdk == null || sdkNv > maxAnySecSdk) maxAnySecSdk = sdkNv;
                                if (!sdkNv.IsPrerelease && (maxStableSecSdk == null || sdkNv > maxStableSecSdk)) maxStableSecSdk = sdkNv;
                            }
                        }
                        if (r.Runtime?.Version != null && NuGetVersion.TryParse(r.Runtime.Version, out var rtNv))
                        {
                            if (maxAnyRuntime == null || rtNv > maxAnyRuntime) maxAnyRuntime = rtNv;
                            if (!rtNv.IsPrerelease && (maxStableRuntime == null || rtNv > maxStableRuntime)) maxStableRuntime = rtNv;
                            if (sec)
                            {
                                if (maxAnySecRuntime == null || rtNv > maxAnySecRuntime) maxAnySecRuntime = rtNv;
                                if (!rtNv.IsPrerelease && (maxStableSecRuntime == null || rtNv > maxStableSecRuntime)) maxStableSecRuntime = rtNv;
                            }
                        }

                        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(r.Sdk?.Version)) keys.Add(r.Sdk!.Version!);
                        if (!string.IsNullOrWhiteSpace(r.Runtime?.Version)) keys.Add(r.Runtime!.Version!);
                        if (!string.IsNullOrWhiteSpace(r.ReleaseVersion)) keys.Add(r.ReleaseVersion!);
                        foreach (var k in keys)
                        {
                            if (r.ReleaseDate.HasValue && !resolved.ReleaseDates.ContainsKey(k)) resolved.ReleaseDates[k] = r.ReleaseDate.Value.Date;
                            if (!string.IsNullOrWhiteSpace(r.ReleaseNotes) && !resolved.ReleaseNotes.ContainsKey(k)) resolved.ReleaseNotes[k] = r.ReleaseNotes;
                        }
                    }
                    resolved.LatestSdk = (maxStableSdk ?? maxAnySdk)?.ToNormalizedString();
                    resolved.LatestRuntime = (maxStableRuntime ?? maxAnyRuntime)?.ToNormalizedString();
                    resolved.LatestSecuritySdk = (maxStableSecSdk ?? maxAnySecSdk)?.ToNormalizedString();
                    resolved.LatestSecurityRuntime = (maxStableSecRuntime ?? maxAnySecRuntime)?.ToNormalizedString();
                }
                _channelCache[ch] = resolved;
            }
            catch { }
        }
        _metaCacheTime = DateTime.UtcNow;
        IsUsingSnapshot.Value = usedSnapshot;
        IsUsingCachedLive.Value = !usedSnapshot && usedDiskCache;
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
