using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    // User-approved public distribution repository. Release notification is safe without executing
    // downloaded code; direct installation remains separately gated by the approved signer below.
    public const string ApprovedRepository = "vince-productivity-tray";
    // Set only after the release installer is Authenticode-signed by the approved publisher.
    public const string ApprovedSignerThumbprint = "";

    public static bool IsNotificationConfigured =>
        !string.IsNullOrWhiteSpace(ApprovedRepository);

    public static bool IsAutoInstallConfigured =>
        IsNotificationConfigured &&
        !string.IsNullOrWhiteSpace(ApprovedSignerThumbprint);

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

    public static void VerifyPublisher(string path)
    {
        if (!IsAutoInstallConfigured)
        {
            throw new InvalidOperationException("Updater publisher policy is not configured.");
        }

        WinTrust.VerifyEmbeddedSignature(path);
#pragma warning disable SYSLIB0057 // WinTrust verifies the signature; this API extracts its signer certificate.
        using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
        var actual = certificate.Thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal);
        var expected = ApprovedSignerThumbprint.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The installer publisher certificate is not approved.");
        }
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

    public GitHubUpdateClient(HttpClient http)
    {
        _http = http;
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

        if (UpdaterPolicy.IsAutoInstallConfigured &&
            (!UpdaterPolicy.IsTrustedAssetUri(new Uri(installerUrl!), installer) ||
             !UpdaterPolicy.IsTrustedAssetUri(new Uri(checksumUrl!), checksum)))
        {
            throw new InvalidDataException("The release contains an asset URL outside the approved GitHub repository.");
        }

        return new UpdateRelease(version, root.GetProperty("body").GetString() ?? string.Empty,
            releasePageUrl, installerUrl, checksumUrl, installer);
    }

    public async Task<string> DownloadVerifiedAsync(UpdateRelease release, CancellationToken cancellationToken)
    {
        if (!UpdaterPolicy.IsAutoInstallConfigured ||
            release.InstallerUrl is null ||
            release.ChecksumUrl is null)
        {
            throw new InvalidOperationException("Verified in-app installation is not configured.");
        }
        var staging = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VinceProductivityTray", "Updates", release.Version.ToString());
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

        await using var verify = File.OpenRead(temporary);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(verify, cancellationToken));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporary);
            throw new InvalidDataException("The downloaded installer failed SHA-256 verification.");
        }

        File.Move(temporary, finalPath, overwrite: true);
        UpdaterPolicy.VerifyPublisher(finalPath);
        return finalPath;
    }

    public static Version CurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
}

internal static class WinTrust
{
    private static readonly Guid ActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public static void VerifyEmbeddedSignature(string path)
    {
        using var file = new WinTrustFileInfo(path);
        using var data = new WinTrustData(file);
        var action = ActionGenericVerifyV2;
        var result = WinVerifyTrust(IntPtr.Zero, ref action, data.Pointer);
        data.CloseState(ref action);
        if (result != 0) throw new InvalidDataException($"Authenticode verification failed (0x{result:X8}).");
    }

    private sealed class WinTrustFileInfo : IDisposable
    {
        public IntPtr Pointer { get; }
        private readonly IntPtr _path;

        public WinTrustFileInfo(string path)
        {
            _path = Marshal.StringToCoTaskMemUni(path);
            var value = new FileInfoNative
            {
                Size = (uint)Marshal.SizeOf<FileInfoNative>(),
                FilePath = _path,
            };
            Pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<FileInfoNative>());
            Marshal.StructureToPtr(value, Pointer, false);
        }

        public void Dispose()
        {
            Marshal.FreeCoTaskMem(Pointer);
            Marshal.FreeCoTaskMem(_path);
        }
    }

    private sealed class WinTrustData : IDisposable
    {
        public IntPtr Pointer { get; }

        public WinTrustData(WinTrustFileInfo file)
        {
            var value = new TrustDataNative
            {
                Size = (uint)Marshal.SizeOf<TrustDataNative>(),
                UIChoice = 2,
                RevocationChecks = 1,
                UnionChoice = 1,
                FileInfo = file.Pointer,
                StateAction = 1,
                ProviderFlags = 0x00000040,
            };
            Pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<TrustDataNative>());
            Marshal.StructureToPtr(value, Pointer, false);
        }

        public void CloseState(ref Guid action)
        {
            var value = Marshal.PtrToStructure<TrustDataNative>(Pointer);
            value.StateAction = 2;
            Marshal.StructureToPtr(value, Pointer, true);
            _ = WinVerifyTrust(IntPtr.Zero, ref action, Pointer);
        }

        public void Dispose()
        {
            Marshal.DestroyStructure<TrustDataNative>(Pointer);
            Marshal.FreeCoTaskMem(Pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfoNative
    {
        public uint Size;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustDataNative
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SIPClientData;
        public uint UIChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr URLReference;
        public uint ProviderFlags;
        public uint UIContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid actionId, IntPtr trustData);
}

internal static class UpdateDeferral
{
    public static bool IsDue(DateTimeOffset? deferredUntil, DateTimeOffset now) =>
        deferredUntil is null || deferredUntil <= now;

    public static DateTimeOffset Defer(DateTimeOffset now, int hours) =>
        now.AddHours(Math.Clamp(hours, 1, 168));
}

internal static class UpdateInstallGuard
{
    public static bool CanInstall(bool recordingActive, bool timeDraftPending) =>
        !recordingActive && !timeDraftPending;
}
