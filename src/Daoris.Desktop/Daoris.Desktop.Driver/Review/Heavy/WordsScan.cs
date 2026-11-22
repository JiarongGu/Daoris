using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The one boundary a reached command's words pass before they are kept or told: the whole log goes through <see cref="GitWords.Redact"/> (headers,
/// address user-info, token shapes, key blocks) before it is split into lines, and a line it cut, or one holding a Windows or Unix home path, is
/// withheld whole, judged on the complete line before any display cut; an id the outcome names passes the same way.
/// </summary>
internal static partial class WordsScan
{
    internal const string Withheld = "[a line holding what looks like a secret or a home path is withheld]";

    [GeneratedRegex(@"[A-Za-z]:(?:[\\]{1,2}|/)Users(?:[\\]{1,2}|/)[A-Za-z0-9._-]+|/(?:home|Users)/[a-z][a-z0-9._-]+", RegexOptions.IgnoreCase)]
    private static partial Regex Home();

    internal static List<string> Lines(string log) =>
        [.. (GitWords.Redact(log) ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => Line(line.TrimEnd()))];

    internal static string Id(string id) => Line(GitWords.Redact(id) ?? "");

    private static string Line(string redacted) =>
        redacted.Contains(GitWords.Cut, StringComparison.Ordinal) || Home().IsMatch(redacted) ? Withheld : redacted;
}
