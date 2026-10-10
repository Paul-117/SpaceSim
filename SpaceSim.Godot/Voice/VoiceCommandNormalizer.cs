using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpaceSim.GodotClient.Voice;

/// <summary>
/// Translates a deliberately small German speech vocabulary into the same canonical
/// commands accepted by the browser command console. It does not grant authority;
/// Flight still validates every command on the simulation thread.
/// </summary>
public static partial class VoiceCommandNormalizer
{
    private static readonly IReadOnlyDictionary<string, int> GermanNumbers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["NULL"] = 0, ["ZEHN"] = 10, ["ZWANZIG"] = 20, ["DREISSIG"] = 30,
        ["VIERZIG"] = 40, ["FUNFZIG"] = 50, ["SECHZIG"] = 60, ["SIEBZIG"] = 70,
        ["ACHTZIG"] = 80, ["NEUNZIG"] = 90, ["HUNDERT"] = 100
    };

    public static string ToCanonicalCommand(string transcript)
    {
        return TryToCanonicalCommand(transcript, out string command) ? command : Normalize(transcript);
    }

    /// <summary>Returns only commands that can be recognized without a language model.</summary>
    public static bool TryToCanonicalCommand(string transcript, out string command)
    {
        string normalized = Normalize(transcript);
        command = string.Empty;
        if (string.IsNullOrEmpty(normalized)) return false;

        if (normalized.Contains("AUTOPILOT", StringComparison.Ordinal))
        {
            if (ContainsAny(normalized, "AUS", "OFF", "DEAKTIVIER", "STOPP"))
            {
                command = "AUTOPILOT OFF";
                return true;
            }
            if (ContainsAny(normalized, "AN", "EIN", "ON", "AKTIVIER", "START"))
            {
                command = "AUTOPILOT ON";
                return true;
            }
        }

        if (normalized.Contains("SONAR", StringComparison.Ordinal))
        {
            if (ContainsAny(normalized, "AUS", "OFF", "DEAKTIVIER", "STOPP"))
            {
                command = "SONAR OFF";
                return true;
            }
            if (ContainsAny(normalized, "AN", "EIN", "ON", "AKTIVIER", "START"))
            {
                command = "SONAR ON";
                return true;
            }
        }

        if (normalized.Contains("REAKTOR", StringComparison.Ordinal) || normalized.Contains("REACTOR", StringComparison.Ordinal))
        {
            Match number = NumberPattern().Match(normalized);
            if (number.Success && int.TryParse(number.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) &&
                level is >= 0 and <= 100)
            {
                command = $"REACTOR {level}%";
                return true;
            }

            foreach ((string word, int wordLevel) in GermanNumbers)
                if (Regex.IsMatch(normalized, $@"\b{word}\b", RegexOptions.CultureInvariant))
                {
                    command = $"REACTOR {wordLevel}%";
                    return true;
                }
        }
        return false;
    }

    private static string Normalize(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(character);
        }
        var wordsOnly = new StringBuilder(result.Length);
        foreach (char character in result.ToString().ToUpperInvariant())
            wordsOnly.Append(char.IsLetterOrDigit(character) || character == '%' ? character : ' ');
        return WhitespacePattern().Replace(wordsOnly.ToString(), " ").Trim();
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(term =>
        Regex.IsMatch(value, $@"\b{Regex.Escape(term)}", RegexOptions.CultureInvariant));

    [GeneratedRegex(@"\d{1,3}")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
