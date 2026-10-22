namespace ChristianSpellHelper.Core;

public enum NativeCorrectiveAction
{
    None = 0,
    GetSuggestions = 1,
    Replace = 2,
    Delete = 3,
}

public sealed record SpellIssue(
    int Start,
    int Length,
    NativeCorrectiveAction Action,
    string? Replacement);

public sealed record AppliedCorrection(
    int Start,
    int Length,
    string Original,
    string Replacement,
    string Reason);

public enum CorrectionStatus
{
    Corrected,
    NoChange,
    SkippedAmbiguous,
    SkippedProtectedContent,
    NoSupportedLanguage,
    Failed,
}

public sealed record CorrectionResult(
    CorrectionStatus Status,
    string OriginalText,
    string CorrectedText,
    string? LanguageTag,
    IReadOnlyList<AppliedCorrection> Corrections,
    string Message,
    TimeSpan Elapsed);

public interface ISpellService : IDisposable
{
    IReadOnlyList<string> SupportedLanguages { get; }

    IReadOnlyList<SpellIssue> Check(string languageTag, string text);

    IReadOnlyList<string> Suggest(string languageTag, string word, int maximum);
}
