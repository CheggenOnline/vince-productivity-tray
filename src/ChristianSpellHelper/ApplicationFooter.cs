namespace ChristianSpellHelper;

internal static class ApplicationFooter
{
    public const string Credit = "Developed by Christian Holberg Heggen";

    public static Control Create(Control? actions = null)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1,
            Margin = new Padding(0), Padding = new Padding(0, 8, 0, 0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(new Label
        {
            Text = Credit, AutoSize = true, Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily, 8f),
            Margin = new Padding(0, 4, 12, 4),
        }, 0, 0);
        if (actions is not null)
        {
            actions.Dock = DockStyle.None;
            actions.Anchor = AnchorStyles.Right;
            actions.AutoSize = true;
            actions.Margin = new Padding(0);
            if (actions is FlowLayoutPanel flow)
            {
                flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                flow.WrapContents = false;
                flow.Padding = Padding.Empty;
            }
            footer.Controls.Add(actions, 1, 0);
        }
        return footer;
    }
}
