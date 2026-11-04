using static Daoris.Driver.TraceLookups;

namespace Daoris.Driver;

/// <summary>The session link of a chain: its record's facts, then this machine's, then what stood when it started.</summary>
internal static class TraceSessionLinks
{
    /// <summary>The states a record ends in (<see cref="SessionRecord"/>'s list): anything else still runs or waits.</summary>
    private static readonly HashSet<string> Endings = new(StringComparer.Ordinal)
    {
        "completed", "declined", "failed", "stopped", "stood-down",
    };

    /// <summary>One session: its record's facts, then this machine's (its events, its rules, its landing), then what stood when it started.</summary>
    internal static TraceSessionLink Session(TracedSession session, TraceFacts facts, TraceAskRead? ask, TraceHere here)
    {
        var events = session.Teammate ? null : TraceMachineReads.Events(session, here);
        return new TraceSessionLink(session.Id)
        {
            Kind = session.Kind,
            State = StateOf(session),
            Opened = session.Created,
            Moved = session.Updated,
            Agent = AgentOf(session),
            Teammate = session.Teammate,
            Tree = session.Teammate ? null : FolderOf(session.Tree),
            TreePath = session.Teammate ? null : session.Tree,
            BaseCommit = session.Teammate ? null : session.BaseCommit,
            Quest = session.Quest,
            Ask = session.Ask,
            Before = Before(session, facts),
            Took = session.Took,
            Note = session.Note,
            Answer = session.Answer,
            Evidence = Evidence(session),
            Events = events,
            Rules = session.Teammate ? null : TraceMachineReads.Rules(session, here),
            Landing = session.Teammate ? null : TraceLandings.Landing(session, facts, events),
            Stood = TraceStoods.Stood(session, ask, here.Config),
        };
    }

    /// <summary>The record before it on its quest, by when each opened: its ending, and whether it ran in the same tree.</summary>
    private static TraceBefore? Before(TracedSession session, TraceFacts facts)
    {
        if (session.Quest is null || session.Created is not { } opened) return null;
        var before = facts.Sessions
            .Where(each => Same(each.Quest, session.Quest) && each.Created is { } at && at < opened)
            .MaxBy(each => each.Created);
        if (before is null) return new TraceBefore(First: true);

        return new TraceBefore(First: false)
        {
            Session = before.Id,
            State = before.State,
            Ended = Endings.Contains(before.State),
            At = before.Updated,
            Tree = before.Tree is null || session.Tree is null ? TraceTrees.Unknown
                : SamePath(before.Tree, session.Tree) ? TraceTrees.Same
                : TraceTrees.Other,
        };
    }

    /// <summary>The commits the driver read off its tree at its end, as its record keeps them.</summary>
    private static TraceEvidence? Evidence(TracedSession session)
    {
        if (session.Evidence is not { } evidence) return null;
        var lines = evidence.ReplaceLineEndings("\n").Split('\n');
        return new TraceEvidence(
            lines[0],
            [.. lines.Skip(1).Where(line => line.Trim().Length > 0).Select(line => line.Trim())],
            [.. session.Commits.Select(commit => new TraceCommit(commit.Sha, commit.Line))]);
    }
}
