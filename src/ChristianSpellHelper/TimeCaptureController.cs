namespace ChristianSpellHelper;

internal sealed class TimeCaptureController : IDisposable
{
    private readonly Func<AppSettings> _settings;
    private readonly TimeCaptureForm _form;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly TimeCaptureScheduleState _schedule = new();
    private DateTimeOffset? _snoozedUntil;

    public TimeCaptureController(Func<AppSettings> settings)
    {
        _settings = settings;
        _form = new TimeCaptureForm(new TimeCaptureStore(), settings);
        _form.Snoozed += (_, _) => _snoozedUntil = DateTimeOffset.Now.AddMinutes(_settings().TimeCaptureSnoozeMinutes);
        _timer = new System.Windows.Forms.Timer { Interval = 15_000, Enabled = true };
        _timer.Tick += (_, _) => Poll(DateTimeOffset.Now);
    }

    public void ShowForEntry() => _form.ShowForEntry();

    public bool HasUnsavedDraft => _form.HasUnsavedDraft;

    internal bool ShouldShow(DateTimeOffset now)
    {
        var settings = _settings();
        if (!settings.TimeCaptureEnabled || !settings.TimeCaptureHourlyPopup) return false;
        if (_snoozedUntil is not null && now < _snoozedUntil) return false;
        if (_snoozedUntil is not null && now >= _snoozedUntil) { _snoozedUntil = null; return true; }
        return _schedule.ShouldTrigger(now.LocalDateTime, settings.TimeCaptureStartHour, settings.TimeCaptureEndHour);
    }

    private void Poll(DateTimeOffset now)
    {
        if (ShouldShow(now)) _form.ShowForEntry();
    }

    public void Dispose()
    {
        _timer.Stop(); _timer.Dispose(); _form.Dispose();
    }
}

internal sealed class TimeCaptureScheduleState
{
    private DateTime? _lastHour;

    public bool ShouldTrigger(DateTime localNow, int firstHour, int lastHour)
    {
        if (!TimeCaptureProgress.IsScheduledHour(localNow, firstHour, lastHour)) return false;
        var hour = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0, localNow.Kind);
        if (_lastHour == hour) return false;
        _lastHour = hour;
        return true;
    }
}
