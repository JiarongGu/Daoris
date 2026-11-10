namespace Daoris.Driver;

/// <summary>
/// A further part of a session: a new quest of an entry with a part of its own run here goes into the tree that run's newest part held when
/// nothing live holds it, and otherwise starts at once alongside, in a tree of its own; nothing waits. A quest that is a run of its own starts
/// in a tree and on a branch of its own beside the line's other runs, so no two runs work one branch (<see cref="LineRuns"/>). Its entry is read from the records
/// (<see cref="EntryParts.KeyOf"/>), never re-derived. Different entries in one repository stay side by side, and a repository whose
/// sessions run in its checkout keeps one at a time: the entry changes only its grouping there.
/// </summary>
internal sealed class RoundJoining(PlanRound round)
{
    internal Consideration Join(Consideration start)
    {
        var snapshot = round.Snapshot;
        var quest = start.Quest;
        if (!round.Config.OpensOwnTree(quest.To) || EntryParts.KeyOf(quest, snapshot) is not { } key) return start;
        var parts = EntryParts.Earlier(key, quest, snapshot);
        if (parts.Count == 0) return start;

        var entry = new EntryParts(key, parts);
        var own = LineRuns.Own(quest, parts, snapshot);
        if (own.Count == 0)
        {
            return start with
            {
                Entry = entry,
                Reason = $"starting in `{quest.To}`, in a tree of its own beside session `{parts[0].Session}`'s — a further part of its line, "
                         + "on its own run's branch.",
            };
        }

        if (own[0] is not { Tree: { Length: > 0 } tree } newest) return start with { Entry = entry };

        if (snapshot.Active.FirstOrDefault(s => Planner.SameTree(s.Tree, tree)) is { } holder)
        {
            return start with
            {
                Entry = entry with { HeldBy = holder.Id },
                Reason = $"starting in `{quest.To}`, alongside session `{holder.Id}`, which is at work in its session's tree — "
                         + "a further part, in a tree of its own.",
            };
        }

        // A part waiting on its own helper keeps its tree, as a park does, though no live list shows it.
        if (Planner.WaitingInTree(snapshot, tree, except: null) is { } waiting)
        {
            return start with
            {
                Entry = entry with { HeldBy = waiting },
                Reason = $"starting in `{quest.To}`, alongside session `{waiting}`, which waits on its own helper in its session's tree — "
                         + "a further part, in a tree of its own.",
            };
        }

        if (round.TreesThisTick.FirstOrDefault(claim => Planner.SameTree(claim.Tree, tree)) is { Quest: { } ahead })
        {
            return start with
            {
                Entry = entry,
                Reason = $"starting in `{quest.To}`, alongside quest `#{ahead}`, which goes into its session's tree at this look — "
                         + "a further part, in a tree of its own.",
            };
        }

        round.TreesThisTick.Add((tree, quest.Id));
        return start with
        {
            Entry = entry with { Into = newest },
            Reason = $"starting in `{quest.To}`, in the tree session `{newest.Session}` worked in — a further part of its session.",
        };
    }
}
