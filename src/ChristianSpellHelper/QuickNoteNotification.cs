namespace ChristianSpellHelper;

internal sealed class QuickNoteNotification : Form
{
    private readonly Label _message;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3500 };

    public QuickNoteNotification()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(380, 92);
        BackColor = Color.FromArgb(34, 39, 46);
        Padding = new Padding(18);
        _message = new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily, 10f),
        };
        Controls.Add(_message);
        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
        _message.Click += (_, _) => { _timer.Stop(); Hide(); };
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return parameters;
        }
    }

    public void ShowMessage(string message)
    {
        _timer.Stop();
        _message.Text = message;
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(workArea.Right - Width - 16, workArea.Bottom - Height - 16);
        Show();
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
