using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The words after <c>events relay</c> (EVENTHOOK1c; the event hook design §6.1): <c>--where</c> alone, which lays the copy out and prints
/// its command, or <c>--once</c> with the home it is told, the watermark of the last batch read, the kinds it prints and the session it ends
/// with. Words it does not take are refused whole, by a fixed sentence.
/// </summary>
/// <param name="Where">Whether this is <c>--where</c>.</param>
/// <param name="Home">The home <c>--home</c> named, or null.</param>
/// <param name="Since">The watermark <c>--since</c> gave, checked, or null.</param>
/// <param name="Kinds">The kinds printed: <c>--kinds</c>'s, else the first build's four.</param>
/// <param name="SessionPid">The process whose exit ends the relay, printing nothing, or null.</param>
public sealed record RelayAsk(bool Where, string? Home, (string Run, long Seq)? Since, IReadOnlySet<string> Kinds, int? SessionPid)
{
    /// <summary>The words read, or null with the sentence that refuses them.</summary>
    public static RelayAsk? Read(IReadOnlyList<string> args, out string? refused)
    {
        refused = RelaySaid.Usage;
        if (args is ["--where"]) return Taken(new RelayAsk(true, null, null, RelayBatch.Default, null), out refused);

        var once = false;
        string? home = null;
        (string, long)? since = null;
        IReadOnlySet<string> kinds = RelayBatch.Default;
        int? session = null;
        for (var at = 0; at < args.Count; at++)
        {
            var word = args[at];
            if (word == "--once" && !once)
            {
                once = true;
                continue;
            }

            if (at + 1 >= args.Count) return null;
            var value = args[++at];
            switch (word)
            {
                case "--home" when home is null && value.Length > 0:
                    home = value;
                    break;
                case "--since" when since is null:
                    if (RelayChecks.Since(value) is not { } watermark)
                    {
                        refused = RelaySaid.SinceRefused;
                        return null;
                    }

                    since = watermark;
                    break;
                case "--kinds" when ReferenceEquals(kinds, RelayBatch.Default):
                    var named = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (named.Length == 0 || named.Any(kind => !RelaySaid.Said.Contains(kind, StringComparer.Ordinal))) return null;
                    kinds = named.ToHashSet(StringComparer.Ordinal);
                    break;
                case "--session-pid" when session is null:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0) return null;
                    session = pid;
                    break;
                default:
                    return null;
            }
        }

        return once ? Taken(new RelayAsk(false, home, since, kinds, session), out refused) : null;
    }

    private static RelayAsk Taken(RelayAsk ask, out string? refused)
    {
        refused = null;
        return ask;
    }
}
