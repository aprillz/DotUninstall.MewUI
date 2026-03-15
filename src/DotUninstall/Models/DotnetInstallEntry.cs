namespace DotUninstall.Models;

public partial record DotnetInstallEntry(
    string Type,
    string Id,
    string Version,
    string Architecture,
    bool CanUninstall,
    string? Reason
);

public partial record DotnetInstallEntry
{
    public string? Channel { get; init; }
    public string? SupportPhase { get; init; }
    public bool IsPreview { get; init; }
    public bool IsOutOfSupport { get; init; }
    public string? ReleaseType { get; init; }
    public string? PreviewKind { get; init; }
    public int? PreviewNumber { get; init; }
    public bool IsSecurityUpdate { get; init; }
    public DateTime? EolDate { get; init; }
    public bool IsGa => PreviewKind == "ga";
    public string? PreviewKindDisplay => PreviewKind switch
    {
        "preview" => "Preview",
        "rc" => "RC",
        "ga" => "GA",
        _ => PreviewKind
    };
    public string? UninstallCommand { get; init; }
    public string? DisplayName { get; init; }
    public string? SubType { get; init; }
    public SecurityStatus SecurityStatus { get; init; } = SecurityStatus.None;
    public bool IsSecurityPatch => SecurityStatus == SecurityStatus.SecurityPatch;
    public string? SecurityTooltip { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public string? ReleaseDateValue => ReleaseDate?.ToString("yyyy-MM-dd");
    public string? ReleaseNotesUrl { get; init; }

    public string? StageDisplay
    {
        get
        {
            if (IsGa)
            {
                return "GA";
            }

            if (string.IsNullOrEmpty(PreviewKindDisplay))
            {
                return null;
            }

            if (PreviewNumber is int n and > 0)
            {
                return $"{PreviewKindDisplay} {n}";
            }

            return PreviewKindDisplay;
        }
    }
}

public enum SecurityStatus
{
    None = 0,
    SecurityPatch = 1,
    Patched = 2,
    UpdateNeeded = 3,
    Unpatched = 4
}
