using System.Text.Json;

namespace ChristianSpellHelper;

internal sealed record AppSettings
{
    public bool RecordingEnabled { get; init; } = true;
    public bool QuickNotesEnabled { get; init; } = true;
    public bool SpellcheckEnabled { get; init; } = true;
    public string NotesFolder { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Quick Notes");
    public string RecordingFolder { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Meeting Recordings");
    public string SpellcheckHotkey { get; init; } = "Ctrl+Shift+X";
    public string NotesHotkey { get; init; } = "Ctrl+Alt+N";
    public string RecordingHotkey { get; init; } = "Ctrl+Alt+R";
    public bool AutoRecordMeetings { get; init; }
    public bool TimeCaptureEnabled { get; init; }
    public bool TimeCaptureUseCustomerList { get; init; } = true;
    public string TimeCaptureHotkey { get; init; } = "Ctrl+Shift+A";
    public string TimeCaptureFolder { get; init; } = @"C:\TimeLog";
    public bool TimeCaptureHourlyPopup { get; init; } = true;
    public int TimeCaptureStartHour { get; init; } = 8;
    public int TimeCaptureEndHour { get; init; } = 16;
    public int TimeCaptureSnoozeMinutes { get; init; } = 10;
    public string TimeCaptureWorkdayStart { get; init; } = "09:00";
    public decimal TimeCaptureTargetHours { get; init; } = 8m;
    public bool StartWithWindows { get; init; } = true;
    public bool AutoUpdateEnabled { get; init; }
    public DateTimeOffset? DeferredUpdateUntilUtc { get; init; }

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VinceProductivityTray", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception exception)
        {
            LocalLog.Write("Settings could not be read; defaults are in use", exception);
            return new AppSettings();
        }
    }

    public void Save()
    {
        SaveTo(SettingsPath);
    }

    internal void SaveTo(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(
                       temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       bufferSize: 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

internal sealed record TrayActionState(string RecordingText, bool RecordingEnabled, bool NotesEnabled, bool TimeCaptureEnabled);

internal static class FeatureGate
{
    public static TrayActionState TrayActions(AppSettings settings, bool isRecording, bool recorderAvailable) => new(
        isRecording ? "Stop Recording" : "Start Recording",
        isRecording || (settings.RecordingEnabled && recorderAvailable),
        settings.QuickNotesEnabled,
        settings.TimeCaptureEnabled);
}
