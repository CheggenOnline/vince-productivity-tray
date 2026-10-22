using System.Text.Json;

namespace VinceProductivityTray.Setup;

public sealed record InstallJournal(
    string OperationId,
    string StagingPath,
    string InstallPath,
    string RollbackPath,
    string FailedPath,
    bool HadPrevious,
    string State);

public static class InstallTransaction
{
    public static void RecoverIfNeeded(string journalPath, Action<string>? log = null)
    {
        if (!File.Exists(journalPath)) return;
        var journal = JsonSerializer.Deserialize<InstallJournal>(File.ReadAllText(journalPath))
            ?? throw new InvalidDataException("The install recovery journal is invalid.");
        if (journal.State is "Completed" or "Validated")
        {
            File.Delete(journalPath);
            return;
        }

        log?.Invoke($"Recovering interrupted install operation {journal.OperationId} ({journal.State}).");
        if (Directory.Exists(journal.InstallPath) && journal.State is "NewMoved")
        {
            MoveAside(journal.InstallPath, journal.FailedPath);
        }
        if (journal.HadPrevious && !Directory.Exists(journal.InstallPath) && Directory.Exists(journal.RollbackPath))
        {
            Directory.Move(journal.RollbackPath, journal.InstallPath);
        }
        File.Delete(journalPath);
    }

    public static void Commit(
        string stagingPath,
        string installPath,
        string rollbackPath,
        string failedPath,
        string journalPath,
        Action validate,
        Action<string>? log = null)
    {
        var hadPrevious = Directory.Exists(installPath);
        var journal = new InstallJournal(
            Guid.NewGuid().ToString("N"), stagingPath, installPath, rollbackPath, failedPath,
            hadPrevious, "Prepared");
        Save(journalPath, journal);

        try
        {
            if (hadPrevious)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(rollbackPath)!);
                Directory.Move(installPath, rollbackPath);
                journal = journal with { State = "PreviousMoved" };
                Save(journalPath, journal);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(installPath)!);
            Directory.Move(stagingPath, installPath);
            journal = journal with { State = "NewMoved" };
            Save(journalPath, journal);

            validate();
            journal = journal with { State = "Validated" };
            Save(journalPath, journal);
            File.Delete(journalPath);
        }
        catch
        {
            log?.Invoke("Install validation failed; restoring the previous program directory.");
            if (Directory.Exists(installPath)) MoveAside(installPath, failedPath);
            if (hadPrevious && Directory.Exists(rollbackPath) && !Directory.Exists(installPath))
            {
                Directory.Move(rollbackPath, installPath);
            }
            if (File.Exists(journalPath)) File.Delete(journalPath);
            throw;
        }
    }

    private static void MoveAside(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var actual = destination;
        if (Directory.Exists(actual)) actual += "-" + Guid.NewGuid().ToString("N");
        Directory.Move(source, actual);
    }

    private static void Save(string path, InstallJournal journal)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, overwrite: true);
    }
}
