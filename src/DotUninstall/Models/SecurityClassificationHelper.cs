using NuGet.Versioning;

namespace DotUninstall.Models;

public static class SecurityClassificationHelper
{
    public static (SecurityStatus status, string? tooltip) Classify(string installedVersion, string? latestSecurityVersion, bool isSecurityPatch)
    {
        SecurityStatus status = SecurityStatus.None;
        string? tooltip = null;

        if (!string.IsNullOrWhiteSpace(latestSecurityVersion)
            && NuGetVersion.TryParse(installedVersion, out var instNv)
            && NuGetVersion.TryParse(latestSecurityVersion, out var latestSecNv))
        {
            if (instNv < latestSecNv)
            {
                status = SecurityStatus.Unpatched;
                tooltip = $"Security release {latestSecurityVersion} is available; this version lacks those fixes.";
            }
            else if (instNv == latestSecNv)
            {
                status = SecurityStatus.SecurityPatch;
                tooltip = $"This is the latest security patch ({latestSecurityVersion}).";
            }
            else
            {
                status = isSecurityPatch ? SecurityStatus.SecurityPatch : SecurityStatus.Patched;
                tooltip = $"Includes all security fixes up to {latestSecurityVersion}.";
            }
        }
        else if (isSecurityPatch)
        {
            status = SecurityStatus.SecurityPatch;
            tooltip = "Security patch release.";
        }
        return (status, tooltip);
    }
}
