using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The rows and entries of <c>SESSION_GROUPS</c> on the wire. Public so each shape is tested without a service.</summary>
public static class SessionsGroupsWires
{
    /// <summary>
    /// One entry of <c>SESSION_GROUPS</c>, field for field what <c>daoris session list --json</c> prints in <c>entries</c>
    ///its key and the key's halves, the group its leading part put it in, and its parts, oldest first, each with
    /// how it began. Ids, words and counts only.
    /// </summary>
    public static object Entry(SessionEntry entry) => new
    {
        Entry = entry.Key,
        entry.Origin,
        entry.Repository,
        entry.Group,
        entry.Leading,
        entry.Head,
        entry.Opens,
        Parts = entry.Parts.Select(part => new { part.Session, part.Quest, part.Shown, part.Group, part.Began, part.After, part.AskedBy, part.Head }).ToArray(),
        // the records serving its parts, drawn inside it and never listed as entries of their own.
        Coworkers = entry.Coworkers.Select(coworker => new { coworker.Session, coworker.Role, coworker.Serves, coworker.Shown, coworker.State }).ToArray(),
    };

    /// <summary>One row of <c>SESSION_GROUPS</c>, field for field what <c>daoris session list --json</c> prints.</summary>
    public static object Grouped(SessionGrouping row) => new
    {
        row.Session,
        row.Group,
        row.State,
        row.Reason,
        row.Shown,
        row.Archived,
        row.Teammate,
        row.Strikes,
        row.Awaits,
        row.AwaitsOf,
        Work = row.Work is { } work ? new { work.Commits, work.Uncommitted, work.Branch } : null,
        // its stop holds its quest here, which its line says.
        row.HoldsQuest,
        // whose pause holds its quest, which its line says and whose *Resume* stands in *Try again*'s place.
        PausedBy = row.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
        // what holds the person's words on a record that resumes later, which its line says.
        Holds = row.Holds is { } holds ? new { holds.Why, holds.Reason, holds.Repository, holds.Until, holds.Account, holds.AccountName } : null,
        // *Delete…* is offered only where it would be taken.
        row.Deletable,
        // what its own tree offers to land, whatever group it rests in, which its page offers Accept beside. The tree's
        // name, never its path.
        Lands = row.Lands is { } lands ? new { lands.Branch, lands.Tree, lands.Commits, lands.Uncommitted } : null,
        // where the line holds its commits by content, its discard instead, with the driver's sentences the page
        // shows as they are: where its work is, and the ref an unforced discard keeps its commits at.
        Discards = row.Discards is { } discards
            ? new { discards.Branch, discards.Tree, discards.Says, discards.Work.Keeps, discards.KeptAt }
            : null,
        // the call a working session's agent is inside, as the loop heard it, which its row and head say with how long;
        // whether it waits on the person's answer to its permission request.
        Calling = row.Calling is { } call ? new { call.Title, call.Since, call.Waiting, call.Told } : null,
        Waits = row.Waits?.Select(wait => new { wait.Run, wait.Node, wait.Why, wait.Since }).ToArray(),
        // its line's state, which the list, the head and Notifications say for it, and the part or co-worker it is read from.
        Line = row.Line is { } line ? new { line.State, line.Group, line.Reason, line.By, line.Role } : null,
    };
}
