using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Http;

namespace ChristianSpellHelper;

internal sealed record UpdaterConfig
{
    public bool Enabled { get; init; }
    public int DeferredHours { get; init; } = 24;

    public static UpdaterConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "updater.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<UpdaterConfig>(File.ReadAllText(path)) ?? new UpdaterConfig()
            : new UpdaterConfig();
    }
}

internal static class UpdaterPolicy
{
    public const string ApprovedOwner = "CheggenOnline";
    // The user distributes unsigned installers. Downloading is separate from executing setup.
    public const string ApprovedRepository = "vince-productivity-tray";

    public static bool IsNotificationConfigured =>
        !string.IsNullOrWhiteSpace(ApprovedRepository);

    public static bool IsManualDownloadConfigured => IsNotificationConfigured;

    // User decision: unsigned distribution, with manual installer launch only.
    public static bool IsAutoInstallConfigured => false;

    public static bool IsTrustedAssetUri(Uri uri, string assetName)
        => IsTrustedAssetUri(uri, assetName, ApprovedOwner, ApprovedRepository);

    internal static bool IsTrustedAssetUri(Uri uri, string assetName, string owner, string repository)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 6 &&
               segments[0].Equals(owner, StringComparison.Ordinal) &&
               segments[1].Equals(repository, StringComparison.Ordinal) &&
               segments[2].Equals("releases", StringComparison.Ordinal) &&
               segments[3].Equals("download", StringComparison.Ordinal) &&
               Uri.UnescapeDataString(segments[^1]).Equals(assetName, StringComparison.Ordinal);
    }

    public static bool IsTrustedReleasePageUri(Uri uri)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 5 &&
               segments[0].Equals(ApprovedOwner, StringComparison.Ordinal) &&
               segments[1].Equals(ApprovedRepository, StringComparison.Ordinal) &&
               segments[2].Equals("releases", StringComparison.Ordinal) &&
               segments[3].Equals("tag", StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(segments[4]);
    }

}

internal sealed record UpdateRelease(
    Version Version,
    string ReleaseNotes,
    string ReleasePageUrl,
    string? InstallerUrl,
    string? ChecksumUrl,
    string InstallerName);

internal sealed class GitHubUpdateClient
{
    private readonly HttpClient _http;
    private readonly string _downloadRoot;

    public GitHubUpdateClient(HttpClient http, string? downloadRoot = null)
    {
        _http = http;
        _downloadRoot = downloadRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VinceProductivityTray", "Updates");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("VinceProductivityTray-Updater/1.0");
        }
    }

    public async Task<UpdateRelease?> CheckAsync(UpdaterConfig config, Version current, CancellationToken cancellationToken)
    {
        if (!config.Enabled || !UpdaterPolicy.IsNotificationConfigured) return null;
        // The project currently publishes prereleases. GitHub's /releases/latest endpoint excludes
        // prereleases and returns 404 when no stable release exists, so inspect the bounded release
        // list and select the highest valid, non-draft version instead.
        var endpoint = $"https://api.github.com/repos/{UpdaterPolicy.ApprovedOwner}/{UpdaterPolicy.ApprovedRepository}/releases?per_page=20";
        using var response = await _http.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var rid = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        return SelectLatest(document.RootElement, current, rid);
    }

    internal static UpdateRelease? SelectLatest(JsonElement releases, Version current, string rid)
    {
        if (releases.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The GitHub releases response is not an array.");
        }

        JsonElement? selected = null;
        Version? selectedVersion = null;
        foreach (var candidate in releases.EnumerateArray())
        {
            if (candidate.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var candidateTag = candidate.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            if (!Version.TryParse(candidateTag, out var candidateVersion) ||
                candidateVersion <= current ||
                selectedVersion is not null && candidateVersion <= selectedVersion)
            {
                continue;
            }
            selected = candidate.Clone();
            selectedVersion = candidateVersion;
        }

        if (selected is null || selectedVersion is null) return null;
        var root = selected.Value;
        var version = selectedVersion;
        var releasePageUrl = root.GetProperty("html_url").GetString()
            ?? throw new InvalidDataException("The release has no official page.");
        if (!UpdaterPolicy.IsTrustedReleasePageUri(new Uri(releasePageUrl)))
        {
            throw new InvalidDataException("The release page is outside the approved GitHub repository.");
        }

        var installer = $"VinceProductivityTray-{version}-{rid}-Setup.exe";
        var checksum = installer + ".sha256";
        string? installerUrl = null;
        string? checksumUrl = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name == installer) installerUrl = asset.GetProperty("browser_download_url").GetString();
            if (name == checksum) checksumUrl = asset.GetProperty("browser_download_url").GetString();
        }

        if (UpdaterPolicy.IsAutoInstallConfigured && (installerUrl is null || checksumUrl is null))
        {
            throw new InvalidDataException($"Release {version} has no verified installer for {rid}.");
        }

        if ((installerUrl is not null && !UpdaterPolicy.IsTrustedAssetUri(new Uri(installerUrl), installer)) ||
            (checksumUrl is not null && !UpdaterPolicy.IsTrustedAssetUri(new Uri(checksumUrl), checksum)))
        {
            throw new InvalidDataException("The release contains an asset URL outside the approved GitHub repository.");
        }

        return new UpdateRelease(version, root.GetProperty("body").GetString() ?? string.Empty,
            releasePageUrl, installerUrl, checksumUrl, installer);
    }

    public async Task<string> DownloadVerifiedAsync(UpdateRelease release, CancellationToken cancellationToken)
    {
        if (!UpdaterPolicy.IsManualDownloadConfigured ||
            release.InstallerUrl is null ||
            release.ChecksumUrl is null)
        {
            throw new InvalidOperationException("This release has no download for this PC.");
        }
        if (!UpdaterPolicy.IsTrustedAssetUri(new Uri(release.InstallerUrl), release.InstallerName) ||
            !UpdaterPolicy.IsTrustedAssetUri(new Uri(release.ChecksumUrl), release.InstallerName + ".sha256"))
            throw new InvalidDataException("The download is outside the approved GitHub repository.");
        var staging = Path.Combine(_downloadRoot, release.Version.ToString());
        Directory.CreateDirectory(staging);
        var finalPath = Path.Combine(staging, release.InstallerName);
        var temporary = finalPath + ".download";

        var expectedText = await _http.GetStringAsync(release.ChecksumUrl, cancellationToken);
        var expected = expectedText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (expected.Length != 64 || expected.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("The release checksum is invalid.");
        }

        await using (var source = await _http.GetStreamAsync(release.InstallerUrl, cancellationToken))
        await using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        }

        string actual;
        await using (var verify = File.OpenRead(temporary))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(verify, cancellationToken));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporary);
            throw new InvalidDataException("The downloaded installer failed SHA-256 verification.");
        }

        File.Move(temporary, finalPath, overwrite: true);
        // Integrity check only. This deliberately does not claim publisher signing or execute setup.
        return finalPath;
    }

    public static Version CurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
}

internal static class UpdateDeferral
{
    public static bool IsDue(DateTimeOffset? deferredUntil, DateTimeOffset now) =>
        deferredUntil is null || deferredUntil <= now;

    public static DateTimeOffset Defer(DateTimeOffset now, int hours) =>
        now.AddHours(Math.Clamp(hours, 1, 168));
}

internal static class UpdateCheckSchedule
{
    public const int InitialIntervalMilliseconds = 30_000;
    public const int RepeatIntervalMilliseconds = 6 * 60 * 60 * 1000;
}

internal static class UpdateInstallGuard
{
    public static bool CanInstall(bool recordingActive, bool timeDraftPending) =>
        !recordingActive && !timeDraftPending;
}
