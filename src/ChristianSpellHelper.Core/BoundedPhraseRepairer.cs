using System.Text.RegularExpressions;

namespace ChristianSpellHelper.Core;

internal sealed partial class BoundedPhraseRepairer
{
    private const int MaximumDictionaryLookups = 96;
    private const int MaximumIssueWindows = 32;
    private readonly ISpellService _spelling;

    public BoundedPhraseRepairer(ISpellService spelling)
    {
        _spelling = spelling;
    }

    public IReadOnlyList<AppliedCorrection> FindRepairs(
        string languageTag,
        string text,
        IReadOnlyList<SpellIssue> issues)
    {
        var matches = WordRegex().Matches(text);
        if (matches.Count < 2)
        {
            return Array.Empty<AppliedCorrection>();
        }

        var tokens = matches
            .Select(match => new WordToken(
                match.Index,
                match.Length,
                match.Value,
                issues.Any(issue => RangesOverlap(match.Index, match.Length, issue.Start, issue.Length))))
            .ToArray();
        var oracle = new WordOracle(_spelling, languageTag, MaximumDictionaryLookups);
        var candidates = new List<PhraseCandidate>();

        AddRepeatedLetterCandidates(languageTag, text, tokens, oracle, candidates);

        var issueWindows = 0;
        for (var index = 0; index + 1 < tokens.Length && issueWindows < MaximumIssueWindows; index++)
        {
            var left = tokens[index];
            var right = tokens[index + 1];
            if (!left.IsMisspelled && !right.IsMisspelled)
            {
                continue;
            }

            var gapStart = left.Start + left.Length;
            var gapLength = right.Start - gapStart;
            if (gapLength <= 0 || gapLength > 3)
            {
                continue;
            }

            var gap = text.AsSpan(gapStart, gapLength);
            if (!IsHorizontalWhitespace(gap))
            {
                continue;
            }

            issueWindows++;
            AddAdjacentCandidates(left, right, oracle, candidates);
        }

        foreach (var token in tokens.Where(static token => token.IsMisspelled).Take(MaximumIssueWindows))
        {
            AddSplitCandidates(token, oracle, candidates);
        }

        var uniquePerRange = candidates
            .GroupBy(static candidate => (candidate.Start, candidate.Length))
            .Select(SelectUniqueBest)
            .Where(static candidate => candidate is not null)
            .Cast<PhraseCandidate>()
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.Start)
            .ToArray();

        var accepted = new List<PhraseCandidate>();
        foreach (var candidate in uniquePerRange)
        {
            if (accepted.Any(existing => RangesOverlap(existing.Start, existing.Length, candidate.Start, candidate.Length)))
            {
                continue;
            }

            accepted.Add(candidate);
        }

        return accepted
            .OrderBy(static candidate => candidate.Start)
            .Select(candidate => new AppliedCorrection(
                candidate.Start,
                candidate.Length,
                text.Substring(candidate.Start, candidate.Length),
                MatchCase(text.Substring(candidate.Start, candidate.Length), candidate.Replacement),
                candidate.Reason))
            .ToArray();
    }

    private static void AddAdjacentCandidates(
        WordToken left,
        WordToken right,
        WordOracle oracle,
        List<PhraseCandidate> candidates)
    {
        if (IsProtectedToken(left.Value) || IsProtectedToken(right.Value))
        {
            return;
        }

        var invalidCount = (left.IsMisspelled ? 1 : 0) + (right.IsMisspelled ? 1 : 0);
        var combined = left.Value + right.Value;
        var rangeLength = right.Start + right.Length - left.Start;

        if (oracle.IsCorrect(combined))
        {
            candidates.Add(new PhraseCandidate(
                left.Start,
                rangeLength,
                combined,
                45 + (invalidCount * 8),
                "unique local join into a dictionary word"));
        }

        var originalBoundary = left.Length;
        var minimum = Math.Max(1, originalBoundary - 2);
        var maximum = Math.Min(combined.Length - 1, originalBoundary + 2);
        for (var boundary = minimum; boundary <= maximum; boundary++)
        {
            if (boundary == originalBoundary)
            {
                continue;
            }

            var first = combined[..boundary];
            var second = combined[boundary..];
            if (!oracle.IsCorrect(first) || !oracle.IsCorrect(second))
            {
                continue;
            }

            var shift = Math.Abs(boundary - originalBoundary);
            candidates.Add(new PhraseCandidate(
                left.Start,
                rangeLength,
                $"{first} {second}",
                60 + (invalidCount * 10) - shift,
                $"unique dictionary-valid word-boundary shift ({shift} character{(shift == 1 ? string.Empty : "s")})"));
        }
    }

    private static void AddSplitCandidates(
        WordToken token,
        WordOracle oracle,
        List<PhraseCandidate> candidates)
    {
        if (token.Length < 4 || token.Length > 32 || IsProtectedToken(token.Value) ||
            oracle.HasCloseSuggestion(token.Value))
        {
            return;
        }

        for (var boundary = 2; boundary <= token.Length - 2; boundary++)
        {
            var first = token.Value[..boundary];
            var second = token.Value[boundary..];
            if (!oracle.IsCorrect(first) || !oracle.IsCorrect(second))
            {
                continue;
            }

            candidates.Add(new PhraseCandidate(
                token.Start,
                token.Length,
                $"{first} {second}",
                44,
                "unique local split into two dictionary words"));
        }
    }

    private static void AddRepeatedLetterCandidates(
        string languageTag,
        string text,
        IReadOnlyList<WordToken> tokens,
        WordOracle oracle,
        List<PhraseCandidate> candidates)
    {
        foreach (var token in tokens)
        {
            if (IsProtectedToken(token.Value))
            {
                continue;
            }

            for (var start = 0; start < token.Value.Length - 1;)
            {
                var end = start + 1;
                while (end < token.Value.Length &&
                       char.ToLowerInvariant(token.Value[end]) == char.ToLowerInvariant(token.Value[start]))
                {
                    end++;
                }

                var runLength = end - start;
                if (runLength >= 2)
                {
                    var collapsed = token.Value.Remove(start + 1, runLength - 1);
                    var allowAcceptedChatToken =
                        !token.IsMisspelled &&
                        IsNorwegian(languageTag) &&
                        tokens.Count >= 4 &&
                        token.Start == tokens[^1].Start &&
                        token.Value.Equals("daa", StringComparison.Ordinal) &&
                        collapsed.Equals("da", StringComparison.Ordinal);
                    if ((token.IsMisspelled || allowAcceptedChatToken) && oracle.IsCorrect(collapsed))
                    {
                        var cost = runLength - 1;
                        candidates.Add(new PhraseCandidate(
                            token.Start,
                            token.Length,
                            collapsed,
                            (token.IsMisspelled ? 55 : 32) - cost,
                            token.IsMisspelled
                                ? $"dictionary-valid repeated-letter collapse ({cost} removed)"
                                : "allow-listed long-phrase Bokmål chat normalization (daa to da)"));
                    }
                }

                start = end;
            }
        }
    }

    private static PhraseCandidate? SelectUniqueBest(IGrouping<(int Start, int Length), PhraseCandidate> group)
    {
        var ordered = group
            .GroupBy(static candidate => candidate.Replacement, StringComparer.OrdinalIgnoreCase)
            .Select(static replacements => replacements.OrderByDescending(candidate => candidate.Score).First())
            .OrderByDescending(static candidate => candidate.Score)
            .ToArray();
        return ordered.Length == 1 ? ordered[0] : null;
    }

    private static bool IsProtectedToken(string token) =>
        token.Any(character => char.IsDigit(character) || character == '_') ||
        (token.Any(char.IsLetter) && token.Where(char.IsLetter).All(char.IsUpper)) ||
        char.IsUpper(token[0]);

    private static bool IsHorizontalWhitespace(ReadOnlySpan<char> gap)
    {
        foreach (var character in gap)
        {
            if (character is not ' ' and not '\t')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNorwegian(string languageTag) =>
        languageTag.StartsWith("nb-", StringComparison.OrdinalIgnoreCase) ||
        languageTag.StartsWith("no-", StringComparison.OrdinalIgnoreCase) ||
        languageTag.StartsWith("nn-", StringComparison.OrdinalIgnoreCase);

    private static string MatchCase(string original, string replacement)
    {
        if (original.Length == 0 || replacement.Length == 0)
        {
            return replacement;
        }

        return char.IsUpper(original[0])
            ? char.ToUpper(replacement[0], System.Globalization.CultureInfo.CurrentCulture) + replacement[1..]
            : replacement;
    }

    private static bool RangesOverlap(int startA, int lengthA, int startB, int lengthB) =>
        startA < startB + lengthB && startB < startA + lengthA;

    [GeneratedRegex(@"[\p{L}]+(?:['’\-][\p{L}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    private sealed record WordToken(int Start, int Length, string Value, bool IsMisspelled);

    private sealed record PhraseCandidate(int Start, int Length, string Replacement, int Score, string Reason);

    private sealed class WordOracle
    {
        private readonly ISpellService _spelling;
        private readonly string _languageTag;
        private readonly int _maximumLookups;
        private readonly Dictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _closeSuggestionCache = new(StringComparer.OrdinalIgnoreCase);
        private int _lookups;
        private int _suggestionLookups;

        public WordOracle(ISpellService spelling, string languageTag, int maximumLookups)
        {
            _spelling = spelling;
            _languageTag = languageTag;
            _maximumLookups = maximumLookups;
        }

        public bool IsCorrect(string word)
        {
            if (_cache.TryGetValue(word, out var correct))
            {
                return correct;
            }

            if (_lookups >= _maximumLookups || string.IsNullOrWhiteSpace(word))
            {
                return false;
            }

            _lookups++;
            correct = _spelling.Check(_languageTag, word).Count == 0;
            _cache[word] = correct;
            return correct;
        }

        public bool HasCloseSuggestion(string word)
        {
            if (_closeSuggestionCache.TryGetValue(word, out var close))
            {
                return close;
            }

            if (_suggestionLookups >= MaximumIssueWindows)
            {
                return false;
            }

            _suggestionLookups++;
            close = _spelling.Suggest(_languageTag, word, 8)
                .Where(candidate => candidate.Length > 0 && candidate.All(character =>
                    char.IsLetter(character) || character is '\'' or '’' or '-'))
                .Any(candidate => ConservativeCorrectionEngine.DamerauLevenshtein(word, candidate) <= 1);
            _closeSuggestionCache[word] = close;
            return close;
        }
    }
}
