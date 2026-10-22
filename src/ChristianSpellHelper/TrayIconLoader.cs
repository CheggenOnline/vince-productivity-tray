namespace ChristianSpellHelper;

internal static class TrayIconLoader
{
    public static Icon Load()
    {
        using var stream = typeof(TrayIconLoader).Assembly.GetManifestResourceStream(
            "ChristianSpellHelper.Assets.vince-tray.ico")
            ?? throw new InvalidOperationException("The embedded Vince tray icon is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
