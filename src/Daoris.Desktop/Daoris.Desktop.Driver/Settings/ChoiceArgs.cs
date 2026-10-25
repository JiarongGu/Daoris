using System.Globalization;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The words of <c>daoris-driver driver</c> read (PUSH1f2a, PUSH1f2b; FOUNDATION1 mod-iii): the operands, the flags and the numbers a
/// verb takes from its arguments, in the CLI's own reading, which the settings' own verbs and the verbs the subdomains above own
/// (<see cref="ChoiceVerb"/>) read alike.
/// </summary>
public static class ChoiceArgs
{
    /// <summary>A rule's own judgement made before the press, as the command's own check: its refusal is the command's words.</summary>
    public static DriverConfig Judged(Func<DriverConfig> apply)
    {
        try
        {
            return apply();
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords(refused.Message);
        }
    }

    /// <summary>Each word that is a flag must be one the verb takes, a valued flag's value skipped.</summary>
    public static void Flags(IReadOnlyList<string> args, string[] valued, string[] switches, string verb, string usage)
    {
        for (var at = 1; at < args.Count; at++)
        {
            var token = args[at];
            if (valued.Contains(token)) at++;
            else if (token.StartsWith("--", StringComparison.Ordinal) && !switches.Contains(token))
            {
                throw new ChoiceWords($"`{token}` is not a flag `driver {verb}` takes — {usage}");
            }
        }
    }

    /// <summary>A moment as the CLI said it: ISO 8601 to the second, in UTC.</summary>
    public static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>The scope of a press: the workspace where one is named, else the repository.</summary>
    public static JsonObject Scoped(string? workspace, string name) =>
        workspace is not null ? new JsonObject { ["workspace"] = workspace } : new JsonObject { ["repository"] = name };

    /// <summary>The verb's name, the first operand after it: a flag's value is never the name (REV3).</summary>
    public static string Named(IReadOnlyList<string> args, string verb) =>
        Operands(args, ["--at", "--session"]).ElementAtOrDefault(1)
        ?? throw new ChoiceWords($"`driver {verb}` needs a name — e.g. `daoris driver {verb} aurora-engine`.");

    /// <summary>The positional operands, in order: every token that is neither a flag nor the value of one (the CLI's <c>operands</c>).</summary>
    public static List<string> Operands(IReadOnlyList<string> args, string[] valued)
    {
        var found = new List<string>();
        for (var at = 0; at < args.Count; at++)
        {
            var token = args[at];
            if (valued.Contains(token)) at++;
            else if (!token.StartsWith("--", StringComparison.Ordinal)) found.Add(token);
        }

        return found;
    }

    /// <summary>The value after a flag, refusing a flag with nothing after it (the CLI's <c>flagValue</c>).</summary>
    public static string? FlagValue(IReadOnlyList<string> args, string flag)
    {
        var at = IndexOf(args, flag);
        if (at == -1) return null;
        var value = args.ElementAtOrDefault(at + 1);
        if (string.IsNullOrEmpty(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ChoiceWords($"{flag} needs a name — e.g. `{flag} aurora`");
        }

        return value;
    }

    /// <summary>Where a word stands among the arguments, or -1.</summary>
    public static int IndexOf(IReadOnlyList<string> args, string word)
    {
        for (var at = 0; at < args.Count; at++)
        {
            if (args[at] == word) return at;
        }

        return -1;
    }

    /// <summary>A name in the list in any case, as it is spelled there, or null.</summary>
    public static string? FindName(IEnumerable<string> names, string name) =>
        names.FirstOrDefault(each => string.Equals(each, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The entry of a name in any case, or null.</summary>
    public static T? AtName<T>(IReadOnlyDictionary<string, T> map, string name) where T : class =>
        FindName(map.Keys, name) is { } key ? map[key] : null;

    /// <summary>A word read as a number as the CLI read it (JavaScript's <c>Number</c>): blank is 0, anything else not a number is NaN.</summary>
    public static double Number(string word)
    {
        var text = word.Trim();
        if (text.Length == 0) return 0;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex))
        {
            return hex;
        }

        if (text is "Infinity" or "+Infinity") return double.PositiveInfinity;
        if (text == "-Infinity") return double.NegativeInfinity;
        return text.All(c => char.IsAsciiDigit(c) || c is '.' or 'e' or 'E' or '+' or '-')
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : double.NaN;
    }

    /// <summary>Whether a number is a whole one.</summary>
    public static bool IsWhole(double value) => double.IsFinite(value) && Math.Floor(value) == value;
}
