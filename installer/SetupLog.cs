namespace VinceProductivityTray.Setup;

internal sealed class SetupLog : IDisposable
{
    private readonly StreamWriter _writer;

    public SetupLog(string dataRoot)
    {
        var directory = System.IO.Path.Combine(dataRoot, "SetupLogs");
        Directory.CreateDirectory(directory);
        Path = System.IO.Path.Combine(directory,
            $"setup-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Environment.ProcessId}.log");
        _writer = new StreamWriter(new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
    }

    public string Path { get; }

    public void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {message}";
        Console.WriteLine(message);
        _writer.WriteLine(line);
    }

    public void WriteException(Exception exception) => Write(exception.ToString());

    public void Dispose() => _writer.Dispose();
}
