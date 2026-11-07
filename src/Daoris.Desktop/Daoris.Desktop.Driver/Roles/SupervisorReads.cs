namespace Daoris.Driver;

/// <summary>
/// What the look and the cards read of the live decisions (§6.1, §6.3, §4.2): the attribution of a word, the hold, the sessions answering,
/// and a session's decisions live and as history. Reads memory alone, through the <see cref="SupervisorBook"/>; changes nothing.
/// </summary>
internal sealed class SupervisorReads(SupervisorBook book)
{
    /// <summary>
    /// Whether <paramref name="question"/> of <paramref name="session"/> has a decision, live or closed as the owner's (§3.1's
    /// <c>answered</c> and <c>overruled</c>): what <c>SUPERVISOR_WAITING</c> leaves out.
    /// </summary>
    public bool Decided(string session, string question) =>
        book.Closed.ContainsKey((session, question)) || book.Live.Values.Any(each => each.Session == session && each.Question.Id == question);

    /// <summary>
    /// Whether <paramref name="word"/> is the supervisor's: its id is the one memory recorded from a live decision's own release, from
    /// <c>released</c> through <c>admitted</c> and <c>went-on</c>. Ids, never text: an identical word posted by another is not.
    /// </summary>
    public bool Authored(string session, string word) =>
        book.Live.Values.Any(each => each.Session == session && each.Word == word
            && each.State is SupervisorState.Released or SupervisorState.Admitted or SupervisorState.WentOn);

    /// <summary>
    /// Whether <paramref name="word"/> may be planned or handed: authored, and its decision <c>released</c> (eligible to plan), or
    /// <c>admitted</c> under <paramref name="ticket"/> (handed).
    /// </summary>
    public bool Handable(string session, string word, SupervisorTicket? ticket = null) =>
        book.Live.Values.Any(each => each.Session == session && each.Word == word
            && (each.State == SupervisorState.Released
                || (each.State == SupervisorState.Admitted && ticket is not null && ticket.Decision == each.Id && ticket.Word == word)));

    /// <summary>
    /// The hold (§6.3): the moment a held decision of <paramref name="session"/> goes on, or null where none is held. While it is held
    /// and no word of the owner's waits, the record is held whatever else waits.
    /// </summary>
    public DateTimeOffset? HeldUntil(string session) =>
        book.Live.Values.Where(each => each.Session == session && each.State == SupervisorState.Held)
            .Select(each => (DateTimeOffset?)each.GoesOnAt).Min();

    /// <summary>The soonest moment a held decision goes on, across every session, for the loop's nudge (§3.4); null where none is held.</summary>
    public DateTimeOffset? NextGoesOn() =>
        book.Live.Values.Where(each => each.State == SupervisorState.Held).Select(each => (DateTimeOffset?)each.GoesOnAt).Min();

    /// <summary>The sessions a held decision holds now (§6.3), for a look that reads no snapshot of the driver's.</summary>
    public IReadOnlySet<string> HeldSessions() =>
        new HashSet<string>(book.Live.Values.Where(each => each.State == SupervisorState.Held).Select(each => each.Session), StringComparer.Ordinal);

    /// <summary>The sessions a held or released decision answers for now (§4.2): their parks are not a wait on the owner.</summary>
    public IReadOnlySet<string> Answering() => new HashSet<string>(
        book.Live.Values.Where(each => each.State is SupervisorState.Held or SupervisorState.Released).Select(each => each.Session),
        StringComparer.Ordinal);

    /// <summary>Whether <paramref name="session"/> has a decision that is not final, which the look settles, compares and releases.</summary>
    public bool Moving(string session) => book.Live.Values.Any(each => each.Session == session && !SupervisorStates.Final(each.State));

    /// <summary>The decisions live in memory for <paramref name="session"/>, as they stand.</summary>
    public IReadOnlyList<SupervisorDecisionView> LiveOf(string session) =>
        [.. book.Live.Values.Where(each => each.Session == session).OrderBy(each => each.KeptAt).Select(View)];

    /// <summary>
    /// <paramref name="session"/>'s decisions for its history (§6.1): those live here from memory, every other read from its files as
    /// history, marked <see cref="SupervisorDecisionView.Earlier"/>, granting nothing.
    /// </summary>
    public IReadOnlyList<SupervisorDecisionView> History(string session)
    {
        var views = LiveOf(session).ToList();
        if (!SessionEventText.IsLocalId(session) || !Directory.Exists(book.Files.FolderOf(session))) return views;
        foreach (var file in Directory.EnumerateFiles(book.Files.FolderOf(session), "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (book.Live.ContainsKey(id) || book.Files.Earlier(session, id) is not { } earlier) continue;
            views.Add(earlier);
        }

        return [.. views.OrderBy(view => view.KeptAt)];
    }

    /// <summary>
    /// <paramref name="session"/>'s course decisions, oldest first: each read from its file, and where memory still holds it, its state
    /// as memory has it, which alone decides; the rest are history, marked <see cref="SupervisorCourseView.Earlier"/>.
    /// </summary>
    public IReadOnlyList<SupervisorCourseView> CoursesOf(string session)
    {
        if (!SessionEventText.IsLocalId(session) || !Directory.Exists(book.Files.FolderOf(session))) return [];
        var views = new List<SupervisorCourseView>();
        foreach (var file in Directory.EnumerateFiles(book.Files.FolderOf(session), "*.json"))
        {
            if (SupervisorCourseView.Read(book.Files, session, Path.GetFileNameWithoutExtension(file)) is not { } read) continue;
            views.Add(book.Courses.TryGetValue(read.Id, out var live) ? read with { State = live.State, StateAt = live.StateAt, Why = live.Why, Earlier = false } : read);
        }

        return [.. views.OrderBy(view => view.KeptAt).ThenBy(view => view.Id, StringComparer.Ordinal)];
    }

    private static SupervisorDecisionView View(SupervisorLiveDecision each) => new(
        each.Id, each.Session, each.Question, each.Choice, each.Reasons, each.Holder, each.Generation, each.KeptAt, each.GoesOnAt,
        each.State, each.StateAt, each.Why, each.Word, Earlier: false, each.Bound);
}
