namespace Daoris.Driver;

/// <summary>
/// The notes for a turn that ended while the session's own background work runs: each English as its site always wrote it,
/// byte for byte, with its parts.
/// </summary>
public static class BackgroundNotes
{
    /// <summary>Whether the quest waits on that work could not be read, the program's own message beneath (it may hold a path).</summary>
    public static Noted Unread(string message) =>
        Noted.Of(ChatNoteCodes.BackgroundUnread,
                "— its turn ended while its own background work runs, and whether its quest waits on that work could not be read:")
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>The session waits for the work it names.</summary>
    public static Noted Waits(IReadOnlyList<string> works) =>
        Noted.Of(
            ChatNoteCodes.BackgroundWaits,
            $"— its turn ended while its own background work runs: {string.Join(", ", works.Select(name => $"`{name}`"))}. "
            + "The session waits for that work, and goes on when it ends.",
            ("works", works));

    /// <summary>
    /// The quest closed, was declined or asked another repository while the session waited, so the session ends, and the work
    /// it names, still running, ends with it.
    /// </summary>
    public static Noted WaitOver(IReadOnlyList<string> works) =>
        Noted.Of(
            ChatNoteCodes.BackgroundWaitOver,
            "— its quest no longer waits on its background work: the session ends, and the work still running ends with it: "
            + $"{string.Join(", ", works.Select(name => $"`{name}`"))}.",
            ("works", works));

    /// <summary>
    /// The quest no longer waits and a turn its harness started on its own, as a scheduled wakeup does, said nothing for the
    /// bound: the session ends without that turn's end, which it may never say.
    /// </summary>
    public static Noted TurnLeft() =>
        Noted.Of(
            ChatNoteCodes.BackgroundTurnLeft,
            "— its quest no longer waits, and a turn that started on its own was silent: the session ends without that turn's end.");

    /// <summary>
    /// The quest no longer waits while the session waited for the turn its harness would wake for work that ended: the
    /// session ends, and Daoris's go-on is not sent to a quest that has closed.
    /// </summary>
    public static Noted QuestClosed() =>
        Noted.Of(
            ChatNoteCodes.BackgroundQuestClosed,
            "— its quest no longer waits while the session waited for its agent's next turn: the session ends, and no go-on is sent.");

    /// <summary>
    /// The turn ended with its quest done and its tree holding uncommitted work: the session is carried on to commit it
    /// by its repository's conventions, or to restore what it does not want, rather than end with that work left.
    /// </summary>
    public static Noted CarriedOn(int paths) =>
        Noted.Of(
            ChatNoteCodes.BackgroundCarriedOn,
            $"— its quest is done, but its tree holds uncommitted work (paths: {paths}): the session was carried on to commit it by its "
            + "repository's conventions, or to restore what it does not want and say which.",
            ("paths", paths));

    /// <summary>
    /// The session was carried on as often as its run is (<see cref="TurnCarry.Limit"/>) and its tree is still not clean:
    /// it ends. The words name no count, since the limit is the look's own.
    /// </summary>
    public static Noted CarryBound() =>
        Noted.Of(
            ChatNoteCodes.BackgroundCarryBound,
            "— its session was carried on for the uncommitted work in its tree and it is still not clean: the session ends, and that "
            + "work waits uncommitted for your review.");

    /// <summary>Each work and the state it ended in, as the English and the prompt to the agent both say them.</summary>
    public static string EndedSaid(IEnumerable<(string Name, string State)> ended) =>
        string.Join("; ", ended.Select(work => $"`{work.Name}` ({work.State})"));

    /// <summary>The work ended and the session goes on, a part to each work.</summary>
    public static Noted Ended(IReadOnlyList<(string Name, string State)> ended)
    {
        var noted = new Noted("— its background work ended: ", []);
        for (var each = 0; each < ended.Count; each++)
        {
            var (name, state) = ended[each];
            noted = noted.Then(each == 0 ? "" : "; ",
                Noted.Of(ChatNoteCodes.BackgroundWorkEnded, EndedSaid([ended[each]]), ("work", name), ("state", state)));
        }

        return noted.Then(". ", Noted.Of(ChatNoteCodes.BackgroundGoesOn, "The session goes on."));
    }
}
