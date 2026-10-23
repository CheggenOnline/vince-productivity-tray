using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using ChristianSpellHelper;
using ChristianSpellHelper.Core;
using VinceProductivityTray.Setup;

namespace ChristianSpellHelper.Tests;

internal static partial class Program
{
    private static readonly List<string> Passed = [];
    private static readonly List<string> Failed = [];
    private static readonly List<string> Notes = [];

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Run("Damerau transposition", () => Equal(1, ConservativeCorrectionEngine.DamerauLevenshtein("teh", "the")));
        Run("Protect URL", ProtectUrl);
        Run("Skip likely name", SkipLikelyName);
        Run("Reject tied suggestions", RejectTiedSuggestions);
        Run("Detect English from error rate", DetectEnglish);
        Run("Correct confident Norwegian multi-error sentence", CorrectNorwegianMultipleTypos);
        Run("Correct confident English multi-error sentence", CorrectEnglishMultipleTypos);
        Run("Prefer Bokmål when both texts are clean", PreferNorwegianBokmal);
        Run("Preserve paragraph breaks", PreserveParagraphBreaks);
        Run("Preserve Norwegian phrase repair example", PreserveNorwegianPhraseRepair);
        Run("Reject case-only replacement and prefer Norwegian diacritic", PreferNorwegianDiacriticOverCaseOnlyReplacement);
        Run("Recover Norwegian diacritic when dictionary misses the source token", RecoverMissedNorwegianDiacritic);
        Run("Norwegian diacritic recovery remains bounded", NorwegianDiacriticRecoveryIsBounded);
        Run("Quick notes preserve UTF-8 and multiline text", QuickNotesPreserveText);
        Run("Quick notes use local date at save time", QuickNotesUseSaveDate);
        Run("Quick notes serialize repeated appends", QuickNotesSerializeAppends);
        Run("Quick note close hides and can reopen", QuickNoteCloseHidesAndReopens);
        Run("Hotkey parser supports configured defaults", HotkeyDefaultsParse);
        Run("Hotkeys rebind immediately after settings save", HotkeysRebindTransactionally);
        Run("Failed hotkey rebind restores previous bindings", FailedHotkeyRebindRestoresPrevious);
        Run("Hotkey availability probe cleans up temporary registration", HotkeyProbeCleansUp);
        Run("Hotkey release requires a stable up interval", HotkeyReleaseRequiresStableInterval);
        Run("Transient input retry succeeds once and stops on terminal failure", TransientInputRetryPolicy);
        Run("Settings normalize folders and hotkeys", SettingsNormalizeValues);
        Run("Settings reject unsafe folder and hotkey collisions", SettingsRejectCollisions);
        Run("Settings save atomically as UTF-8", SettingsSaveAtomically);
        Run("Time capture preserves weekly format and escaping", TimeCapturePreservesFormat);
        Run("Time capture customers stay beside weekly logs", TimeCaptureCustomersStayBesideLogs);
        Run("Time capture progress matches legacy half-hour rules", TimeCaptureProgressMatchesLegacyRules);
        Run("Time capture schedule suppresses duplicate popups", TimeCaptureScheduleSuppressesDuplicates);
        Run("Time capture layout matches compact original flow", TimeCaptureLayoutMatchesOriginal);
        Run("Customer manager stays above its modal owner", CustomerManagerLayeringIsSafe);
        Run("Meeting autorecord requires an enabled rising edge", MeetingAutorecordRequiresRisingEdge);
        Run("Settings controls remain visible after DPI scaling", SettingsControlsRemainVisible);
        Run("Settings use five feature tabs and compatible toggles", SettingsTabsAndFeatureToggles);
        Run("Settings tab rows do not overlap", SettingsRowsDoNotOverlap);
        Run("Disabled features may reuse inactive hotkeys", DisabledFeaturesMayReuseHotkeys);
        Run("Tray actions follow feature and recording state", TrayActionsFollowFeatureState);
        Run("Vince OneDrive resolver is business-only and unambiguous", VinceOneDriveResolverIsSafe);
        Run("VinceUtility subfolders are idempotent and preserve contents", VinceUtilitySubfoldersAreSafe);
        Run("Fresh install settings use all VinceUtility folders", FreshInstallSettingsUseVinceUtility);
        Run("Existing custom settings are never overwritten", ExistingSettingsArePreserved);
        Run("Fresh install accepts existing empty VinceUtility folders", ExistingEmptyVinceUtilityFoldersAreSafe);
        Run("Missing or ambiguous Vince OneDrive requires selection", MissingOrAmbiguousOneDriveRequiresSelection);
        Run("Deferred updates honor cooldown", DeferredUpdatesHonorCooldown);
        Run("Updater accepts only approved GitHub release assets", UpdaterRestrictsAssets);
        Run("Updater separates notification from verified installation", UpdaterSeparatesModes);
        Run("Updater detects the newest published prerelease and ignores drafts", UpdaterSelectsPublishedPrerelease);
        Run("Updater uses a bounded recurring check schedule", UpdaterCheckScheduleIsBounded);
        Run("Unsigned updater presents an honest release-page action", UnsignedUpdaterActionIsHonest);
        Run("Updater refuses active recording or unsent time draft", UpdaterRefusesActiveWork);
        Run("Recorder settings preserve folder and park transcription", RecorderSettingsAreSafe);
        Run("Installer validation failure restores previous files", InstallerRollbackRestoresPrevious);
        Run("Installer recovers an interrupted replacement", InstallerRecoversInterruptedReplacement);
        Run("Installer transaction supports a repeated upgrade", InstallerSupportsRepeatedUpgrade);
        Run("Installer recognizes its running executable by installed path", InstallerRecognizesRunningExecutablePath);
        Run("Installed apps and Start-menu integration upgrades idempotently", UserShellIntegrationUpgradesIdempotently);
        Run("Shell integration install failure restores prior state", UserShellIntegrationApplyRollsBack);
        Run("Uninstall removes only app-owned shell integration", UserShellIntegrationRemovalIsScoped);
        Run("Uninstall failure restores shell integration", UserShellIntegrationRemovalRollsBack);

        var coreOnly = args.Contains("--core-only", StringComparer.OrdinalIgnoreCase);
        var nativeOnly = args.Contains("--native-only", StringComparer.OrdinalIgnoreCase);
        NativeBenchmarks? native = null;
        if (!coreOnly)
        {
            try
            {
                native = RunNativeDictionaryChecks();
            }
            catch (Exception exception)
            {
                Failed.Add($"Native dictionary checks: {exception.GetType().Name}: {exception.Message}");
            }
        }

        ClipboardRoundtripMetrics? clipboard = null;
        if (!coreOnly && !nativeOnly)
        {
            try
            {
                clipboard = RunSyntheticSelectionRoundtripWithRetry();
            }
            catch (Exception exception)
            {
                Failed.Add($"Synthetic selection roundtrip: {exception.GetType().Name}: {exception.Message}");
            }
        }
        else if (coreOnly) Notes.Add("Native dictionary and foreground-selection integration tests were intentionally skipped by --core-only.");
        else Notes.Add("Foreground-selection and clipboard integration tests were intentionally skipped by --native-only.");

        LiveHotkeyMetrics? liveHotkey = null;
        if (args.Contains("--live-hotkey", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                liveHotkey = RunLiveResidentHotkeyRoundtripWithRetry();
            }
            catch (Exception exception)
            {
                Failed.Add($"Live resident hotkey roundtrip: {exception.GetType().Name}: {exception.Message}");
            }
        }

        var previewIndex = Array.IndexOf(args, "--time-ui-preview");
        if (previewIndex >= 0 && previewIndex + 1 < args.Length)
        {
            RenderTimeCapturePreview(Path.GetFullPath(args[previewIndex + 1]));
        }
        var settingsPreviewIndex = Array.IndexOf(args, "--settings-ui-preview");
        if (settingsPreviewIndex >= 0 && settingsPreviewIndex + 1 < args.Length)
        {
            RenderSettingsPreviews(Path.GetFullPath(args[settingsPreviewIndex + 1]));
        }
        var customerPreviewIndex = Array.IndexOf(args, "--customer-ui-preview");
        if (customerPreviewIndex >= 0 && customerPreviewIndex + 1 < args.Length)
        {
            RenderCustomerManagerPreview(Path.GetFullPath(args[customerPreviewIndex + 1]));
        }

        var report = new TestReport(
            DateTimeOffset.UtcNow,
            Environment.OSVersion.VersionString,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Passed,
            Failed,
            Notes,
            native,
            clipboard,
            liveHotkey);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);

        var reportIndex = Array.IndexOf(args, "--report");
        if (reportIndex >= 0 && reportIndex + 1 < args.Length)
        {
            var reportPath = Path.GetFullPath(args[reportIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, json);
        }

        return Failed.Count == 0 ? 0 : 1;
    }

    private static void ProtectUrl()
    {
        using var service = FakeSpellService.EnglishOnly(("exampl", "example"));
        var result = new ConservativeCorrectionEngine(service).Correct("https://exampl.com/path");
        Equal(CorrectionStatus.SkippedProtectedContent, result.Status);
        Equal("https://exampl.com/path", result.CorrectedText);
    }

    private static void SkipLikelyName()
    {
        using var service = FakeSpellService.EnglishOnly(("Christan", "Christian"));
        var result = new ConservativeCorrectionEngine(service).Correct("Hello Christan");
        Equal(CorrectionStatus.SkippedAmbiguous, result.Status);
        Equal("Hello Christan", result.CorrectedText);
    }

    private static void RejectTiedSuggestions()
    {
        using var service = FakeSpellService.WithSuggestions(
            ["en-US"],
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["cot"] = ["cat", "cut"],
            });
        var result = new ConservativeCorrectionEngine(service).Correct("A cot mistake");
        Equal(CorrectionStatus.SkippedAmbiguous, result.Status);
        Equal("A cot mistake", result.CorrectedText);
    }

    private static void DetectEnglish()
    {
        using var service = FakeSpellService.LanguageAware();
        var result = new ConservativeCorrectionEngine(service).Correct("This sentnce is written in English");
        Equal("en-US", result.LanguageTag);
        Equal("This sentence is written in English", result.CorrectedText);
    }

    private static void CorrectNorwegianMultipleTypos()
    {
        using var service = FakeSpellService.MultipleTypoExamples();
        var result = new ConservativeCorrectionEngine(service).Correct("vorfor komer du ike?");
        Equal("nb-NO", result.LanguageTag);
        Equal("hvorfor kommer du ikke?", result.CorrectedText);
    }

    private static void CorrectEnglishMultipleTypos()
    {
        using var service = FakeSpellService.MultipleTypoExamples();
        var result = new ConservativeCorrectionEngine(service).Correct("Ths sentnce has erors.");
        Equal("en-US", result.LanguageTag);
        Equal("This sentence has errors.", result.CorrectedText);
    }

    private static void PreferNorwegianBokmal()
    {
        using var service = FakeSpellService.WithSuggestions(["en-US", "nb-NO"], new Dictionary<string, string[]>());
        var result = new ConservativeCorrectionEngine(service).Correct("Hei verden");
        Equal("nb-NO", result.LanguageTag);
        Equal(CorrectionStatus.NoChange, result.Status);
    }

    private static void PreserveParagraphBreaks()
    {
        using var service = FakeSpellService.EnglishOnly(("sentnce", "sentence"), ("paragraf", "paragraph"));
        var original = "This sentnce.\r\n\r\nAnother paragraf.";
        var result = new ConservativeCorrectionEngine(service).Correct(original);
        Equal("This sentence.\r\n\r\nAnother paragraph.", result.CorrectedText);
    }

    private static void PreserveNorwegianPhraseRepair()
    {
        using var service = FakeSpellService.NorwegianPhraseExample();
        var result = new ConservativeCorrectionEngine(service).Correct("hva me dnå daa?");
        Equal("nb-NO", result.LanguageTag);
        Equal("hva med nå da?", result.CorrectedText);
    }

    private static void PreferNorwegianDiacriticOverCaseOnlyReplacement()
    {
        using var service = FakeSpellService.CaseOnlyReplacementWithSuggestion("na", "Na", "n\u00e5");
        var result = new ConservativeCorrectionEngine(service).Correct("hva med na da?");
        Equal("hva med n\u00e5 da?", result.CorrectedText);
    }

    private static void RecoverMissedNorwegianDiacritic()
    {
        using var service = FakeSpellService.WithSuggestions(
            ["nb-NO"], new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
        var result = new ConservativeCorrectionEngine(service).Correct("hva med na da?");
        Equal("hva med nå da?", result.CorrectedText);
        True(result.Corrections.Any(correction => correction.Reason.Contains("diacritic", StringComparison.Ordinal)),
            "The bounded diacritic recovery reason was not recorded.");
    }

    private static void NorwegianDiacriticRecoveryIsBounded()
    {
        using var service = FakeSpellService.WithSuggestions(
            ["nb-NO"], new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
        var engine = new ConservativeCorrectionEngine(service);
        Equal("na", engine.Correct("na").CorrectedText);
        Equal("Vi bruker NA internt", engine.Correct("Vi bruker NA internt").CorrectedText);
        Equal("Vi møtte Na Hansen", engine.Correct("Vi møtte Na Hansen").CorrectedText);
    }

    private static string NewNoteFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "VinceProductivityTray.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void QuickNotesPreserveText()
    {
        var folder = NewNoteFolder();
        var path = new DailyNoteWriter().Append(folder, "møte på blåbær\nlinje to", new DateTimeOffset(2026, 10, 9, 13, 4, 5, TimeSpan.FromHours(2)));
        var text = File.ReadAllText(path, new UTF8Encoding(false));
        True(text.Contains("[13:04:05] møte på blåbær\nlinje to", StringComparison.Ordinal), "UTF-8 note text or line breaks changed.");
        True(!File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "Daily note unexpectedly contains a UTF-8 BOM.");
    }

    private static void QuickNotesUseSaveDate()
    {
        var folder = NewNoteFolder();
        var writer = new DailyNoteWriter();
        var first = writer.Append(folder, "before", new DateTimeOffset(2026, 10, 9, 23, 59, 59, TimeSpan.Zero));
        var second = writer.Append(folder, "after", new DateTimeOffset(2026, 10, 10, 0, 0, 1, TimeSpan.Zero));
        Equal("2026-10-09.txt", Path.GetFileName(first));
        Equal("2026-10-10.txt", Path.GetFileName(second));
    }

    private static void QuickNotesSerializeAppends()
    {
        var folder = NewNoteFolder();
        var writer = new DailyNoteWriter();
        Parallel.For(0, 40, index => writer.Append(folder, $"note-{index}", new DateTimeOffset(2026, 10, 9, 12, 0, index, TimeSpan.Zero)));
        var lines = File.ReadAllLines(Path.Combine(folder, "2026-10-09.txt"));
        Equal(40, lines.Length);
        True(Enumerable.Range(0, 40).All(index => lines.Any(line => line.EndsWith($"note-{index}", StringComparison.Ordinal))),
            "A repeated note append was lost.");
    }

    private static void QuickNoteCloseHidesAndReopens()
    {
        var originalFolder = NewNoteFolder();
        var updatedFolder = NewNoteFolder();
        var settings = new AppSettings { NotesFolder = originalFolder };
        using var form = new QuickNoteForm(new DailyNoteWriter(), () => settings)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000),
        };
        form.ShowForEntry();
        Application.DoEvents();
        var input = Descendants(form).OfType<TextBox>().Single();
        input.Text = "synthetic unsent draft";
        form.Close();
        Application.DoEvents();
        True(!form.IsDisposed && !form.Visible, "Closing Quick Note disposed the reusable form.");

        form.ShowForEntry();
        Application.DoEvents();
        Equal("synthetic unsent draft", input.Text);
        settings = settings with { NotesFolder = updatedFolder };
        var save = typeof(QuickNoteForm).GetMethod("TrySave",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Quick Note save method was not found.");
        save.Invoke(form, null);
        True(!form.IsDisposed && !form.Visible, "Successful Quick Note save disposed the reusable form.");
        var saved = Directory.GetFiles(updatedFolder, "*.txt", SearchOption.TopDirectoryOnly);
        True(saved.Length == 1 && File.ReadAllText(saved[0], Encoding.UTF8).Contains("synthetic unsent draft", StringComparison.Ordinal),
            "Reopened Quick Note did not use the latest settings callback or save the synthetic draft.");
        True(Directory.GetFiles(originalFolder, "*.txt", SearchOption.TopDirectoryOnly).Length == 0,
            "Reopened Quick Note wrote to the stale settings folder.");
    }

    private static void HotkeyDefaultsParse()
    {
        _ = HotkeyDefinition.Parse("Ctrl+Shift+X");
        _ = HotkeyDefinition.Parse("Ctrl+Alt+N");
        _ = HotkeyDefinition.Parse("Ctrl+Alt+R");
        _ = HotkeyDefinition.Parse("Ctrl+Shift+A");
    }

    private static void HotkeysRebindTransactionally()
    {
        var native = new FakeHotkeyNativeApi();
        using var window = new HotkeyWindow(native);
        var original = new[]
        {
            Binding("Ctrl+Shift+X", "Spellcheck"),
            Binding("Ctrl+Alt+N", "Quick note"),
        };
        True(window.TryReplaceAll(original, out var initialConflicts),
            "Initial synthetic hotkeys failed: " + string.Join(", ", initialConflicts));

        var replacement = new[]
        {
            Binding("Ctrl+Shift+S", "Spellcheck"),
            Binding("Ctrl+Shift+X", "Quick note"),
            Binding("Ctrl+Shift+C", "Recording"),
        };
        True(window.TryReplaceAll(replacement, out var conflicts),
            "Synthetic hotkey rebind failed: " + string.Join(", ", conflicts));
        Equal(3, native.ActiveCount);
        True(native.IsActive(HotkeyDefinition.Parse("Ctrl+Shift+S")), "New spellcheck hotkey is inactive.");
        True(native.IsActive(HotkeyDefinition.Parse("Ctrl+Shift+X")), "Swapped quick-note hotkey is inactive.");
        True(native.IsActive(HotkeyDefinition.Parse("Ctrl+Shift+C")), "New recording hotkey is inactive.");
        True(!native.IsActive(HotkeyDefinition.Parse("Ctrl+Alt+N")), "Old quick-note hotkey remained active.");
    }

    private static void FailedHotkeyRebindRestoresPrevious()
    {
        var native = new FakeHotkeyNativeApi();
        using var window = new HotkeyWindow(native);
        var original = new[]
        {
            Binding("Ctrl+Shift+X", "Spellcheck"),
            Binding("Ctrl+Alt+N", "Quick note"),
        };
        True(window.TryReplaceAll(original, out _), "Initial synthetic hotkeys failed.");
        native.Occupy(HotkeyDefinition.Parse("Ctrl+Shift+S"));

        var replacement = new[]
        {
            Binding("Ctrl+Shift+S", "Spellcheck"),
            Binding("Ctrl+Shift+X", "Quick note"),
        };
        True(!window.TryReplaceAll(replacement, out var conflicts), "Occupied replacement hotkey was accepted.");
        True(conflicts.Count > 0, "Hotkey conflict was not reported.");
        Equal(2, native.ActiveCount);
        True(native.IsActive(HotkeyDefinition.Parse("Ctrl+Shift+X")), "Previous spellcheck hotkey was not restored.");
        True(native.IsActive(HotkeyDefinition.Parse("Ctrl+Alt+N")), "Previous quick-note hotkey was not restored.");
    }

    private static void HotkeyProbeCleansUp()
    {
        var native = new FakeHotkeyNativeApi();
        using var window = new HotkeyWindow(native);
        True(window.TryReplaceAll([Binding("Ctrl+Shift+X", "Spellcheck")], out _),
            "Initial synthetic hotkey failed.");
        var before = native.ActiveCount;
        var result = window.Probe("Ctrl+Alt+N");
        Equal(HotkeyProbeStatus.Available, result.Status);
        Equal(before, native.ActiveCount);
        True(!native.IsActive(HotkeyDefinition.Parse("Ctrl+Alt+N")), "Probe leaked a temporary hotkey registration.");
    }

    private static HotkeyBinding Binding(string text, string label) =>
        new(HotkeyDefinition.Parse(text), () => { }, label);

    private static void TimeCapturePreservesFormat()
    {
        var folder = NewNoteFolder();
        var store = new TimeCaptureStore();
        store.SaveCustomers(folder, ["Vince", "Kunde \"Nord\""]);
        var timestamp = new DateTimeOffset(2026, 10, 22, 10, 30, 0, TimeSpan.FromHours(2));
        var path = store.Append(folder, "m\u00f8te\nmed team", 0.5m, "Kunde \"Nord\"", timestamp);
        Equal("log_2026-W43.txt", Path.GetFileName(path));
        var text = File.ReadAllText(path, new UTF8Encoding(false));
        True(text.Contains("2026-10-22 10:30 - m\u00f8te med team | dur=0.5h | cust=\"Kunde \\\"Nord\\\"\"", StringComparison.Ordinal),
            "The compatible weekly time-entry format or escaping changed.");
        Equal(0.5m, store.GetTodayRegisteredHours(folder, new DateOnly(2026, 10, 22)));
        Equal(2, store.LoadCustomers(folder).Count);
    }

    private static void TimeCaptureCustomersStayBesideLogs()
    {
        var folder = NewNoteFolder();
        var customerPath = TimeCaptureStore.GetCustomersPath(folder);
        var logPath = TimeCaptureStore.GetWeeklyLogPath(folder, new DateOnly(2026, 10, 22));
        Equal(Path.GetFullPath(folder), Path.GetDirectoryName(customerPath));
        Equal(Path.GetDirectoryName(logPath), Path.GetDirectoryName(customerPath));
        Equal("customers.txt", Path.GetFileName(customerPath));
    }

    private static void TimeCaptureProgressMatchesLegacyRules()
    {
        var start = new TimeOnly(9, 0);
        Equal(0m, TimeCaptureProgress.ExpectedHours(new DateTime(2026, 10, 22, 8, 59, 0), start, 8m));
        Equal(1.5m, TimeCaptureProgress.ExpectedHours(new DateTime(2026, 10, 22, 10, 16, 0), start, 8m));
        Equal(8m, TimeCaptureProgress.ExpectedHours(new DateTime(2026, 10, 22, 20, 0, 0), start, 8m));
        True(TimeCaptureProgress.IsScheduledHour(new DateTime(2026, 10, 22, 8, 0, 30), 8, 16), "08:00 popup was not due.");
        True(!TimeCaptureProgress.IsScheduledHour(new DateTime(2026, 10, 22, 8, 1, 0), 8, 16), "A non-hour popup was incorrectly due.");
    }

    private static void TimeCaptureScheduleSuppressesDuplicates()
    {
        var state = new TimeCaptureScheduleState();
        True(state.ShouldTrigger(new DateTime(2026, 10, 22, 8, 0, 2), 8, 16), "First hourly popup was missed.");
        True(!state.ShouldTrigger(new DateTime(2026, 10, 22, 8, 0, 45), 8, 16), "Duplicate hourly popup was allowed.");
        True(state.ShouldTrigger(new DateTime(2026, 10, 22, 9, 0, 1), 8, 16), "Next-hour popup was missed.");
    }

    private static void TimeCaptureLayoutMatchesOriginal()
    {
        var folder = NewNoteFolder();
        using var form = new TimeCaptureForm(new TimeCaptureStore(), () => new AppSettings
        {
            TimeCaptureFolder = folder,
            TimeCaptureSnoozeMinutes = 10,
        });
        form.PerformLayout();
        True(form.ClientSize.Width is >= 420 and <= 480, "Time capture window is no longer compact.");
        var controls = Descendants(form).ToArray();
        var labels = controls.OfType<Label>().Select(label => label.Text).ToArray();
        True(labels.Contains("Relative Progress:") && labels.Contains("What are you working on right now?") &&
             labels.Contains("Duration (hours):") && labels.Contains("Customer:"),
            "The original TimeCapture field sequence is incomplete.");
        True(controls.OfType<TextBox>().Any(text => text.Multiline), "The work-entry draft field is missing.");
        True(controls.OfType<Button>().Any(button => button.Text == "Submit") &&
             controls.OfType<Button>().Any(button => button.Text.StartsWith("Snooze", StringComparison.Ordinal)) &&
             controls.OfType<Button>().Any(button => button.AccessibleName == "Manage customers"),
            "The compact TimeCapture actions are incomplete.");

        form.Scale(new SizeF(1.5f, 1.5f));
        form.PerformLayout();
        True(Descendants(form).OfType<Button>().All(button => button.Height >= button.PreferredSize.Height),
            "A TimeCapture button is clipped after DPI scaling.");
    }

    private static void RenderTimeCapturePreview(string path)
    {
        var folder = NewNoteFolder();
        using var form = new TimeCaptureForm(new TimeCaptureStore(), () => new AppSettings
        {
            TimeCaptureFolder = folder,
            TimeCaptureSnoozeMinutes = 10,
        });
        var controls = Descendants(form).ToArray();
        controls.OfType<TextBox>().First(text => text.Multiline).Text = "Planning customer follow-up";
        controls.OfType<ComboBox>().Last().Text = "Vince AS";
        var progress = controls.OfType<Label>().First(label => !label.AutoSize);
        progress.Text = "-1.5 h";
        progress.ForeColor = Color.Firebrick;
        form.Show();
        try
        {
            Application.DoEvents();
            form.PerformLayout();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
            image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            form.Hide();
        }
        Notes.Add($"Synthetic TimeCapture UI preview: {path}");
    }

    private static void RenderCustomerManagerPreview(string path)
    {
        var folder = NewNoteFolder();
        using var dialog = new CustomerManagerForm(new TimeCaptureStore(), folder);
        dialog.Show();
        try
        {
            Application.DoEvents();
            dialog.PerformLayout();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var image = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size));
            image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            dialog.Hide();
        }
        Notes.Add($"Synthetic customer-manager UI preview: {path}");
    }

    private static void MeetingAutorecordRequiresRisingEdge()
    {
        var state = new MeetingAutoRecordState();
        True(!state.Observe(false, true, false), "Idle Teams state started recording.");
        True(state.Observe(true, true, false), "Enabled meeting transition did not request recording.");
        True(!state.Observe(true, true, false), "Same meeting requested a duplicate recording after manual stop.");
        True(!state.Observe(false, true, false), "Meeting end requested recording.");
        True(!state.Observe(true, false, false), "Disabled autorecord started on a meeting transition.");
    }

    private static void SettingsControlsRemainVisible()
    {
        using var form = new SettingsForm(new AppSettings(), _ => new HotkeyProbeResult(HotkeyProbeStatus.Available, "Available"));
        form.Scale(new SizeF(1.5f, 1.5f));
        form.PerformLayout();
        var buttons = Descendants(form).OfType<Button>().ToArray();
        True(buttons.Count(button => button.Text == "Browse...") == 3, "A Browse button is missing.");
        True(buttons.Any(button => button.Text == "Save") && buttons.Any(button => button.Text == "Cancel"), "Save or Cancel is missing.");
        True(buttons.All(button => button.Height >= button.PreferredSize.Height), "A button is clipped vertically after scaling.");
        foreach (var text in new[] { "Autorecord meetings", "Start with Windows" })
        {
            var label = Descendants(form).OfType<Label>().Single(control => control.Text == text);
            True(label.Width >= label.PreferredWidth,
                $"The '{text}' label is clipped horizontally after scaling ({label.Width} < {label.PreferredWidth}).");
        }
        var credit = Descendants(form).OfType<Label>().Single(label =>
            label.Text == "developed by Christian Holberg Heggen");
        True(credit.Width >= credit.PreferredWidth && credit.Height >= credit.PreferredHeight,
            "The Application developer credit is clipped after DPI scaling.");
    }

    private static void CustomerManagerLayeringIsSafe()
    {
        using var owner = new Form { TopMost = true };
        using var dialog = new Form();
        var previous = CustomerDialogLayering.Prepare(owner, dialog);
        True(previous, "The owner's original TopMost state was not captured.");
        True(!owner.TopMost && dialog.TopMost, "The modal customer dialog was not placed above its former TopMost owner.");
        True(!dialog.ShowInTaskbar && dialog.StartPosition == FormStartPosition.CenterParent,
            "The customer dialog is not configured as a centered owned tool window.");
        CustomerDialogLayering.Restore(owner, previous);
        True(owner.TopMost, "The Time Capture TopMost state was not restored after the dialog closed.");
    }

    private static void SettingsTabsAndFeatureToggles()
    {
        using var form = new SettingsForm(new AppSettings(), _ =>
            new HotkeyProbeResult(HotkeyProbeStatus.Available, "Available"));
        var tabs = Descendants(form).OfType<TabControl>().Single();
        True(tabs.TabPages.Cast<TabPage>().Select(page => page.Text).SequenceEqual(
                ["General", "Recording", "Quick Notes", "Time Capture", "Spell Check"], StringComparer.Ordinal),
            "Settings tabs are missing or out of order.");
        var toggles = Descendants(form).OfType<CheckBox>().Select(check => check.Text).ToArray();
        foreach (var expected in new[] { "Enable recording", "Enable quick notes", "Enable integrated time capture", "Enable spell check" })
            True(toggles.Contains(expected, StringComparer.Ordinal), $"Missing feature toggle: {expected}");
        True(Descendants(form).OfType<Label>().Any(label => label.Text == "Auto update"),
            "General settings are missing Auto update.");
        True(!form.GetSettings().AutoUpdateEnabled, "Auto update must default off while publisher trust is not configured.");
        var pages = tabs.TabPages.Cast<TabPage>().ToDictionary(page => page.Text, StringComparer.Ordinal);
        True(!Descendants(pages["General"]).OfType<Button>().Any(button => button.Text is "Open notes folder" or "Open recordings folder" or "Open time log folder"),
            "Feature folder buttons remained on General.");
        True(!Descendants(pages["General"]).Any(control => control.Text == "Folders and application") &&
             !Descendants(pages["General"]).OfType<Button>().Any(button => button.Text == "Exit application"),
            "The removed General folders/application section or redundant Exit button is still present.");
        True(Descendants(pages["General"]).OfType<Button>().Any(button => button.Text == "Open diagnostics folder"),
            "Open diagnostics folder is missing from the General Application section.");
        True(Descendants(pages["Recording"]).OfType<Button>().Any(button => button.Text == "Open recordings folder") &&
             Descendants(pages["Quick Notes"]).OfType<Button>().Any(button => button.Text == "Open notes folder") &&
             Descendants(pages["Time Capture"]).OfType<Button>().Any(button => button.Text == "Open time log folder"),
            "A feature folder button is not on its respective tab.");
        var legacy = JsonSerializer.Deserialize<AppSettings>("{}") ?? throw new InvalidOperationException("Legacy settings did not deserialize.");
        True(legacy.RecordingEnabled && legacy.QuickNotesEnabled && legacy.SpellcheckEnabled,
            "Missing feature flags in an older settings file did not preserve previously enabled features.");
    }

    private static void SettingsRowsDoNotOverlap()
    {
        using var form = new SettingsForm(new AppSettings(), _ =>
            new HotkeyProbeResult(HotkeyProbeStatus.Available, "Available"));
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-10000, -10000); form.Show();
        var tabs = Descendants(form).OfType<TabControl>().Single();
        foreach (TabPage page in tabs.TabPages)
        {
            tabs.SelectedTab = page; Application.DoEvents(); form.PerformLayout();
            foreach (var table in Descendants(page).OfType<TableLayoutPanel>())
            {
                var direct = table.Controls.Cast<Control>().Where(control => control.Visible).ToArray();
                for (var first = 0; first < direct.Length; first++)
                for (var second = first + 1; second < direct.Length; second++)
                {
                    var a = table.GetCellPosition(direct[first]);
                    var b = table.GetCellPosition(direct[second]);
                    if (a.Row == b.Row || a.Column != b.Column) continue;
                    True(!direct[first].Bounds.IntersectsWith(direct[second].Bounds),
                        $"Settings controls overlap on {page.Text}: '{direct[first].Text}' and '{direct[second].Text}'.");
                }
            }
        }
        form.Hide();
    }

    private static void DisabledFeaturesMayReuseHotkeys()
    {
        var settings = new AppSettings
        {
            SpellcheckEnabled = true,
            QuickNotesEnabled = false,
            RecordingEnabled = false,
            TimeCaptureEnabled = false,
            SpellcheckHotkey = "Ctrl+Shift+X",
            NotesHotkey = "Ctrl+Shift+X",
            RecordingHotkey = "Ctrl+Shift+X",
            TimeCaptureHotkey = "Ctrl+Shift+X",
        };
        var normalized = SettingsValidator.ValidateAndNormalize(settings);
        True(normalized.SpellcheckEnabled && !normalized.QuickNotesEnabled && !normalized.RecordingEnabled,
            "Feature enabled states were not preserved.");
    }

    private static void TrayActionsFollowFeatureState()
    {
        var settings = new AppSettings { RecordingEnabled = false, QuickNotesEnabled = false, TimeCaptureEnabled = false };
        var idle = FeatureGate.TrayActions(settings, isRecording: false, recorderAvailable: true);
        Equal("Start Recording", idle.RecordingText);
        True(!idle.RecordingEnabled && !idle.NotesEnabled && !idle.TimeCaptureEnabled,
            "Disabled feature actions remained enabled in the tray state.");
        var active = FeatureGate.TrayActions(settings, isRecording: true, recorderAvailable: true);
        Equal("Stop Recording", active.RecordingText);
        True(active.RecordingEnabled, "An active recording lost its safe Stop action after recording was disabled.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void RenderSettingsPreviews(string folder)
    {
        Directory.CreateDirectory(folder);
        using var form = new SettingsForm(new AppSettings(), _ =>
            new HotkeyProbeResult(HotkeyProbeStatus.Available, "Available"));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        form.Show();
        Application.DoEvents();
        var tabs = Descendants(form).OfType<TabControl>().Single();
        for (var index = 0; index < tabs.TabPages.Count; index++)
        {
            tabs.SelectedIndex = index;
            Application.DoEvents();
            form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            var safe = tabs.TabPages[index].Text.Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
            bitmap.Save(Path.Combine(folder, $"settings-{index + 1}-{safe}.png"));
        }
        form.Hide();
    }

    private static void VinceOneDriveResolverIsSafe()
    {
        var result = OrganizationFolderResolver.ResolveUniqueVinceRoot([
            new OneDriveCandidate(@"C:\Users\Example\OneDrive", false, null),
            new OneDriveCandidate(@"C:\Users\Example\OneDrive - Vince AS", true, "Vince AS"),
        ]);
        Equal(Path.GetFullPath(@"C:\Users\Example\OneDrive - Vince AS"), result);
        var ambiguous = OrganizationFolderResolver.ResolveUniqueVinceRoot([
            new OneDriveCandidate(@"C:\A\OneDrive - Vince AS", true, "Vince AS"),
            new OneDriveCandidate(@"C:\B\OneDrive - Vince AS", true, "Vince AS"),
        ]);
        Equal<string?>(null, ambiguous);
        var personalOnly = OrganizationFolderResolver.ResolveUniqueVinceRoot([
            new OneDriveCandidate(@"C:\Users\Example\OneDrive - Vince AS", false, "Vince AS"),
        ]);
        Equal<string?>(null, personalOnly);
    }

    private static void VinceUtilitySubfoldersAreSafe()
    {
        var target = Path.Combine(NewNoteFolder(), "VinceUtility");
        OrganizationFolderResolver.EnsureVinceUtilityStructure(target);
        True(OrganizationFolderResolver.VinceUtilitySubfolders.SequenceEqual(
                ["Quick Notes", "Meeting Recordings", "Time Capture"], StringComparer.Ordinal),
            "VinceUtility subfolder names changed unexpectedly.");
        var marker = Path.Combine(target, "Quick Notes", "existing.txt");
        File.WriteAllText(marker, "preserve", new UTF8Encoding(false));
        OrganizationFolderResolver.EnsureVinceUtilityStructure(target);
        foreach (var name in OrganizationFolderResolver.VinceUtilitySubfolders)
            True(Directory.Exists(Path.Combine(target, name)), $"Missing VinceUtility subfolder: {name}");
        Equal("preserve", File.ReadAllText(marker, Encoding.UTF8));
    }

    private static void FreshInstallSettingsUseVinceUtility()
    {
        var sandbox = NewNoteFolder();
        var target = Path.Combine(sandbox, "VinceUtility");
        var settingsPath = Path.Combine(sandbox, "state", "settings.json");
        True(FreshInstallSettings.WriteIfMissing(settingsPath, target), "Fresh settings were not created.");

        using var json = JsonDocument.Parse(File.ReadAllText(settingsPath, Encoding.UTF8));
        var root = json.RootElement;
        Equal(Path.Combine(target, "Quick Notes"), root.GetProperty("NotesFolder").GetString());
        Equal(Path.Combine(target, "Meeting Recordings"), root.GetProperty("RecordingFolder").GetString());
        Equal(Path.Combine(target, "Time Capture"), root.GetProperty("TimeCaptureFolder").GetString());
        True(root.GetProperty("RecordingEnabled").GetBoolean(), "Fresh install disabled recording unexpectedly.");
        True(root.GetProperty("QuickNotesEnabled").GetBoolean(), "Fresh install disabled quick notes unexpectedly.");
        True(root.GetProperty("SpellcheckEnabled").GetBoolean(), "Fresh install disabled spellcheck unexpectedly.");
        True(!root.GetProperty("AutoRecordMeetings").GetBoolean(), "Fresh install enabled automatic recording.");
        True(!root.GetProperty("TimeCaptureEnabled").GetBoolean(), "Fresh install enabled time capture.");
        True(!root.GetProperty("AutoUpdateEnabled").GetBoolean(), "Fresh install enabled automatic updates.");
    }

    private static void ExistingSettingsArePreserved()
    {
        var sandbox = NewNoteFolder();
        var target = Path.Combine(sandbox, "VinceUtility");
        var settingsPath = Path.Combine(sandbox, "state", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var original = "{\n  \"NotesFolder\": \"Z:\\\\custom-missing-notes\",\n  \"RecordingFolder\": \"Y:\\\\custom-missing-recordings\",\n  \"TimeCaptureFolder\": \"X:\\\\custom-missing-time\"\n}";
        File.WriteAllText(settingsPath, original, new UTF8Encoding(false));

        OrganizationFolderResolver.EnsureVinceUtilityStructure(target);
        True(!FreshInstallSettings.WriteIfMissing(settingsPath, target), "Existing settings were treated as fresh.");
        Equal(original, File.ReadAllText(settingsPath, Encoding.UTF8));
    }

    private static void ExistingEmptyVinceUtilityFoldersAreSafe()
    {
        var sandbox = NewNoteFolder();
        var target = Path.Combine(sandbox, "VinceUtility");
        OrganizationFolderResolver.EnsureVinceUtilityStructure(target);
        True(OrganizationFolderResolver.VinceUtilitySubfolders.All(name =>
                !Directory.EnumerateFileSystemEntries(Path.Combine(target, name)).Any()),
            "Synthetic VinceUtility folders were not empty before the test.");

        var settingsPath = Path.Combine(sandbox, "state", "settings.json");
        True(FreshInstallSettings.WriteIfMissing(settingsPath, target), "Existing empty folders blocked fresh settings.");
        True(OrganizationFolderResolver.VinceUtilitySubfolders.All(name =>
                Directory.Exists(Path.Combine(target, name))),
            "An existing empty VinceUtility folder was removed.");
    }

    private static void MissingOrAmbiguousOneDriveRequiresSelection()
    {
        Equal<string?>(null, OrganizationFolderResolver.ResolveUniqueVinceRoot([]));
        Equal<string?>(null, OrganizationFolderResolver.ResolveUniqueVinceRoot([
            new OneDriveCandidate(@"C:\A\OneDrive - Vince AS", true, "Vince AS"),
            new OneDriveCandidate(@"C:\B\OneDrive - Vince AS", true, "Vince AS"),
        ]));
    }

    private static void HotkeyReleaseRequiresStableInterval()
    {
        long now = 0;
        var states = new Queue<bool>([true, false, false, true, false, false, false, false]);
        var current = true;
        var pauses = 0;
        var released = InputTimingPolicy.WaitForStableRelease(
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(20),
            () => current,
            () => now,
            () =>
            {
                now += 10;
                pauses++;
                if (states.Count > 0) current = states.Dequeue();
            });

        True(released, "A stable release interval was not accepted.");
        True(pauses >= 6, "A brief key-up bounce was incorrectly accepted as a stable release.");

        now = 0;
        var timedOut = InputTimingPolicy.WaitForStableRelease(
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(10),
            () => true,
            () => now,
            () => now += 5);
        True(!timedOut, "Held shortcut keys did not time out.");
        True(InputTimingPolicy.HotkeyReleaseTimeout >= TimeSpan.FromSeconds(2),
            "The production release timeout is too short for an ordinary key release.");
    }

    private static void TransientInputRetryPolicy()
    {
        var attempts = 0;
        var waits = 0;
        var recovered = InputTimingPolicy.RunTransientAttempts(
            2,
            _ => ++attempts == 1 ? TransientAttemptResult.Retry : TransientAttemptResult.Success,
            _ => waits++);
        True(recovered, "A transient first failure was not retried.");
        Equal(2, attempts);
        Equal(1, waits);

        attempts = 0;
        waits = 0;
        var stopped = InputTimingPolicy.RunTransientAttempts(
            2,
            _ =>
            {
                attempts++;
                return TransientAttemptResult.Stop;
            },
            _ => waits++);
        True(!stopped, "A terminal focus/safety failure was treated as retryable.");
        Equal(1, attempts);
        Equal(0, waits);
        Equal(2, InputTimingPolicy.CopyAttempts);
    }

    private static void SettingsNormalizeValues()
    {
        var root = NewNoteFolder();
        var notes = Path.Combine(root, "notater");
        var recordings = Path.Combine(root, "opptak");
        var normalized = SettingsValidator.ValidateAndNormalize(new AppSettings
        {
            NotesFolder = $"  \"{notes}\"  ",
            RecordingFolder = recordings,
            SpellcheckHotkey = " Ctrl+Shift+X ",
            NotesHotkey = "Ctrl+Alt+N",
            RecordingHotkey = "Ctrl+Alt+R",
        });

        Equal(Path.GetFullPath(notes), normalized.NotesFolder);
        Equal(Path.GetFullPath(recordings), normalized.RecordingFolder);
        Equal("Ctrl+Shift+X", normalized.SpellcheckHotkey);
    }

    private static void SettingsRejectCollisions()
    {
        var root = NewNoteFolder();
        var sameFolderRejected = false;
        try
        {
            _ = SettingsValidator.ValidateAndNormalize(new AppSettings
            {
                NotesFolder = root,
                RecordingFolder = root,
            });
        }
        catch (ArgumentException)
        {
            sameFolderRejected = true;
        }

        True(sameFolderRejected, "The same notes and recording folder was accepted.");

        var duplicateHotkeyRejected = false;
        try
        {
            _ = SettingsValidator.ValidateAndNormalize(new AppSettings
            {
                NotesFolder = Path.Combine(root, "notes"),
                RecordingFolder = Path.Combine(root, "recordings"),
                SpellcheckHotkey = "Ctrl+Alt+N",
                NotesHotkey = "Control+Alt+N",
                RecordingHotkey = "Ctrl+Alt+R",
            });
        }
        catch (ArgumentException)
        {
            duplicateHotkeyRejected = true;
        }

        True(duplicateHotkeyRejected, "Equivalent duplicate hotkeys were accepted.");
    }

    private static void SettingsSaveAtomically()
    {
        var folder = NewNoteFolder();
        var path = Path.Combine(folder, "settings.json");
        var settings = new AppSettings
        {
            NotesFolder = Path.Combine(folder, "blåbærnotater"),
            RecordingFolder = Path.Combine(folder, "møteopptak"),
        };

        settings.SaveTo(path);
        var bytes = File.ReadAllBytes(path);
        True(!bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "Settings unexpectedly contain a UTF-8 BOM.");
        var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path, Encoding.UTF8));
        Equal(settings.NotesFolder, loaded?.NotesFolder);
        Equal(0, Directory.GetFiles(folder, "*.tmp").Length);
    }

    private static void DeferredUpdatesHonorCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        var deferred = UpdateDeferral.Defer(now, 24);
        True(!UpdateDeferral.IsDue(deferred, now.AddHours(23)), "Update became due before the cooldown elapsed.");
        True(UpdateDeferral.IsDue(deferred, now.AddHours(25)), "Update did not become due after the cooldown.");
    }

    private static void UpdaterRestrictsAssets()
    {
        const string asset = "VinceProductivityTray-1.0.0-win-arm64-Setup.exe";
        True(UpdaterPolicy.IsTrustedAssetUri(
            new Uri($"https://github.com/CheggenOnline/tray-dist/releases/download/v1.0.0/{asset}"),
            asset, "CheggenOnline", "tray-dist"), "Expected release asset was rejected.");
        True(!UpdaterPolicy.IsTrustedAssetUri(
            new Uri($"https://evil.example/CheggenOnline/tray-dist/releases/download/v1.0.0/{asset}"),
            asset, "CheggenOnline", "tray-dist"), "A third-party host was accepted.");
        True(!UpdaterPolicy.IsTrustedAssetUri(
            new Uri($"https://github.com/CheggenOnline/other/releases/download/v1.0.0/{asset}"),
            asset, "CheggenOnline", "tray-dist"), "A different repository was accepted.");
        True(!UpdaterPolicy.IsTrustedAssetUri(
            new Uri($"https://github.com/CheggenOnline/tray-dist/releases/download/v1.0.0/{asset}?redirect=1"),
            asset, "CheggenOnline", "tray-dist"), "A mutable query-bearing asset URL was accepted.");
    }

    private static void UpdaterSeparatesModes()
    {
        True(UpdaterPolicy.IsNotificationConfigured, "Approved public release notification policy is missing.");
        True(!UpdaterPolicy.IsAutoInstallConfigured, "Unsigned development candidate unexpectedly enabled installer execution.");
        True(UpdaterPolicy.IsTrustedReleasePageUri(
            new Uri("https://github.com/CheggenOnline/vince-productivity-tray/releases/tag/v1.0.0")),
            "Approved release page was rejected.");
        True(!UpdaterPolicy.IsTrustedReleasePageUri(
            new Uri("https://github.com/CheggenOnline/other/releases/tag/v1.0.0")),
            "A different repository release page was accepted.");
    }

    private static void UpdaterSelectsPublishedPrerelease()
    {
        const string json = """
        [
          {
            "tag_name": "v0.9.14",
            "draft": true,
            "prerelease": true,
            "html_url": "https://github.com/CheggenOnline/vince-productivity-tray/releases/tag/v0.9.14",
            "body": "draft",
            "assets": []
          },
          {
            "tag_name": "v0.9.13",
            "draft": false,
            "prerelease": true,
            "html_url": "https://github.com/CheggenOnline/vince-productivity-tray/releases/tag/v0.9.13",
            "body": "published prerelease",
            "assets": []
          },
          {
            "tag_name": "v0.9.12",
            "draft": false,
            "prerelease": true,
            "html_url": "https://github.com/CheggenOnline/vince-productivity-tray/releases/tag/v0.9.12",
            "body": "current",
            "assets": []
          }
        ]
        """;
        using var document = JsonDocument.Parse(json);
        var update = GitHubUpdateClient.SelectLatest(document.RootElement, new Version(0, 9, 12), "win-arm64");
        Equal(new Version(0, 9, 13), update?.Version);
        Equal("published prerelease", update?.ReleaseNotes);
        True(!UpdaterPolicy.IsAutoInstallConfigured,
            "Prerelease discovery unexpectedly weakened the Authenticode installation gate.");
        True(GitHubUpdateClient.SelectLatest(document.RootElement, new Version(0, 9, 13), "win-arm64") is null,
            "The updater offered a draft or an already installed release.");
    }

    private static void UpdaterRefusesActiveWork()
    {
        True(UpdateInstallGuard.CanInstall(recordingActive: false, timeDraftPending: false),
            "An idle app was incorrectly blocked from updating.");
        True(!UpdateInstallGuard.CanInstall(recordingActive: true, timeDraftPending: false),
            "An active recording was allowed to update.");
        True(!UpdateInstallGuard.CanInstall(recordingActive: false, timeDraftPending: true),
            "An unsent Time Capture draft was allowed to update.");
    }

    private static void UnsignedUpdaterActionIsHonest()
    {
        var actions = new SettingsActions(
            "0.9.14", () => "Ready", "nb-NO", () => "Version 0.9.15 is available.",
            "Open release page", () => true, () => Task.CompletedTask, () => Task.CompletedTask,
            () => { }, () => { }, () => { }, () => { });
        using var form = new SettingsForm(new AppSettings(), _ =>
            new HotkeyProbeResult(HotkeyProbeStatus.Available, "Available"), actions);
        var updateAction = Descendants(form).OfType<Button>().Single(button => button.Text == "Open release page");
        True(updateAction.Enabled, "The safe manual release-page action is disabled after an update is available.");
        True(!Descendants(form).OfType<Button>().Any(button => button.Text == "Install update" && button.Enabled),
            "Unsigned Settings misleadingly offers enabled automatic installation.");
    }

    private static void UpdaterCheckScheduleIsBounded()
    {
        Equal(30_000, UpdateCheckSchedule.InitialIntervalMilliseconds);
        Equal(6 * 60 * 60 * 1000, UpdateCheckSchedule.RepeatIntervalMilliseconds);
        True(UpdateCheckSchedule.RepeatIntervalMilliseconds > UpdateCheckSchedule.InitialIntervalMilliseconds,
            "Recurring update checks are unexpectedly more frequent than the initial startup check.");
    }

    private static void InstallerRecognizesRunningExecutablePath()
    {
        const string root = @"C:\Users\Example\AppData\Local\Programs\VinceProductivityTray";
        True(VinceProductivityTray.Setup.Program.IsInstalledApplicationProcessPath(
                Path.Combine(root, "VinceProductivityTray.exe"), root),
            "The installed tray executable was not recognized by path.");
        True(VinceProductivityTray.Setup.Program.IsInstalledApplicationProcessPath(
                Path.Combine(root.ToUpperInvariant(), "VINCEPRODUCTIVITYTRAY.EXE"), root),
            "Installed-path process recognition is unexpectedly case-sensitive.");
        True(!VinceProductivityTray.Setup.Program.IsInstalledApplicationProcessPath(
                @"C:\Other\VinceProductivityTray.exe", root),
            "An unrelated executable with the same name was treated as the installed app.");
        True(!VinceProductivityTray.Setup.Program.IsInstalledApplicationProcessPath(
                Path.Combine(root, "Uninstall.exe"), root),
            "The installer/uninstaller process was mistaken for the running tray app.");
    }

    private static void RecorderSettingsAreSafe()
    {
        var folder = @"C:\Opptak\Moter og blabar";
        var text = PowerShellRecorderController.BuildSettingsText(folder);
        True(text.Contains($"Utmappe = {folder}", StringComparison.Ordinal), "Recorder output folder changed.");
        True(text.Contains("Transkriber = nei", StringComparison.Ordinal), "Local transcription was not parked.");
        True(!text.Contains("api", StringComparison.OrdinalIgnoreCase), "Recorder settings unexpectedly reference an API.");
        _ = PowerShellRecorderController.IsTeamsUsingMicrophone();
    }

    private static string NewInstallerTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "VinceProductivityTray.InstallerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void InstallerRollbackRestoresPrevious()
    {
        var root = NewInstallerTestRoot();
        var install = Path.Combine(root, "install");
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(install, "version.txt"), "old");
        File.WriteAllText(Path.Combine(staging, "version.txt"), "new");
        var threw = false;
        try
        {
            InstallTransaction.Commit(staging, install, Path.Combine(root, "rollback"),
                Path.Combine(root, "failed"), Path.Combine(root, "journal.json"),
                () => throw new InvalidOperationException("synthetic validation failure"));
        }
        catch (InvalidOperationException) { threw = true; }
        True(threw, "Synthetic validation failure did not propagate.");
        Equal("old", File.ReadAllText(Path.Combine(install, "version.txt")));
        Equal("new", File.ReadAllText(Path.Combine(root, "failed", "version.txt")));
        True(!File.Exists(Path.Combine(root, "journal.json")), "Completed rollback left a stale journal.");
    }

    private static void InstallerRecoversInterruptedReplacement()
    {
        var root = NewInstallerTestRoot();
        var install = Path.Combine(root, "install");
        var rollback = Path.Combine(root, "rollback");
        var failed = Path.Combine(root, "failed");
        var journal = Path.Combine(root, "journal.json");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(rollback);
        File.WriteAllText(Path.Combine(install, "version.txt"), "new");
        File.WriteAllText(Path.Combine(rollback, "version.txt"), "old");
        File.WriteAllText(journal, JsonSerializer.Serialize(new InstallJournal(
            "test", Path.Combine(root, "staging"), install, rollback, failed, true, "NewMoved")));
        InstallTransaction.RecoverIfNeeded(journal);
        Equal("old", File.ReadAllText(Path.Combine(install, "version.txt")));
        Equal("new", File.ReadAllText(Path.Combine(failed, "version.txt")));
        True(!File.Exists(journal), "Recovery left a stale journal.");
    }

    private static void InstallerSupportsRepeatedUpgrade()
    {
        var root = NewInstallerTestRoot();
        var install = Path.Combine(root, "install");
        var first = Path.Combine(root, "first");
        Directory.CreateDirectory(first);
        File.WriteAllText(Path.Combine(first, "version.txt"), "one");
        InstallTransaction.Commit(first, install, Path.Combine(root, "rollback-one"),
            Path.Combine(root, "failed-one"), Path.Combine(root, "journal.json"), () => { });
        var second = Path.Combine(root, "second");
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(second, "version.txt"), "two");
        InstallTransaction.Commit(second, install, Path.Combine(root, "rollback-two"),
            Path.Combine(root, "failed-two"), Path.Combine(root, "journal.json"), () => { });
        Equal("two", File.ReadAllText(Path.Combine(install, "version.txt")));
        Equal("one", File.ReadAllText(Path.Combine(root, "rollback-two", "version.txt")));
    }

    private static void UserShellIntegrationUpgradesIdempotently()
    {
        var folder = NewNoteFolder();
        var store = new FakeUserShellIntegrationStore(folder)
        {
            RunValueExisted = true,
            RunValue = "existing-startup-command",
            UnrelatedValue = "preserve-me",
        };
        var shortcut = TestShortcut(folder);

        UserShellIntegrationManager.Apply(store, TestRegistration("0.9.9"), shortcut);
        UserShellIntegrationManager.Apply(store, TestRegistration("0.9.10"), shortcut);

        Equal("0.9.10", store.Registration?.DisplayVersion);
        Equal("preserve-me", store.UnrelatedValue);
        Equal("existing-startup-command", store.RunValue);
        True(File.Exists(shortcut.Path), "The app-owned Start-menu shortcut was not written.");
        Equal(shortcut.TargetPath, File.ReadAllText(shortcut.Path, Encoding.UTF8));
    }

    private static void UserShellIntegrationApplyRollsBack()
    {
        var folder = NewNoteFolder();
        var shortcut = TestShortcut(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(shortcut.Path)!);
        File.WriteAllText(shortcut.Path, "old-shortcut", Encoding.UTF8);
        var store = new FakeUserShellIntegrationStore(folder)
        {
            Registration = TestRegistration("0.9.8"),
            RunValueExisted = true,
            RunValue = "old-run",
            FailShortcutWrite = true,
        };

        try
        {
            UserShellIntegrationManager.Apply(store, TestRegistration("0.9.10"), shortcut);
            throw new InvalidOperationException("The simulated shortcut failure was not surfaced.");
        }
        catch (IOException)
        {
        }

        Equal("0.9.8", store.Registration?.DisplayVersion);
        Equal("old-shortcut", File.ReadAllText(shortcut.Path, Encoding.UTF8));
        Equal("old-run", store.RunValue);
    }

    private static void UserShellIntegrationRemovalIsScoped()
    {
        var folder = NewNoteFolder();
        var shortcut = TestShortcut(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(shortcut.Path)!);
        File.WriteAllText(shortcut.Path, "app-shortcut", Encoding.UTF8);
        var store = new FakeUserShellIntegrationStore(folder)
        {
            Registration = TestRegistration("0.9.10"),
            RunValueExisted = true,
            RunValue = "app-run",
            UnrelatedValue = "preserve-me",
        };
        var moved = false;

        UserShellIntegrationManager.Remove(store, () => moved = true);

        True(moved, "Recoverable program-file removal was not invoked.");
        True(store.Registration is null, "Installed apps registration remained after uninstall.");
        True(!File.Exists(shortcut.Path), "App-owned shortcut remained after uninstall.");
        True(!store.RunValueExisted, "App-owned startup value remained after uninstall.");
        Equal("preserve-me", store.UnrelatedValue);
    }

    private static void UserShellIntegrationRemovalRollsBack()
    {
        var folder = NewNoteFolder();
        var shortcut = TestShortcut(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(shortcut.Path)!);
        File.WriteAllText(shortcut.Path, "app-shortcut", Encoding.UTF8);
        var store = new FakeUserShellIntegrationStore(folder)
        {
            Registration = TestRegistration("0.9.10"),
            RunValueExisted = true,
            RunValue = "app-run",
        };

        try
        {
            UserShellIntegrationManager.Remove(store, () => throw new IOException("simulated move failure"));
            throw new InvalidOperationException("The simulated uninstall failure was not surfaced.");
        }
        catch (IOException)
        {
        }

        Equal("0.9.10", store.Registration?.DisplayVersion);
        Equal("app-shortcut", File.ReadAllText(shortcut.Path, Encoding.UTF8));
        Equal("app-run", store.RunValue);
    }

    private static InstalledAppRegistration TestRegistration(string version) => new(
        "Vince Productivity Tray", version, "Vince AS", @"C:\App\VinceProductivityTray.exe,0",
        @"C:\App", "\"C:\\App\\Uninstall.exe\" --uninstall", 1234);

    private static StartMenuShortcut TestShortcut(string folder) => new(
        Path.Combine(folder, "Start Menu", "Vince Productivity Tray.lnk"),
        @"C:\App\VinceProductivityTray.exe", @"C:\App", @"C:\App\VinceProductivityTray.exe,0",
        "Open Vince Productivity Tray");

    private static NativeBenchmarks RunNativeDictionaryChecks()
    {
        using var service = new NativeSpellService();
        Notes.Add($"Windows spell checker languages: {string.Join(", ", service.SupportedLanguages)}");
        True(service.SupportedLanguages.Any(language => language.StartsWith("en-", StringComparison.OrdinalIgnoreCase)), "English dictionary is not available.");

        var hasNorwegian = service.SupportedLanguages.Any(language =>
            language.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) ||
            language.StartsWith("no-", StringComparison.OrdinalIgnoreCase) ||
            language.StartsWith("nn-", StringComparison.OrdinalIgnoreCase));
        Notes.Add(hasNorwegian
            ? "A Norwegian Windows dictionary is available."
            : "Norwegian Windows Basic Typing is not installed; Norwegian correction is disabled until nb-NO is installed.");

        var engine = new ConservativeCorrectionEngine(service);
        var englishSample = "This sentnce has a deliberate spelling error.\r\nThe paragraf break must remain.";
        var english = engine.Correct(englishSample);
        True(english.LanguageTag?.StartsWith("en-", StringComparison.OrdinalIgnoreCase) == true, "Native engine did not select English.");
        True(english.CorrectedText.Contains("sentence", StringComparison.OrdinalIgnoreCase), "Native dictionary did not correct sentnce.");
        True(english.CorrectedText.Contains("\r\n", StringComparison.Ordinal), "Native correction removed the paragraph break.");

        CorrectionResult? norwegian = null;
        if (hasNorwegian)
        {
            norwegian = engine.Correct("Dette er en setnign på norsk.");
            True(norwegian.LanguageTag?.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) == true,
                $"Native engine did not select Norwegian Bokmål; selected {norwegian.LanguageTag ?? "none"}.");
            True(norwegian.CorrectedText.Contains("setning", StringComparison.OrdinalIgnoreCase),
                $"Native Norwegian dictionary did not correct setnign: {norwegian.CorrectedText}");
            Passed.Add("Native Norwegian Bokmål detection and correction");

            var exactPhrase = engine.Correct("hva med na da?");
            Equal("hva med nå da?", exactPhrase.CorrectedText);
            Passed.Add("Native Norwegian exact phrase correction");

            var multipleNorwegian = engine.Correct("vorfor komer du ike?");
            Equal("nb-NO", multipleNorwegian.LanguageTag);
            Equal("hvorfor kommer du ikke?", multipleNorwegian.CorrectedText);
            Passed.Add("Native Norwegian multi-error correction");
        }

        var multipleEnglish = engine.Correct("Ths sentnce has erors.");
        True(multipleEnglish.LanguageTag?.StartsWith("en-", StringComparison.OrdinalIgnoreCase) == true,
            $"Native engine did not select English for a multi-error sentence; selected {multipleEnglish.LanguageTag ?? "none"}.");
        Equal("This sentence has errors.", multipleEnglish.CorrectedText);
        Passed.Add("Native English multi-error correction");

        for (var index = 0; index < 10; index++)
        {
            _ = engine.Correct(index % 2 == 0 || norwegian is null ? englishSample : "Dette er en setnign på norsk.");
        }

        var durations = new double[200];
        for (var index = 0; index < durations.Length; index++)
        {
            var watch = Stopwatch.StartNew();
            _ = engine.Correct(index % 2 == 0 || norwegian is null ? englishSample : "Dette er en setnign på norsk.");
            watch.Stop();
            durations[index] = watch.Elapsed.TotalMilliseconds;
        }

        Array.Sort(durations);
        Passed.Add("Native Windows dictionary correction and 200-iteration latency benchmark");
        return new NativeBenchmarks(
            service.SupportedLanguages.ToArray(),
            hasNorwegian,
            durations.Average(),
            Percentile(durations, 0.50),
            Percentile(durations, 0.95),
            durations[^1],
            english.LanguageTag,
            english.CorrectedText,
            norwegian?.LanguageTag,
            norwegian?.CorrectedText);
    }

    private static ClipboardRoundtripMetrics RunSyntheticSelectionRoundtripWithRetry()
    {
        Exception? lastFailure = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return RunSyntheticSelectionRoundtrip();
            }
            catch (Exception exception)
            {
                lastFailure = exception;
                Application.DoEvents();
                Thread.Sleep(100);
            }
        }

        throw lastFailure!;
    }

    private static ClipboardRoundtripMetrics RunSyntheticSelectionRoundtrip()
    {
        using var service = FakeSpellService.EnglishOnly(("Helllo", "Hello"), ("Seconnd", "Second"));
        var engine = new ConservativeCorrectionEngine(service);

        var clipboardData = new DataObject();
        clipboardData.SetData(DataFormats.UnicodeText, "clipboard sentinel");
        clipboardData.SetData("ChristianSpellHelper.Test", "custom sentinel");
        Clipboard.SetDataObject(clipboardData, copy: true);

        using var form = new Form
        {
            Text = "Christian Spell Helper synthetic test",
            Width = 420,
            Height = 180,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(20, 20),
            TopMost = true,
        };
        using var editor = new RichTextBox { Dock = DockStyle.Fill, DetectUrls = false };
        form.Controls.Add(editor);
        editor.Text = "Helllo world\r\nSeconnd paragraph";
        editor.Select(0, 6);
        editor.SelectionFont = new Font("Consolas", 18f, FontStyle.Bold);
        editor.Select(6, editor.TextLength - 6);
        editor.SelectionFont = new Font("Arial", 9f, FontStyle.Italic);
        form.Show();
        form.Activate();
        editor.Focus();
        editor.SelectAll();
        Application.DoEvents();
        Thread.Sleep(60);

        var watch = Stopwatch.StartNew();
        var result = new SelectionTransaction(engine).Run();
        watch.Stop();
        Application.DoEvents();

        True(result.Status == SelectionTransactionStatus.Replaced, $"Expected Replaced, got {result.Status}: {result.Message}");
        Equal("Hello world\nSecond paragraph", editor.Text.Replace("\r\n", "\n", StringComparison.Ordinal));
        string? restoredText = null;
        object? restoredCustom = null;
        RetryClipboard(() => restoredText = Clipboard.GetText(TextDataFormat.UnicodeText));
        RetryClipboard(() => restoredCustom = Clipboard.GetData("ChristianSpellHelper.Test"));
        Equal("clipboard sentinel", restoredText);
        Equal("custom sentinel", restoredCustom as string);

        var formats = new HashSet<string>();
        for (var index = 0; index < editor.TextLength; index++)
        {
            if (editor.Text[index] is '\r' or '\n')
            {
                continue;
            }

            editor.Select(index, 1);
            var font = editor.SelectionFont;
            if (font is not null)
            {
                formats.Add($"{font.FontFamily.Name}|{font.SizeInPoints:F1}|{font.Style}");
            }
        }

        form.Close();
        Passed.Add("Synthetic RichTextBox selection/copy/check/plain-text-paste/clipboard-restore roundtrip");
        return new ClipboardRoundtripMetrics(
            result.TotalElapsed.TotalMilliseconds,
            result.EngineElapsed.TotalMilliseconds,
            watch.Elapsed.TotalMilliseconds,
            formats.Count == 1,
            formats.OrderBy(static value => value).ToArray(),
            true,
            true);
    }

    private static LiveHotkeyMetrics RunLiveResidentHotkeyRoundtrip()
    {
        var helpers = Process.GetProcessesByName("ChristianSpellHelper");
        True(helpers.Length == 1, $"Expected one resident helper process, found {helpers.Length}.");
        foreach (var helper in helpers) helper.Dispose();

        var clipboardData = new DataObject();
        clipboardData.SetData(DataFormats.UnicodeText, "live hotkey clipboard sentinel");
        clipboardData.SetData("ChristianSpellHelper.LiveTest", "live custom sentinel");
        RetryClipboard(() => Clipboard.SetDataObject(clipboardData, copy: true));

        using var form = new Form
        {
            Text = "Christian Spell Helper live hotkey scratch test",
            Width = 480,
            Height = 180,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(30, 30),
            TopMost = true,
        };
        using var editor = new RichTextBox { Dock = DockStyle.Fill, DetectUrls = false };
        form.Controls.Add(editor);
        editor.Text = "Dette er en setnign på norsk.\r\nNeste paragraf.";
        editor.Select(0, 10);
        editor.SelectionFont = new Font("Arial", 16f, FontStyle.Bold);
        editor.Select(10, editor.TextLength - 10);
        editor.SelectionFont = new Font("Calibri", 9f, FontStyle.Italic);
        form.Show();
        form.Activate();
        ForceForeground(form.Handle);
        editor.Focus();
        Application.DoEvents();
        Thread.Sleep(100);
        editor.SelectAll();
        Application.DoEvents();
        True(GetForegroundWindow() == form.Handle, "The synthetic scratch editor could not become the foreground window.");
        Equal(editor.TextLength, editor.SelectionLength);

        var watch = Stopwatch.StartNew();
        True(TriggerSpellHotkey(), "Windows rejected the Ctrl+Shift+X test input.");
        var expected = "Dette er en setning på norsk.\nNeste paragraf.";
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            if (editor.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Equals(expected, StringComparison.Ordinal))
            {
                break;
            }

            Thread.Sleep(10);
        }

        watch.Stop();
        Equal(expected, editor.Text.Replace("\r\n", "\n", StringComparison.Ordinal));

        string? restoredText = null;
        object? restoredCustom = null;
        RetryClipboard(() => restoredText = Clipboard.GetText(TextDataFormat.UnicodeText));
        RetryClipboard(() => restoredCustom = Clipboard.GetData("ChristianSpellHelper.LiveTest"));
        Equal("live hotkey clipboard sentinel", restoredText);
        Equal("live custom sentinel", restoredCustom as string);

        var formats = new HashSet<string>();
        for (var index = 0; index < editor.TextLength; index++)
        {
            if (editor.Text[index] is '\r' or '\n') continue;
            editor.Select(index, 1);
            var font = editor.SelectionFont;
            if (font is not null) formats.Add($"{font.FontFamily.Name}|{font.SizeInPoints:F1}|{font.Style}");
        }

        form.Close();
        Passed.Add("Live resident Ctrl+Shift+X Norwegian roundtrip");
        return new LiveHotkeyMetrics(
            watch.Elapsed.TotalMilliseconds,
            formats.Count == 1,
            formats.OrderBy(static value => value).ToArray(),
            true);
    }

    private static LiveHotkeyMetrics RunLiveResidentHotkeyRoundtripWithRetry()
    {
        Exception? lastFailure = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return RunLiveResidentHotkeyRoundtrip();
            }
            catch (Exception exception)
            {
                lastFailure = exception;
                Application.DoEvents();
                Thread.Sleep(200);
            }
        }

        throw lastFailure!;
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static void RetryClipboard(Action action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (System.Runtime.InteropServices.ExternalException) when (attempt < 19)
            {
                Application.DoEvents();
                Thread.Sleep(Math.Min(10 * (attempt + 1), 50));
            }
        }
    }

    private static void ForceForeground(IntPtr window)
    {
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread &&
                       AttachThreadInput(currentThread, foregroundThread, attach: true);
        try
        {
            _ = BringWindowToTop(window);
            _ = SetForegroundWindow(window);
        }
        finally
        {
            if (attached) _ = AttachThreadInput(currentThread, foregroundThread, attach: false);
        }
    }

    private static bool TriggerSpellHotkey()
    {
        const ushort control = 0x11;
        const ushort shift = 0x10;
        const ushort x = 0x58;
        var inputs = new[]
        {
            CreateTestKey(control, false), CreateTestKey(shift, false), CreateTestKey(x, false),
            CreateTestKey(x, true), CreateTestKey(shift, true), CreateTestKey(control, true),
        };
        return SendInputForTest((uint)inputs.Length, inputs, Marshal.SizeOf<TestInput>()) == inputs.Length;
    }

    private static TestInput CreateTestKey(ushort key, bool keyUp) => new()
    {
        Type = 1,
        Union = new TestInputUnion
        {
            Keyboard = new TestKeyboardInput { VirtualKey = key, Flags = keyUp ? 0x0002u : 0 },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct TestInput
    {
        public uint Type;
        public TestInputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct TestInputUnion
    {
        [FieldOffset(0)] public TestKeyboardInput Keyboard;
        [FieldOffset(0)] public TestMouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TestKeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TestMouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
    private static extern uint SendInputForTest(uint inputCount, TestInput[] inputs, int inputSize);

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Passed.Add(name);
        }
        catch (Exception exception)
        {
            Failed.Add($"{name}: {exception.Message}");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record TestReport(
        DateTimeOffset TimestampUtc,
        string OperatingSystem,
        string ProcessArchitecture,
        IReadOnlyList<string> Passed,
        IReadOnlyList<string> Failed,
        IReadOnlyList<string> Notes,
        NativeBenchmarks? NativeDictionary,
        ClipboardRoundtripMetrics? ClipboardRoundtrip,
        LiveHotkeyMetrics? LiveHotkeyRoundtrip);

    private sealed record NativeBenchmarks(
        IReadOnlyList<string> WindowsLanguages,
        bool NorwegianAvailable,
        double MeanMilliseconds,
        double P50Milliseconds,
        double P95Milliseconds,
        double MaximumMilliseconds,
        string? EnglishDetectedLanguage,
        string EnglishCorrectedSample,
        string? NorwegianDetectedLanguage,
        string? NorwegianCorrectedSample);

    private sealed record ClipboardRoundtripMetrics(
        double TransactionMilliseconds,
        double EngineMilliseconds,
        double WallClockMilliseconds,
        bool UniformDestinationFormatting,
        IReadOnlyList<string> ResultingFormats,
        bool ParagraphBreaksPreserved,
        bool ClipboardFormatsRestored);

    private sealed record LiveHotkeyMetrics(
        double WallClockMilliseconds,
        bool UniformDestinationFormatting,
        IReadOnlyList<string> ResultingFormats,
        bool ClipboardFormatsRestored);

    private sealed class FakeUserShellIntegrationStore : IUserShellIntegrationStore
    {
        private readonly string _shortcutPath;

        public FakeUserShellIntegrationStore(string folder)
        {
            _shortcutPath = TestShortcut(folder).Path;
        }

        public InstalledAppRegistration? Registration { get; set; }
        public bool RunValueExisted { get; set; }
        public string? RunValue { get; set; }
        public string UnrelatedValue { get; set; } = "unrelated";
        public bool FailShortcutWrite { get; set; }

        public UserShellIntegrationSnapshot Capture()
        {
            IReadOnlyList<RegistrySnapshotValue>? values = Registration is null
                ? null
                :
                [
                    new("DisplayName", Registration.DisplayName, RegistryValueKind.String),
                    new("DisplayVersion", Registration.DisplayVersion, RegistryValueKind.String),
                    new("Publisher", Registration.Publisher, RegistryValueKind.String),
                    new("DisplayIcon", Registration.DisplayIcon, RegistryValueKind.String),
                    new("InstallLocation", Registration.InstallLocation, RegistryValueKind.String),
                    new("UninstallString", Registration.UninstallString, RegistryValueKind.String),
                    new("EstimatedSize", Registration.EstimatedSizeKilobytes, RegistryValueKind.DWord),
                ];
            return new UserShellIntegrationSnapshot(
                values,
                File.Exists(_shortcutPath) ? File.ReadAllBytes(_shortcutPath) : null,
                RunValueExisted,
                RunValue);
        }

        public void WriteInstalledApp(InstalledAppRegistration registration) => Registration = registration;

        public void WriteShortcut(StartMenuShortcut shortcut)
        {
            if (FailShortcutWrite) throw new IOException("simulated shortcut failure");
            Directory.CreateDirectory(Path.GetDirectoryName(shortcut.Path)!);
            File.WriteAllText(shortcut.Path, shortcut.TargetPath, Encoding.UTF8);
        }

        public void RemoveInstalledApp() => Registration = null;

        public void RemoveShortcut()
        {
            if (File.Exists(_shortcutPath)) File.Delete(_shortcutPath);
        }

        public void RemoveRunValue()
        {
            RunValueExisted = false;
            RunValue = null;
        }

        public void Restore(UserShellIntegrationSnapshot snapshot)
        {
            Registration = SnapshotRegistration(snapshot.InstalledAppValues);
            if (File.Exists(_shortcutPath)) File.Delete(_shortcutPath);
            if (snapshot.ShortcutBytes is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_shortcutPath)!);
                File.WriteAllBytes(_shortcutPath, snapshot.ShortcutBytes);
            }
            RunValueExisted = snapshot.RunValueExisted;
            RunValue = snapshot.RunValue;
        }

        private static InstalledAppRegistration? SnapshotRegistration(IReadOnlyList<RegistrySnapshotValue>? values)
        {
            if (values is null) return null;
            object Read(string name) => values.Single(value => value.Name == name).Value;
            return new InstalledAppRegistration(
                (string)Read("DisplayName"),
                (string)Read("DisplayVersion"),
                (string)Read("Publisher"),
                (string)Read("DisplayIcon"),
                (string)Read("InstallLocation"),
                (string)Read("UninstallString"),
                (int)Read("EstimatedSize"));
        }
    }

    private sealed class FakeHotkeyNativeApi : IHotkeyNativeApi
    {
        private const uint ModNoRepeat = 0x4000;
        private readonly Dictionary<int, (uint Modifiers, uint VirtualKey)> _active = [];
        private readonly HashSet<(uint Modifiers, uint VirtualKey)> _occupied = [];

        public int ActiveCount => _active.Count;

        public bool Register(IntPtr window, int id, uint modifiers, uint virtualKey)
        {
            var value = (modifiers, virtualKey);
            if (_occupied.Contains(value) || _active.Values.Contains(value)) return false;
            _active[id] = value;
            return true;
        }

        public bool Unregister(IntPtr window, int id) => _active.Remove(id);

        public void Occupy(HotkeyDefinition definition) =>
            _occupied.Add((definition.Modifiers | ModNoRepeat, definition.VirtualKey));

        public bool IsActive(HotkeyDefinition definition) =>
            _active.Values.Contains((definition.Modifiers | ModNoRepeat, definition.VirtualKey));
    }

    private sealed partial class FakeSpellService : ISpellService
    {
        private readonly Dictionary<string, string[]> _suggestions;
        private readonly Func<string, string, IReadOnlyList<SpellIssue>>? _customCheck;
        private readonly Func<string, string, IReadOnlyList<string>>? _customSuggest;

        private FakeSpellService(
            IReadOnlyList<string> languages,
            Dictionary<string, string[]> suggestions,
            Func<string, string, IReadOnlyList<SpellIssue>>? customCheck = null,
            Func<string, string, IReadOnlyList<string>>? customSuggest = null)
        {
            SupportedLanguages = languages;
            _suggestions = suggestions;
            _customCheck = customCheck;
            _customSuggest = customSuggest;
        }

        public IReadOnlyList<string> SupportedLanguages { get; }

        public static FakeSpellService EnglishOnly(params (string Misspelling, string Correction)[] pairs) =>
            WithSuggestions(
                ["en-US"],
                pairs.ToDictionary(pair => pair.Misspelling, pair => new[] { pair.Correction }, StringComparer.OrdinalIgnoreCase));

        public static FakeSpellService WithSuggestions(
            IReadOnlyList<string> languages,
            Dictionary<string, string[]> suggestions) => new(languages, suggestions);

        public static FakeSpellService CaseOnlyReplacementWithSuggestion(
            string misspelling, string replacement, string suggestion)
        {
            var suggestions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [misspelling] = [replacement, suggestion],
            };
            return new FakeSpellService(["nb-NO"], suggestions, (_, text) => Tokens().Matches(text)
                .Where(match => match.Value.Equals(misspelling, StringComparison.OrdinalIgnoreCase))
                .Select(match => new SpellIssue(match.Index, match.Length, NativeCorrectiveAction.Replace, replacement))
                .ToArray());
        }

        public static FakeSpellService LanguageAware()
        {
            var suggestions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["sentnce"] = ["sentence"],
            };
            var englishWords = new HashSet<string>(["This", "sentnce", "is", "written", "in", "English"], StringComparer.OrdinalIgnoreCase);
            var norwegianWords = new HashSet<string>(["er", "i"], StringComparer.OrdinalIgnoreCase);
            return new FakeSpellService(["en-US", "nb-NO"], suggestions, (language, text) =>
            {
                var known = language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? englishWords : norwegianWords;
                return Tokens().Matches(text)
                    .Where(match => !known.Contains(match.Value) || match.Value.Equals("sentnce", StringComparison.OrdinalIgnoreCase))
                    .Select(match => new SpellIssue(match.Index, match.Length, NativeCorrectiveAction.GetSuggestions, null))
                    .ToArray();
            });
        }

        public static FakeSpellService MultipleTypoExamples()
        {
            var misspellings = new HashSet<string>(
                ["vorfor", "komer", "ike", "Ths", "sentnce", "erors"],
                StringComparer.OrdinalIgnoreCase);
            var norwegian = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["vorfor"] = ["v\u00e6rform", "hvorfor", "v\u00e6rfore"],
                ["komer"] = ["kommer", "kromer", "komet"],
                ["ike"] = ["ikke", "IKE", "Ikea", "Iker"],
                ["Ths"] = ["THCs", "This", "Thus"],
                ["sentnce"] = ["sentere", "senene", "senter"],
                ["erors"] = ["erots", "eros", "efors"],
            };
            var english = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["vorfor"] = ["variform", "verbform", "horror"],
                ["komer"] = ["comer", "homer", "Kromer"],
                ["ike"] = ["ice", "Ike", "eke", "like"],
                ["Ths"] = ["This", "Thus", "Thys", "The"],
                ["sentnce"] = ["sentence"],
                ["erors"] = ["errors", "error's", "errs"],
            };
            return new FakeSpellService(
                ["en-US", "nb-NO"],
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                (_, text) => Tokens().Matches(text)
                    .Where(match => misspellings.Contains(match.Value))
                    .Select(match => new SpellIssue(match.Index, match.Length, NativeCorrectiveAction.GetSuggestions, null))
                    .ToArray(),
                (language, word) =>
                {
                    var suggestions = language.StartsWith("nb-", StringComparison.OrdinalIgnoreCase)
                        ? norwegian
                        : english;
                    return suggestions.TryGetValue(word, out var values) ? values : [];
                });
        }

        public static FakeSpellService NorwegianPhraseExample()
        {
            var accepted = new HashSet<string>(
                ["hva", "me", "med", "nå", "da", "daa"],
                StringComparer.OrdinalIgnoreCase);
            return new FakeSpellService(
                ["nb-NO"],
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                (_, text) => Tokens().Matches(text)
                    .Where(match => !accepted.Contains(match.Value))
                    .Select(match => new SpellIssue(match.Index, match.Length, NativeCorrectiveAction.GetSuggestions, null))
                    .ToArray());
        }

        public IReadOnlyList<SpellIssue> Check(string languageTag, string text)
        {
            if (_customCheck is not null)
            {
                return _customCheck(languageTag, text);
            }

            return Tokens().Matches(text)
                .Where(match => _suggestions.ContainsKey(match.Value))
                .Select(match => new SpellIssue(match.Index, match.Length, NativeCorrectiveAction.GetSuggestions, null))
                .ToArray();
        }

        public IReadOnlyList<string> Suggest(string languageTag, string word, int maximum)
        {
            var values = _customSuggest is null
                ? (_suggestions.TryGetValue(word, out var configured) ? configured : [])
                : _customSuggest(languageTag, word);
            return values.Take(maximum).ToArray();
        }

        public void Dispose()
        {
        }

        [GeneratedRegex(@"[\p{L}]+")]
        private static partial Regex Tokens();
    }
}
