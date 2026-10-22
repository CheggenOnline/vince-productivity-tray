using System.Globalization;

namespace ChristianSpellHelper;

internal sealed class TimeCaptureForm : Form
{
    private readonly TimeCaptureStore _store;
    private readonly Func<AppSettings> _settings;
    private readonly TextBox _entry = new() { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _duration = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _customer = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 322 };
    private readonly Label _progress = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _snoozeButton = new() { Text = "Snooze", AutoSize = true };
    private bool _saving;

    public TimeCaptureForm(TimeCaptureStore store, Func<AppSettings> settings)
    {
        _store = store;
        _settings = settings;
        Text = "Time capture";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily, 10f);
        MinimumSize = new Size(420, 430);
        ClientSize = new Size(440, 440);
        _duration.Items.AddRange(["0.5", "1", "2", "3", "4", "5", "6", "7", "8"]);
        _duration.SelectedItem = "1";

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 9 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(new Label { Text = "Relative Progress:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _progress.Font = new Font(Font, FontStyle.Bold);
        table.Controls.Add(_progress, 0, 1);
        table.Controls.Add(new Label { Text = "What are you working on right now?", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        table.Controls.Add(_entry, 0, 3);
        table.Controls.Add(new Label { Text = "Duration (hours):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        table.Controls.Add(_duration, 0, 5);
        table.Controls.Add(new Label { Text = "Customer:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        var customerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 3, 0, 3) };
        customerPanel.Controls.Add(_customer);
        var manage = new Button { Text = "⚙", Width = 38, Height = 30, AccessibleName = "Manage customers" };
        manage.Click += (_, _) => ManageCustomers();
        customerPanel.Controls.Add(manage);
        table.Controls.Add(customerPanel, 0, 7);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 12, 0, 0) };
        var submit = new Button { Text = "Submit", Width = 100, Height = 34 };
        var cancel = new Button { Text = "Hide", Width = 76, Height = 34 };
        submit.Click += (_, _) => TrySave();
        _snoozeButton.Click += (_, _) => { Hide(); Snoozed?.Invoke(this, EventArgs.Empty); };
        cancel.Click += (_, _) => Hide();
        _snoozeButton.Height = 34;
        buttons.Controls.Add(submit); buttons.Controls.Add(_snoozeButton); buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, 8);
        Controls.Add(table);
        AcceptButton = submit;
        KeyDown += (_, eventArgs) => { if (eventArgs.KeyCode == Keys.Escape) { eventArgs.Handled = true; Hide(); } };
        FormClosing += (_, eventArgs) => { if (eventArgs.CloseReason == CloseReason.UserClosing) { eventArgs.Cancel = true; Hide(); } };
    }

    public event EventHandler? Snoozed;

    public bool HasUnsavedDraft => !string.IsNullOrWhiteSpace(_entry.Text);

    public void ShowForEntry()
    {
        RefreshFromDisk();
        if (!Visible) Show();
        Activate();
        _entry.Focus();
    }

    private void RefreshFromDisk()
    {
        var settings = _settings();
        var selected = _customer.Text;
        _customer.Items.Clear();
        _customer.Items.AddRange(_store.LoadCustomers(settings.TimeCaptureFolder).Cast<object>().ToArray());
        if (selected.Length != 0) _customer.Text = selected;
        else if (_customer.Items.Count != 0) _customer.SelectedIndex = 0;
        _snoozeButton.Text = $"Snooze {settings.TimeCaptureSnoozeMinutes} min";
        var start = TimeOnly.ParseExact(settings.TimeCaptureWorkdayStart, "HH:mm", CultureInfo.InvariantCulture);
        var now = DateTime.Now;
        var registered = _store.GetTodayRegisteredHours(settings.TimeCaptureFolder, DateOnly.FromDateTime(now));
        var value = TimeCaptureProgress.Relative(registered, now, start, settings.TimeCaptureTargetHours);
        _progress.Text = value > 0 ? $"+{value:0.#} h" : $"{value:0.#} h";
        _progress.ForeColor = value < 0 ? Color.Firebrick : value > 0 ? Color.ForestGreen : SystemColors.ControlText;
    }

    private void TrySave()
    {
        if (_saving) return;
        _saving = true;
        try
        {
            var duration = decimal.Parse(_duration.Text, CultureInfo.InvariantCulture);
            _store.Append(_settings().TimeCaptureFolder, _entry.Text, duration, _customer.Text, DateTimeOffset.Now);
            _entry.Clear();
            Hide();
        }
        catch (Exception exception)
        {
            LocalLog.Write("Time capture save failed", exception);
            MessageBox.Show(this, $"The time entry was not saved.\n\n{exception.Message}", "Time capture",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _entry.Focus();
        }
        finally { _saving = false; }
    }

    private void ManageCustomers()
    {
        using var form = new CustomerManagerForm(_store, _settings().TimeCaptureFolder);
        var wasTopMost = CustomerDialogLayering.Prepare(this, form);
        try
        {
            form.ShowDialog(this);
            RefreshFromDisk();
        }
        finally
        {
            CustomerDialogLayering.Restore(this, wasTopMost);
            Activate();
            _customer.Focus();
        }
    }
}

internal static class CustomerDialogLayering
{
    public static bool Prepare(Form owner, Form dialog)
    {
        var wasTopMost = owner.TopMost;
        owner.TopMost = false;
        dialog.TopMost = wasTopMost;
        dialog.ShowInTaskbar = false;
        dialog.StartPosition = FormStartPosition.CenterParent;
        return wasTopMost;
    }

    public static void Restore(Form owner, bool wasTopMost) => owner.TopMost = wasTopMost;
}

internal sealed class CustomerManagerForm : Form
{
    private readonly TimeCaptureStore _store;
    private readonly string _folder;
    private readonly ListBox _customers = new() { Dock = DockStyle.Fill };
    private readonly TextBox _newCustomer = new() { Dock = DockStyle.Fill };

    public CustomerManagerForm(TimeCaptureStore store, string folder)
    {
        _store = store; _folder = folder;
        Text = "Customers";
        Width = 490; Height = 390; MinimumSize = new Size(450, 340);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily, 10f);
        _customers.Items.AddRange(_store.LoadCustomers(folder).Cast<object>().ToArray());
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = "Customers (stored in customers.txt, one per line):", AutoSize = true }, 0, 0); table.SetColumnSpan(table.GetControlFromPosition(0, 0)!, 2);
        table.Controls.Add(_customers, 0, 1); table.SetColumnSpan(_customers, 2);
        table.Controls.Add(new Label { Text = "Add customer:", AutoSize = true }, 0, 2); table.SetColumnSpan(table.GetControlFromPosition(0, 2)!, 2);
        table.Controls.Add(_newCustomer, 0, 3);
        var add = new Button { Text = "Add", AutoSize = true };
        add.Click += (_, _) => AddCustomer();
        table.Controls.Add(add, 1, 3);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Save", AutoSize = true };
        var remove = new Button { Text = "Remove", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true };
        save.Click += (_, _) => _store.SaveCustomers(_folder, _customers.Items.Cast<string>());
        remove.Click += (_, _) => { if (_customers.SelectedIndex >= 0) _customers.Items.RemoveAt(_customers.SelectedIndex); };
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close); buttons.Controls.Add(save); buttons.Controls.Add(remove);
        table.Controls.Add(buttons, 0, 4); table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = add;
        Shown += (_, _) => { Activate(); _newCustomer.Focus(); };
    }

    private void AddCustomer()
    {
        var value = _newCustomer.Text.Trim();
        if (value.Length == 0 || _customers.Items.Cast<string>().Contains(value, StringComparer.OrdinalIgnoreCase)) return;
        _customers.Items.Add(value); _newCustomer.Clear();
    }
}
