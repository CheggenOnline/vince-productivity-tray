using System.Diagnostics;
using System.Runtime.InteropServices;
using ChristianSpellHelper.Core;

namespace ChristianSpellHelper;

internal sealed class SelectionTransaction
{
    private readonly ConservativeCorrectionEngine _engine;

    public SelectionTransaction(ConservativeCorrectionEngine engine)
    {
        _engine = engine;
    }

    public SelectionTransactionResult Run()
    {
        var total = Stopwatch.StartNew();
        var stage = "capturing the focused selection";
        var hotkeyTarget = SelectionIdentity.Capture();
        if (hotkeyTarget.ForegroundWindow == IntPtr.Zero)
        {
            return Result(SelectionTransactionStatus.NoReadableSelection, "No foreground application was found.", total);
        }

        if (hotkeyTarget.IsPassword)
        {
            return Result(SelectionTransactionStatus.PasswordField, "Password fields are never read or changed.", total);
        }

        if (!KeyboardInput.WaitForHotkeyRelease(
                InputTimingPolicy.HotkeyReleaseTimeout,
                InputTimingPolicy.HotkeyReleaseStableDuration))
        {
            return Result(SelectionTransactionStatus.ModifiersStillHeld, "Release Ctrl, Shift, and X, then try again.", total);
        }

        PumpMessagesFor(InputTimingPolicy.PostHotkeyReleaseSettle);
        var initial = SelectionIdentity.Capture();
        if (!hotkeyTarget.MatchesTarget(initial))
        {
            return Result(SelectionTransactionStatus.FocusChanged,
                "Focus changed while the shortcut keys were being released; nothing was pasted.", total);
        }

        try
        {
            stage = "preserving the clipboard";
            using var clipboard = new ClipboardPreserver();
            stage = "copying the selected text";
            if (!TryCopySelection(initial, out var selectedText, out var copyFailure))
            {
                return Result(SelectionTransactionStatus.NoReadableSelection, copyFailure, total);
            }

            stage = "checking spelling";
            var correction = _engine.Correct(selectedText);
            if (correction.Status != CorrectionStatus.Corrected || correction.CorrectedText == selectedText)
            {
                return new SelectionTransactionResult(
                    SelectionTransactionStatus.NoChange,
                    correction.Message,
                    correction.LanguageTag,
                    correction.Corrections.Count,
                    correction.Elapsed,
                    Stop(total));
            }

            if (!initial.MatchesCurrentSelection(selectedText))
            {
                return new SelectionTransactionResult(
                    SelectionTransactionStatus.FocusChanged,
                    "Focus or the selection changed while checking; nothing was pasted.",
                    correction.LanguageTag,
                    0,
                    correction.Elapsed,
                    Stop(total));
            }

            stage = "re-verifying the selected text";
            if (!TryCopySelection(initial, out var verificationText, out _) ||
                !NormalizeLineEndings(verificationText).Equals(NormalizeLineEndings(selectedText), StringComparison.Ordinal))
            {
                return new SelectionTransactionResult(
                    SelectionTransactionStatus.FocusChanged,
                    "The selected text changed while checking; nothing was pasted.",
                    correction.LanguageTag,
                    0,
                    correction.Elapsed,
                    Stop(total));
            }

            if (!initial.MatchesCurrentSelection(verificationText))
            {
                return new SelectionTransactionResult(
                    SelectionTransactionStatus.FocusChanged,
                    "Focus or the selection changed while checking; nothing was pasted.",
                    correction.LanguageTag,
                    0,
                    correction.Elapsed,
                    Stop(total));
            }

            stage = "placing corrected text on the clipboard";
            SetClipboardTextWithRetry(correction.CorrectedText);
            if (initial.IsClassicEditControl)
            {
                stage = "pasting into a classic edit control";
                if (!KeyboardInput.PasteDirect(initial.FocusWindow))
                {
                    return new SelectionTransactionResult(
                        SelectionTransactionStatus.Failed,
                        "The edit control rejected the plain-text paste; nothing was intentionally changed.",
                        correction.LanguageTag,
                        0,
                        correction.Elapsed,
                        Stop(total));
                }

                PumpMessagesFor(TimeSpan.FromMilliseconds(20));
            }
            else
            {
                if (!KeyboardInput.Paste())
                {
                    return new SelectionTransactionResult(
                        SelectionTransactionStatus.Failed,
                        "Windows rejected the paste input; nothing was intentionally changed.",
                        correction.LanguageTag,
                        0,
                        correction.Elapsed,
                        Stop(total));
                }

                // SendInput queues the paste. Give rich editors time to synchronously consume
                // the plain-text clipboard before the user's original multi-format object is restored.
                PumpMessagesFor(TimeSpan.FromMilliseconds(120));

                stage = "confirming the paste";
                var pasteState = InspectPasteResult(initial, selectedText, correction.CorrectedText);
                if (pasteState == PasteInspection.FocusChanged)
                {
                    clipboard.Abandon();
                    return new SelectionTransactionResult(
                        SelectionTransactionStatus.FocusChanged,
                        "Focus or the selected text changed during paste confirmation; the clipboard was left untouched.",
                        correction.LanguageTag,
                        0,
                        correction.Elapsed,
                        Stop(total));
                }

                if (pasteState == PasteInspection.OriginalStillSelected)
                {
                    if (!initial.MatchesCurrentSelection(selectedText))
                    {
                        clipboard.Abandon();
                        return new SelectionTransactionResult(
                            SelectionTransactionStatus.FocusChanged,
                            "Focus or the selection changed before the paste fallback; nothing else was pasted.",
                            correction.LanguageTag,
                            0,
                            correction.Elapsed,
                            Stop(total));
                    }

                    stage = "using the classic edit-control paste fallback";
                    SetClipboardTextWithRetry(correction.CorrectedText);
                    if (!KeyboardInput.PasteDirect(initial.FocusWindow))
                    {
                        return new SelectionTransactionResult(
                            SelectionTransactionStatus.Failed,
                            "The target application did not accept either safe paste method.",
                            correction.LanguageTag,
                            0,
                            correction.Elapsed,
                            Stop(total));
                    }

                    PumpMessagesFor(TimeSpan.FromMilliseconds(20));
                }
            }

            stage = "restoring the clipboard";
            try
            {
                clipboard.Restore();
            }
            catch (ExternalException exception)
            {
                clipboard.Abandon();
                LocalLog.Write("Text was replaced but the original clipboard could not be restored", exception);
                return new SelectionTransactionResult(
                    SelectionTransactionStatus.Replaced,
                    "The text was corrected, but Windows could not restore the previous clipboard content.",
                    correction.LanguageTag,
                    correction.Corrections.Count,
                    correction.Elapsed,
                    Stop(total));
            }

            return new SelectionTransactionResult(
                SelectionTransactionStatus.Replaced,
                correction.Message,
                correction.LanguageTag,
                correction.Corrections.Count,
                correction.Elapsed,
                Stop(total));
        }
        catch (COMException exception)
        {
            LocalLog.Write($"COM operation failed while {stage}", exception);
            return Result(SelectionTransactionStatus.Failed, "The foreground application did not expose a safe selection; nothing was pasted.", total);
        }
        catch (ExternalException exception)
        {
            LocalLog.Write("Clipboard operation failed", exception);
            return Result(SelectionTransactionStatus.ClipboardUnavailable, "The clipboard was busy; nothing was pasted.", total);
        }
    }

    private static bool TryCopySelection(SelectionIdentity identity, out string text, out string failure)
    {
        var capturedText = string.Empty;
        var failureMessage = "The selection could not be read. Select editable text and try again.";
        var succeeded = InputTimingPolicy.RunTransientAttempts(
            InputTimingPolicy.CopyAttempts,
            _ =>
            {
                if (!identity.MatchesCurrentTarget() ||
                    (identity.AutomationSelectedText is not null &&
                     !identity.MatchesCurrentSelection(identity.AutomationSelectedText)))
                {
                    failureMessage = "Focus or the selection changed before it could be read; nothing was pasted.";
                    return TransientAttemptResult.Stop;
                }

                ClearClipboardWithRetry();
                var clearedSequence = GetClipboardSequenceNumber();
                if (!KeyboardInput.Copy())
                {
                    failureMessage = "Windows rejected the copy input; nothing was pasted.";
                    return TransientAttemptResult.Retry;
                }

                var deadline = Environment.TickCount64 + (long)InputTimingPolicy.CopyAttemptTimeout.TotalMilliseconds;
                while (Environment.TickCount64 < deadline)
                {
                    Application.DoEvents();
                    if (GetClipboardSequenceNumber() != clearedSequence && ClipboardContainsUnicodeTextWithRetry())
                    {
                        capturedText = GetClipboardTextWithRetry();
                        if (!string.IsNullOrEmpty(capturedText) && identity.MatchesCurrentSelection(capturedText))
                        {
                            return TransientAttemptResult.Success;
                        }

                        failureMessage = "The selection or foreground window changed while it was being read; nothing was pasted.";
                        return TransientAttemptResult.Stop;
                    }

                    Thread.Sleep(5);
                }

                return TransientAttemptResult.Retry;
            },
            _ => PumpMessagesFor(InputTimingPolicy.CopyRetrySettle));

        text = capturedText;
        failure = failureMessage;
        return succeeded;
    }

    private static PasteInspection InspectPasteResult(
        SelectionIdentity originalIdentity,
        string originalText,
        string correctedText)
    {
        var current = SelectionIdentity.Capture();
        if (current.ForegroundWindow != originalIdentity.ForegroundWindow ||
            (originalIdentity.FocusWindow != IntPtr.Zero && current.FocusWindow != originalIdentity.FocusWindow) ||
            current.IsPassword)
        {
            return PasteInspection.FocusChanged;
        }

        if (current.AutomationSelectedText is not null)
        {
            var selected = NormalizeLineEndings(current.AutomationSelectedText);
            if (selected.Equals(NormalizeLineEndings(correctedText), StringComparison.Ordinal))
            {
                return PasteInspection.Consumed;
            }

            if (selected.Equals(NormalizeLineEndings(originalText), StringComparison.Ordinal))
            {
                return PasteInspection.OriginalStillSelected;
            }

            if (selected.Length > 0)
            {
                return PasteInspection.FocusChanged;
            }
        }

        ClearClipboardWithRetry();
        var clearedSequence = GetClipboardSequenceNumber();
        if (!KeyboardInput.Copy())
        {
            return PasteInspection.FocusChanged;
        }

        var deadline = Environment.TickCount64 + 150;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            if (GetClipboardSequenceNumber() != clearedSequence && ClipboardContainsUnicodeTextWithRetry())
            {
                var selected = NormalizeLineEndings(GetClipboardTextWithRetry());
                if (selected.Equals(NormalizeLineEndings(originalText), StringComparison.Ordinal))
                {
                    return PasteInspection.OriginalStillSelected;
                }

                return selected.Equals(NormalizeLineEndings(correctedText), StringComparison.Ordinal)
                    ? PasteInspection.Consumed
                    : PasteInspection.FocusChanged;
            }

            Thread.Sleep(5);
        }

        return PasteInspection.Consumed;
    }

    private static void ClearClipboardWithRetry() => RetryClipboard(Clipboard.Clear);

    private static string GetClipboardTextWithRetry()
    {
        string text = string.Empty;
        RetryClipboard(() => text = Clipboard.GetText(TextDataFormat.UnicodeText));
        return text;
    }

    private static void SetClipboardTextWithRetry(string text) =>
        RetryClipboard(() => Clipboard.SetText(text, TextDataFormat.UnicodeText));

    private static bool ClipboardContainsUnicodeTextWithRetry()
    {
        var containsText = false;
        RetryClipboard(() => containsText = Clipboard.ContainsText(TextDataFormat.UnicodeText));
        return containsText;
    }

    private static void RetryClipboard(Action operation)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                operation();
                return;
            }
            catch (ExternalException) when (attempt < 19)
            {
                Application.DoEvents();
                Thread.Sleep(Math.Min(10 * (attempt + 1), 50));
            }
        }
    }

    private static void PumpMessagesFor(TimeSpan duration)
    {
        var deadline = Environment.TickCount64 + (long)duration.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(2);
        }
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static SelectionTransactionResult Result(
        SelectionTransactionStatus status,
        string message,
        Stopwatch total) => new(status, message, null, 0, TimeSpan.Zero, Stop(total));

    private static TimeSpan Stop(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    private enum PasteInspection
    {
        Consumed,
        OriginalStillSelected,
        FocusChanged,
    }
}
