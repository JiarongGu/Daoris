global using static Daoris.Knowledge.Clipped;

namespace Daoris.Knowledge;

/// <summary>
/// The one way the service core clips text it quotes back in a refusal or a log line (DEDUP9c).
/// Five private copies each held the same cut with its limit baked in; a file that reads
/// <c>using static Daoris.Knowledge.Clipped;</c> calls <c>Clip(text)</c> as before.
/// </summary>
internal static class Clipped
{
    /// <summary>The limit a quoted value is clipped to unless its caller says otherwise.</summary>
    internal const int DefaultLimit = 120;

    /// <summary>The text itself at or under <paramref name="limit"/> characters, else its first <paramref name="limit"/> and an ellipsis.</summary>
    internal static string Clip(string text, int limit = DefaultLimit) =>
        text.Length <= limit ? text : text[..limit] + "…";

    /// <summary>A commit id's first <paramref name="length"/> characters (DEDUP7b), the id itself when it is no longer; no mark, since an id is not prose.</summary>
    internal static string ShortCommit(string commit, int length) =>
        commit.Length > length ? commit[..length] : commit;

    /// <summary>One line: line ends become spaces and the ends are trimmed, so a quote never breaks its caller's layout.</summary>
    internal static string OneLine(string text) => text.Replace('\n', ' ').Replace('\r', ' ').Trim();
}
