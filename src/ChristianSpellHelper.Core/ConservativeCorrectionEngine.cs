using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChristianSpellHelper.Core;

public sealed partial class ConservativeCorrectionEngine
{
    private const int MaximumSelectionLength = 20_000;
    private const double MultiErrorLanguageMargin = 0.04;
    private static readonly IReadOnlyDictionary<string, string> NorwegianDiacriticRecovery =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["na"] = "nå",
        };
    private readonly ISpellService _spelling;
    private readonly string[] _languageCandidates;

    public ConservativeCorrectionEngine(ISpellService spelling)
    {
        _spelling = spelling;
        _languageCandidates = SelectLanguageCandidates(spelling.SupportedLanguages).ToArray();
    }

    public IReadOnlyList<string> LanguageCandidates => _languageCandidates;

    public CorrectionResult Correct(string text)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result(CorrectionStatus.NoChange, text, text, null, [], "The selection contains no words.", stopwatch);
        }

        if (text.Length > MaximumSelectionLength)
        {
            return Result(CorrectionStatus.Failed, text, text, null, [], "The selection is over the 20,000-character safety limit.", stopwatch);
        }

        if (_languageCandidates.Length == 0)
        {
            return Result(CorrectionStatus.NoSupportedLanguage, text, text, null, [], "No English or Norwegian Windows spelling dictionary is installed.", stopwatch);
        }

        if (LooksLikeProtectedContent(text))
        {
            return Result(CorrectionStatus.SkippedProtectedContent, text, text, null, [], "Skipped a URL, email address, or code-like selection.", stopwatch);
        }

        var words = WordRegex().Matches(text);
        if (words.Count == 0)
        {
            return Result(CorrectionStatus.NoChange, text, text, null, [], "The selection contains no alphabetic words.", stopwatch);
        }

        var analyses = _languageCandidates
            .Select(language => Analyse(language, text, words.Count))
            .OrderBy(static analysis => analysis.Score)
            .ThenBy(static analysis => LanguagePreference(analysis.LanguageTag))
            .ToArray();

        var selected = analyses[0];
        if (analyses.Length > 1 && IsLanguageAmbiguous(analyses[0], analyses[1], words.Count))
        {
            return Result(CorrectionStatus.SkippedAmbiguous, text, text, null, [], "Language or correction confidence was too low; nothing was changed.", stopwatch);
        }

        // Ambiguous multi-language input returned above. For a remaining sentence with
        // multiple dictionary issues, Windows' native suggestion order may safely break
        // edit-distance-one ties while single-error text keeps the stricter old behavior.
        var allowRankedTieBreak = selected.Issues.Count >= 2;

        var phraseRepairs = new BoundedPhraseRepairer(_spelling)
            .FindRepairs(selected.LanguageTag, text, selected.Issues);
        var diacriticRepairs = FindNorwegianDiacriticRepairs(selected.LanguageTag, text, words.Count)
            .Where(repair => !phraseRepairs.Any(existing =>
                RangesOverlap(existing.Start, existing.Length, repair.Start, repair.Length)))
            .ToArray();
        var initialRepairs = phraseRepairs.Concat(diacriticRepairs).ToArray();
        var phraseCorrected = ApplyCorrections(text, initialRepairs);
        var currentIssues = phraseCorrected.Equals(text, StringComparison.Ordinal)
            ? selected.Issues
            : _spelling.Check(selected.LanguageTag, phraseCorrected)
                .Where(issue => IsValidIssue(phraseCorrected, issue))
                .ToArray();
        var currentWordCount = WordRegex().Matches(phraseCorrected).Count;
        var tokenCorrections = new List<AppliedCorrection>();
        foreach (var issue in currentIssues)
        {
            if (!TryCreateCorrection(selected.LanguageTag, phraseCorrected, currentWordCount, issue,
                    allowRankedTieBreak, out var correction))
            {
                continue;
            }

            if (tokenCorrections.Any(existing => RangesOverlap(existing.Start, existing.Length, correction.Start, correction.Length)))
            {
                continue;
            }

            tokenCorrections.Add(correction);
        }

        var correctionCount = initialRepairs.Length + tokenCorrections.Count;
        if (correctionCount == 0)
        {
            var status = selected.Issues.Count > 0 ? CorrectionStatus.SkippedAmbiguous : CorrectionStatus.NoChange;
            var message = selected.Issues.Count > 0
                ? "Potential errors were ambiguous, names, or unsupported automatic deletions; nothing was changed."
                : "No spelling corrections were needed.";
            return Result(status, text, text, selected.LanguageTag, [], message, stopwatch);
        }

        var corrected = ApplyCorrections(phraseCorrected, tokenCorrections);
        var allCorrections = initialRepairs.Concat(tokenCorrections).ToArray();

        return Result(
            CorrectionStatus.Corrected,
            text,
            corrected,
            selected.LanguageTag,
            allCorrections,
            $"Applied {correctionCount} conservative spelling correction(s).",
            stopwatch);
    }

    private IReadOnlyList<AppliedCorrection> FindNorwegianDiacriticRepairs(
        string language,
        string text,
        int wordCount)
    {
        if (wordCount < 2 ||
            !(language.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) ||
              language.StartsWith("no-", StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        var repairs = new List<AppliedCorrection>();
        foreach (Match match in WordRegex().Matches(text))
        {
            var original = match.Value;
            if (IsAllCaps(original) || IsLikelyName(text, match.Index, original) ||
                !NorwegianDiacriticRecovery.TryGetValue(original, out var candidate))
            {
                continue;
            }

            if (_spelling.Check(language, candidate).Any()) continue;
            repairs.Add(new AppliedCorrection(
                match.Index,
                match.Length,
                original,
                MatchCase(original, candidate),
                "bounded Norwegian diacritic recovery"));
        }

        return repairs;
    }

    private LanguageAnalysis Analyse(string language, string text, int wordCount)
    {
        var issues = _spelling.Check(language, text)
            .Where(issue => IsValidIssue(text, issue))
            .ToArray();
        var alphabeticCharacters = text.Count(char.IsLetter);
        var errorCharacters = issues.Sum(static issue => issue.Length);
        var errorRate = alphabeticCharacters == 0 ? 1d : (double)errorCharacters / alphabeticCharacters;
        var shortPenalty = wordCount == 1 ? 0.04 : 0d;
        var suggestionCost = issues
            .Take(16)
            .Select(issue => BestSuggestionDistance(language, text, issue))
            .Sum();
        var suggestionPenalty = alphabeticCharacters == 0
            ? 1d
            : (double)suggestionCost / alphabeticCharacters;
        return new LanguageAnalysis(language, issues, errorRate + shortPenalty + suggestionPenalty);
    }

    private int BestSuggestionDistance(string language, string text, SpellIssue issue)
    {
        var original = text.Substring(issue.Start, issue.Length);
        if (!original.Any(char.IsLetter) || original.Any(char.IsDigit) || original.Contains('_'))
        {
            return Math.Max(2, issue.Length);
        }

        var best = _spelling.Suggest(language, original, 8)
            .Where(IsSimpleWord)
            .Where(candidate => !IsCaseOnlyReplacement(original, candidate))
            .Select(candidate => DamerauLevenshtein(original, candidate))
            .DefaultIfEmpty(Math.Max(2, issue.Length))
            .Min();
        return Math.Min(best, Math.Max(2, issue.Length));
    }

    private bool TryCreateCorrection(
        string language,
        string text,
        int wordCount,
        SpellIssue issue,
        bool allowRankedTieBreak,
        out AppliedCorrection correction)
    {
        correction = null!;
        var original = text.Substring(issue.Start, issue.Length);

        if (!original.Any(char.IsLetter) || original.Any(char.IsDigit) || original.Contains('_'))
        {
            return false;
        }

        if (IsAllCaps(original) || IsLikelyName(text, issue.Start, original))
        {
            return false;
        }

        if (wordCount == 1 && original.Length < 4)
        {
            return false;
        }

        string? replacement = null;
        var reason = string.Empty;
        if (issue.Action == NativeCorrectiveAction.Replace &&
            !string.IsNullOrWhiteSpace(issue.Replacement) &&
            !IsCaseOnlyReplacement(original, issue.Replacement))
        {
            replacement = issue.Replacement;
            reason = "Windows dictionary automatic replacement";
        }
        else if (issue.Action == NativeCorrectiveAction.GetSuggestions ||
                 (issue.Action == NativeCorrectiveAction.Replace &&
                  !string.IsNullOrWhiteSpace(issue.Replacement) &&
                  IsCaseOnlyReplacement(original, issue.Replacement)))
        {
            var suggestions = _spelling.Suggest(language, original, 8)
                .Select((candidate, rank) => (candidate, rank))
                .Where(candidate => IsSimpleWord(candidate.candidate))
                .Where(candidate => !IsCaseOnlyReplacement(original, candidate.candidate))
                .Select(candidate => new Candidate(
                    candidate.candidate,
                    DamerauLevenshtein(original, candidate.candidate),
                    candidate.rank))
                .OrderBy(static candidate => candidate.Distance)
                .ThenBy(static candidate => candidate.Rank)
                .ToArray();

            if (suggestions.Length == 0)
            {
                return false;
            }

            var best = suggestions[0];
            var allowedDistance = original.Length >= 8 ? 2 : 1;
            var allowShortRankedCorrection = allowRankedTieBreak && best.Distance == 1 && original.Length >= 3;
            if (best.Distance > allowedDistance ||
                ((double)best.Distance / original.Length > 0.26 &&
                 !AreDiacriticVariants(original, best.Value) &&
                 !allowShortRankedCorrection))
            {
                return false;
            }

            var tied = suggestions.Count(candidate => candidate.Distance == best.Distance);
            if (tied > 1 && (!allowRankedTieBreak || best.Distance != 1))
            {
                return false;
            }

            replacement = best.Value;
            reason = tied == 1
                ? $"unique dictionary suggestion at edit distance {best.Distance}"
                : $"top-ranked Windows dictionary suggestion at edit distance {best.Distance} in a confident multi-error sentence";
        }

        if (string.IsNullOrWhiteSpace(replacement) || replacement.Equals(original, StringComparison.Ordinal))
        {
            return false;
        }

        replacement = MatchCase(original, replacement);
        correction = new AppliedCorrection(issue.Start, issue.Length, original, replacement, reason);
        return true;
    }

    private static bool IsLanguageAmbiguous(LanguageAnalysis first, LanguageAnalysis second, int wordCount)
    {
        var margin = second.Score - first.Score;
        if (first.Issues.Count == 0)
        {
            return false;
        }

        var requiredMargin = wordCount == 1
            ? 0.18
            : first.Issues.Count >= 2
                ? MultiErrorLanguageMargin
                : 0.08;
        return margin < requiredMargin;
    }

    private static IEnumerable<string> SelectLanguageCandidates(IReadOnlyList<string> supported)
    {
        var english = supported
            .Where(static language => language.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static language => language.Equals("en-US", StringComparison.OrdinalIgnoreCase) ? 0 :
                                        language.Equals("en-GB", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .FirstOrDefault();
        if (english is not null)
        {
            yield return english;
        }

        var norwegian = supported
            .Where(static language => language.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) ||
                                      language.StartsWith("no-", StringComparison.OrdinalIgnoreCase) ||
                                      language.StartsWith("nn-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static language => language.Equals("nb-NO", StringComparison.OrdinalIgnoreCase) ? 0 :
                                        language.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) ? 1 :
                                        language.StartsWith("no-", StringComparison.OrdinalIgnoreCase) ? 2 : 3)
            .FirstOrDefault();
        if (norwegian is not null)
        {
            yield return norwegian;
        }
    }

    private static int LanguagePreference(string languageTag) =>
        languageTag.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) ||
        languageTag.StartsWith("no-", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static bool IsValidIssue(string text, SpellIssue issue) =>
        issue.Start >= 0 && issue.Length > 0 && issue.Start + issue.Length <= text.Length;

    private static bool RangesOverlap(int startA, int lengthA, int startB, int lengthB) =>
        startA < startB + lengthB && startB < startA + lengthA;

    private static string ApplyCorrections(string text, IEnumerable<AppliedCorrection> corrections)
    {
        var corrected = text;
        foreach (var correction in corrections.OrderByDescending(static correction => correction.Start))
        {
            corrected = corrected.Remove(correction.Start, correction.Length)
                .Insert(correction.Start, correction.Replacement);
        }

        return corrected;
    }

    private static bool IsLikelyName(string text, int start, string word)
    {
        if (!char.IsUpper(word[0]) || !word.Skip(1).Any(char.IsLower))
        {
            return false;
        }

        if (start == 0)
        {
            return WordRegex().Matches(text).Count == 1;
        }

        var index = start - 1;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            if (text[index] is '\r' or '\n')
            {
                return false;
            }

            index--;
        }

        return index >= 0 && text[index] is not '.' and not '!' and not '?' and not '\n' and not '\r';
    }

    private static bool IsAllCaps(string word) =>
        word.Any(char.IsLetter) && word.Where(char.IsLetter).All(char.IsUpper);

    private static bool IsSimpleWord(string candidate) =>
        candidate.Length > 0 && candidate.All(character => char.IsLetter(character) || character is '\'' or '’' or '-');

    private static string MatchCase(string original, string replacement)
    {
        if (original.All(character => !char.IsLetter(character) || char.IsUpper(character)))
        {
            return replacement.ToUpper(CultureInfo.CurrentCulture);
        }

        if (char.IsUpper(original[0]) && original.Skip(1).All(character => !char.IsLetter(character) || char.IsLower(character)))
        {
            return char.ToUpper(replacement[0], CultureInfo.CurrentCulture) + replacement[1..];
        }

        if (original.Where(char.IsLetter).All(char.IsLower))
        {
            return replacement.ToLower(CultureInfo.CurrentCulture);
        }

        return replacement;
    }

    private static bool IsCaseOnlyReplacement(string original, string replacement) =>
        replacement.Equals(original, StringComparison.OrdinalIgnoreCase) &&
        !replacement.Equals(original, StringComparison.Ordinal);

    private static bool AreDiacriticVariants(string first, string second) =>
        StripDiacritics(first).Equals(StripDiacritics(second), StringComparison.OrdinalIgnoreCase);

    private static string StripDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static bool LooksLikeProtectedContent(string text)
    {
        if (UrlOrEmailRegex().IsMatch(text) || CodeTokenRegex().IsMatch(text))
        {
            return true;
        }

        var punctuation = text.Count(character => "{}[]<>\\|_=;`".Contains(character));
        return punctuation >= 2 && punctuation * 5 >= text.Length;
    }

    internal static int DamerauLevenshtein(string first, string second)
    {
        first = first.ToLowerInvariant();
        second = second.ToLowerInvariant();
        var distances = new int[first.Length + 1, second.Length + 1];
        for (var i = 0; i <= first.Length; i++) distances[i, 0] = i;
        for (var j = 0; j <= second.Length; j++) distances[0, j] = j;

        for (var i = 1; i <= first.Length; i++)
        {
            for (var j = 1; j <= second.Length; j++)
            {
                var cost = first[i - 1] == second[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);

                if (i > 1 && j > 1 && first[i - 1] == second[j - 2] && first[i - 2] == second[j - 1])
                {
                    distances[i, j] = Math.Min(distances[i, j], distances[i - 2, j - 2] + cost);
                }
            }
        }

        return distances[first.Length, second.Length];
    }

    private static CorrectionResult Result(
        CorrectionStatus status,
        string original,
        string corrected,
        string? language,
        IReadOnlyList<AppliedCorrection> corrections,
        string message,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new CorrectionResult(status, original, corrected, language, corrections, message, stopwatch.Elapsed);
    }

    [GeneratedRegex(@"[\p{L}]+(?:['’\-][\p{L}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"(?ix)(?:\b(?:https?|ftp)://|\bwww\.|\b[\w.+-]+@[\w.-]+\.[a-z]{2,}\b|\b[a-z0-9-]+\.(?:com|net|org|no|io|dev|app)\b)")]
    private static partial Regex UrlOrEmailRegex();

    [GeneratedRegex(@"(?x)(?:=>|::|\#include|\b(?:class|function|const|let|var|SELECT|INSERT|UPDATE)\b\s+\w|\w+\s*\([^\r\n]*\)|</?\w+[^>]*>)")]
    private static partial Regex CodeTokenRegex();

    private sealed record LanguageAnalysis(string LanguageTag, IReadOnlyList<SpellIssue> Issues, double Score);

    private sealed record Candidate(string Value, int Distance, int Rank);
}
