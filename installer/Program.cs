using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VinceProductivityTray.Setup;

internal static class Program
{
    private static readonly string InstallRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "VinceProductivityTray");
    private static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VinceProductivityTray");
    private static readonly string SettingsPath = Path.Combine(DataRoot, "settings.json");
    private static readonly string MigrationBackupPath = Path.Combine(DataRoot, "legacy-spell-startup.txt");
    private static readonly string RecorderMigrationManifestPath = Path.Combine(DataRoot, "legacy-recorder-startup.json");
    private static readonly string InstallJournalPath = Path.Combine(DataRoot, "install-transaction.json");
    private static readonly string VinceUtilityFolderStatePath = Path.Combine(DataRoot, "vince-utility-folder.txt");
    private static readonly string StartMenuShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        "Programs", "Vince Productivity Tray.lnk");
    private static SetupLog? _log;

    private static int Main(string[] args)
    {
        try
        {
            _log = new SetupLog(DataRoot);
            _log.Write($"Vince Productivity Tray setup {Assembly.GetExecutingAssembly().GetName().Version}; process {Environment.ProcessId}.");
            _log.Write($"Log: {_log.Path}");
            InstallTransaction.RecoverIfNeeded(InstallJournalPath, Log);

            if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase)) return LaunchUninstallWorker();
            if (args.Contains("--uninstall-worker", StringComparer.OrdinalIgnoreCase)) return Uninstall();
            return Install(ParseUpdatePid(args));
        }
        catch (Exception exception)
        {
            _log?.Write("SETUP FAILED. Program files and app-owned shell integration were restored when possible.");
            _log?.WriteException(exception);
            Console.Error.WriteLine($"Setup failed. Program files were rolled back when possible.\n{exception.Message}");
            if (_log is not null) Console.Error.WriteLine($"Details: {_log.Path}");
            Pause();
            return 1;
        }
        finally
        {
            _log?.Dispose();
        }
    }

    private static int Install(int? updatePid)
    {
        if (updatePid is not null) WaitForApprovedUpdateProcess(updatePid.Value);
        RefuseRunningProcesses();
        DescribeLegacyState();
        var firstInstall = !File.Exists(SettingsPath);
        var hasExistingProgram = File.Exists(Path.Combine(InstallRoot, "VinceProductivityTray.exe"));

        if (updatePid is null)
        {
            Console.WriteLine("Install Vince Productivity Tray for the current Windows user?");
            Console.WriteLine($"Destination: {InstallRoot}");
            Console.WriteLine("New installations start with Windows by default. Upgrades preserve the existing startup choice. Legacy startup items are not changed.");
            if (firstInstall)
            {
                Console.WriteLine("First-install storage will use VinceUtility\\Quick Notes, Meeting Recordings, and Time Capture.");
                Console.WriteLine("These folders are inside OneDrive - Vince AS. Notes, time logs, and meeting audio saved there may sync to OneDrive and be subject to company access and retention policies.");
            }
            Console.Write("Type Y to continue: ");
            if (!string.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase))
            {
                Log("Installation cancelled by the user.");
                return 2;
            }
        }

        Directory.CreateDirectory(DataRoot);
        var vinceUtilityFolder = EnsureVinceUtilityFolder();
        if (firstInstall)
        {
            var created = FreshInstallSettings.WriteIfMissing(SettingsPath, vinceUtilityFolder);
            Log(created
                ? "First-install settings now use the three VinceUtility storage folders. Automatic recording and integrated time capture remain off."
                : "Settings appeared while setup was running and were preserved without modification.");
        }

        var enableStartupDefault = FreshInstallSettings.ShouldEnableStartup(SettingsPath, hasExistingProgram);
        var operation = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N")[..8];
        var staging = Path.Combine(DataRoot, "InstallStaging", operation);
        var rollback = Path.Combine(DataRoot, "Rollback", operation);
        var failed = Path.Combine(DataRoot, "FailedInstall", operation);
        Directory.CreateDirectory(staging);
        Log($"Extracting verified embedded payload to staging operation {operation}.");
        using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
            ?? throw new InvalidOperationException("The installer payload is missing."))
        using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
        {
            archive.ExtractToDirectory(staging);
        }

        ValidatePayload(staging);
        // Recheck after extraction as well as before prompting. The tray app can be started while
        // setup is waiting/extracting, and an executable still running from InstallRoot can make
        // the directory swap fail with a misleading access-denied error.
        RefuseRunningProcesses();
        InstallTransaction.Commit(
            staging, InstallRoot, rollback, failed, InstallJournalPath,
            () =>
            {
                ValidateInstalledApplication();
                ApplyUserShellIntegration(enableStartupDefault);
            }, Log);

        Log("Installation completed and the no-audio startup self-check passed.");
        Log(enableStartupDefault ? "Start with Windows was enabled for this new installation." : "The existing Windows startup choice was preserved.");
        Log("Legacy startup items were not changed.");
        if (updatePid is not null)
        {
            var executable = Path.Combine(InstallRoot, "VinceProductivityTray.exe");
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            Log("The updated tray application was relaunched after successful validation.");
            return 0;
        }
        Console.WriteLine();
        Console.WriteLine("You can change Start with Windows in Settings.");
        Console.WriteLine($"Setup log: {_log!.Path}");
        var launchResult = InstallCompletion.OfferLaunch(
            () => MessageBoxW(IntPtr.Zero,
                "Installation completed successfully.\n\nStart Vince Productivity Tray now?\n\n" +
                "The app will appear in the notification area by the clock (possibly under the hidden-icons arrow).",
                "Vince Productivity Tray - installation complete",
                0x00000004 | 0x00000040 | 0x00010000) == 6, // Yes/No, information, default Yes, foreground.
            () =>
            {
                var executable = Path.Combine(InstallRoot, "VinceProductivityTray.exe");
                _ = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true })
                    ?? throw new InvalidOperationException("Windows did not start the application.");
                Log("The user chose to start the tray app after installation.");
            },
            exception =>
            {
                _log!.WriteException(exception);
                Console.WriteLine("Installation is complete, but the app could not be started. Launch Vince Productivity Tray from the Start menu.");
            });
        if (launchResult == LaunchAfterInstallResult.Started) return 0;
        if (launchResult == LaunchAfterInstallResult.Declined)
            Console.WriteLine("The app was not started. You can launch Vince Productivity Tray from the Start menu later.");
        Pause();
        return 0;
    }

    private static void ValidatePayload(string root)
    {
        var required = new[]
        {
            "VinceProductivityTray.exe",
            "VinceProductivityTray.dll",
            Path.Combine("RecorderAssets", "opptak.ps1"),
            Path.Combine("RecorderAssets", "lib", "NAudio.Core.dll"),
            Path.Combine("RecorderAssets", "lib", "NAudio.Wasapi.dll"),
        };
        var missing = required.Where(relative => !File.Exists(Path.Combine(root, relative))).ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidDataException("The staged application is incomplete: " + string.Join(", ", missing));
        }
    }

    private static void ValidateInstalledApplication()
    {
        var executable = Path.Combine(InstallRoot, "VinceProductivityTray.exe");
        File.Copy(Environment.ProcessPath!, Path.Combine(InstallRoot, "Uninstall.exe"), overwrite: true);
        var token = Guid.NewGuid();
        var healthDirectory = Path.Combine(DataRoot, "Health");
        Directory.CreateDirectory(healthDirectory);
        var healthFile = Path.Combine(healthDirectory, token.ToString("N") + ".ready");

        Log("Launching the isolated startup self-check. Recording, Teams detection, hotkeys, clipboard access, and updates are disabled in this mode.");
        using var process = Process.Start(new ProcessStartInfo(executable, $"--setup-check {token:D}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Windows did not start the application self-check.");

        if (!process.WaitForExit(30_000))
        {
            throw new TimeoutException("The application self-check did not finish within 30 seconds.");
        }
        if (process.ExitCode != 0 || !File.Exists(healthFile))
        {
            throw new InvalidOperationException($"The application self-check failed with exit code {process.ExitCode}.");
        }
        File.Delete(healthFile);
        if (!File.Exists(executable))
        {
            throw new IOException("The installed launcher disappeared during validation. Check Windows Security history.");
        }
    }

    private static void DescribeLegacyState()
    {
        var spell = GetStartupValue("ChristianSpellHelper");
        var recorderItems = GetLegacyRecorderStartupFiles();
        var recorderBackedUp = File.Exists(RecorderMigrationManifestPath);
        if (spell is not null) Log("Legacy Christian Spell Helper startup entry detected; it will not be changed.");
        foreach (var item in recorderItems) Log($"Legacy meeting-recorder startup item detected; it will not be changed: {item}");
        if (recorderBackedUp && recorderItems.Count == 0)
        {
            Log("The legacy meeting-recorder startup item is already backed up/off. This state will be preserved.");
        }
    }

    private static int LaunchUninstallWorker()
    {
        var runnerDirectory = Path.Combine(DataRoot, "UninstallRunner");
        Directory.CreateDirectory(runnerDirectory);
        var runner = Path.Combine(runnerDirectory, $"Uninstall-{Guid.NewGuid():N}.exe");
        File.Copy(Environment.ProcessPath!, runner, overwrite: false);
        Process.Start(new ProcessStartInfo(runner, "--uninstall-worker") { UseShellExecute = true });
        return 0;
    }

    private static int Uninstall()
    {
        RefuseRunningProcesses();
        Console.WriteLine("Uninstall Vince Productivity Tray? Settings, notes, recordings, logs, and migration backups will be kept.");
        Console.Write("Type Y to continue: ");
        if (!string.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase)) return 2;

        string? tombstone = null;
        var shell = new WindowsUserShellIntegrationStore(StartMenuShortcutPath);
        UserShellIntegrationManager.Remove(shell, () =>
        {
            if (!Directory.Exists(InstallRoot)) return;
            tombstone = Path.Combine(DataRoot, "UninstallPending", DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
            Directory.CreateDirectory(Path.GetDirectoryName(tombstone)!);
            Directory.Move(InstallRoot, tombstone);
        });
        if (tombstone is not null) Log($"Application files moved to recoverable location {tombstone}.");
        Log("Uninstall completed. Legacy startup backups were preserved and not restored automatically.");
        Pause();
        return 0;
    }

    private static string EnsureVinceUtilityFolder()
    {
        string? target = null;
        if (File.Exists(VinceUtilityFolderStatePath))
        {
            var remembered = File.ReadAllText(VinceUtilityFolderStatePath).Trim();
            if (Path.IsPathFullyQualified(remembered)) target = Path.GetFullPath(remembered);
        }

        if (target is null)
        {
            var root = OrganizationFolderResolver.ResolveUniqueVinceRoot(DiscoverBusinessOneDriveCandidates());
            if (root is null)
            {
                Console.WriteLine();
                Console.WriteLine("Setup could not uniquely identify this user's OneDrive - Vince AS folder.");
                Console.Write("Enter the existing OneDrive - Vince AS root folder (or leave blank to cancel): ");
                var answer = Console.ReadLine()?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(answer))
                    throw new OperationCanceledException("Installation cancelled because the Vince AS OneDrive location was not selected.");
                root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(answer));
                if (!Directory.Exists(root))
                    throw new DirectoryNotFoundException("The selected Vince AS OneDrive root does not exist.");
            }
            target = string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(root)), "VinceUtility", StringComparison.OrdinalIgnoreCase)
                ? root : Path.Combine(root, "VinceUtility");
        }

        OrganizationFolderResolver.EnsureVinceUtilityStructure(target);
        var temporary = VinceUtilityFolderStatePath + ".tmp";
        File.WriteAllText(temporary, target);
        File.Move(temporary, VinceUtilityFolderStatePath, overwrite: true);
        Log($"VinceUtility folder and the Quick Notes, Meeting Recordings, and Time Capture subfolders are present: {target}.");
        Log("Existing contents and configured app data locations were not changed. No files were migrated.");
        return target;
    }

    private static void ApplyUserShellIntegration(bool enableStartup)
    {
        var executable = Path.Combine(InstallRoot, "VinceProductivityTray.exe");
        var uninstaller = Path.Combine(InstallRoot, "Uninstall.exe");
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var estimatedBytes = Directory.EnumerateFiles(InstallRoot, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
        var registration = new InstalledAppRegistration(
            "Vince Productivity Tray",
            version,
            "Vince AS",
            executable + ",0",
            InstallRoot,
            $"\"{uninstaller}\" --uninstall",
            checked((int)Math.Min(int.MaxValue, (estimatedBytes + 1023) / 1024)));
        var shortcut = new StartMenuShortcut(
            StartMenuShortcutPath,
            executable,
            InstallRoot,
            executable + ",0",
            "Open Vince Productivity Tray");
        UserShellIntegrationManager.Apply(
            new WindowsUserShellIntegrationStore(StartMenuShortcutPath), registration, shortcut, enableStartup);
        Log("Per-user Installed apps registration and Start-menu shortcut are present.");
    }

    private static IEnumerable<OneDriveCandidate> DiscoverBusinessOneDriveCandidates()
    {
        var commercial = Environment.GetEnvironmentVariable("OneDriveCommercial");
        if (!string.IsNullOrWhiteSpace(commercial)) yield return new OneDriveCandidate(commercial, true, null);

        using var accounts = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts", writable: false);
        if (accounts is null) yield break;
        foreach (var name in accounts.GetSubKeyNames().Where(value => value.StartsWith("Business", StringComparison.OrdinalIgnoreCase)))
        {
            using var account = accounts.OpenSubKey(name, writable: false);
            var root = account?.GetValue("UserFolder") as string;
            if (string.IsNullOrWhiteSpace(root)) continue;
            var organization = account?.GetValue("BusinessName") as string ?? account?.GetValue("TenantName") as string;
            yield return new OneDriveCandidate(root, true, organization);
        }
    }

    private static string? GetStartupValue(string name)
    {
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        return run?.GetValue(name) as string;
    }

    private static List<string> GetLegacyRecorderStartupFiles()
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (!Directory.Exists(startup)) return [];
        return Directory.EnumerateFiles(startup)
            .Where(path => Path.GetFileName(path).Contains("MoteOpptak", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void RefuseRunningProcesses()
    {
        var running = Process.GetProcesses()
            .Where(process => process.Id != Environment.ProcessId)
            .Where(process =>
            {
                try
                {
                    if (process.HasExited) return false;
                    if (process.ProcessName.Equals("VinceProductivityTray", StringComparison.OrdinalIgnoreCase) ||
                        process.ProcessName.Equals("ChristianSpellHelper", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return IsInstalledApplicationProcessPath(process.MainModule?.FileName, InstallRoot);
                }
                catch { return false; }
            })
            .ToArray();
        if (running.Length != 0)
        {
            throw new InvalidOperationException(
                "Vince Productivity Tray or the legacy spell helper is still running. " +
                "Use Exit from its tray menu, wait for it to close, and then run setup again. " +
                "Setup will not stop it or risk an active recording or unsaved draft.");
        }
        if (IsLegacyRecorderRunning())
        {
            throw new InvalidOperationException("The legacy MoteOpptak recorder is running. Stop/exit it first; setup will not terminate it.");
        }
    }

    internal static bool IsInstalledApplicationProcessPath(string? executablePath, string installRoot)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(installRoot)) return false;
        try
        {
            var fullPath = Path.GetFullPath(executablePath);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot)) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                   Path.GetFileName(fullPath).Equals("VinceProductivityTray.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsLegacyRecorderRunning()
    {
        var found = false;
        _ = EnumWindows((window, _) =>
        {
            var title = new StringBuilder(512);
            _ = GetWindowText(window, title, title.Capacity);
            var text = title.ToString();
            if (text.Contains("MoteOpptak.ahk", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Moteopptak", StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static int? ParseUpdatePid(string[] args)
    {
        var index = Array.FindIndex(args, value => value.Equals("--update-pid", StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var pid) || pid <= 0)
            throw new ArgumentException("--update-pid requires a valid process ID.");
        return pid;
    }

    private static void WaitForApprovedUpdateProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(30_000)) throw new TimeoutException("The tray app did not exit; update cancelled.");
        }
        catch (ArgumentException) { }
    }

    private static void Log(string message) => _log?.Write(message);

    private static void Pause()
    {
        Console.WriteLine("Press Enter to close.");
        Console.ReadLine();
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
}
