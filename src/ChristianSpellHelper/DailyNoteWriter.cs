using System.Text;

namespace ChristianSpellHelper;

internal sealed class DailyNoteWriter
{
    private readonly object _gate = new();

    public string Append(string folder, string text, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("The note is empty.", nameof(text));
        lock (_gate)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, timestamp.ToString("yyyy-MM-dd") + ".txt");
            var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var entry = $"[{timestamp:HH:mm:ss}] {normalized}{Environment.NewLine}";
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(entry);
            writer.Flush();
            stream.Flush(flushToDisk: true);
            return path;
        }
    }
}
