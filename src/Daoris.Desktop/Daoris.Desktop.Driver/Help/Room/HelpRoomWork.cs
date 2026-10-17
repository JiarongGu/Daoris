using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Where each unit of work has got to (FABRIC-MILESTONE1b, the fabric events design §D0): whether its end was reached, the result
/// kept, its cleanup (pending, with what waits and why, or completed, with what went) and the recovery retained, in the words
/// `daoris-driver work state` says (<see cref="WorkWords"/>), so the helper answers from the records and never judges an end itself.
/// </summary>
internal sealed class HelpRoomWork : IHelpRoomSection
{
    /// <summary>The most units the room lists: those whose cleanup waits or whose recovery is kept come first, and the count says the rest.</summary>
    internal const int Listed = 10;

    /// <summary>The most recoveries no unit names that the room lists, the rest counted (RECOVERYASK1).</summary>
    internal const int RecoveriesListed = 5;

    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) =>
        machine with { Work = sources.Work, WorkUnread = sources.WorkUnread, WorkRecovery = sources.WorkRecovery };

    /// <summary>A unit the person has something to ask about: its cleanup waits, its owner is unknown, or a recovery is kept.</summary>
    private static bool Open(WorkMilestone unit) =>
        unit.Cleanup.State != "completed" || unit.End.State == "unknown" || unit.Recovery.Count > 0;

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## Where work has got to\n\n");
        if (machine.Work is not { } units)
        {
            text.Append("The milestones were not read, so do not say where any work stands: the person may ask in the terminal, `daoris-driver work state`.\n\n");
            return text.ToString();
        }

        if (units.Count == 0)
        {
            text.Append("No unit of work is recorded on this machine.\n\n");
            Recoveries(text, machine.WorkRecovery);
            return text.ToString();
        }

        text.Append("Each unit's milestone is read from this machine's records and never judged again here: whether its end was reached,\n");
        text.Append("the result kept, its cleanup (pending, with what waits and why, or completed, with what went) and the recovery\n");
        text.Append("retained and until when. Say it as listed; the terminal's `daoris-driver work state [<unit>]` says the same.\n\n");
        var open = units.Where(Open).ToList();
        if (open.Count > 0)
        {
            text.Append("```text\n");
            var first = true;
            foreach (var unit in open.Take(Listed))
            {
                if (!first) text.Append('\n');
                first = false;
                foreach (var line in WorkWords.Milestone(unit)) text.Append(line).Append('\n');
            }

            text.Append("```\n");
            if (open.Count > Listed) text.Append($"and {open.Count - Listed} more.\n");
            text.Append('\n');
        }

        var done = units.Count - open.Count;
        if (done > 0) text.Append($"{done} {(done == 1 ? "unit finished its cleanup" : "units finished their cleanup")} with nothing retained.\n\n");
        foreach (var said in machine.WorkUnread) text.Append($"- not read: {said}\n");
        if (machine.WorkUnread.Count > 0) text.Append('\n');
        Recoveries(text, machine.WorkRecovery);
        return text.ToString();
    }

    /// <summary>
    /// The recoveries no unit names (RECOVERYASK1, CLEANUP1): each says what keeps it, and a recovery ref nothing holds says its
    /// `git update-ref -d` line, which only the person can run. Those come first; at most <see cref="RecoveriesListed"/> are listed.
    /// </summary>
    private static void Recoveries(StringBuilder text, IReadOnlyList<InventoryEntry> recoveries)
    {
        if (recoveries.Count == 0) return;
        var ordered = recoveries.OrderBy(each => each.Kind == InventoryKind.RecoveryRef && each.Until is null ? 0 : 1).ToList();
        text.Append("Recoveries kept on this machine that no unit names (each by its own rule, waiting on nobody; a recovery ref nothing holds\n");
        text.Append("goes only when the person deletes it, with the line said):\n\n```text\n");
        foreach (var each in ordered.Take(RecoveriesListed)) text.Append($"{each.Kind} `{each.Resource}`: {each.Detail}\n");
        text.Append("```\n");
        if (ordered.Count > RecoveriesListed) text.Append($"and {ordered.Count - RecoveriesListed} more.\n");
        text.Append('\n');
    }
}
