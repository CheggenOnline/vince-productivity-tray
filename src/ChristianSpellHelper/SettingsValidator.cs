namespace ChristianSpellHelper;

internal static class SettingsValidator
{
    public static AppSettings ValidateAndNormalize(AppSettings settings)
    {
        var notesFolder = NormalizeFolder(settings.NotesFolder, "Notes folder");
        var recordingFolder = NormalizeFolder(settings.RecordingFolder, "Recording folder");
        if (notesFolder.Equals(recordingFolder, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Notes and recordings must use separate folders.");
        }

        var spellcheckHotkey = NormalizeHotkey(settings.SpellcheckHotkey, "Spellcheck hotkey", out var spellcheck);
        var notesHotkey = NormalizeHotkey(settings.NotesHotkey, "Quick note hotkey", out var notes);
        var recordingHotkey = NormalizeHotkey(settings.RecordingHotkey, "Recording hotkey", out var recording);
        var timeCaptureHotkey = NormalizeHotkey(settings.TimeCaptureHotkey, "Time capture hotkey", out var timeCapture);
        var activeHotkeys = new List<HotkeyDefinition>();
        if (settings.SpellcheckEnabled) activeHotkeys.Add(spellcheck);
        if (settings.QuickNotesEnabled) activeHotkeys.Add(notes);
        if (settings.RecordingEnabled) activeHotkeys.Add(recording);
        if (settings.TimeCaptureEnabled) activeHotkeys.Add(timeCapture);
        if (activeHotkeys.Distinct().Count() != activeHotkeys.Count)
        {
            throw new ArgumentException("Spellcheck, quick notes, recording, and time capture must use different hotkeys.");
        }

        var timeCaptureFolder = NormalizeFolder(settings.TimeCaptureFolder, "Time capture folder");
        if (timeCaptureFolder.Equals(notesFolder, StringComparison.OrdinalIgnoreCase) ||
            timeCaptureFolder.Equals(recordingFolder, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Time capture must use a folder separate from notes and recordings.");
        if (settings.TimeCaptureStartHour is < 0 or > 23 || settings.TimeCaptureEndHour is < 0 or > 23 ||
            settings.TimeCaptureStartHour > settings.TimeCaptureEndHour)
            throw new ArgumentException("Time capture popup hours must be between 0 and 23, with start no later than end.");
        if (settings.TimeCaptureSnoozeMinutes is < 1 or > 240)
            throw new ArgumentException("Time capture snooze must be between 1 and 240 minutes.");
        if (!TimeOnly.TryParseExact(settings.TimeCaptureWorkdayStart?.Trim(), "HH:mm", out _))
            throw new ArgumentException("Time capture workday start must use HH:mm, for example 09:00.");
        if (settings.TimeCaptureTargetHours is <= 0 or > 24)
            throw new ArgumentException("Time capture target must be above 0 and at most 24 hours.");

        return settings with
        {
            NotesFolder = notesFolder,
            RecordingFolder = recordingFolder,
            SpellcheckHotkey = spellcheckHotkey,
            NotesHotkey = notesHotkey,
            RecordingHotkey = recordingHotkey,
            TimeCaptureHotkey = timeCaptureHotkey,
            TimeCaptureFolder = timeCaptureFolder,
            TimeCaptureWorkdayStart = settings.TimeCaptureWorkdayStart.Trim(),
            AutoRecordMeetings = settings.RecordingEnabled && settings.AutoRecordMeetings,
        };
    }

    internal static string NormalizeFolder(string? value, string label)
    {
        var candidate = value?.Trim().Trim('"').Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException($"{label} cannot be empty.");
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate));
    }

    private static string NormalizeHotkey(string? value, string label, out HotkeyDefinition parsed)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException($"{label} cannot be empty.");
        }

        try
        {
            parsed = HotkeyDefinition.Parse(candidate);
            return candidate;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException($"{label}: {exception.Message}", exception);
        }
    }
}
