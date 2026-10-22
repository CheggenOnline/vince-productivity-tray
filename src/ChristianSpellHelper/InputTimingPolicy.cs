namespace ChristianSpellHelper;

internal enum TransientAttemptResult
{
    Success,
    Retry,
    Stop,
}

internal static class InputTimingPolicy
{
    internal static readonly TimeSpan HotkeyReleaseTimeout = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan HotkeyReleaseStableDuration = TimeSpan.FromMilliseconds(40);
    internal static readonly TimeSpan PostHotkeyReleaseSettle = TimeSpan.FromMilliseconds(35);
    internal static readonly TimeSpan CopyAttemptTimeout = TimeSpan.FromMilliseconds(650);
    internal static readonly TimeSpan CopyRetrySettle = TimeSpan.FromMilliseconds(40);
    internal const int CopyAttempts = 2;

    internal static bool WaitForStableRelease(
        TimeSpan timeout,
        TimeSpan stableDuration,
        Func<bool> anyHotkeyKeyDown,
        Func<long> currentMilliseconds,
        Action pause)
    {
        ArgumentNullException.ThrowIfNull(anyHotkeyKeyDown);
        ArgumentNullException.ThrowIfNull(currentMilliseconds);
        ArgumentNullException.ThrowIfNull(pause);
        if (timeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (stableDuration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(stableDuration));

        var started = currentMilliseconds();
        long? releasedSince = null;
        while (currentMilliseconds() - started < timeout.TotalMilliseconds)
        {
            var now = currentMilliseconds();
            if (anyHotkeyKeyDown())
            {
                releasedSince = null;
            }
            else
            {
                releasedSince ??= now;
                if (now - releasedSince.Value >= stableDuration.TotalMilliseconds)
                {
                    return true;
                }
            }

            pause();
        }

        return false;
    }

    internal static bool RunTransientAttempts(
        int maximumAttempts,
        Func<int, TransientAttemptResult> attempt,
        Action<int> beforeRetry)
    {
        if (maximumAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(beforeRetry);

        for (var index = 0; index < maximumAttempts; index++)
        {
            var result = attempt(index);
            if (result == TransientAttemptResult.Success) return true;
            if (result == TransientAttemptResult.Stop) return false;
            if (index + 1 < maximumAttempts) beforeRetry(index);
        }

        return false;
    }
}
