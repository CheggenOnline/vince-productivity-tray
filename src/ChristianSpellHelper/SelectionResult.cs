namespace ChristianSpellHelper;

internal enum SelectionTransactionStatus
{
    Replaced,
    NoChange,
    NoReadableSelection,
    PasswordField,
    FocusChanged,
    ClipboardUnavailable,
    ModifiersStillHeld,
    Failed,
}

internal sealed record SelectionTransactionResult(
    SelectionTransactionStatus Status,
    string Message,
    string? LanguageTag,
    int CorrectionCount,
    TimeSpan EngineElapsed,
    TimeSpan TotalElapsed);
