using NuGet.Versioning;

namespace DotUninstall.Models;

public sealed class ChannelGroup
{
    public string Channel { get; }
    public List<DotnetInstallEntry> Items { get; }
    public string? ReleaseType { get; }
    public string? SupportPhase { get; }
    public DateTime? EolDate { get; }
    public string? EolDisplay => EolDate.HasValue ? $"End of life {EolDate:yyyy-MM-dd}" : null;
    public string? EolDateValue => EolDate.HasValue ? EolDate.Value.ToString("yyyy-MM-dd") : null;
    public string LifecycleState { get; }
    public bool IsExpiringSoon => LifecycleState == "expiring";
    public bool IsEol => LifecycleState == "eol";
    public DateTime? MauiEolDate { get; }
    public string? MauiEolDateValue => MauiEolDate.HasValue ? MauiEolDate.Value.ToString("yyyy-MM-dd") : null;
    public string? MauiEolInfoUrl => MauiEolDate.HasValue ? "https://dotnet.microsoft.com/platform/support/policy/maui" : null;
    public string? LatestSdkVersion { get; }
    public string? LatestRuntimeVersion { get; }
    public string? LatestSecuritySdkVersion { get; }
    public string? LatestSecurityRuntimeVersion { get; }
    public bool IsSdkGroup { get; }
    public string ChannelDownloadUrl { get; }
    public string? LatestRelevantVersion => IsSdkGroup ? LatestSdkVersion : LatestRuntimeVersion;
    public bool IsLatestRelevantInstalled => IsSdkGroup ? IsLatestSdkInstalled : IsLatestRuntimeInstalled;
    public bool ShowLatestRelevantMissing => !string.IsNullOrWhiteSpace(LatestRelevantVersion) && !IsLatestRelevantInstalled;
    public bool LatestRelevantIsSecurity { get; }
    public bool ShowLatestRelevantMissingSecurity => ShowLatestRelevantMissing && LatestRelevantIsSecurity;
    public bool ShowLatestRelevantMissingNormal => ShowLatestRelevantMissing && !LatestRelevantIsSecurity;
    public bool IsLatestSdkInstalled { get; }
    public bool IsLatestRuntimeInstalled { get; }
    public bool IsLatestSecuritySdkInstalled { get; }
    public bool IsLatestSecurityRuntimeInstalled { get; }

    public string? ReleaseTypeInfoUrl => ReleaseType is null ? null : "https://learn.microsoft.com/lifecycle/faq/dotnet-core";
    public string? SupportPhaseInfoUrl => SupportPhase is null ? null : "https://dotnet.microsoft.com/platform/support/policy/dotnet-core";
    public string? EolInfoUrl => EolDate.HasValue ? "https://learn.microsoft.com/lifecycle/products/microsoft-net-and-net-core" : null;

    public ChannelGroup(
        string channel,
        IEnumerable<DotnetInstallEntry> items,
        string? releaseType,
        string? supportPhase,
        DateTime? eolDate,
        DateTime? mauiEolDate,
        string? latestSdkVersion,
        string? latestRuntimeVersion,
        string? latestSecuritySdkVersion,
        string? latestSecurityRuntimeVersion,
        bool isSdkGroup,
        bool latestRelevantIsSecurity)
    {
        Channel = string.IsNullOrWhiteSpace(channel) ? "Other" : channel;

        var ordered = items
            .Select(i =>
            {
                NuGetVersion? v = NuGetVersion.TryParse(i.Version, out var parsed) ? parsed : null;
                return (entry: i, version: v);
            })
            .OrderBy(t => t.version, new NuGetVersionDescComparer())
            .ThenBy(t => t.entry.Version, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.entry);

        Items = new List<DotnetInstallEntry>(ordered);
        ReleaseType = releaseType;
        SupportPhase = supportPhase;
        EolDate = eolDate;
        MauiEolDate = mauiEolDate;
        LatestSdkVersion = latestSdkVersion;
        LatestRuntimeVersion = latestRuntimeVersion;
        LatestSecuritySdkVersion = latestSecuritySdkVersion;
        LatestSecurityRuntimeVersion = latestSecurityRuntimeVersion;
        IsSdkGroup = isSdkGroup;

        if (!string.IsNullOrWhiteSpace(LatestSdkVersion))
        {
            IsLatestSdkInstalled = Items.Any(i => i.Type == "sdk" && string.Equals(i.Version, LatestSdkVersion, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(LatestRuntimeVersion))
        {
            IsLatestRuntimeInstalled = Items.Any(i => i.Type == "runtime" && string.Equals(i.Version, LatestRuntimeVersion, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(LatestSecuritySdkVersion))
        {
            IsLatestSecuritySdkInstalled = Items.Any(i => i.Type == "sdk" && string.Equals(i.Version, LatestSecuritySdkVersion, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(LatestSecurityRuntimeVersion))
        {
            IsLatestSecurityRuntimeInstalled = Items.Any(i => i.Type == "runtime" && string.Equals(i.Version, LatestSecurityRuntimeVersion, StringComparison.OrdinalIgnoreCase));
        }

        ChannelDownloadUrl = $"https://dotnet.microsoft.com/download/dotnet/{Channel}";
        LifecycleState = ComputeLifecycleState();
        LatestRelevantIsSecurity = latestRelevantIsSecurity;
    }

    private sealed class NuGetVersionDescComparer : IComparer<NuGetVersion?>
    {
        public int Compare(NuGetVersion? x, NuGetVersion? y)
        {
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is null)
            {
                return 1;
            }

            if (y is null)
            {
                return -1;
            }

            return y.CompareTo(x);
        }
    }

    private string ComputeLifecycleState()
    {
        var today = DateTime.UtcNow.Date;
        if (SupportPhase == "eol" || (EolDate.HasValue && EolDate.Value < today))
        {
            return "eol";
        }

        if (EolDate.HasValue)
        {
            var days = (EolDate.Value - today).TotalDays;
            if (days <= 90)
            {
                return "expiring";
            }
        }
        return "supported";
    }
}
