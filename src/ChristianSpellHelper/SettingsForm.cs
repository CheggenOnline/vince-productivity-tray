using System.Globalization;

namespace ChristianSpellHelper;

internal sealed record SettingsActions(
    string Version, Func<string> ApplicationStatus, string SpellcheckStatus,
    Func<string> UpdateStatus, string UpdateActionText, Func<bool> CanUseUpdateAction,
    Func<Task> CheckForUpdates, Func<Task> UseUpdateAction,
    Action OpenNotes, Action OpenRecordings, Action OpenTimeLogs,
    Action OpenDiagnostics);

internal sealed class SettingsForm : Form
{
    private readonly DateTimeOffset? _deferredUpdateUntilUtc;
    private readonly Func<string, HotkeyProbeResult> _probe;
    private readonly CheckBox _recordingEnabled, _notesEnabled, _spellEnabled, _timeEnabled, _autoUpdate;
    private readonly TextBox _notes, _recordings, _spellHotkey, _noteHotkey, _recordHotkey, _timeHotkey, _timeFolder, _workdayStart;
    private readonly CheckBox _autoRecord, _startup, _hourlyPopup;
    private readonly NumericUpDown _popupStart, _popupEnd, _snooze, _target;

    public SettingsForm(AppSettings current, Func<string, HotkeyProbeResult> probe) : this(current, probe, null) { }

    public SettingsForm(AppSettings current, Func<string, HotkeyProbeResult> probe, SettingsActions? actions)
    {
        _deferredUpdateUntilUtc = current.DeferredUpdateUntilUtc;
        _probe = probe;
        Text = "Vince Productivity Tray settings";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(940, 700);
        MinimumSize = new Size(820, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily, 10f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(14) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 7) };
        tabs.TabPages.Add(BuildGeneralTab(current, actions, out _startup, out _autoUpdate));

        var recordingTab = FeatureTab("Recording", "Enable recording", current.RecordingEnabled, out _recordingEnabled, out var recording);
        _recordings = AddFolderRow(recording, 0, "Recording folder", current.RecordingFolder);
        _recordHotkey = AddHotkeyRow(recording, 1, "Recording hotkey", current.RecordingHotkey);
        _autoRecord = AddCheckRow(recording, 2, "Autorecord meetings", current.AutoRecordMeetings,
            "Starts only on a detected Teams microphone-use transition; default is off.");
        AddHelp(recording, 3, "Disabling recording removes its hotkey and meeting detection. It never stops an active recording.");
        AddActionRow(recording, 4, "Open recordings folder", actions?.OpenRecordings);
        tabs.TabPages.Add(recordingTab);

        var notesTab = FeatureTab("Quick Notes", "Enable quick notes", current.QuickNotesEnabled, out _notesEnabled, out var notes);
        _notes = AddFolderRow(notes, 0, "Notes folder", current.NotesFolder);
        _noteHotkey = AddHotkeyRow(notes, 1, "Quick note hotkey", current.NotesHotkey);
        AddHelp(notes, 2, "Enter saves the UTF-8 daily note, Shift+Enter adds a line, and Escape cancels.");
        AddActionRow(notes, 3, "Open notes folder", actions?.OpenNotes);
        tabs.TabPages.Add(notesTab);

        var timeTab = FeatureTab("Time Capture", "Enable integrated time capture", current.TimeCaptureEnabled, out _timeEnabled, out var time);
        _timeFolder = AddFolderRow(time, 0, "Time log folder", current.TimeCaptureFolder);
        _timeHotkey = AddHotkeyRow(time, 1, "Time capture hotkey", current.TimeCaptureHotkey);
        _hourlyPopup = AddCheckRow(time, 2, "Hourly popup", current.TimeCaptureHourlyPopup, "At minute 00 within the configured hour range.");
        _popupStart = AddNumberRow(time, 3, "First popup hour", current.TimeCaptureStartHour, 0, 23);
        _popupEnd = AddNumberRow(time, 4, "Last popup hour", current.TimeCaptureEndHour, 0, 23);
        _snooze = AddNumberRow(time, 5, "Snooze minutes", current.TimeCaptureSnoozeMinutes, 1, 240);
        _workdayStart = AddTextRow(time, 6, "Workday starts", current.TimeCaptureWorkdayStart);
        _target = AddNumberRow(time, 7, "Daily target hours", current.TimeCaptureTargetHours, 0.5m, 24m, 0.5m);
        AddHelp(time, 8, "Keep off while legacy TimeCapture.ahk is active to avoid duplicate popups and hotkeys.");
        AddActionRow(time, 9, "Open time log folder", actions?.OpenTimeLogs);
        tabs.TabPages.Add(timeTab);

        var spellTab = FeatureTab("Spell Check", "Enable spell check", current.SpellcheckEnabled, out _spellEnabled, out var spell);
        _spellHotkey = AddHotkeyRow(spell, 0, "Spellcheck hotkey", current.SpellcheckHotkey);
        AddHelp(spell, 1, "Uses installed Windows English/Norwegian dictionaries only. Text is not sent to an API. " +
            (actions?.SpellcheckStatus ?? string.Empty));
        tabs.TabPages.Add(spellTab);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), WrapContents = false };
        var save = new Button { Text = "Save", AutoSize = true, MinimumSize = new Size(96, 36) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(96, 36) };
        save.Click += (_, _) =>
        {
            try { _ = GetSettings(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            { MessageBox.Show(this, exception.Message, "Invalid settings", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel);
        root.Controls.Add(tabs, 0, 0); root.Controls.Add(buttons, 0, 1); Controls.Add(root);
        AcceptButton = save; CancelButton = cancel;
    }

    public AppSettings GetSettings() => SettingsValidator.ValidateAndNormalize(new AppSettings
    {
        RecordingEnabled = _recordingEnabled.Checked, QuickNotesEnabled = _notesEnabled.Checked,
        SpellcheckEnabled = _spellEnabled.Checked, TimeCaptureEnabled = _timeEnabled.Checked,
        NotesFolder = _notes.Text, RecordingFolder = _recordings.Text,
        SpellcheckHotkey = _spellHotkey.Text, NotesHotkey = _noteHotkey.Text, RecordingHotkey = _recordHotkey.Text,
        AutoRecordMeetings = _autoRecord.Checked, StartWithWindows = _startup.Checked,
        AutoUpdateEnabled = _autoUpdate.Enabled && _autoUpdate.Checked,
        TimeCaptureFolder = _timeFolder.Text, TimeCaptureHotkey = _timeHotkey.Text,
        TimeCaptureHourlyPopup = _hourlyPopup.Checked, TimeCaptureStartHour = decimal.ToInt32(_popupStart.Value),
        TimeCaptureEndHour = decimal.ToInt32(_popupEnd.Value), TimeCaptureSnoozeMinutes = decimal.ToInt32(_snooze.Value),
        TimeCaptureWorkdayStart = _workdayStart.Text, TimeCaptureTargetHours = _target.Value,
        DeferredUpdateUntilUtc = _deferredUpdateUntilUtc,
    });

    private static TabPage BuildGeneralTab(AppSettings current, SettingsActions? actions,
        out CheckBox startup, out CheckBox autoUpdate)
    {
        var page = new TabPage("General") { Padding = new Padding(18), AutoScroll = true };
        var c = Content();
        AddHeading(c, 0, "Application");
        var version = new Label { Text = $"Version: {actions?.Version ?? "development"}", AutoSize = true, Anchor = AnchorStyles.Left };
        c.Controls.Add(version, 0, 1); c.SetColumnSpan(version, 4); Row(c, 1);
        if (actions is not null) version.Text += $"    Status: {actions.ApplicationStatus()}";
        var credit = new Label
        {
            Text = "developed by Christian Holberg Heggen",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            MinimumSize = new Size(420, 32),
        };
        Row(c, 2, 48); c.Controls.Add(credit, 0, 2); c.SetColumnSpan(credit, 4);
        startup = AddCheckRow(c, 3, "Start with Windows", current.StartWithWindows, "Current user; enabling asks for separate confirmation.");
        autoUpdate = AddCheckRow(c, 4, "Auto update", current.AutoUpdateEnabled && UpdaterPolicy.IsAutoInstallConfigured,
            UpdaterPolicy.IsAutoInstallConfigured ? "Install verified signed updates only while recording and drafts are idle." : "Unavailable until repository and Authenticode publisher are configured.");
        autoUpdate.Enabled = UpdaterPolicy.IsAutoInstallConfigured;
        var status = new Label { Text = actions?.UpdateStatus() ?? "Update integration unavailable in this view.", AutoSize = false, Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText };
        Row(c, 5, 58); c.Controls.Add(status, 0, 5); c.SetColumnSpan(status, 4);
        var updates = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var check = new Button { Text = "Check for updates", AutoSize = true, MinimumSize = new Size(150, 36), Enabled = actions is not null };
        var install = new Button { Text = actions?.UpdateActionText ?? "Install update", AutoSize = true, MinimumSize = new Size(150, 36), Enabled = actions?.CanUseUpdateAction() == true };
        check.Click += async (_, _) => { if (actions is null) return; await actions.CheckForUpdates(); status.Text = actions.UpdateStatus(); install.Enabled = actions.CanUseUpdateAction(); };
        install.Click += async (_, _) => { if (actions is not null) await actions.UseUpdateAction(); };
        updates.Controls.Add(check); updates.Controls.Add(install); Row(c, 6, 52); c.Controls.Add(updates, 0, 6); c.SetColumnSpan(updates, 4);
        AddActionRow(c, 7, "Open diagnostics folder", actions?.OpenDiagnostics);
        page.Controls.Add(c); return page;
    }

    private static TabPage FeatureTab(string title, string toggleText, bool enabled, out CheckBox toggle, out TableLayoutPanel content)
    {
        var page = new TabPage(title) { Padding = new Padding(18), AutoScroll = true };
        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var toggleControl = new CheckBox { Text = toggleText, Checked = enabled, AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), ForeColor = Color.FromArgb(216, 94, 0), Anchor = AnchorStyles.Left };
        var contentPanel = Content(); contentPanel.Enabled = enabled;
        toggleControl.CheckedChanged += (_, _) => contentPanel.Enabled = toggleControl.Checked;
        toggle = toggleControl; content = contentPanel;
        root.Controls.Add(toggleControl, 0, 0); root.Controls.Add(contentPanel, 0, 1); page.Controls.Add(root); return page;
    }

    private static TableLayoutPanel Content()
    {
        var c = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(0, 0, 8, 16) };
        c.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260)); c.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        c.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); c.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175)); return c;
    }

    private TextBox AddHotkeyRow(TableLayoutPanel t, int r, string label, string value)
    {
        var box = AddTextRow(t, r, label, value);
        var check = new Button { Text = "Check hotkey", Dock = DockStyle.Fill, Margin = new Padding(4, 5, 4, 5) };
        var result = new Label { Text = "Not checked", AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        check.Click += (_, _) => { var p = _probe(box.Text); result.Text = p.Message; result.ForeColor = p.Status == HotkeyProbeStatus.Available ? Color.ForestGreen : p.Status == HotkeyProbeStatus.AlreadyUsedByThisApp ? Color.DarkOrange : Color.Firebrick; };
        t.Controls.Add(check, 2, r); t.Controls.Add(result, 3, r); return box;
    }

    private static TextBox AddFolderRow(TableLayoutPanel t, int r, string label, string value)
    {
        var box = AddTextRow(t, r, label, value); var browse = new Button { Text = "Browse...", Dock = DockStyle.Fill, Margin = new Padding(4, 5, 4, 5) };
        browse.Click += (_, _) => { using var d = new FolderBrowserDialog { InitialDirectory = box.Text }; if (d.ShowDialog() == DialogResult.OK) box.Text = d.SelectedPath; };
        t.Controls.Add(browse, 2, r); return box;
    }

    private static TextBox AddTextRow(TableLayoutPanel t, int r, string label, string value)
    {
        Row(t, r); t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 10, 4, 4) }, 0, r);
        var box = new TextBox { Text = value, Dock = DockStyle.Fill, Margin = new Padding(4, 7, 4, 7) }; t.Controls.Add(box, 1, r); return box;
    }

    private static CheckBox AddCheckRow(TableLayoutPanel t, int r, string label, bool value, string help)
    {
        Row(t, r); t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
        var check = new CheckBox { Checked = value, AutoSize = true, Anchor = AnchorStyles.Left, Text = help }; t.Controls.Add(check, 1, r); t.SetColumnSpan(check, 3); return check;
    }

    private static NumericUpDown AddNumberRow(TableLayoutPanel t, int r, string label, decimal value, decimal min, decimal max, decimal increment = 1m)
    {
        Row(t, r); t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
        var n = new NumericUpDown { Value = Math.Clamp(value, min, max), Minimum = min, Maximum = max, Increment = increment, DecimalPlaces = increment < 1 ? 1 : 0, Width = 120, Anchor = AnchorStyles.Left }; t.Controls.Add(n, 1, r); return n;
    }

    private static void AddActionRow(TableLayoutPanel t, int r, string text, Action? action)
    {
        Row(t, r, 52);
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(175, 36), Enabled = action is not null };
        button.Click += (_, _) => action?.Invoke();
        t.Controls.Add(button, 1, r); t.SetColumnSpan(button, 2);
    }

    private static void AddHeading(TableLayoutPanel t, int r, string text)
    { Row(t, r, 48); var l = new Label { Text = text, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), ForeColor = Color.FromArgb(216, 94, 0), AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 12, 0, 4) }; t.Controls.Add(l, 0, r); t.SetColumnSpan(l, 4); }
    private static void AddHelp(TableLayoutPanel t, int r, string text)
    { Row(t, r, 62); var l = new Label { Text = text, AutoSize = false, Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 8, 0, 8) }; t.Controls.Add(l, 0, r); t.SetColumnSpan(l, 4); }
    private static void Row(TableLayoutPanel t, int r, int height = 48)
    { while (t.RowStyles.Count <= r) t.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); }
}
