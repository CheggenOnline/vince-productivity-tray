namespace VinceProductivityTray.Setup;

public sealed record OneDriveCandidate(string RootPath, bool IsBusiness, string? OrganizationName);

public static class OrganizationFolderResolver
{
    public static readonly IReadOnlyList<string> VinceUtilitySubfolders =
        ["Quick Notes", "Meeting Recordings", "Time Capture"];

    public static string? ResolveUniqueVinceRoot(IEnumerable<OneDriveCandidate> candidates)
    {
        var matches = candidates
            .Where(candidate => candidate.IsBusiness && !string.IsNullOrWhiteSpace(candidate.RootPath))
            .Where(candidate => string.Equals(candidate.OrganizationName?.Trim(), "Vince AS", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(candidate.RootPath)),
                                    "OneDrive - Vince AS", StringComparison.OrdinalIgnoreCase))
            .Select(candidate => Path.GetFullPath(candidate.RootPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static void EnsureVinceUtilityStructure(string target)
    {
        target = Path.GetFullPath(target);
        Directory.CreateDirectory(target);
        foreach (var name in VinceUtilitySubfolders) Directory.CreateDirectory(Path.Combine(target, name));
    }
}
