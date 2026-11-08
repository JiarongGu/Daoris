namespace Daoris.Driver;

/// <summary>
/// The supervisor's decisions, live in this instance's memory:
/// one per instance, shared by the modules' handler and the driver's look, since both run in the window's process.
/// </summary>
/// <remarks>
/// <para>The decisions' memory is <see cref="SupervisorBook"/>; the doors here are one-line, each to the part that does it:
/// <see cref="SupervisorKeeping"/>, <see cref="SupervisorReleasing"/>, <see cref="SupervisorSweeps"/>, <see cref="SupervisorTickets"/>
/// and <see cref="SupervisorReads"/>; the files are <see cref="SupervisorDecisionFiles"/>.</para>
///
/// <para><b>Every check reads memory alone.</b> Each live decision is kept whole here: its question's snapshot, choice, reasons, holder,
/// generation, when it is kept and goes on, its state, the release's receipt, the id of the word its release wrote, and an admission's
/// ticket. The release, the hold, the admission, the hand, the attribution and the overrule read nothing else.</para>
///
/// <para><b>The files are written and never read back to decide</b>: <c>&lt;session&gt;.decisions/&lt;decision&gt;.json</c>, written once,
/// and its <c>.states</c> lines appended, for the session's history, the card after a restart and the start's sweep; and the
/// current-question pointer <c>&lt;session&gt;.supervisor.json</c>. They are in an agent's ordinary reach, so a file that differs from
/// what memory wrote moves its decision to <c>stale</c> (<see cref="SupervisorWhy.Tampered"/>), compared under the decision's lock so a
/// state line being appended is never read as a difference. A decision file an agent writes is never in memory, so never released nor
/// handed.</para>
///
/// <para><b>One lock per session</b>, async, which may be held across a call to the service; every move is made under it, and the lock
/// order is the decision's, then the judge's briefly (inside <see cref="IDecisionAdmission"/>, never across an await), then the ledger's
/// step inside the service.</para>
///
/// <para><b>A restart empties memory</b>: the start's sweep (<see cref="SweepAtStart"/>) reads the files only to close them, <c>held</c>
/// and <c>released</c> to <c>stale</c> and <c>admitted</c> to <c>interrupted</c>, and remembers each closed question as the owner's.</para>
/// </remarks>
public sealed class QuestionSupervisorDecisions
{
    /// <summary>Row 1's overrule window: five minutes, where the role keeps none.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(RoleHold.DefaultWindow);

    /// <summary>The window the role may keep (<see cref="RoleHold"/>): 1 to 60 minutes.</summary>
    public static readonly TimeSpan ShortestWindow = TimeSpan.FromMinutes(RoleHold.ShortestWindow), LongestWindow = TimeSpan.FromMinutes(RoleHold.LongestWindow);

    /// <summary>The window a decision is held for: the role's where within the bounds, else <see cref="Window"/>.</summary>
    public static TimeSpan WindowOf(DecisionAdmitted admitted) =>
        admitted.Window is { } set && set >= ShortestWindow && set <= LongestWindow ? set : Window;

    public const string DecisionsSuffix = SessionHomeFiles.SupervisorDecisionsSuffix;
    public const string PointerSuffix = SessionHomeFiles.SupervisorPointerSuffix;

    /// <summary>A reason's bounds, and how many: 1 to 5, each <c>says</c> 1-300 and <c>source</c> 1-200.</summary>
    public const int MostReasons = 5, MaxSays = 300, MaxSource = 200;

    private readonly SupervisorBook _book;
    private readonly SupervisorKeeping _keeping;
    private readonly SupervisorReleasing _releasing;
    private readonly SupervisorSweeps _sweeps;
    private readonly SupervisorTickets _tickets;
    private readonly SupervisorReads _reads;
    private readonly SupervisorCourses _courses;
    private readonly IDecisionAdmission _admission;

    public QuestionSupervisorDecisions(string home, IDecisionAdmission admission, MachineLog? log = null)
    {
        _book = new SupervisorBook(home, log);
        _keeping = new SupervisorKeeping(_book, admission, log);
        _releasing = new SupervisorReleasing(_book, admission, log);
        _sweeps = new SupervisorSweeps(_book, log);
        _tickets = new SupervisorTickets(_book, admission);
        _reads = new SupervisorReads(_book);
        _courses = new SupervisorCourses(_book, admission, log);
        _admission = admission;
    }

    /// <summary>Whether a holder is admitted for <paramref name="repository"/> now: the agent supervisor is on there.</summary>
    public bool Admits(string? repository) =>
        repository is not null && _admission.Admit(SupervisorKinds.Question, new RolePlace(Repository: repository), SupervisorKinds.Answer) is { Why: null, Generation: not null };

    /// <summary>Keep a course act on a referred point (<see cref="SupervisorCourses.KeepAsync"/>).</summary>
    public Task<SupervisorKept> CourseAsync(SupervisorCourse course, ReferredPoint point, DateTimeOffset now, CancellationToken ct = default) =>
        _courses.KeepAsync(course, point, now, ct);

    /// <summary>A session's course decisions as the page and the print read them (<see cref="SupervisorReads.CoursesOf"/>).</summary>
    public IReadOnlyList<SupervisorCourseView> CoursesOf(string session) => _reads.CoursesOf(session);

    /// <summary>Release each held go-on whose moment came (<see cref="SupervisorCourses.ReleaseAsync"/>).</summary>
    public Task<IReadOnlyList<SupervisorState>> ReleaseCoursesAsync(
        CourseLine line, DateTimeOffset now, IReadOnlyCollection<string>? counts = null, CancellationToken ct = default) =>
        _courses.ReleaseAsync(line, now, counts, ct);

    /// <summary>Whether a referred point waits for the holder (<see cref="SupervisorCourses.Waits"/>).</summary>
    public bool Refers(string session, string point, string kind = SupervisorKinds.Course) => _courses.Waits(session, point, kind);

    /// <summary>Run <paramref name="body"/> under <paramref name="session"/>'s decision lock.</summary>
    public Task<T> UnderAsync<T>(string session, Func<Task<T>> body, CancellationToken ct = default) => _book.UnderAsync(session, body, ct);

    /// <summary>Keep a decision for an answer (<see cref="SupervisorKeeping.KeepAsync"/>).</summary>
    public Task<SupervisorKept> KeepAsync(SupervisorAnswer answer, SupervisedPark park, DateTimeOffset now, CancellationToken ct = default) =>
        _keeping.KeepAsync(answer, park, now, ct);

    /// <summary>Keep a defer, leaving the question to the owner (<see cref="SupervisorKeeping.LeaveAsync"/>).</summary>
    public Task<SupervisorKept> LeaveAsync(SupervisorDefer defer, SupervisedPark park, DateTimeOffset now, CancellationToken ct = default) =>
        _keeping.LeaveAsync(defer, park, now, ct);

    /// <summary>Release each held decision whose time has come (<see cref="SupervisorReleasing.ReleaseAsync"/>).</summary>
    public Task<IReadOnlyList<SupervisorState>> ReleaseAsync(
        string session, SupervisorSay say, DateTimeOffset now, CancellationToken ct = default, SupervisorBound? bound = null) =>
        _releasing.ReleaseAsync(session, say, now, ct, bound);

    /// <summary>The look's reading of a record before it plans (<see cref="SupervisorReleasing.SettleAsync"/>).</summary>
    public Task SettleAsync(
        string session, bool parked, bool ownerWaits, IReadOnlyCollection<string> waiting, SupervisorTakeBack takeBack, DateTimeOffset now,
        CancellationToken ct = default) =>
        _releasing.SettleAsync(session, parked, ownerWaits, waiting, takeBack, now, ct);

    /// <summary>
    /// <c>CannotGoOn</c> for a park whose own conversation cannot resume: its released decision goes <c>stale</c>, its word taken
    /// back, and the park is a wait on the owner again.
    /// </summary>
    public Task CannotGoOnAsync(string session, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        StaleAsync(session, SupervisorWhy.CannotGoOn, takeBack, now, ct);

    /// <summary>A session's held and released decisions gone stale (<see cref="SupervisorSweeps.StaleAsync"/>).</summary>
    public Task StaleAsync(string session, string why, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        _sweeps.StaleAsync(session, why, takeBack, now, ct);

    /// <summary>A revoke's or a replacing appointment's sweep (<see cref="SupervisorSweeps.RevokedAsync"/>).</summary>
    /// <returns>How many went stale.</returns>
    public Task<int> RevokedAsync(
        string generation, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default, string? repository = null) =>
        _sweeps.RevokedAsync(generation, takeBack, now, ct, repository);

    /// <summary>The files compared with what memory wrote (<see cref="SupervisorSweeps.CompareAsync"/>).</summary>
    /// <returns>How many went stale.</returns>
    public Task<int> CompareAsync(string session, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        _sweeps.CompareAsync(session, takeBack, now, ct);

    /// <summary>The start's sweep of the files the last run left (<see cref="SupervisorSweeps.SweepAtStart"/>).</summary>
    public IReadOnlyList<string> SweepAtStart(DateTimeOffset now) => _sweeps.SweepAtStart(now);

    /// <summary>Whether <paramref name="subject"/> is closed to the person, so no list hands it to a supervisor of any holder again.</summary>
    public bool Closed(SupervisorSubject subject) => _book.Closed.ContainsKey(subject);

    public SupervisorAttempts Attempts => _book.Attempts;

    public SupervisorRuns Runs { get; } = new();

    /// <summary>The owner's <b>Overrule</b> of a course decision (<see cref="SupervisorCourses.OverruleAsync"/>) or an answer (<see cref="SupervisorReleasing.OverruleAsync"/>).</summary>
    public async Task<SupervisorState?> OverruleAsync(
        string session, string decision, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        await _courses.OverruleAsync(session, decision, now, ct).ConfigureAwait(false)
        ?? await _releasing.OverruleAsync(session, decision, takeBack, now, ct).ConfigureAwait(false);

    /// <summary>The owner's words named a question (<see cref="SupervisorReleasing.SupersedeAsync"/>).</summary>
    /// <returns>How many moved.</returns>
    public Task<int> SupersedeAsync(string session, string question, DateTimeOffset now, CancellationToken ct = default) =>
        _releasing.SupersedeAsync(session, question, now, ct);

    /// <summary>The go-on's move to working (<see cref="SupervisorTickets.AdmitAsync"/>).</summary>
    public Task<(bool Moved, SupervisorTicket? Ticket)> AdmitAsync(
        string session, Func<IReadOnlyList<string>, Task<bool>> move, string goOn, DateTimeOffset now, CancellationToken ct = default) =>
        _tickets.AdmitAsync(session, move, goOn, now, ct);

    /// <summary>The paragraph the ticket's prompt hands, consuming the ticket (<see cref="SupervisorTickets.Hand"/>).</summary>
    public string? Hand(SupervisorTicket ticket) => _tickets.Hand(ticket);

    /// <summary>The run built with the ticket's word is spawned: <c>went-on</c>, at that moment.</summary>
    public Task<bool> WentOnAsync(SupervisorTicket ticket, DateTimeOffset at, CancellationToken ct = default) =>
        _tickets.AfterAdmissionAsync(ticket, SupervisorState.WentOn, null, at, ct);

    /// <summary>A failure between the admission and the wire: <c>not-handed</c>, its caller restoring the park less the word.</summary>
    public Task<bool> NotHandedAsync(SupervisorTicket ticket, string why, DateTimeOffset at, CancellationToken ct = default) =>
        _tickets.AfterAdmissionAsync(ticket, SupervisorState.NotHanded, why, at, ct);

    /// <summary>Whether a question has a decision, live or closed as the owner's (<see cref="SupervisorReads.Decided"/>).</summary>
    public bool Decided(string session, string question) => _reads.Decided(session, question);

    /// <summary>Whether a word is the supervisor's (<see cref="SupervisorReads.Authored"/>).</summary>
    public bool Authored(string session, string word) => _reads.Authored(session, word);

    /// <summary>Whether a word may be planned or handed (<see cref="SupervisorReads.Handable"/>).</summary>
    public bool Handable(string session, string word, SupervisorTicket? ticket = null) => _reads.Handable(session, word, ticket);

    /// <summary>A word claiming the supervisor that Daoris did not attribute: quarantined, never handed, taken back by the next look.</summary>
    public bool Unattributed(string session, string word) => !Authored(session, word);

    /// <summary>The moment a held decision of a session goes on (<see cref="SupervisorReads.HeldUntil"/>).</summary>
    public DateTimeOffset? HeldUntil(string session) => _reads.HeldUntil(session);

    /// <summary>The soonest moment a held decision or go-on goes on, across every session (<see cref="SupervisorReads.NextGoesOn"/>).</summary>
    public DateTimeOffset? NextGoesOn() => new[] { _reads.NextGoesOn(), _courses.NextGoesOn() }.Min();

    /// <summary>The sessions a held decision holds now (<see cref="SupervisorReads.HeldSessions"/>).</summary>
    public IReadOnlySet<string> HeldSessions() => _reads.HeldSessions();

    /// <summary>The sessions a held or released decision answers for now (<see cref="SupervisorReads.Answering"/>).</summary>
    public IReadOnlySet<string> Answering() => _reads.Answering();

    /// <summary>Whether a session has a decision that is not final (<see cref="SupervisorReads.Moving"/>).</summary>
    public bool Moving(string session) => _reads.Moving(session);

    /// <summary>The decisions live in memory for a session (<see cref="SupervisorReads.LiveOf"/>).</summary>
    public IReadOnlyList<SupervisorDecisionView> LiveOf(string session) => _reads.LiveOf(session);

    /// <summary>A session's decisions for its history (<see cref="SupervisorReads.History"/>).</summary>
    public IReadOnlyList<SupervisorDecisionView> History(string session) => _reads.History(session);

    /// <summary>Exposed for the tests of the move table: try any move on a live decision; true where it moved.</summary>
    internal Task<bool> TryMoveAsync(string session, string decision, SupervisorState to, DateTimeOffset at) =>
        _book.TryMoveAsync(session, decision, to, at);
}
