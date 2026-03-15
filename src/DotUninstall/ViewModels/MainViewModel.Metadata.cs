using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using NuGet.Versioning;

namespace DotUninstall.ViewModels;

partial class MainViewModel
{
    // --- Metadata ---

    private const string AppDataDirectoryName = "dotnet-uninstall-ui";

    private static readonly Uri ReleaseMetadataIndex = new("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json");
    private static Dictionary<string, ChannelIndexInfo>? _channelIndexCache;
    private static DateTime _metaCacheTime = DateTime.MinValue;
    private static Dictionary<string, ChannelResolved>? _channelCache;
    private static Dictionary<string, DateTime?>? _mauiLifecycle;
    private static bool _mauiLifecycleLoaded;

    public async Task ClearCacheAsync()
    {
        if (IsLoading.Value)
        {
            return;
        }

        try
        {
            var cacheDir = Path.Combine(GetAppDataRootPath(), "cache");
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
            }
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

    internal static string DeriveChannel(string version)
    {
        if (NuGetVersion.TryParse(version, out var nv))
        {
            return nv.Major + "." + nv.Minor;
        }

        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
        {
            return parts[0] + "." + parts[1];
        }

        if (parts.Length >= 1)
        {
            return parts[0];
        }

        return version;
    }

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

    private static void EnsureMauiLifecycleLoaded()
    {
        if (_mauiLifecycleLoaded)
        {
            return;
        }

        _mauiLifecycleLoaded = true;
        try
        {
            var asm = typeof(MainViewModel).Assembly;
            var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("MetadataSnapshot.maui-lifecycle.json", StringComparison.OrdinalIgnoreCase));
            if (res == null)
            {
                return;
            }

            using var s = asm.GetManifestResourceStream(res);
            if (s == null)
            {
                return;
            }

            using var doc = JsonDocument.Parse(s);
            var dict = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in entries.EnumerateArray())
                {
                    var channel = e.TryGetProperty("channel", out var chEl) ? chEl.GetString() : null;
                    DateTime? eol = null;
                    if (e.TryGetProperty("eolDate", out var eolEl) && eolEl.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(eolEl.GetString(), out var parsed))
                        {
                            eol = parsed.Date;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(channel))
                    {
                        dict[channel!] = eol;
                    }
                }
            }
            _mauiLifecycle = dict;
        }
        catch { }
    }

    private static (string kind, int? number) DerivePreviewInfo(string version)
    {
        if (NuGetVersion.TryParse(version, out var nv))
        {
            if (!nv.IsPrerelease)
            {
                return ("ga", null);
            }

            var labels = nv.ReleaseLabels?.ToArray() ?? Array.Empty<string>();
            if (labels.Length == 0)
            {
                return ("ga", null);
            }

            var first = labels[0].ToLowerInvariant();
            if (first is "preview" or "rc")
            {
                int? num = null;
                if (labels.Length > 1 && int.TryParse(labels[1], out var n))
                {
                    num = n;
                }

                return (first, num);
            }
            return ("ga", null);
        }
        var m = Regex.Match(version.ToLowerInvariant(), "-(preview|rc)(?:\\.(?<n>[0-9]+))?");
        if (!m.Success)
        {
            return ("ga", null);
        }

        int? num2 = null;
        if (m.Groups["n"].Success && int.TryParse(m.Groups["n"].Value, out var p2))
        {
            num2 = p2;
        }

        return (m.Groups[1].Value, num2);
    }

    private static DateTime? ResolveReleaseDate(ChannelResolved? meta, string version)
    {
        if (meta == null)
        {
            return null;
        }

        return meta.ReleaseDates.TryGetValue(version, out var dt) ? dt : null;
    }

    private static string? ResolveReleaseNotes(ChannelResolved? meta, string version)
    {
        if (meta == null)
        {
            return null;
        }

        return meta.ReleaseNotes.TryGetValue(version, out var rn) ? rn : null;
    }

    private async Task ApplyMetadataModeChangeAsync(bool enabled)
    {
        await _metadataModeChangeGate.WaitAsync();
        try
        {
            if (IsMetadataEnabledInSession.Value == enabled)
            {
                return;
            }

            IsMetadataEnabledInSession.Value = enabled;
            StatusMessage.Value = enabled ? "Metadata mode enabled. Refreshing..." : "Metadata mode disabled. Refreshing...";
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage.Value = ex.Message; }
        finally { _metadataModeChangeGate.Release(); }
    }

    private async Task EnsureReleasesIndexAsync()
    {
        if (_channelIndexCache != null)
        {
            return;
        }

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
                {
                    try { await File.WriteAllTextAsync(indexCachePath, JsonSerializer.Serialize(indexRoot, AppJsonContext.Default.ReleasesIndexRoot)); } catch { }
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
                    }
                }
            }
            catch { }
        }

        if (indexRoot?.ReleasesIndex == null)
        {
            return;
        }

        _channelIndexCache = new(StringComparer.OrdinalIgnoreCase);
        foreach (var ci in indexRoot.ReleasesIndex)
        {
            if (string.IsNullOrWhiteSpace(ci.ChannelVersion))
            {
                continue;
            }

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
        {
            if (neededChannels.All(c => _channelCache.ContainsKey(c)))
            {
                return;
            }
        }

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
                    if (indexRoot != null)
                    {
                        usedDiskCache = true;
                    }
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

        if (indexRoot?.ReleasesIndex == null)
        {
            return;
        }

        var indexLookup = indexRoot.ReleasesIndex
            .Where(c => !string.IsNullOrWhiteSpace(c.ChannelVersion))
            .ToDictionary(c => c.ChannelVersion!, StringComparer.OrdinalIgnoreCase);

        foreach (var ch in neededChannels)
        {
            if (_channelCache.ContainsKey(ch))
            {
                continue;
            }

            if (!indexLookup.TryGetValue(ch, out var ci) || string.IsNullOrWhiteSpace(ci.ReleasesJson))
            {
                continue;
            }

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
                                        if (diskCached != null)
                                        {
                                            usedDiskCache = true;
                                        }
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
                            {
                                rels = await JsonSerializer.DeserializeAsync(rs2, AppJsonContext.Default.ChannelReleases);
                            }
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
                        void AddSdk(string? v) { if (!string.IsNullOrWhiteSpace(v)) { resolved.SdkVersions.Add(v); if (sec) { resolved.SecurityVersions.Add(v); } } }
                        void AddRuntime(string? v) { if (!string.IsNullOrWhiteSpace(v)) { resolved.RuntimeVersions.Add(v); if (sec) { resolved.SecurityVersions.Add(v); } } }

                        if (r.Sdk?.Version is { } sv && !string.IsNullOrWhiteSpace(sv))
                        {
                            AddSdk(sv);
                        }

                        if (r.Sdks != null)
                        {
                            foreach (var srel in r.Sdks)
                            {
                                if (!string.IsNullOrWhiteSpace(srel.Version))
                                {
                                    AddSdk(srel.Version);
                                }
                            }
                        }

                        if (r.Runtime?.Version is { } rv && !string.IsNullOrWhiteSpace(rv))
                        {
                            AddRuntime(rv);
                        }

                        if (!string.IsNullOrWhiteSpace(r.Sdk?.Version) && NuGetVersion.TryParse(r.Sdk!.Version, out var sdkNv))
                        {
                            if (maxAnySdk == null || sdkNv > maxAnySdk)
                            {
                                maxAnySdk = sdkNv;
                            }

                            if (!sdkNv.IsPrerelease && (maxStableSdk == null || sdkNv > maxStableSdk))
                            {
                                maxStableSdk = sdkNv;
                            }

                            if (sec)
                            {
                                if (maxAnySecSdk == null || sdkNv > maxAnySecSdk)
                                {
                                    maxAnySecSdk = sdkNv;
                                }

                                if (!sdkNv.IsPrerelease && (maxStableSecSdk == null || sdkNv > maxStableSecSdk))
                                {
                                    maxStableSecSdk = sdkNv;
                                }
                            }
                        }
                        if (r.Runtime?.Version != null && NuGetVersion.TryParse(r.Runtime.Version, out var rtNv))
                        {
                            if (maxAnyRuntime == null || rtNv > maxAnyRuntime)
                            {
                                maxAnyRuntime = rtNv;
                            }

                            if (!rtNv.IsPrerelease && (maxStableRuntime == null || rtNv > maxStableRuntime))
                            {
                                maxStableRuntime = rtNv;
                            }

                            if (sec)
                            {
                                if (maxAnySecRuntime == null || rtNv > maxAnySecRuntime)
                                {
                                    maxAnySecRuntime = rtNv;
                                }

                                if (!rtNv.IsPrerelease && (maxStableSecRuntime == null || rtNv > maxStableSecRuntime))
                                {
                                    maxStableSecRuntime = rtNv;
                                }
                            }
                        }

                        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(r.Sdk?.Version))
                        {
                            keys.Add(r.Sdk!.Version!);
                        }

                        if (!string.IsNullOrWhiteSpace(r.Runtime?.Version))
                        {
                            keys.Add(r.Runtime!.Version!);
                        }

                        if (!string.IsNullOrWhiteSpace(r.ReleaseVersion))
                        {
                            keys.Add(r.ReleaseVersion!);
                        }

                        foreach (var k in keys)
                        {
                            if (r.ReleaseDate.HasValue && !resolved.ReleaseDates.ContainsKey(k))
                            {
                                resolved.ReleaseDates[k] = r.ReleaseDate.Value.Date;
                            }

                            if (!string.IsNullOrWhiteSpace(r.ReleaseNotes) && !resolved.ReleaseNotes.ContainsKey(k))
                            {
                                resolved.ReleaseNotes[k] = r.ReleaseNotes;
                            }
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

    private sealed class ChannelIndexInfo
    {
        public string? ReleaseType { get; set; }

        public string? SupportPhase { get; set; }

        public DateTime? EolDate { get; set; }
    }

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
}
