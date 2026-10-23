using System.Diagnostics;
using System.Net.Http;
using ChristianSpellHelper.Core;

namespace ChristianSpellHelper;

internal sealed class SpellHelperApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _recordingItem;
    private readonly ToolStripMenuItem _noteItem;
    private readonly ToolStripMenuItem _timeItem;
    private readonly HotkeyWindow _hotkeys = new();
    private readonly SelectionTransaction _selectionTransaction;
    private readonly QuickNoteForm _quickNote;
    private readonly TimeCaptureController _timeCapture;
    private readonly IRecorderController _recorder;
    private readonly HttpClient _updateHttp = new();
    private readonly UpdaterConfig _updaterConfig = UpdaterConfig.Load();
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly string _languageStatus;
    private AppSettings _settings;
    private UpdateRelease? _availableUpdate;
    private int _operationActive;

    public SpellHelperApplicationContext(
        ConservativeCorrectionEngine engine,
        IReadOnlyList<string> allInstalledLanguages,
        IRecorderController recorder)
    {
        _settings = AppSettings.Load() with { StartWithWindows = StartupManager.IsEnabled() };
        _selectionTransaction = new SelectionTransaction(engine);
        _recorder = recorder;
        _quickNote = new QuickNoteForm(new DailyNoteWriter(), () => _settings);
        _timeCapture = new TimeCaptureController(() => _settings);
        _languageStatus = BuildLanguageText(engine.LanguageCandidates, allInstalledLanguages);

        _statusItem = new ToolStripMenuItem("Starting…") { Enabled = false };
        _recordingItem = new ToolStripMenuItem("Start Recording", null, (_, _) => ToggleRecording());
        _noteItem = new ToolStripMenuItem("New quick note", null, (_, _) => { if (_settings.QuickNotesEnabled) _quickNote.ShowForEntry(); });
        _timeItem = new ToolStripMenuItem("New time entry", null, (_, _) => { if (_settings.TimeCaptureEnabled) _timeCapture.ShowForEntry(); });
        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => RequestExit());
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_recordingItem, _noteItem, _timeItem, settingsItem, new ToolStripSeparator(), exitItem]);

        _trayIcon = TrayIconLoader.Load();
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Vince Productivity Tray",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => { if (_settings.QuickNotesEnabled) _quickNote.ShowForEntry(); };

        RegisterHotkeys();
        _recorder.StatusChanged += (_, _) => UpdateRecorderUi();
        UpdateRecorderUi();
        _updateTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Stop();
            await CheckForUpdatesAsync(manual: false);
        };
        _updateTimer.Start();

        if (!engine.LanguageCandidates.Any(IsNorwegian))
        {
            ShowBalloon("Norwegian dictionary missing",
                "Install Windows Basic Typing for Norwegian Bokmål (nb-NO) to enable Norwegian correction.",
                ToolTipIcon.Warning);
        }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (!_updaterConfig.Enabled || !UpdaterPolicy.IsNotificationConfigured)
        {
            if (manual)
            {
                MessageBox.Show("Update checks are disabled until the GitHub repository visibility and name are confirmed.",
                    "Vince Productivity Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }

        if (!manual && !UpdateDeferral.IsDue(_settings.DeferredUpdateUntilUtc, DateTimeOffset.UtcNow)) return;
        try
        {
            var client = new GitHubUpdateClient(_updateHttp);
            var release = await client.CheckAsync(_updaterConfig, GitHubUpdateClient.CurrentVersion(), CancellationToken.None);
            if (release is null)
            {
                if (manual) MessageBox.Show("You already have the latest published version.", "Vince Productivity Tray");
                return;
            }

            RememberAvailableUpdate(release);
            if (!manual && _settings.AutoUpdateEnabled && UpdaterPolicy.IsAutoInstallConfigured)
            {
                await InstallAvailableUpdateAsync(requireConfirmation: false);
                return;
            }
            if (_recorder.IsRecording)
            {
                ShowBalloon($"Update {release.Version} available",
                    "The update will not run during an active recording. Choose Check for updates after recording stops.",
                    ToolTipIcon.Info);
                return;
            }

            var notes = release.ReleaseNotes.Length > 900 ? release.ReleaseNotes[..900] + "…" : release.ReleaseNotes;
            if (!UpdaterPolicy.IsAutoInstallConfigured)
            {
                if (!manual) return;
                var open = MessageBox.Show(
                    $"Version {release.Version} is available.\n\n{notes}\n\nOpen the official GitHub release page to download it manually?",
                    "Vince Productivity Tray update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (open == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(release.ReleasePageUrl) { UseShellExecute = true });
                }
                else
                {
                    _settings = _settings with
                    {
                        DeferredUpdateUntilUtc = UpdateDeferral.Defer(DateTimeOffset.UtcNow, _updaterConfig.DeferredHours),
                    };
                    _settings.Save();
                }
                return;
            }

            if (manual)
            {
                MessageBox.Show(
                    $"Version {release.Version} is available.\n\n{notes}\n\nRight-click the tray icon and choose Install update when you are ready.",
                    "Vince Productivity Tray update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            LocalLog.Write("Update check failed", exception);
            if (manual)
            {
                MessageBox.Show($"The update check failed. The current version is unchanged.\n\n{exception.Message}",
                    "Vince Productivity Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void RememberAvailableUpdate(UpdateRelease release)
    {
        _availableUpdate = release;
        _notifyIcon.Text = $"Vince Productivity Tray - update {release.Version} available";
        ShowBalloon($"Update {release.Version} available",
            UpdaterPolicy.IsAutoInstallConfigured
                ? "Right-click the tray icon and choose Install update when you are ready."
                : "The signed automatic installer is not configured; use the official release page.",
            ToolTipIcon.Info);
    }

    private async Task InstallAvailableUpdateAsync(bool requireConfirmation = true)
    {
        var release = _availableUpdate;
        if (release is null || !UpdaterPolicy.IsAutoInstallConfigured) return;
        if (!UpdateInstallGuard.CanInstall(_recorder.IsRecording, _timeCapture.HasUnsavedDraft))
        {
            if (!requireConfirmation)
            {
                ShowBalloon($"Update {release.Version} waiting",
                    "Automatic update is waiting until recording and Time Capture drafts are idle.", ToolTipIcon.Info);
                return;
            }
            MessageBox.Show(_recorder.IsRecording
                    ? "Stop the active recording before installing an update."
                    : "Submit or clear the unsent Time Capture draft before installing an update.",
                "Vince Productivity Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (requireConfirmation)
        {
            var choice = MessageBox.Show(
                $"Download and install version {release.Version}?\n\nThe app will close, Windows will verify the signed installer, and the app will reopen only after setup succeeds.",
                "Vince Productivity Tray update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (choice != DialogResult.Yes) return;
        }

        try
        {
            var client = new GitHubUpdateClient(_updateHttp);
            var installer = await client.DownloadVerifiedAsync(release, CancellationToken.None);
            if (!UpdateInstallGuard.CanInstall(_recorder.IsRecording, _timeCapture.HasUnsavedDraft))
            {
                if (requireConfirmation)
                    MessageBox.Show("Work became active while the update downloaded. The verified update was staged but will not run.",
                        "Vince Productivity Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    ShowBalloon($"Update {release.Version} staged",
                        "The update will not run until recording and Time Capture drafts are idle.", ToolTipIcon.Info);
                return;
            }

            Process.Start(new ProcessStartInfo(installer, $"--update-pid {Environment.ProcessId}")
            {
                UseShellExecute = true,
            });
            ExitThread();
        }
        catch (Exception exception)
        {
            LocalLog.Write("Update installation failed", exception);
            MessageBox.Show($"The update was not installed. The current version is unchanged.\n\n{exception.Message}",
                "Vince Productivity Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RegisterHotkeys()
    {
        if (TryApplyHotkeys(_settings, out var conflicts))
        {
            SetStatus($"Ready — notes {_settings.NotesHotkey}, spellcheck {_settings.SpellcheckHotkey}", false);
        }
        else
        {
            SetStatus("One or more hotkeys are unavailable", true, ToolTipIcon.Warning,
                string.Join(Environment.NewLine, conflicts) + Environment.NewLine + "No existing hotkey was changed.");
        }
    }

    private bool TryApplyHotkeys(AppSettings settings, out IReadOnlyList<string> conflicts)
    {
        var bindings = new List<HotkeyBinding>();
        try
        {
            if (settings.SpellcheckEnabled)
                bindings.Add(new(HotkeyDefinition.Parse(settings.SpellcheckHotkey), HandleSpellcheck, "Spellcheck"));
            if (settings.QuickNotesEnabled)
                bindings.Add(new(HotkeyDefinition.Parse(settings.NotesHotkey), _quickNote.ShowForEntry, "Quick note"));
            if (settings.RecordingEnabled && _recorder.IsAvailable)
                bindings.Add(new(HotkeyDefinition.Parse(settings.RecordingHotkey), ToggleRecording, "Recording"));
            if (settings.TimeCaptureEnabled)
                bindings.Add(new(HotkeyDefinition.Parse(settings.TimeCaptureHotkey), _timeCapture.ShowForEntry, "Time capture"));
        }
        catch (FormatException exception)
        {
            conflicts = [exception.Message];
            return false;
        }

        return _hotkeys.TryReplaceAll(bindings, out conflicts);
    }

    private void HandleSpellcheck()
    {
        if (!_settings.SpellcheckEnabled) return;
        if (Interlocked.Exchange(ref _operationActive, 1) != 0) return;
        try
        {
            SetStatus("Checking selection…", false);
            var result = _selectionTransaction.Run();
            var timing = $"{result.TotalElapsed.TotalMilliseconds:F0} ms";
            if (result.Status == SelectionTransactionStatus.Replaced)
            {
                SetStatus($"Corrected ({timing})", false);
                LocalLog.Write($"Corrected {result.CorrectionCount} word(s) as {result.LanguageTag ?? "unknown"}; {timing}. No text content logged.");
            }
            else if (result.Status == SelectionTransactionStatus.NoChange)
            {
                SetStatus($"No safe change ({timing})", false);
                LocalLog.Write($"Spellcheck result {result.Status}; {timing}. No text content logged.");
            }
            else
            {
                SetStatus("Selection not changed", true, ToolTipIcon.Warning, result.Message);
                LocalLog.Write($"Spellcheck result {result.Status}; {timing}. No text content logged.");
            }
        }
        catch (Exception exception)
        {
            LocalLog.Write("Unexpected spellcheck failure", exception);
            SetStatus("Selection not changed", true, ToolTipIcon.Error, "The selection could not be checked safely.");
        }
        finally
        {
            Volatile.Write(ref _operationActive, 0);
        }
    }

    private void ToggleRecording()
    {
        if (!_recorder.IsRecording && !_settings.RecordingEnabled) return;
        try { _recorder.Toggle(); }
        catch (Exception exception)
        {
            ShowBalloon("Recorder unavailable", exception.Message, ToolTipIcon.Warning);
        }
        UpdateRecorderUi();
    }

    private void RequestExit()
    {
        if (_recorder.IsRecording)
        {
            var choice = MessageBox.Show(
                "A recording is active. Stop it gracefully and exit the tray app?",
                "Vince Productivity Tray", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes) return;
            try { _recorder.Stop(); }
            catch (Exception exception)
            {
                MessageBox.Show($"The recording could not be stopped safely. The tray app will remain open.\n\n{exception.Message}",
                    "Vince Productivity Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        ExitThread();
    }

    private void UpdateRecorderUi()
    {
        var state = FeatureGate.TrayActions(_settings, _recorder.IsRecording, _recorder.IsAvailable);
        _recordingItem.Text = state.RecordingText;
        _recordingItem.Enabled = state.RecordingEnabled;
        _noteItem.Enabled = state.NotesEnabled;
        _timeItem.Enabled = state.TimeCaptureEnabled;
    }

    private void ShowSettings()
    {
        var actions = new SettingsActions(
            GitHubUpdateClient.CurrentVersion().ToString(),
            () => _statusItem.Text ?? string.Empty,
            _languageStatus,
            UpdateStatusText,
            () => _availableUpdate is not null && UpdaterPolicy.IsAutoInstallConfigured,
            () => CheckForUpdatesAsync(manual: true),
            () => InstallAvailableUpdateAsync(),
            () => OpenFolder(_settings.NotesFolder),
            () => OpenFolder(_settings.RecordingFolder),
            () => OpenFolder(_settings.TimeCaptureFolder),
            () => OpenFolder(LocalLog.DirectoryPath));
        using var form = new SettingsForm(_settings, _hotkeys.Probe, actions);
        if (form.ShowDialog() != DialogResult.OK) return;
        try
        {
            var updated = form.GetSettings();
            if (!_settings.StartWithWindows && updated.StartWithWindows)
            {
                var approve = MessageBox.Show(
                    "Enable per-user startup now? This writes a Windows Run entry and may require company IT approval for unsigned internal software.",
                    "Start with Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (approve != DialogResult.Yes) updated = updated with { StartWithWindows = false };
            }
            Directory.CreateDirectory(updated.NotesFolder);
            Directory.CreateDirectory(updated.RecordingFolder);
            if (updated.TimeCaptureEnabled) Directory.CreateDirectory(updated.TimeCaptureFolder);
            var previous = _settings;
            if (!TryApplyHotkeys(updated, out var conflicts))
            {
                throw new InvalidOperationException(
                    "The new hotkeys could not be registered. Existing hotkeys remain active.\n\n" +
                    string.Join(Environment.NewLine, conflicts));
            }
            try
            {
                updated.Save();
                StartupManager.Apply(updated.StartWithWindows);
                _settings = updated;
            }
            catch
            {
                _ = TryApplyHotkeys(previous, out _);
                throw;
            }
            SetStatus($"Ready - notes {_settings.NotesHotkey}, spellcheck {_settings.SpellcheckHotkey}", false);
            UpdateRecorderUi();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Settings were not saved.\n\n{exception.Message}",
                "Vince Productivity Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string UpdateStatusText()
    {
        if (!_updaterConfig.Enabled) return "Update checking is disabled in this build.";
        if (_availableUpdate is null) return "No newer update has been found in this session.";
        return UpdaterPolicy.IsAutoInstallConfigured
            ? $"Version {_availableUpdate.Version} is available and ready for verified installation."
            : $"Version {_availableUpdate.Version} is available, but direct installation requires an approved Authenticode publisher.";
    }

    private void SetStatus(string status, bool notify, ToolTipIcon icon = ToolTipIcon.Info, string? detail = null)
    {
        _statusItem.Text = status;
        _notifyIcon.Text = status.Length <= 63 ? status : status[..63];
        if (notify) ShowBalloon("Vince Productivity Tray", detail ?? status, icon);
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(3500);
    }

    private static string BuildLanguageText(IReadOnlyList<string> candidates, IReadOnlyList<string> allInstalled)
    {
        var active = candidates.Count == 0 ? "none" : string.Join(", ", candidates);
        return $"Spellcheck dictionaries: {active} (Windows reports {allInstalled.Count})";
    }

    private static bool IsNorwegian(string language) =>
        language.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) ||
        language.StartsWith("no-", StringComparison.OrdinalIgnoreCase);

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    protected override void ExitThreadCore()
    {
        _updateTimer.Stop();
        _updateTimer.Dispose();
        _updateHttp.Dispose();
        _recorder.Dispose();
        _quickNote.Dispose();
        _timeCapture.Dispose();
        _hotkeys.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}

