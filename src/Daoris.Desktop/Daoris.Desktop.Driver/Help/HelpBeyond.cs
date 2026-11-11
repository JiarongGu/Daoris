namespace Daoris.Driver;

/// <summary>
/// One thing an Ask Daoris turn did beyond its conversation's focus, as Daoris's doors saw it: when, its act (<c>search</c>, <c>read</c>,
/// <c>propose</c>, <c>press</c>, or <c>asked</c> for one put to the person on a card), the place (this machine for every workspace), what
/// it named of it, a press's outcome or an asked one's card's state, and how many a search found where its tool said so.
/// </summary>
public sealed record HelpBeyondEntry(DateTimeOffset At, string Act, HelpFocus Place, string? What, string? Outcome = null, int? Count = null)
{
    /// <summary>The entry as <c>HELP_BEYOND</c> answers it: <c>{ at, act, place, what, outcome, count }</c>.</summary>
    public IReadOnlyDictionary<string, object?> Wire() => new Dictionary<string, object?>
    {
        ["at"] = At, ["act"] = Act, ["place"] = Place.Wire(), ["what"] = What, ["outcome"] = Outcome, ["count"] = Count,
    };
}

/// <summary>
/// What an Ask Daoris conversation looked at beyond its focus (`docs/2026-11-11-ask-scope-ux-design.md` §4), composed from the places
/// its connector noted (<see cref="HelpPlaceNotes"/>), each judged against the focus the conversation held at its moment: its first,
/// then each change's from the message that carried it (<see cref="HelpFocusMark"/>). The focus fences nothing; this only says.
/// </summary>
public static class HelpBeyond
{
    /// <summary>
    /// Whether <paramref name="place"/> is beyond <paramref name="focus"/>: nothing is beyond this machine; every other workspace is
    /// beyond a workspace; beyond a repository, every other workspace and every other repository, its own workspace's shared records
    /// being near. Every workspace (this machine as a place) is beyond any narrower focus.
    /// </summary>
    public static bool Outside(HelpFocus focus, HelpFocus place) => focus.Kind switch
    {
        HelpFocus.MachineKind => false,
        _ when place.Kind == HelpFocus.MachineKind => true,
        HelpFocus.WorkspaceKind => !focus.Holds(place),
        _ => place.Kind == HelpFocus.WorkspaceKind ? !HelpFocus.OfWorkspace(focus.Workspace!).Equals(place) : !focus.Holds(place),
    };

    /// <summary>The focus a conversation held at <paramref name="at"/>: its first, then each change's from its message's moment.</summary>
    public static HelpFocus FocusAt(HelpFoci held, IReadOnlyList<HelpFocusMark> marks, DateTimeOffset at)
    {
        if (marks.Count == 0) return held.Focus;
        var ordered = marks.OrderBy(each => each.At ?? DateTimeOffset.MinValue).ToList();
        var focus = ordered[0].From;
        foreach (var mark in ordered)
        {
            if (mark.At is { } when && when <= at) focus = mark.To;
        }

        return focus;
    }

    /// <summary>
    /// Each place <paramref name="session"/>'s connector noted beyond the focus held at the time, first to last.
    /// </summary>
    /// <param name="workspace">The conversation's home workspace.</param>
    /// <param name="registry">The registered repositories now, which place a repository named alone.</param>
    public static IReadOnlyList<HelpBeyondEntry> Of(string home, string session, string? workspace, IReadOnlyList<RepoView> registry)
    {
        var notes = HelpPlaceNotes.Read(home, session);
        if (notes.Count == 0) return [];
        var kept = new HelpConversations(home).Read(session);
        var held = HelpFoci.Of(kept, workspace);
        var entries = new List<HelpBeyondEntry>();
        foreach (var note in notes)
        {
            var focus = FocusAt(held, kept.Marks, note.At);
            if (Place(note, focus, registry) is { } place && Outside(focus, place))
            {
                entries.Add(new(note.At, note.Act, place, note.What, Count: note.Found));
            }
        }

        return entries;
    }

    /// <summary>
    /// The place a call named: every workspace for <c>all</c>, else the workspace and repository it named; a repository named alone
    /// is the registry's, the one the focus holds where two workspaces hold its name. Null for one the registry does not hold.
    /// </summary>
    public static HelpFocus? Place(HelpPlaceNote note, HelpFocus focus, IReadOnlyList<RepoView> registry)
    {
        if (note.Workspace is { } named && named.Equals("all", StringComparison.OrdinalIgnoreCase)) return HelpFocus.Machine;
        if (note.Repository is not { } repository) return note.Workspace is { } only ? HelpFocus.OfWorkspace(only) : null;
        if (note.Workspace is { } workspace) return HelpFocus.OfRepository(workspace, repository);
        var held = registry
            .Where(known => string.Equals(known.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(known => HelpFocus.OfRepository(known.Workspace, known.Repository))
            .ToList();
        return held.FirstOrDefault(each => !Outside(focus, each)) ?? held.FirstOrDefault();
    }
}
