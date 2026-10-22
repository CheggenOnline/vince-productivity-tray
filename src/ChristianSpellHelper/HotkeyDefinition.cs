using System.Globalization;

namespace ChristianSpellHelper;

internal readonly record struct HotkeyDefinition(uint Modifiers, uint VirtualKey)
{
    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint Shift = 0x0004;
    private const uint Win = 0x0008;

    public static HotkeyDefinition Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new FormatException("A hotkey needs at least one modifier and one key.");
        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => Control,
                "ALT" => Alt,
                "SHIFT" => Shift,
                "WIN" or "WINDOWS" => Win,
                _ => throw new FormatException($"Unknown modifier: {part}"),
            };
        }

        var keyText = parts[^1].ToUpperInvariant();
        uint key = keyText.Length == 1 && char.IsLetterOrDigit(keyText[0])
            ? keyText[0]
            : keyText.StartsWith('F') && int.TryParse(keyText[1..], CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 24
                ? (uint)(0x70 + f - 1)
                : throw new FormatException($"Unsupported key: {parts[^1]}");
        return new HotkeyDefinition(modifiers, key);
    }
}
