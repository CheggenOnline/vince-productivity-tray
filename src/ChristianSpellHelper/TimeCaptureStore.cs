using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChristianSpellHelper;

internal sealed partial class TimeCaptureStore
{
    private static readonly Dictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object PathLocksGate = new();

    public string Append(string folder, string text, decimal durationHours, string customer, DateTimeOffset now)
    {
        folder = SettingsValidator.NormalizeFolder(folder, "Time capture folder");
        text = text.Trim();
        if (text.Length == 0) throw new ArgumentException("Time entry text cannot be empty.");
        if (durationHours is < 0.5m or > 24m || durationHours * 2 != decimal.Truncate(durationHours * 2))
            throw new ArgumentException("Duration must be in half-hour steps from 0.5 to 24 hours.");

        Directory.CreateDirectory(folder);
        var local = now.DateTime;
        var path = GetWeeklyLogPath(folder, DateOnly.FromDateTime(local));
        var duration = durationHours.ToString("0.#", CultureInfo.InvariantCulture);
        var line = $"{local:yyyy-MM-dd HH:mm} - {NormalizeSingleLine(text)} | dur={duration}h";
        customer = customer.Trim();
        if (customer.Length != 0) line += $" | cust=\"{EscapeCustomer(customer)}\"";
        line += Environment.NewLine;

        lock (GetPathLock(path))
        {
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read,
                4096, FileOptions.WriteThrough);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(line);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        return path;
    }

    public IReadOnlyList<string> LoadCustomers(string folder)
    {
        var path = GetCustomersPath(folder);
        if (!File.Exists(path)) return [];
        lock (GetPathLock(path))
        {
            return File.ReadAllLines(path, Encoding.UTF8)
                .Select(value => value.Trim())
                .Where(value => value.Length != 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public void SaveCustomers(string folder, IEnumerable<string> customers)
    {
        folder = SettingsValidator.NormalizeFolder(folder, "Time capture folder");
        Directory.CreateDirectory(folder);
        var path = GetCustomersPath(folder);
        var values = customers.Select(value => value.Trim()).Where(value => value.Length != 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        lock (GetPathLock(path))
        {
            try
            {
                File.WriteAllLines(temporary, values, new UTF8Encoding(false));
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public decimal GetTodayRegisteredHours(string folder, DateOnly date)
    {
        var path = GetWeeklyLogPath(SettingsValidator.NormalizeFolder(folder, "Time capture folder"), date);
        if (!File.Exists(path)) return 0m;
        var prefix = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        decimal total = 0m;
        lock (GetPathLock(path))
        {
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var match = DurationRegex().Match(line);
                total += match.Success && decimal.TryParse(match.Groups[1].Value,
                    NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var duration)
                    ? duration : 1m;
            }
        }
        return total;
    }

    internal static string GetWeeklyLogPath(string folder, DateOnly date) => Path.Combine(folder,
        $"log_{ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue)):0000}-W{ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):00}.txt");

    internal static string GetCustomersPath(string folder) => Path.Combine(
        SettingsValidator.NormalizeFolder(folder, "Time capture folder"), "customers.txt");

    private static string NormalizeSingleLine(string value) =>
        Regex.Replace(value.Replace('\r', ' ').Replace('\n', ' '), @"\s+", " ").Trim();

    private static string EscapeCustomer(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static object GetPathLock(string path)
    {
        lock (PathLocksGate)
        {
            if (!PathLocks.TryGetValue(path, out var gate)) PathLocks[path] = gate = new object();
            return gate;
        }
    }

    [GeneratedRegex(@"(?i)\bdur\s*=\s*([0-9]+(?:\.[0-9]+)?)h\b")]
    private static partial Regex DurationRegex();
}

internal static class TimeCaptureProgress
{
    public static decimal ExpectedHours(DateTime localNow, TimeOnly workdayStart, decimal targetHours)
    {
        var elapsed = (decimal)(localNow.TimeOfDay - workdayStart.ToTimeSpan()).TotalHours;
        if (elapsed <= 0) return 0m;
        return Math.Clamp(Math.Round(elapsed * 2m, MidpointRounding.AwayFromZero) / 2m, 0m, targetHours);
    }

    public static decimal Relative(decimal registered, DateTime localNow, TimeOnly workdayStart, decimal targetHours) =>
        registered - ExpectedHours(localNow, workdayStart, targetHours);

    public static bool IsScheduledHour(DateTime localNow, int firstHour, int lastHour) =>
        localNow.Minute == 0 && localNow.Hour >= firstHour && localNow.Hour <= lastHour;
}
