using ChristianSpellHelper.Core;

namespace ChristianSpellHelper;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains("--setup-check", StringComparer.OrdinalIgnoreCase))
        {
            return TryGetSetupCheckToken(args, out var setupToken) ? RunSetupCheck(setupToken) : 1;
        }

        using var mutex = new Mutex(initiallyOwned: true, @"Local\VinceProductivityTray", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Vince Productivity Tray is already running in the notification area.",
                "Vince Productivity Tray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 2;
        }

        try
        {
            using var spelling = new NativeSpellService();
            var engine = new ConservativeCorrectionEngine(spelling);
            using var context = new SpellHelperApplicationContext(
                engine,
                spelling.SupportedLanguages,
                new PowerShellRecorderController(
                    Path.Combine(AppContext.BaseDirectory, "RecorderAssets"),
                    () => AppSettings.Load().RecordingFolder,
                    () =>
                    {
                        var settings = AppSettings.Load();
                        return settings.RecordingEnabled && settings.AutoRecordMeetings;
                    }));
            Application.Run(context);
            return 0;
        }
        catch (Exception exception)
        {
            LocalLog.Write("Fatal startup failure", exception);
            MessageBox.Show(
                $"Vince Productivity Tray could not start.\n\n{exception.Message}",
                "Vince Productivity Tray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static bool TryGetSetupCheckToken(string[] args, out Guid token)
    {
        token = Guid.Empty;
        var index = Array.FindIndex(args, value => value.Equals("--setup-check", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && Guid.TryParse(args[index + 1], out token);
    }

    private static int RunSetupCheck(Guid token)
    {
        try
        {
            using var spelling = new NativeSpellService();
            _ = spelling.SupportedLanguages.Count;
            var assets = Path.Combine(AppContext.BaseDirectory, "RecorderAssets");
            foreach (var relative in new[]
            {
                "opptak.ps1",
                "felles.ps1",
                Path.Combine("lib", "NAudio.Core.dll"),
                Path.Combine("lib", "NAudio.Wasapi.dll"),
            })
            {
                if (!File.Exists(Path.Combine(assets, relative)))
                    throw new FileNotFoundException("A packaged recorder dependency is missing.", relative);
            }

            var healthDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VinceProductivityTray", "Health");
            Directory.CreateDirectory(healthDirectory);
            var path = Path.Combine(healthDirectory, token.ToString("N") + ".ready");
            File.WriteAllText(path, $"ok {DateTimeOffset.UtcNow:O}");
            return 0;
        }
        catch (Exception exception)
        {
            LocalLog.Write("Setup startup self-check failed", exception);
            return 1;
        }
    }
}
