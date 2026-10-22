using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

[assembly: InternalsVisibleTo("ChristianSpellHelper.Tests")]

namespace VinceProductivityTray.Setup;

internal sealed record InstalledAppRegistration(
    string DisplayName,
    string DisplayVersion,
    string Publisher,
    string DisplayIcon,
    string InstallLocation,
    string UninstallString,
    int EstimatedSizeKilobytes);

internal sealed record StartMenuShortcut(
    string Path,
    string TargetPath,
    string WorkingDirectory,
    string IconLocation,
    string Description);

internal sealed record RegistrySnapshotValue(string Name, object Value, RegistryValueKind Kind);

internal sealed record UserShellIntegrationSnapshot(
    IReadOnlyList<RegistrySnapshotValue>? InstalledAppValues,
    byte[]? ShortcutBytes,
    bool RunValueExisted,
    string? RunValue);

internal interface IUserShellIntegrationStore
{
    UserShellIntegrationSnapshot Capture();
    void WriteInstalledApp(InstalledAppRegistration registration);
    void WriteShortcut(StartMenuShortcut shortcut);
    void RemoveInstalledApp();
    void RemoveShortcut();
    void RemoveRunValue();
    void Restore(UserShellIntegrationSnapshot snapshot);
}

internal static class UserShellIntegrationManager
{
    public static void Apply(
        IUserShellIntegrationStore store,
        InstalledAppRegistration registration,
        StartMenuShortcut shortcut)
    {
        var snapshot = store.Capture();
        try
        {
            store.WriteInstalledApp(registration);
            store.WriteShortcut(shortcut);
        }
        catch
        {
            store.Restore(snapshot);
            throw;
        }
    }

    public static void Remove(IUserShellIntegrationStore store, Action removeProgramFiles)
    {
        var snapshot = store.Capture();
        try
        {
            store.RemoveRunValue();
            store.RemoveShortcut();
            store.RemoveInstalledApp();
            removeProgramFiles();
        }
        catch
        {
            store.Restore(snapshot);
            throw;
        }
    }
}

internal sealed class WindowsUserShellIntegrationStore : IUserShellIntegrationStore
{
    internal const string UninstallSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VinceProductivityTray";
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "VinceProductivityTray";
    private readonly string _shortcutPath;

    public WindowsUserShellIntegrationStore(string shortcutPath)
    {
        _shortcutPath = Path.GetFullPath(shortcutPath);
    }

    public UserShellIntegrationSnapshot Capture()
    {
        IReadOnlyList<RegistrySnapshotValue>? installedApp = null;
        using (var key = Registry.CurrentUser.OpenSubKey(UninstallSubKey, writable: false))
        {
            if (key is not null)
            {
                installedApp = key.GetValueNames()
                    .Select(name => new RegistrySnapshotValue(name, key.GetValue(name)!, key.GetValueKind(name)))
                    .ToArray();
            }
        }

        var shortcut = File.Exists(_shortcutPath) ? File.ReadAllBytes(_shortcutPath) : null;
        using var run = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        var runExisted = run?.GetValueNames().Contains(RunValueName, StringComparer.OrdinalIgnoreCase) == true;
        return new UserShellIntegrationSnapshot(
            installedApp,
            shortcut,
            runExisted,
            runExisted ? run!.GetValue(RunValueName) as string : null);
    }

    public void WriteInstalledApp(InstalledAppRegistration registration)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallSubKey, writable: true)
            ?? throw new IOException("The per-user Installed apps registration could not be opened.");
        key.SetValue("DisplayName", registration.DisplayName, RegistryValueKind.String);
        key.SetValue("DisplayVersion", registration.DisplayVersion, RegistryValueKind.String);
        key.SetValue("Publisher", registration.Publisher, RegistryValueKind.String);
        key.SetValue("DisplayIcon", registration.DisplayIcon, RegistryValueKind.String);
        key.SetValue("InstallLocation", registration.InstallLocation, RegistryValueKind.String);
        key.SetValue("UninstallString", registration.UninstallString, RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", Math.Max(0, registration.EstimatedSizeKilobytes), RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
    }

    public void WriteShortcut(StartMenuShortcut shortcut)
    {
        var directory = Path.GetDirectoryName(shortcut.Path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory,
            Path.GetFileNameWithoutExtension(shortcut.Path) + "." + Guid.NewGuid().ToString("N") + ".lnk");
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            var shell = Activator.CreateInstance(shellType)!;
            object? link = null;
            try
            {
                link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [temporary]);
                var linkType = link!.GetType();
                Set(linkType, link, "TargetPath", shortcut.TargetPath);
                Set(linkType, link, "WorkingDirectory", shortcut.WorkingDirectory);
                Set(linkType, link, "IconLocation", shortcut.IconLocation);
                Set(linkType, link, "Description", shortcut.Description);
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
                if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }

            File.Move(temporary, shortcut.Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void RemoveInstalledApp() => Registry.CurrentUser.DeleteSubKeyTree(UninstallSubKey, throwOnMissingSubKey: false);

    public void RemoveShortcut()
    {
        if (File.Exists(_shortcutPath)) File.Delete(_shortcutPath);
    }

    public void RemoveRunValue()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
        run?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    public void Restore(UserShellIntegrationSnapshot snapshot)
    {
        RemoveInstalledApp();
        if (snapshot.InstalledAppValues is not null)
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallSubKey, writable: true)
                ?? throw new IOException("The previous Installed apps registration could not be restored.");
            foreach (var value in snapshot.InstalledAppValues)
                key.SetValue(value.Name, value.Value, value.Kind);
        }

        RemoveShortcut();
        if (snapshot.ShortcutBytes is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_shortcutPath)!);
            File.WriteAllBytes(_shortcutPath, snapshot.ShortcutBytes);
        }

        using var run = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true);
        if (snapshot.RunValueExisted) run?.SetValue(RunValueName, snapshot.RunValue ?? string.Empty, RegistryValueKind.String);
        else run?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private static void Set(Type type, object instance, string property, string value) =>
        type.InvokeMember(property, BindingFlags.SetProperty, null, instance, [value]);
}
