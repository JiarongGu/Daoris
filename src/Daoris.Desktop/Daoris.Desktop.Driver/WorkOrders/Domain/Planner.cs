namespace Daoris.Driver;

/// <summary>
/// The decision half of a tick: which open quests start, and why every other one is sitting.
/// </summary>
/// <remarks>
/// <para><b>Pure, deliberately.</b> Plan and apply are separate functions — the CLI's own convention,
/// held here for the same reason: a decision that can be asserted without spawning anything stays
/// tested, and a printed plan is a driver that can explain itself.</para>
///
/// <para><b>The plan never writes quest state and never outranks the ledger.</b> The service re-judges
/// every open through <c>SessionLedger</c>; this planner exists to avoid asking for work the ledger
/// would refuse, and to give every refusal a sentence before it happens.</para>
/// </remarks>
public static class Planner
{
    /// <param name="door">
    /// Which door this machine's starts ride: the configured adapter's wire. It decides whether a
    /// repository that registered without adopting can be driven. Defaults to the pipe, the door
    /// with the stricter requirement, so a caller that does not know cannot start more than it should.
    /// </param>
    /// <param name="window">
    /// Whether this loop carries Daoris's browser, the window a local set-up step shows its chain's work in. Defaults to none, the
    /// headless loop's case, so a caller that does not know sits such a step rather than start one with nowhere to show it.
    /// </param>
    /// <param name="roles">
    /// The run's memory of a role's acts: a role's release in <see cref="DriverConfig.RoleReleases"/> is a release only where it holds
    /// that release's operation at <c>released</c>. Null honours none, as a build before the key reads it.
    /// </param>
    public static IReadOnlyList<Consideration> Plan(
        Snapshot snapshot, DriverConfig config, SessionWire door = SessionWire.Pipe, bool window = false, IRoleStarts? roles = null,
        Action<LineCourse>? decided = null) =>
        new PlanRound(snapshot, config, door, window, roles, decided).Run();

    /// <summary>
    /// Whether a taken quest whose last session here ended so is carried on from it: what the planner plans a carry-on for, and what
    /// a look reads whose the take is for, one rule for both.
    /// </summary>
    internal static bool CarriesOn(PriorSession last) =>
        string.Equals(last.State, "failed", StringComparison.OrdinalIgnoreCase)
        // A stop that was not the person's — the sweep's or a shutdown's.
        || last is { State: "stopped", Interrupted: true }
        // The person's own stop, once they released it: a held one never reaches the planner's carry-on.
        || last.PersonStopped
        // The person answered a session that parked to ask them.
        || last is { State: "completed", PersonSaid: true }
        // The person's reopen of the work it grew from stopped it: carried on once they go on, held until then.
        || StoppedByReopen(last);

    /// <summary>Whether <paramref name="last"/> ended past its transient bound (<see cref="LimitResume.EndedTransient"/>).</summary>
    internal static bool EndedTransient(PriorSession last) => LimitResume.EndedTransient(last);

    /// <summary>
    /// Whether <paramref name="last"/> ended because the person reopened the work its quest grew from
    /// (<see cref="NoteCodes.EndedSourceReopened"/>): <c>completed</c>, by its note's code, never its English.
    /// </summary>
    internal static bool StoppedByReopen(PriorSession last) =>
        string.Equals(last.State, "completed", StringComparison.OrdinalIgnoreCase)
        && last.NoteParts?.Any(part => part.Code == NoteCodes.EndedSourceReopened.Code) == true;

    /// <summary>
    /// The step before, named where a session ends stopped by the person's reopen of the work its quest grew from (what
    /// <see cref="Observation.Conclude"/> ends as <see cref="NoteCodes.EndedSourceReopened"/>): its quest held by the reopen as it ends,
    /// or gone on with since <paramref name="started"/>, the reopen and the go-on both made while it ran, so the step reads unheld,
    /// taken, grown from another submission than at its start, with no result of its own since. Either way the person's go-on carries
    /// it on with its branch brought up to date with the source's new submission (<see cref="ReopenTelling.BringsToSource"/>); read as
    /// an ordinary end, its record ended after the go-on, which neither that carry-on nor the telling reads. Null for every other end.
    /// </summary>
    /// <param name="started">The quest as the start that ran the session read it.</param>
    /// <param name="after">The quest as it stands at the session's end; null where the service no longer answers it.</param>
    internal static string? ReopenedFrom(QuestView started, QuestView? after) =>
        after is { HeldByReopen: true, Parent: { Length: > 0 } held } ? held
        : after is { Status: "Taken", HeldByReopen: false, Submission: null, GrewFrom: { } now, Parent: { Length: > 0 } source }
          && !Equals(started.GrewFrom, now)
            ? source
            : null;

    /// <summary>
    /// Whether the person went on with <paramref name="step"/> on its source's new submission after <paramref name="last"/>, its last
    /// session here, had ended normally: what <see cref="ReopenTelling"/> tells that session, so the go-on with Daoris's words goes on
    /// in it. The service's fact, read from the step and the record alone: the step is taken and unheld, grew from a submission, and
    /// has no result of its own, which the go-on cleared, nor a reopen of one; the record is this machine's, parked with nothing said
    /// to it or completed, and the step moved after it did, by the rebound.
    /// </summary>
    /// <remarks>
    /// A session the reopen stopped is carried on instead (<see cref="StoppedByReopen"/>), and a cut-off, a stop, a stand-down, a record
    /// with words waiting and a running one read as before. <b>Once</b>, with nothing kept: the record that goes on moves after the step,
    /// and a <c>ready</c> gives the step a result again. A step whose session ran across the go-on is not read here: its record ended after
    /// the step moved, as the reopen stopped it (<see cref="ReopenedFrom"/>), so it is carried on instead.
    /// </remarks>
    internal static bool GoneOnSince(QuestView step, PriorSession last) =>
        step is { Status: "Taken", Awaits: null or "", HeldByReopen: false, Submission: null, Reopened: null, GrewFrom: not null, Parent: { Length: > 0 } }
        && !last.Teammate && !last.Running && !last.WordsWaiting && !last.PersonSaid
        && (last.Parked || (string.Equals(last.State, "completed", StringComparison.OrdinalIgnoreCase) && !StoppedByReopen(last)))
        && last.Updated is { } ended && step.Updated is { } moved && moved > ended;

    /// <summary>
    /// The commit a step the person went on with is brought up to date with: its source's current submission's, where that is the one
    /// the step grew from now. Null where the source is not read, names another submission or none, or names no commit (a host before
    /// the field, or a submission whose commit no session's end read).
    /// </summary>
    internal static string? SourceCommit(QuestView step, QuestView? source) =>
        step.GrewFrom is { } grew && source is { Submission: { Commit: { } commit } current } && source.Id == step.Parent
        && current.Machine == grew.Machine && current.Sequence == grew.Sequence
            ? commit
            : null;

    /// <summary>
    /// Whether a taken quest's take is not this machine's (<see cref="QuestTakes.TakenAway"/>), as the planner reads it and the
    /// hand-over's judge asks it before a quest's session goes on with another agent.
    /// </summary>
    internal static bool TakenAway(QuestView quest, PriorSession last, QuestTake take) => QuestTakes.TakenAway(quest, last, take);

    /// <summary>When a taken quest's take was made (<see cref="QuestTakes.TakenAt"/>).</summary>
    internal static DateTimeOffset? TakenAt(QuestView quest, QuestTake take) => QuestTakes.TakenAt(quest, take);

    /// <summary>Whether a record that stood down is carried on by the person's bring-forward of its lost take (<see cref="QuestTakes.BroughtForward"/>).</summary>
    internal static bool BroughtForward(PriorSession lost, QuestTake take) => QuestTakes.BroughtForward(lost, take);

    /// <summary>
    /// Whether a bring-forward's carry-on (<see cref="Consideration.BroughtForward"/>) is first brought up to date with its step's
    /// current source, then the line, as the go-on's is: a step that grew from a source, whose bring-forward took it as it stands. Any
    /// other carry-on of it syncs with the line alone. The sibling of <see cref="ReopenTelling.BringsToSource"/>, which keys on the
    /// person's reopen.
    /// </summary>
    internal static bool BringsForwardToSource(QuestView step, Consideration start) =>
        start.BroughtForward is not null && step is { GrewFrom: not null, Parent: { Length: > 0 } };

    /// <summary>
    /// The starts still opening a snapshot carries that take a place of their own: each but one whose quest an active session already
    /// stands for, its record opened since, which the active sessions count. What the planner and a look's intakes count under the cap.
    /// </summary>
    internal static IReadOnlyList<StartReservation> Opening(Snapshot snapshot) =>
        [.. snapshot.Starting.Where(start => start.Quest is null
                                             || !snapshot.Active.Any(s => string.Equals(s.Quest, start.Quest, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>
    /// The session that ended waiting on its own helper in <paramref name="tree"/> while its quest is still taken, or null: it goes on
    /// there with the answer, or parks for the person, so it keeps the tree as a park does, though it is completed and in no live
    /// list. <paramref name="except"/> is the record going back in, which holds nothing against itself.
    /// </summary>
    internal static string? WaitingInTree(Snapshot snapshot, string tree, string? except) =>
        snapshot.LastRun.Values.FirstOrDefault(last =>
            HelperWaits.HoldsTree(last) && SameTree(last.Tree, tree)
            && !string.Equals(last.Session, except, StringComparison.OrdinalIgnoreCase)
            && snapshot.Quests.Any(quest => quest.Status == "Taken" && string.Equals(quest.Id, last.Quest, StringComparison.OrdinalIgnoreCase)))?.Session;

    /// <summary>Two tree paths are one tree — separators and case aside, as Windows sees them.</summary>
    internal static bool SameTree(string? a, string? b) => TreeNames.Same(a, b);
}
