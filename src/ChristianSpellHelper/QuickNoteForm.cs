namespace ChristianSpellHelper;

internal sealed class QuickNoteForm : Form
{
    private readonly TextBox _input;
    private readonly DailyNoteWriter _writer;
    private readonly Func<AppSettings> _settings;
    private bool _saving;

    public QuickNoteForm(DailyNoteWriter writer, Func<AppSettings> settings)
    {
        _writer = writer;
        _settings = settings;
        Text = "Quick note";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 520;
        Height = 130;
        KeyPreview = true;

        _input = new TextBox { Multiline = true, Dock = DockStyle.Fill, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical };
        Controls.Add(_input);
        KeyDown += HandleKeyDown;
        _input.KeyDown += HandleKeyDown;
        // Keep an unsaved draft visible when focus changes. Only a successful save or
        // an explicit Escape should close it.
    }

    public void ShowForEntry()
    {
        if (Visible) return;
        Show();
        Activate();
        _input.Focus();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            _input.Clear();
            Hide();
            return;
        }

        if (e.KeyCode != Keys.Enter || e.Shift) return;
        e.SuppressKeyPress = true;
        TrySave();
    }

    private void TrySave()
    {
        if (_saving || string.IsNullOrWhiteSpace(_input.Text)) return;
        _saving = true;
        try
        {
            _writer.Append(_settings().NotesFolder, _input.Text, DateTimeOffset.Now);
            _input.Clear();
            Hide();
        }
        catch (Exception exception)
        {
            LocalLog.Write("Quick note save failed", exception);
            MessageBox.Show(this, $"The note was not saved.\n\n{exception.Message}", "Quick note",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _input.Focus();
        }
        finally
        {
            _saving = false;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !Disposing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}
