namespace Daoris.Knowledge;

/// <summary>The sentences more than one exchange answer shares: the lost take's stand-down, the chain's next step, quests as an answer names them.</summary>
internal static class QuestWords
{
    /// <summary>
    /// A move or a wait refused because this machine's take lost, in one sentence for both: what happened to
    /// the take, then <paramref name="what"/>, what that means for this one, then the stand-down the take's own loss says.
    /// A remote refusing such a push says it in the same words, and the pushing machine's pass relays them.
    /// <paramref name="reopened"/> where the take lost to the person's reopen of the work its step grew from: said as
    /// that, never as another machine's take. <paramref name="outgrown"/> where it lost with the work it was made on, and the step stands
    /// open on another submission, published anew by the chain that won its source: said as that, since nobody took
    /// the step itself first. A refusal on a step open reads one or the other from its history (<see cref="QuestMove.LostToTheReopen"/>).
    /// </summary>
    public static string LostTake(string id, string what, bool reopened = false, bool outgrown = false) =>
        reopened
            ? $"Quest `#{id}` moved under this machine's take: the person's reopen of the work it grew from reached the remote first, so "
              + $"this machine's take lost and is kept on the quest as a conflict, {what} Stand down: this session's work stays in its tree, "
              + "and the step is open for the work as it stands now."
        : outgrown
            ? $"Quest `#{id}` was published anew on other work: the submission this machine's take of it was made on went, so the take "
              + $"lost and is kept on the quest as a conflict, {what} Stand down: this session's work stays in its tree, and the step is "
              + "open for the work it grows from now."
        : $"Quest `#{id}` was taken on another machine first: this machine's take lost and is kept on the quest as a conflict, "
          + $"{what} Stand down rather than doubling the work.";

    /// <summary>
    /// The lost take's words for a move or a set-up the store refused with <see cref="QuestMove.ClaimLost"/>: on a step open, the
    /// person's reopen or the work the take was made on gone, as the store read it inside its write.
    /// </summary>
    public static string LostTake(string id, string what, QuestMove move) =>
        LostTake(id, what, reopened: move is { Quest.Status: QuestStatus.Open, LostToTheReopen: true },
            outgrown: move is { Quest.Status: QuestStatus.Open, LostToTheReopen: false });

    /// <summary>
    /// A done, a decline or a set-up refused on a take this machine lost: the lost take's words, then the
    /// two choices that stand for the person on its conflict, since a remote cannot tell their move from the session's and refuses both.
    /// </summary>
    public static string LostStep(string id, string what, QuestMove move) => LostTake(id, what, move) + Choices(move.Quest!);

    /// <summary>
    /// The person's two choices on this machine's lost take (D178 point 2), named as the page and the terminal name them, with why
    /// <i>Bring mine forward</i> does not stand yet where it does not (point 3): the step held by another machine, or its source held by the
    /// reopen. A closed step offers neither, and no refusal here meets one.
    /// </summary>
    public static string Choices(Quest step)
    {
        const string Lead = " The person chooses how its conflict ends: *Drop mine* dismisses it and leaves this machine's work where it is, "
            + "or *Bring mine forward* takes the step afresh and goes on with that work in its kept tree";
        const string Doors = " (on the quest's page, or `daoris remote sync dismiss` and `sync forward`).";
        return step switch
        {
            { Status: QuestStatus.Open, SourceReopened: not null } => Lead + ", once the work the step grows from is submitted again" + Doors,
            { Status: QuestStatus.Open } => Lead + Doors,
            { Status: QuestStatus.Taken } => Lead + ", once the step is open again: another machine holds it now" + Doors,
            _ => "",
        };
    }

    /// <summary>
    /// Why a session's take, done or <c>ready</c> on a step moved nothing: the work it grew from was reopened, and
    /// the person chooses. Said as a stand-down, since its session should go no further.
    /// </summary>
    public static string SourceReopenedWords(Quest step) =>
        $"Quest `#{step.Id}` grew from `#{step.Parent}`'s work, which the person reopened: "
        + (step.Status == QuestStatus.Open
            ? "it is withdrawn, and no session takes it"
            : "it is held, and goes no further")
        + " until they choose to go on with it on the new work or to abandon it. Nothing moved. Stop here.";

    /// <summary>The chain's next step a close or a yes published (D65 §4), said so whoever moved it knows whose it now is.</summary>
    public static string Then(Quest? followUp) => followUp is { } next
        ? $"\n\nThen: published `#{next.Id}` to `{next.To}` on behalf of `{next.From}` — {next.Status}."
          + (next.Then.Count > 0 ? $" {next.Then.Count} more step(s) follow it." : "")
        : "";

    /// <summary>The quests waiting on a quest that moved, and that they resume at the driver's next look; empty where none wait.</summary>
    public static string Resumes(IReadOnlyList<Quest> waiting) => waiting.Count == 0
        ? ""
        : $"\n\n{Listed(waiting)} {(waiting.Count == 1 ? "waits" : "wait")} on it, and {(waiting.Count == 1 ? "resumes" : "resume")} "
          + "at the driver's next look.";

    /// <summary>Quests by id and receiver, as an answer names them.</summary>
    public static string Listed(IReadOnlyList<Quest> quests) =>
        string.Join(", ", quests.Select(quest => $"`#{quest.Id}` (`{quest.To}`)"));

    /// <summary>A commit as a sentence names it: its first seven characters.</summary>
    public static string Short(string commit) => Clipped.ShortCommit(commit, 7);

    public static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

    public static string When(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    public static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
