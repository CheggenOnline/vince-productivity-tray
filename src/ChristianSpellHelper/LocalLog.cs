namespace ChristianSpellHelper;

internal static class LocalLog
{
    private static readonly object Gate = new();

    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VinceProductivityTray",
        "Logs");

    public static string FilePath => Path.Combine(DirectoryPath, "activity.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                var suffix = exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message}";
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} | {message}{suffix}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never interfere with the hotkey.
        }
    }
}
