namespace Daoris.Driver;

/// <summary>
/// The terminal's words for an abandon: English, one line each, the first under the binary's name. The page says its own from the
/// answer's facts.
/// </summary>
internal static class AbandonLines
{
    /// <summary>What a terminal prints of the first press.</summary>
    internal static IReadOnlyList<string> Of(AbandonPlan plan)
    {
        var scope = plan.Work.Pieces.Scope;
        var id = plan.Work.Pieces.Id;
        var what = Named(scope, id);
        if (!plan.Abandonable)
        {
            return
            [
                $"nothing of {what} is left to abandon on this machine"
                + (plan.Closes is { } open ? $"; `daoris ask close {open} --reason \"…\"` closes it." : "."),
            ];
        }

        var lines = new List<string>
        {
            $"abandoning {what} would take {Count(plan.Pieces.Count, "piece")} and keep {plan.Kept.Count}; "
            + $"`{WorkAbandoning.Door(scope, id)} --reason \"…\" --yes` abandons what this list holds.",
        };
        foreach (var quest in plan.Quests.Where(quest => quest.Act == AbandonAct.Decline))
        {
            lines.Add(quest.WhileOpen
                ? $"  declines #{quest.Quest.Quest.Id}: it applies only while it is open."
                : $"  declines #{quest.Quest.Quest.Id}, taken here: its sessions are stopped first.");
        }

        foreach (var session in plan.Sessions.Where(session => AbandonAct.Takes(session.Act)))
        {
            var then = session.Archive ? ", then archives it." : "; it stays to review: its tree keeps work.";
            lines.Add(session.Act switch
            {
                AbandonAct.Stop => $"  stops {SessionNamed(session.Session)}{then}",
                AbandonAct.End => $"  ends {SessionNamed(session.Session)} unanswered{then}",
                _ => $"  archives {session.Session.Record.Id}.",
            });
        }

        foreach (var tree in plan.Trees.Where(tree => AbandonAct.Takes(tree.Act)))
        {
            lines.Add(tree.Act == AbandonAct.Discard
                ? $"  discards {tree.Tree.Repository} {tree.Tree.Branch} with its tree: {Holding(tree.Judged)}."
                : $"  deletes {tree.Tree.Repository} {tree.Tree.Branch}, whose tree is gone: {CommitsOf(tree.Judged)}.");
        }

        if (plan.Closes is { } ask) lines.Add($"  closes ask #{ask} with your reason.");
        lines.AddRange(plan.Kept.Select(keep => $"  keeps {PieceNamed(plan, keep.Piece)}: {Because(keep)}"));
        return lines;
    }

    /// <summary>What a terminal prints of the second press.</summary>
    internal static IReadOnlyList<string> Of(AbandonOutcome outcome)
    {
        var what = Named(outcome.Scope, outcome.Id);
        switch (outcome.Verdict)
        {
            case AbandonVerdict.Unknown:
                return [WorkPausing.Unknown(outcome.Scope, outcome.Id)];
            case AbandonVerdict.Reason:
                return [WorkAbandoning.NeedsReason];
            case AbandonVerdict.Nothing:
                return [$"nothing of {what} is left to abandon on this machine, so nothing changed."];
        }

        var changes = outcome.Changed.Count switch
        {
            0 => ".",
            1 => "; 1 changed since the list and was kept.",
            var n => $"; {n} changed since the list and were kept.",
        };
        var lines = new List<string> { $"abandoned {what}: {outcome.Went} of {Count(outcome.Listed, "piece")}{changes}" };
        if (outcome.Stopped.Count > 0) lines.Add($"  stopped {string.Join(", ", outcome.Stopped.Select(stop => stop.Session))}.");
        if (outcome.Declined.Count > 0) lines.Add($"  declined {string.Join(", ", outcome.Declined.Select(quest => $"#{quest}"))} with your reason.");
        if (outcome.Closed) lines.Add($"  closed ask #{outcome.Id} with your reason.");
        lines.AddRange(outcome.Discarded.Select(tree =>
            $"  {(tree.Alone ? "deleted" : "discarded")} {tree.Repository} {tree.Branch} at {tree.Tip}: "
            + $"`git branch {tree.Branch} {tree.Tip}`, in {tree.Repository}, brings it back while git keeps its commits."));
        if (outcome.Archived.Count > 0) lines.Add($"  archived {string.Join(", ", outcome.Archived)}.");
        if (outcome.RunsEnded.Count > 0) lines.Add($"  stopped the runs' steps {string.Join(", ", outcome.RunsEnded)} with your reason: none waits on you.");
        lines.AddRange(outcome.Declines.Select(decline => decline.Answer switch
        {
            DeclineAnswer.Confirmed => $"  #{decline.Quest}'s decline was confirmed by the remote.",
            DeclineAnswer.Lost => $"  #{decline.Quest} was taken on another machine before your decline reached the remote; it stays theirs, "
                                  + "and the conflict is on the quest.",
            _ => $"  #{decline.Quest}'s decline is unconfirmed: it travels on the next sync, where the same rule applies.",
        }));
        lines.AddRange(outcome.Changed.Select(keep => $"  kept {Piece(keep.Piece)}, which changed since the list: {Because(keep)}"));
        lines.AddRange(outcome.Failed.Select(Failure));
        lines.AddRange(outcome.Joined.Select(piece => $"  {Piece(piece)} joined the work after the list, so it was not taken."));
        lines.AddRange(outcome.Stayed.Select(keep => $"  kept {Piece(keep.Piece)}: {Because(keep)}"));
        if (outcome.StillPaused)
        {
            var why = outcome.Failed.Count > 0 ? "a step could not finish" : "something joined the work after the list";
            var resume = new PausedBy(outcome.Scope, outcome.Id).Door;
            lines.Add($"  {what} stays paused: {why}; `{WorkAbandoning.Door(outcome.Scope, outcome.Id)}` again, or `{resume}`.");
        }

        return lines;
    }

    /// <summary>A kept piece's reason.</summary>
    private static string Because(AbandonKeep keep) => keep.Why switch
    {
        AbandonWhy.TakenElsewhere => $"taken on {keep.Machine ?? "another machine"}: its work is theirs; decline it on its page if you mean to stop it there.",
        AbandonWhy.SourceReopened => "you reopened the work its step grew from, which reached the remote before this machine's take did: the step is open again for the work as it stands now.",
        AbandonWhy.TakenOutside => "taken here outside Daoris: its work is wherever its taker works, not in a tree Daoris made.",
        AbandonWhy.Done => "done: finished work keeps its record.",
        AbandonWhy.Teammate => $"it runs on {keep.Machine}; nothing this machine sends reaches it.",
        AbandonWhy.Review => "its tree keeps work.",
        AbandonWhy.Landed => "a landing took its work.",
        AbandonWhy.Elsewhere => keep.Where is { } where ? $"its commits are on `{where}`." : "some of its commits are on another branch, a remote or a tag.",
        AbandonWhy.CheckedOut => "its branch is checked out somewhere else.",
        AbandonWhy.InUse => "a session still runs in it.",
        AbandonWhy.Gone => "it is gone, or needs nothing now.",
        AbandonWhy.Unreached => "a session of it could not be stopped.",
        AbandonWhy.Refused => $"it was refused: {keep.Detail}",
        _ => "git could not say whether its work is anywhere else.",
    };

    /// <summary>A piece a step could not take, said with what refused it.</summary>
    private static string Failure(AbandonKeep keep) => keep.Why switch
    {
        AbandonWhy.Unreached when keep.Piece.StartsWith("session:", StringComparison.Ordinal) => keep.Detail switch
        {
            PauseKept.Elsewhere => $"  could not stop {Piece(keep.Piece)}: the Daoris process that runs it took no request in time "
                                     + "(a terminal's chat stops with its own Ctrl+C).",
            PauseKept.Unanswered => $"  could not stop {Piece(keep.Piece)}: the service did not move its record.",
            _ => $"  could not stop {Piece(keep.Piece)}: it is queued, and nothing on this machine runs it yet.",
        },
        AbandonWhy.Unreached => $"  kept {Piece(keep.Piece)}: its session {keep.Detail} could not be stopped, so it was not declined.",
        AbandonWhy.InUse => $"  kept {Piece(keep.Piece)}: a session still runs in it.",
        _ => $"  kept {Piece(keep.Piece)}: {keep.Detail}",
    };

    /// <summary>A piece's key as a person reads it: <c>#q1</c>, a session's id, a tree's repository and branch, <c>ask #a1</c>.</summary>
    private static string Piece(string key) => key.Split(':', 2) switch
    {
        ["quest", var quest] => $"#{quest}",
        ["session", var session] => session,
        ["ask", var ask] => $"ask #{ask}",
        ["tree", var tree] when tree.IndexOf(':') is > 0 and var at => $"{tree[..at]} {tree[(at + 1)..]}",
        _ => key,
    };

    /// <summary>A kept piece as the first press names it, a session with its quest.</summary>
    private static string PieceNamed(AbandonPlan plan, string key) =>
        plan.Sessions.FirstOrDefault(session => string.Equals(session.Key, key, StringComparison.Ordinal)) is { } session
            ? session.Why == AbandonWhy.Review ? $"{session.Session.Record.Id} to review" : SessionNamed(session.Session)
            : Piece(key);

    private static string SessionNamed(WorkSession session) =>
        session.Intake ? $"{session.Record.Id}, the ask's intake"
        : session.Record.Quest is { } quest ? $"{session.Record.Id} on #{quest}"
        : session.Record.Id;

    private static string Holding(TreeOnlyHere judged)
    {
        var commits = CommitsOf(judged);
        if (judged.Uncommitted is not ({ } count and > 0)) return commits;
        var named = string.Join(", ", judged.Files) + (count > judged.Files.Count ? ", …" : "");
        return $"{commits} and {count} uncommitted file(s) ({named})";
    }

    private static string CommitsOf(TreeOnlyHere judged) => judged.Commits is { } commits ? $"{commits} commit(s)" : "commits git could not count";

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Named(WorkScope scope, string id) => scope == WorkScope.Ask ? $"ask #{id}" : $"#{id}";
}
