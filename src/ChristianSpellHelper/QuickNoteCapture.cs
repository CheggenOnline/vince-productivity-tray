using System.Runtime.InteropServices;

namespace ChristianSpellHelper;

internal enum NoteSelectionStatus { Empty, Selected, Unavailable }
internal sealed record NoteSelection(NoteSelectionStatus Status, string Text = "");

internal static class QuickNoteSelectionReader
{
    public static NoteSelection Read(HotkeyDefinition hotkey)
    {
        try
        {
            var initial = SelectionIdentity.Capture();
            if (initial.IsPassword || initial.ForegroundWindow == IntPtr.Zero)
                return new(NoteSelectionStatus.Unavailable);
            if (!KeyboardInput.WaitForHotkeyRelease(hotkey))
                return new(NoteSelectionStatus.Unavailable);
            var current = SelectionIdentity.Capture();
            return Inspect(initial, current);
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            LocalLog.Write("Quick note selection could not be read", exception);
            return new(NoteSelectionStatus.Unavailable);
        }
    }

    internal static NoteSelection Inspect(SelectionIdentity initial, SelectionIdentity current)
    {
        if (initial.IsPassword || initial.ForegroundWindow == IntPtr.Zero || !initial.MatchesTarget(current))
            return new(NoteSelectionStatus.Unavailable);
        // Read the actual accessibility selection, never stale clipboard contents or
        // Ctrl+C in an editor that may copy an entire line when nothing is selected.
        if (current.AutomationSelectedText is null || current.AutomationSelectedText.Length >= 20_001 ||
            initial.AutomationSelectedText != current.AutomationSelectedText)
            return new(NoteSelectionStatus.Unavailable); // Never silently save truncated text.
        return string.IsNullOrWhiteSpace(current.AutomationSelectedText)
            ? new(NoteSelectionStatus.Empty)
            : new(NoteSelectionStatus.Selected, current.AutomationSelectedText);
    }
}

internal sealed class QuickNoteCapture
{
    private readonly DailyNoteWriter _writer;
    private readonly Func<AppSettings> _settings;
    private readonly Func<NoteSelection> _read;
    private readonly Action _openEditor;
    private readonly Action<string> _notify;
    private readonly Action<Exception> _logFailure;
    private bool _active;

    public QuickNoteCapture(DailyNoteWriter writer, Func<AppSettings> settings,
        Func<NoteSelection> read, Action openEditor, Action<string> notify, Action<Exception>? logFailure = null)
    {
        _writer = writer; _settings = settings; _read = read;
        _openEditor = openEditor; _notify = notify;
        _logFailure = logFailure ?? (exception => LocalLog.Write("Selected quick note was not saved", exception));
    }

    public void Capture()
    {
        if (_active) return;
        _active = true;
        try
        {
            var selection = _read();
            if (selection.Status != NoteSelectionStatus.Selected || string.IsNullOrWhiteSpace(selection.Text))
            {
                _openEditor();
                if (selection.Status == NoteSelectionStatus.Unavailable)
                    _notify("Selection unavailable. Enter or paste your quick note.");
                return;
            }
            _writer.Append(_settings().NotesFolder, selection.Text, DateTimeOffset.Now);
            _notify("Text added to Quick Notes.");
        }
        catch (Exception exception)
        {
            _logFailure(exception);
            _notify("Quick note could not be saved. Your selected text is unchanged.");
        }
        finally { _active = false; }
    }
}
