using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a look says, as one string per part, equal when the same is said again whatever the order: the tick is forwarded only when
/// one changes, since every tick the page receives asks its lists again. The loop's doors answer through it.
/// </summary>
internal static class TickSignatures
{
    /// <summary>
    /// The look's lines, however often one repeats: a standing condition (a plugin's hold, a remote the sync cannot reach) says its
    /// line at every look, and forwarding each made the page ask every list again every look, git of every checkout among them. The
    /// page's own rule for a toast (<c>newsFrom</c>): changed since the last, never seen before.
    /// </summary>
    public static string Lines(IEnumerable<string> lines) =>
        string.Join("\n", lines.Distinct(StringComparer.Ordinal).OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// The waits, equal when the same accounts hold the same starts until the same time: a wait that begins or ends for an intake alone
    /// moves no consideration. A wait on signed-out accounts signs with no time.
    /// </summary>
    public static string Waits(IEnumerable<AccountWait> waits) =>
        string.Join("\n", waits
            .Select(wait => string.Join("\t",
                wait.Agent, wait.Account ?? "", wait.Workspace ?? "", wait.Until?.ToUnixTimeSeconds(), wait.Stated,
                string.Join(",", wait.Quests), string.Join(",", wait.Asks), string.Join(",", wait.SignedOut), wait.NoAccount))
            .OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// The kept second-opinion gates that wait on the person, so the page reads the route again when one moves, and a gate that waits
    /// on no one moves nothing.
    /// </summary>
    public static string Judged(IEnumerable<OpinionJudged> judged) =>
        string.Join("\n", judged
            .Where(each => TreesRoutes.WaitsOnPerson(each.Gate, each.Auto))
            .Select(each => string.Join("\t", each.Session, each.Quest, each.Auto, each.Gate.State, each.Gate.Says))
            .OrderBy(line => line, StringComparer.Ordinal));
}
