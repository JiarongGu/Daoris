using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// An argument as a command in the driver's words prints it (ACCTQUOTE1b, D125's ACCTQUOTE1 note). A command the driver names
/// in a hold, a refusal or a card's terminal twin is one a person copies into whichever shell they have: PowerShell, Command
/// Prompt or a POSIX shell, and on Windows the install's <c>daoris</c> is <c>daoris.cmd</c>, which PowerShell finds too
/// (D124 §1.2). An account's id or a workspace's name may hold a character one of them reads, so a value printed bare can
/// change the command it is pasted into: <c>R&amp;D</c> pasted into Command Prompt runs <c>D</c>. One spelling serves every
/// shell: bare where every shell reads the value as itself, in double quotes where they keep it whole in all three, and a
/// placeholder naming the argument where no spelling holds, which the person replaces as their own shell spells the value.
/// </summary>
/// <remarks>
/// <para><b>A twin</b> of the CLI's <c>shellword.ts</c> and the page's <c>shellWord.ts</c>, which carry the reasons beside each
/// kind; <c>ShellWordTests</c> reads their one table, the CLI's <c>test/fixtures/shell-words.json</c>, row for row, and the
/// CLI's suite holds that table against the shells themselves. The rule is read by code point, as theirs is, so a letter
/// written as two UTF-16 units is one letter.</para>
/// <para>It changes no naming rule: a name the contract allows is still allowed, and only a command that names it is spelled.</para>
/// </remarks>
public static class ShellWord
{
    /// <summary>An account's id, where no spelling holds.</summary>
    public const string Account = "<account>";

    /// <summary>A workspace's name, where no spelling holds.</summary>
    public const string Workspace = "<workspace>";

    /// <summary>A name the person gives, where no spelling holds.</summary>
    public const string Name = "<name>";

    /// <summary>
    /// <paramref name="value"/> as a command prints it: bare, in double quotes, or <paramref name="placeholder"/> where no
    /// spelling holds in every shell.
    /// </summary>
    /// <param name="placeholder">What the argument is, as the command names it where the value cannot be printed.</param>
    public static string Of(string value, string placeholder) =>
        IsBare(value) ? value
        : value.Length > 0 && !IsUnspellable(value) ? $"\"{value}\""
        : placeholder;

    /// <summary>Each of <paramref name="values"/> as a command prints it, a space between.</summary>
    public static string Words(IEnumerable<string> values, string placeholder) =>
        string.Join(' ', values.Select(value => Of(value, placeholder)));

    /// <summary>Every shell reads it as itself: a letter or digit of any script or <c>_</c> first, then those and <c>. + : @ / -</c>.</summary>
    private static bool IsBare(string value)
    {
        var first = true;
        foreach (var rune in value.EnumerateRunes())
        {
            var word = Rune.IsLetter(rune) || Rune.IsNumber(rune) || rune.Value == '_';
            if (!word && (first || !IsBareMark(rune.Value))) return false;
            first = false;
        }

        return !first;
    }

    private static bool IsBareMark(int value) => value is '.' or '+' or ':' or '@' or '/' or '-';

    /// <summary>
    /// No one spelling keeps it whole in PowerShell 5.1 (through <c>daoris.cmd</c>), Command Prompt and a POSIX shell:
    /// <c>&amp; | &lt; &gt; ^ % $</c>, a backtick, a double quote, <c>\</c>, <c>!</c>, a quote PowerShell reads as one
    /// (U+2018 to U+201E), and a control, format, line or paragraph separator character.
    /// </summary>
    private static bool IsUnspellable(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is '&' or '|' or '<' or '>' or '^' or '%' or '$' or '`' or '"' or '\\' or '!' or (>= 0x2018 and <= 0x201E))
            {
                return true;
            }

            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                return true;
            }
        }

        return false;
    }
}
