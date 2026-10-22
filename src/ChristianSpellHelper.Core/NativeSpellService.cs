using System.Runtime.InteropServices;

namespace ChristianSpellHelper.Core;

public sealed class NativeSpellService : ISpellService
{
    private readonly ISpellCheckerFactory _factory;
    private readonly Dictionary<string, ISpellChecker> _checkers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public NativeSpellService()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(8))
        {
            throw new PlatformNotSupportedException("The Windows Spell Checking API requires Windows 8 or later.");
        }

        var factoryType = Type.GetTypeFromCLSID(new Guid("7AB36653-1796-484B-BDFA-E74F1DB7C1DC"), throwOnError: true)!;
        _factory = (ISpellCheckerFactory)Activator.CreateInstance(factoryType)!;
        SupportedLanguages = EnumerateStrings(_factory.SupportedLanguages, 256)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static language => language, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> SupportedLanguages { get; }

    public IReadOnlyList<SpellIssue> Check(string languageTag, string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        ArgumentException.ThrowIfNullOrEmpty(text);

        var errors = GetChecker(languageTag).Check(text);
        var result = new List<SpellIssue>();

        try
        {
            while (true)
            {
                ISpellingError? error;
                try
                {
                    error = errors.Next();
                }
                catch (COMException ex) when (ex.HResult == 1)
                {
                    break;
                }

                if (error is null)
                {
                    break;
                }

                try
                {
                    var start = checked((int)error.StartIndex);
                    var length = checked((int)error.Length);
                    var action = (NativeCorrectiveAction)error.CorrectiveAction;
                    var replacement = action == NativeCorrectiveAction.Replace ? error.Replacement : null;
                    result.Add(new SpellIssue(start, length, action, replacement));
                }
                finally
                {
                    Marshal.FinalReleaseComObject(error);
                }
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(errors);
        }

        return result;
    }

    public IReadOnlyList<string> Suggest(string languageTag, string word, int maximum)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (maximum <= 0 || string.IsNullOrWhiteSpace(word))
        {
            return Array.Empty<string>();
        }

        var suggestions = GetChecker(languageTag).Suggest(word);
        try
        {
            return EnumerateStrings(suggestions, maximum)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            Marshal.FinalReleaseComObject(suggestions);
        }
    }

    private ISpellChecker GetChecker(string languageTag)
    {
        if (_checkers.TryGetValue(languageTag, out var checker))
        {
            return checker;
        }

        if (_factory.IsSupported(languageTag) == 0)
        {
            throw new NotSupportedException($"No registered Windows spell checker supports {languageTag}.");
        }

        checker = _factory.CreateSpellChecker(languageTag);
        _checkers.Add(languageTag, checker);
        return checker;
    }

    private static IEnumerable<string> EnumerateStrings(IEnumString values, int maximum)
    {
        for (var index = 0; index < maximum; index++)
        {
            string? value;
            uint fetched;
            try
            {
                values.RemoteNext(1, out value, out fetched);
            }
            catch (COMException ex) when (ex.HResult == 1)
            {
                yield break;
            }

            if (fetched == 0 || value is null)
            {
                yield break;
            }

            yield return value;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var checker in _checkers.Values)
        {
            Marshal.FinalReleaseComObject(checker);
        }

        _checkers.Clear();
        Marshal.FinalReleaseComObject(_factory);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
