using System.Globalization;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// <c>retry</c> (HELP10, D110), as <c>daoris quest retry</c> reads it: a quest by id, <c>#</c> or not, which the
/// loop's last look parked by its strikes, marked forgiven at its failures as that look counted them, as
/// <c>RETRY_QUEST</c> marks it (RETRY1b); or one the person's stop holds (SESSUX1b, D126 §3.4), released from the
/// session the look named, as <c>RETRY_QUEST</c> releases it. Either is said in the terminal's words.
/// </summary>
/// <remarks>
/// The quest must be on one of the look's two lists, since a helper can invent an id, and forgiving a quest that is not
/// parked lets it run past its strikes (D110). The mark is the look's count; the limit said is the one standing when
/// the person applies, as the route reads it.
/// </remarks>
internal static class HelpRetrySetting
{
    public static HelpPlan Plan(string? target, DriverConfig config, HelpMachineFacts facts)
    {
        var quest = target?.TrimStart('#') ?? "";
        if (quest.Length == 0)
        {
            return new HelpPlan(
                "`retry` names the quest its failed sessions parked or your stop holds, by id — `daoris quest retry <quest>`.",
                "", "", null);
        }

        if (facts.Held.FirstOrDefault(held => string.Equals(held.Quest, quest, StringComparison.OrdinalIgnoreCase)) is { } stopped)
        {
            return new HelpPlan(null,
                $"Quest `#{quest}` is released from your stop of session `{stopped.Session}`: the driver takes it up again at its "
                + "next look, and a later stop holds it again.",
                $"daoris quest retry {quest} --session {stopped.Session}",
                // RETRY_QUEST's own edit for a stop: released from that session, and no mark, since a stop is not a strike.
                c => c.WithReleased(quest, stopped.Session));
        }

        var terminal = $"daoris quest retry {quest}";
        if (facts.Parked.FirstOrDefault(parked => string.Equals(parked.Quest, quest, StringComparison.OrdinalIgnoreCase))
            is not { } parkedQuest)
        {
            static string Listed(IEnumerable<string> ids) => ids.Any() ? Names(ids.Select(id => $"#{id}")) : "none";
            return new HelpPlan(
                $"`#{quest}` is neither parked nor held by your stop on this machine — only such a quest starts again, as its "
                + $"page's *Try again* does; at the driver's last look it parked {Listed(facts.Parked.Select(each => each.Quest))} "
                + $"and held {Listed(facts.Held.Select(each => each.Quest))}.",
                "", terminal, null);
        }

        // RETRY_QUEST's own mark (RETRY1b): the quest's failures as the last look counted them, not the limit, which left a
        // quest parked a second time parked. A park carrying no count is marked at the least count the planner parks at.
        int Mark(DriverConfig at) => parkedQuest.Failures ?? at.ForgivenAt(quest) + at.Strikes;
        var strikes = config.Strikes;
        return new HelpPlan(null,
            $"Quest `#{quest}` may be started again. Counting from {Mark(config)} failure(s) — what already happened is still in "
            + $"the records, and {(strikes > 0 ? strikes.ToString(CultureInfo.InvariantCulture) : "no")} more failure(s) will park "
            + "it again.",
            terminal,
            // Marked where it stands rather than erased, so the records still read true.
            c => c.WithForgiven(quest, Mark(c)));
    }
}
