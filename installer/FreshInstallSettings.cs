using System.Text;
using System.Text.Json;

namespace VinceProductivityTray.Setup;

public sealed record FreshInstallStoragePaths(
    string NotesFolder,
    string RecordingFolder,
    string TimeCaptureFolder);

public static class FreshInstallSettings
{
    public static FreshInstallStoragePaths GetStoragePaths(string vinceUtilityFolder)
    {
        var root = Path.GetFullPath(vinceUtilityFolder);
        return new FreshInstallStoragePaths(
            Path.Combine(root, "Quick Notes"),
            Path.Combine(root, "Meeting Recordings"),
            Path.Combine(root, "Time Capture"));
    }

    internal static bool ShouldEnableStartup(string settingsPath, bool hasExistingProgram)
    {
        if (hasExistingProgram || !File.Exists(settingsPath)) return false;
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
        return settings.RootElement.TryGetProperty("StartWithWindows", out var value) &&
               value.ValueKind == JsonValueKind.True;
    }

    public static bool WriteIfMissing(string settingsPath, string vinceUtilityFolder)
    {
        settingsPath = Path.GetFullPath(settingsPath);
        if (File.Exists(settingsPath)) return false;

        OrganizationFolderResolver.EnsureVinceUtilityStructure(vinceUtilityFolder);
        var paths = GetStoragePaths(vinceUtilityFolder);
        var settings = new
        {
            RecordingEnabled = true,
            QuickNotesEnabled = true,
            SpellcheckEnabled = true,
            paths.NotesFolder,
            paths.RecordingFolder,
            SpellcheckHotkey = "Ctrl+Shift+X",
            NotesHotkey = "Ctrl+Alt+N",
            RecordingHotkey = "Ctrl+Alt+R",
            AutoRecordMeetings = false,
            TimeCaptureEnabled = false,
            TimeCaptureHotkey = "Ctrl+Shift+A",
            paths.TimeCaptureFolder,
            TimeCaptureHourlyPopup = true,
            TimeCaptureStartHour = 8,
            TimeCaptureEndHour = 16,
            TimeCaptureSnoozeMinutes = 10,
            TimeCaptureWorkdayStart = "09:00",
            TimeCaptureTargetHours = 8m,
            StartWithWindows = true,
            AutoUpdateEnabled = false,
            DeferredUpdateUntilUtc = (DateTimeOffset?)null,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var temporary = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            try
            {
                File.Move(temporary, settingsPath, overwrite: false);
                return true;
            }
            catch (IOException) when (File.Exists(settingsPath))
            {
                return false;
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
