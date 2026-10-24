using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The presses the standing confirmation may name (D201 point 2, the terminal-trust design §2.1), by the names its record keeps.</summary>
public static class StandingPresses
{
    /// <summary>Stop a session: <c>STOP_SESSION { id, said: true }</c>.</summary>
    public const string Stop = "stop";

    /// <summary>Finish a session that waits on the person, with none of their words: <c>RESOLVE_SESSION { id, state: "completed", said: true }</c>.</summary>
    public const string Finish = "finish";

    /// <summary>Hold a repository: <c>SET_HOLD { repository, held: true }</c>.</summary>
    public const string Hold = "hold";

    /// <summary>Resume a repository a covered hold held during this run: <c>SET_HOLD { repository, held: false }</c>.</summary>
    public const string Resume = "resume";

    /// <summary>
    /// The presses this build's table names, in the order the window offers them. A grant names its own (§2.3): a press a later build
    /// adds (TERMTRUST1b's <c>done</c>) covers nothing until the person grants it.
    /// </summary>
    public static IReadOnlyList<string> Built { get; } = [Stop, Finish, Hold, Resume];
}

/// <summary>Why a press the standing confirmation could have covered asks (the terminal-trust design §5 step 3, §7.2), by code.</summary>
public static class StandingWhy
{
    /// <summary>The grant is off, or its record is not honoured.</summary>
    public const string Off = "off";

    /// <summary>No press of the build's table, or one the record does not name.</summary>
    public const string NotNamed = "not-named";

    /// <summary>The press is not its verb's form: another field, another value, a name twice.</summary>
    public const string Form = "form";

    /// <summary>A resume of a hold this run's memory does not hold.</summary>
    public const string NotHeldHere = "not-held-here";

    /// <summary>A finish of a session with a process on this machine.</summary>
    public const string RunningHere = "running-here";

    /// <summary>The terminal's process is in a job a Daoris on this Windows session made for its agents.</summary>
    public const string InAgentJob = "in-agent-job";

    /// <summary>The judge cannot tell where the terminal's process stands, or an agent of some Daoris here runs uncontained.</summary>
    public const string Untold = "untold";
}

/// <summary>How the standing confirmation's record was read at the instance's start (the machine log's <c>person.standing</c> <c>state</c>).</summary>
public static class StandingRead
{
    /// <summary>A record of this machine and account, read.</summary>
    public const string Found = "found";

    /// <summary>A record that does not read: off.</summary>
    public const string Unread = "unread";

    /// <summary>A record of a newer <c>schemaVersion</c>: off, and never written over by this build.</summary>
    public const string Newer = "newer";

    /// <summary>A record of another machine or account (a <c>data/</c> folder copied here): off, and left.</summary>
    public const string OtherMachine = "other-machine";
}

/// <summary>One judgement: covered, or asks with why (<see cref="StandingWhy"/>); <see cref="Press"/> the press of the table it was, or null.</summary>
/// <param name="Said">Whether the <c>asked</c> line says <see cref="Why"/>: only where the grant is on and names such a press (§7.2).</param>
public sealed record StandingJudgement(bool Covered, string? Why, string? Press, bool Said)
{
    internal static StandingJudgement Asks(string why, string? press, bool said) => new(false, why, press, said);
}

/// <summary>What the window's row reads (<c>STATE</c>'s <c>standingConfirmation</c>, the terminal-trust design §6.1).</summary>
/// <param name="Offered">Whether this build offers the grant (D201's note on order: once part (i) has landed and been looked at on the install).</param>
/// <param name="On">Whether it is on now, honoured, and names a press.</param>
/// <param name="Presses">The presses granted, in the build's order.</param>
/// <param name="Built">The presses this build's table names.</param>
/// <param name="Since">When the grant was made, or null.</param>
/// <param name="Unread">Why the record found is not honoured (<see cref="StandingRead"/>'s <c>unread</c>, <c>newer</c>, <c>other-machine</c>), or null.</param>
/// <param name="Unwritten">That the last revoke could not write its record, so the next start may read it as on.</param>
/// <param name="Used">How many presses it covered since this Daoris started.</param>
/// <param name="Held">The repositories a covered hold held during this run, which a covered resume may release.</param>
/// <param name="AppointmentsOffered">Whether this build offers any role (STEWARDROLE1a-i, D209 §2.4: derived from the two role flags, gating nothing alone).</param>
/// <param name="Appointments">The appointments the record holds, each as its row reads it, dormant where found at a start.</param>
/// <param name="SupervisorOffered">Whether this build offers the supervisor (its own flag, D209 §2.4).</param>
/// <param name="StewardOffered">Whether this build offers the steward (its own flag, D209 §2.4).</param>
/// <param name="Holders">The holders living this run, each with its roles and when its tab was opened, which a later <i>Appoint…</i> may add a role to.</param>
public sealed record StandingNow(
    bool Offered, bool On, IReadOnlyList<string> Presses, IReadOnlyList<string> Built, DateTimeOffset? Since, string? Unread,
    bool Unwritten, int Used, IReadOnlyList<string> Held, bool AppointmentsOffered = false, IReadOnlyList<AppointmentNow>? Appointments = null,
    bool SupervisorOffered = false, bool StewardOffered = false, IReadOnlyList<HolderNow>? Holders = null)
{
    /// <summary>The acts this build offers a steward's appointment, in order (STEWARDROLE1b, <see cref="StewardActs.Offered"/>), which its ask names.</summary>
    public IReadOnlyList<string> StewardActsOffered { get; init; } = [];

    /// <summary>The steward's acts this run, newest first (STEWARDROLE1b, D209 §5.3), from memory alone: the line's entries.</summary>
    public IReadOnlyList<StewardActNow> StewardActs { get; init; } = [];
}

/// <summary>
/// One of the steward's acts this run as the window's line reads it (STEWARDROLE1b, D209 §5.1, §5.3): its id, the act, what it acted on,
/// when; <paramref name="Standing"/> while the owner has not acted on its target since, so its reverse press is offered; and, for a stop,
/// <paramref name="Waiting"/> while its quest waits on a Try again, which <i>What needs you</i> shows as <i>Stopped by the steward</i>.
/// </summary>
public sealed record StewardActNow(
    string Id, string Act, string? Repository, string? Quest, string? Session, DateTimeOffset At, bool Standing, bool Waiting);

/// <summary>A grant's or a revoke's answer: done, or refused as data with a code the page words.</summary>
public sealed record StandingChange(bool Done, string? Why, string? Message);

/// <summary>The roles the owner appoints (D208 point 1, the supervisor design §2; D209 §1), by the names the record keeps.</summary>
public static class Roles
{
    /// <summary>The supervisor (主管), who answers a parked session's recorded question with one offered choice.</summary>
    public const string Supervisor = "supervisor";

    /// <summary>The steward (管家), who makes development's management acts on the owner's behalf (STEWARDROLE1, D209).</summary>
    public const string Steward = "steward";

    /// <summary>The roles this build knows. A role is never one of <see cref="StandingPresses.Built"/>'s presses.</summary>
    public static IReadOnlyList<string> Known { get; } = [Steward, Supervisor];
}

/// <summary>
/// An appointment's scope (D209 §2.3): <c>machine</c>, every repository the service's registry names here as each act is judged (the
/// steward's), or <c>repository:&lt;name&gt;</c> (the supervisor's).
/// </summary>
public static class RoleScopes
{
    public const string Machine = "machine";

    private const string RepositoryPrefix = "repository:";

    /// <summary>The scope of one repository.</summary>
    public static string Repository(string name) => RepositoryPrefix + name;

    /// <summary>The repository a scope names, or null for <c>machine</c> or any other.</summary>
    public static string? RepositoryOf(string scope) =>
        scope.StartsWith(RepositoryPrefix, StringComparison.Ordinal) && scope.Length > RepositoryPrefix.Length ? scope[RepositoryPrefix.Length..] : null;

    /// <summary>Whether <paramref name="scope"/> is one a record or a form may name.</summary>
    public static bool Known(string scope) => scope == Machine || RepositoryOf(scope) is not null;

    /// <summary>Two scopes the same, a repository's name compared as the registry compares names.</summary>
    public static bool Same(string a, string b) =>
        RepositoryOf(a) is { } left && RepositoryOf(b) is { } right ? string.Equals(left, right, StringComparison.OrdinalIgnoreCase) : a == b;
}

/// <summary>Who holds a role (the supervisor design §2.2): a terminal the window started with the holder key, or Daoris's own actor.</summary>
public static class RoleHolders
{
    public const string Terminal = "terminal";
    public const string Actor = "actor";
}

/// <summary>
/// One appointment as the record keeps it (the supervisor design §2.3; D209 §2.3): its role, its scope, the holder's kind, its
/// generation (an opaque id drawn at each appointment, never reused), when, the acts its ask named from the build's offered set, and
/// the owner's own set-up tick. Never the key nor its hash, nor which holder holds it, which is this run's memory alone.
/// </summary>
public sealed record Appointment(
    string Role, string Scope, string Holder, string Generation, DateTimeOffset Since, IReadOnlyList<string> Acts, bool Setup = false)
{
    /// <summary>The repository its scope names, or null for <c>machine</c>.</summary>
    public string? Repository => RoleScopes.RepositoryOf(Scope);
}

/// <summary>
/// An appointment as the window's row reads it (the supervisor design §6.4): never its generation, its key nor its hash.
/// </summary>
/// <param name="Repository">The repository its scope names; null for the steward's <c>machine</c>.</param>
/// <param name="Dormant">Found as Daoris started (§2.3): no key is known for it, so it admits no holder until <i>Appoint again</i>.</param>
/// <param name="Answers">How many acts its holder made under it since this Daoris started.</param>
/// <param name="Scope">Its scope (D209 §2.3).</param>
/// <param name="HolderId">The living holder that holds it this run; null where it is dormant.</param>
public sealed record AppointmentNow(
    string Role, string? Repository, string Holder, DateTimeOffset Since, bool Dormant, int Answers, string Scope = RoleScopes.Machine,
    string? HolderId = null)
{
    /// <summary>The acts its ask named (STEWARDROLE1b, D209 §2.3).</summary>
    public IReadOnlyList<string> Acts { get; init; } = [];

    /// <summary>The owner's own set-up tick (D209 §2.3).</summary>
    public bool Setup { get; init; }
}

/// <summary>A living holder as the window reads it (D209 §2.2): its id, when its tab was opened, its roles. Never its key, its hash nor its process.</summary>
public sealed record HolderNow(string Id, DateTimeOffset Since, IReadOnlyList<string> Roles);

/// <summary>An appointment's change: its answer, and the generations it ended, which the caller sweeps after its step (§5).</summary>
public sealed record AppointmentChange(StandingChange Answer, IReadOnlyList<string> Ended);

/// <summary>
/// The window's appointment (<c>SET_APPOINTMENT { role, scope, holder, on, share?, setup? }</c>, D209 §2.3): <paramref name="Share"/> is a
/// later <i>Appoint…</i>'s choice while a holder lives, <c>own</c> for a terminal of its own or a living holder's id to add the role
/// to it; <paramref name="Setup"/> the owner's own set-up tick, the steward's alone.
/// </summary>
public sealed record AppointRequest(string Role, string Scope, string Holder, bool On, string? Share = null, bool Setup = false);

/// <summary>
/// The judge's role method's answer (the supervisor design §2.2, §3.1; D209 §3.3): covered, with the appointment it acted under, its
/// holder and, for <c>ROLE_ACT</c>, the act and the act's id Daoris drew at admission; or refused with why, never asked.
/// </summary>
/// <param name="Roles">The roles the holder holds now, which every answer of a role's verb names (D209 §2.2).</param>
public sealed record RoleJudgement(
    bool Covered, string? Why, string? Role = null, string? Repository = null, string? Generation = null, string? Holder = null,
    string? Act = null, string? ActId = null, IReadOnlyList<string>? Roles = null)
{
    internal static RoleJudgement Refused(string why) => new(false, why);
}

/// <summary>
/// The person's standing confirmation (TERMTRUST1a part (ii), D201; the terminal-trust design): a grant made in the window, with the
/// person's own press, that lets the presses it names, in exactly their verbs' forms and conditions, from a terminal's process in no job
/// any Daoris on this Windows session made for an agent, run without a card. Everything else asks as today.
/// </summary>
/// <remarks>
/// <para><b>The record</b> (§4.2): <c>data/standing-confirmation.json</c> (<see cref="HomeEntry.StandingConfirmation"/>), <c>presses</c>,
/// <c>since</c>, and <c>machine</c>, a SHA-256 of this machine's name and the account's SID. Read at the instance's start
/// (<see cref="Load"/>, before the pipe serves) and kept in memory, written through on each grant and revoke, atomically, and by
/// nothing else; a write by another process while Daoris runs is not read until the next start. Absent, unreadable, newer or another
/// machine's: off, said in the log and under the row, and never written over by a build that cannot read it.</para>
///
/// <para><b>The hold's memory</b> (§4.2): a covered <c>hold</c> that changed a repository from not held to held is remembered for this
/// run, with its moment; any other change of that repository's hold forgets it (<see cref="Forget"/>), as does a hold the file no longer
/// says, and a start remembers nothing. A covered <c>resume</c> is covered only for a repository remembered so, never a hold the
/// person made, one made alone, or one from before the start.</para>
///
/// <para><b>One lock</b> (§3.3 point 5, §4.3): a press is judged and admitted under it, its use counted and a hold remembered there, so a
/// revoke after it does not recall it and one before it is heard; a revoke clears the memory under it before its record is written.</para>
///
/// <para><b>Until part (i) lands</b> every agent of this process is uncontained, so the caller's look (<see cref="StandingCaller"/>)
/// answers <see cref="StandingWhy.Untold"/> and this covers nothing (D201's note on order). The grant is offered only where
/// <see cref="Offered"/> says so.</para>
///
/// <para><b>The owner's appointments</b> (SUPERVISOR1a-ii, D208 points 2 and 3; the supervisor design §2.2, §2.3): the record carries
/// <c>appointments</c> beside <c>presses</c>, each its role, repository, holder kind, generation and moment, never the key nor its hash.
/// The holder key's SHA-256 is kept in this instance's memory alone, so an appointment read at a start is <b>dormant</b> until the
/// owner presses <i>Appoint again</i>. A role's act is judged by its own method (<see cref="JudgeRole"/>) under the same lock and
/// caller look, then by the holder's proof over the connection's nonce, independent of the presses; and the decisions' admission
/// (<see cref="IDecisionAdmission"/>) answers the effective appointment's generation from the same memory.</para>
///
/// <para><b>One mutation step</b> (§2.3): every read-modify-write of the record, presses and appointments alike, and the memory it
/// installs, runs whole under the lock: read the memory, build the whole record, write it atomically, install the memory. A grant, an
/// appointment, a dismissal and a revoke never interleave; a revoke clears its part of the memory first, inside the step. The lock is
/// never held across an await nor while a decision's lock is taken: a revoke's sweep of its decisions runs after its step
/// (<see cref="AppointmentChange.Ended"/>).</para>
/// </remarks>
public sealed class StandingConfirmation : IDecisionAdmission
{
    /// <summary>The machine log's event for a grant, a revoke, and what a start found (§7.2).</summary>
    public const string LogEvent = "person.standing";

    /// <summary>The record's version this build reads and writes.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The event that tells every window a press ran under it (§5 step 4), which the line in <i>Notifications</i> counts.</summary>
    public const string UsedEvent = "STANDING_USED";

    /// <summary>
    /// Whether this build offers the grant on This Daoris (D201's note on order, 2026-10-23): not until part (i), the containment, has
    /// landed and its case 4 has been looked at on the install. A build that offers it sets this true; until then the row says it is
    /// not offered yet and the grant's route refuses <c>not-offered</c>, while a revoke is always taken.
    /// </summary>
    public const bool OfferedInThisBuild = false;

    /// <summary>The refusal of a grant this build does not offer yet.</summary>
    public const string NotOffered = "not-offered";

    /// <summary>The refusal of a grant naming a press this build's table does not name.</summary>
    public const string UnknownPress = "unknown-press";

    /// <summary>A grant whose record could not be written, so nothing was turned on; or a revoke's, turned off for this run alone.</summary>
    public const string Unwritten = "unwritten";

    /// <summary>
    /// Whether this build offers the supervisor (STEWARDROLE1a-i, D209 §2.4, amending D208 §2.3): its own flag, decoupled from D201's
    /// offering. Only SUPERVISOR1a-iii turns it on, after SUPERVISOR1b and D201 part (i)'s case 4 seen on every door. It gates the
    /// supervisor's ask and row, its <c>SET_APPOINTMENT</c>, <c>SUPERVISOR_ANSWER</c> and <c>SUPERVISOR_DEFER</c>.
    /// </summary>
    public const bool SupervisorOfferedInThisBuild = false;

    /// <summary>
    /// Whether this build offers the steward (STEWARDROLE1a-i, D209 §2.4): its own flag. Only STEWARDROLE1d turns it on. It gates the
    /// steward's <i>Roles</i> row, its ask, its <c>SET_APPOINTMENT</c> and <c>ROLE_ACT</c> for <c>steward</c>.
    /// </summary>
    public const bool StewardOfferedInThisBuild = false;

    /// <summary>Whether this build offers any role: derived from the two flags, and gating nothing by itself (D209 §2.4).</summary>
    public const bool AppointmentsOffered = SupervisorOfferedInThisBuild || StewardOfferedInThisBuild;

    /// <summary>An appointment naming a role this build does not know.</summary>
    public const string UnknownRole = "unknown-role";

    /// <summary>An appointment naming a repository Daoris does not drive here.</summary>
    public const string UnknownRepository = "unknown-repository";

    /// <summary>A terminal appointment whose holder's terminal could not be started: nothing was kept.</summary>
    public const string Unstarted = "unstarted";

    /// <summary>An appointment naming a scope its role does not take (the steward's is <c>machine</c>, the supervisor's a repository).</summary>
    public const string Scope = "scope";

    /// <summary>
    /// A later <i>Appoint…</i> while a holder's tab lives that names no choice (D209 §2.2): the owner adds the role to that terminal or
    /// gives it one of its own; neither is preselected.
    /// </summary>
    public const string ChooseTerminal = "choose-terminal";

    /// <summary>A share naming no living holder.</summary>
    public const string NoHolder = "no-holder";

    /// <summary>A role revoked this run added back to a holder that held it (D209 §2.2): only a terminal of its own.</summary>
    public const string RevokedHolder = "revoked-holder";

    /// <summary>The machine log's event for an appointment, a revoke, a holder's end and what a start found (D209 §5.2).</summary>
    public const string RoleLogEvent = "role.appointment";

    private readonly string _home;
    private readonly MachineLog _log;
    private readonly TimeProvider _clock;
    private readonly string _machine;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _held = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _presses = [];
    private DateTimeOffset? _since;
    private string? _unread;
    private bool _unwritten;
    private bool _newer;
    private int _used;
    private long _seq;
    // SUPERVISOR1a-ii, amended by STEWARDROLE1a-i (D209 §2.2): the record's appointments as this run holds them; the holders living this
    // run by their id, each with its key's hash, its tab, its harness's process and the roles revoked from it; and which holder holds each
    // live appointment, by its generation, in memory alone. An appointment no holder holds is dormant.
    private IReadOnlyList<Appointment> _appointments = [];
    private readonly Dictionary<string, Holding> _holders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _holderOf = new(StringComparer.Ordinal);
    private readonly HashSet<string> _endedTabs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _answers = new(StringComparer.Ordinal);
    private readonly RoleActsMemory _acts = new();
    // STEWARDROLE1a-ii (D209 §5.4): what the owner acted on this run, by its scope's key, through whichever door; and whether a target
    // of theirs could not be resolved, which holds every quest for the run. Memory alone: a restart forgets it.
    private readonly HashSet<string> _ownerSet = new(StringComparer.Ordinal);
    private bool _ownerUnresolved;

    /// <summary>A holder of this run (D209 §2.2): apart from its appointments, shared by them or its own.</summary>
    private sealed class Holding(string id, byte[] hash, string? tab, HolderProcess? process, DateTimeOffset since)
    {
        public string Id { get; } = id;
        public byte[] Hash { get; } = hash;
        public string? Tab { get; } = tab;
        public HolderProcess? Process { get; } = process;
        public DateTimeOffset Since { get; } = since;

        /// <summary>The roles revoked from this holder this run: never added back to it (§2.2).</summary>
        public HashSet<string> Revoked { get; } = new(StringComparer.Ordinal);
    }

    /// <param name="home">The home whose record this is.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="caller">Where a terminal's process stands (<see cref="StandingCaller"/> on Windows); a test hands its own.</param>
    /// <param name="clock">The clock a grant's moment is read by.</param>
    /// <param name="offered">Whether the grant is offered (<see cref="OfferedInThisBuild"/> where none is named).</param>
    /// <param name="machine">This machine and account, as the record binds them (<see cref="ThisMachine"/> where none is named).</param>
    /// <param name="supervisor">Whether the supervisor is offered (<see cref="SupervisorOfferedInThisBuild"/> where none is named), whatever D201's grant is.</param>
    /// <param name="steward">Whether the steward is offered (<see cref="StewardOfferedInThisBuild"/> where none is named), whatever D201's grant is.</param>
    /// <param name="walk">The configuration walk every <c>Role</c> route's admission runs (<see cref="HolderWalk.System"/> where none is named).</param>
    public StandingConfirmation(
        string home, MachineLog? log = null, IStandingCaller? caller = null, TimeProvider? clock = null, bool? offered = null,
        string? machine = null, bool? supervisor = null, bool? steward = null, IHolderWalk? walk = null)
    {
        _home = home;
        _log = log ?? MachineLog.None;
        Caller = caller ?? new StandingCaller();
        _clock = clock ?? TimeProvider.System;
        Offered = offered ?? OfferedInThisBuild;
        _machine = machine ?? ThisMachine();
        // D209 §2.4: each role offered by its own flag, decoupled from D201's offering, which D209 keeps off for good.
        SupervisorOffered = supervisor ?? SupervisorOfferedInThisBuild;
        StewardOffered = steward ?? StewardOfferedInThisBuild;
        Walk = walk ?? HolderWalk.System;
        RunStarted = _clock.GetUtcNow();
    }

    /// <summary>Whether this instance offers the grant (D201's note on order).</summary>
    public bool Offered { get; }

    /// <summary>Whether this instance offers the supervisor (D209 §2.4).</summary>
    public bool SupervisorOffered { get; }

    /// <summary>Whether this instance offers the steward (D209 §2.4).</summary>
    public bool StewardOffered { get; }

    /// <summary>Whether this instance offers any role: derived, and gating nothing by itself.</summary>
    public bool AppointsOffered => SupervisorOffered || StewardOffered;

    /// <summary>Whether <paramref name="role"/> is offered by its own flag.</summary>
    public bool RoleOffered(string role) => role switch
    {
        Roles.Supervisor => SupervisorOffered,
        Roles.Steward => StewardOffered,
        _ => false,
    };

    /// <summary>The configuration walk (D209 §6.1), run for every route of the census's <c>Role</c> class.</summary>
    public IHolderWalk Walk { get; }

    /// <summary>The acts this build offers a role's new appointment (<see cref="StewardActs.Offered"/> for the steward), unless a test names its own.</summary>
    public Func<string, IReadOnlyList<string>> OffersActs { get; set; } = role => role == Roles.Steward ? StewardActs.Offered : [];

    /// <summary>Each role's table for <c>ROLE_ACT</c> (<see cref="StewardActs.TableOf"/>), unless a test hands a stand-in act.</summary>
    public Func<string, IReadOnlyDictionary<string, IRoleAct>> TableOf { get; set; } = StewardActs.TableOf;

    /// <summary>Tells a holder's tab a line, and its title where one is named (the door hands the panel's): a role added, its last role off.</summary>
    public Action<string, string, string?>? Tell { get; set; }

    /// <summary>Closes a holder's tab Daoris ended (the walk found its configuration changed); the door hands the panel's.</summary>
    public Action<string>? Close { get; set; }

    /// <summary>
    /// Told the generations a holder's end or the walk ended outside an appointment's own press, after the step that ended them, for the
    /// door's sweep (§5) as an appointment's change answers them.
    /// </summary>
    public Action<IReadOnlyList<string>>? Ended { get; set; }

    /// <summary>Whether a repository is one Daoris drives on this machine (the loop's registry, which the loop hands in); none is none.</summary>
    public Func<string, bool> Drives { get; set; } = _ => false;

    /// <summary>Where a terminal's process stands.</summary>
    public IStandingCaller Caller { get; }

    /// <summary>Whether a process for a session runs on this machine (the loop's registry, which the loop hands in); none runs nothing.</summary>
    public Func<string, bool> RunsHere { get; set; } = _ => false;

    /// <summary>Whether <c>driver.json</c> holds a repository now (the loop's file, which the loop hands in); none holds nothing.</summary>
    public Func<string, bool> HeldNow { get; set; } = _ => false;

    /// <summary>Told a press it covered, with the window's count (<see cref="UsedEvent"/>); none tells nobody.</summary>
    public Action<long, DateTimeOffset>? Used { get; set; }

    /// <summary>
    /// The run's gates (STEWARDROLE1a-ii, D209 §5.4), one per workspace, repository and quest: a role's act holds its target's and its
    /// dependencies' alone; an owner's press holds what it resolves to beside the owner's other presses. Never the judge's lock.
    /// </summary>
    public RoleGates Gates { get; } = new();

    /// <summary>When this run began, after which a person's door's write on a quest's records marks it (§5.4, <see cref="OwnerRecords"/>).</summary>
    public DateTimeOffset RunStarted { get; }

    /// <summary>The record's path.</summary>
    public string RecordPath => DaorisHome.Entry(_home, HomeEntry.StandingConfirmation);

    /// <summary>
    /// Read the record (§4.2): once, as the instance's loop takes the home and before its pipe serves. What it said is logged by code,
    /// <c>person.standing</c> with <see cref="StandingRead"/>'s <c>state</c> and the presses' names, never a value of the record.
    /// </summary>
    public void Load()
    {
        string state;
        IReadOnlyList<string> presses = [];
        IReadOnlyList<Appointment> appointments = [];
        DateTimeOffset? since = null;
        var newer = false;
        string? text;
        try
        {
            text = File.Exists(RecordPath) ? AtomicFile.ReadText(RecordPath) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            text = "";
        }

        if (text is null)
        {
            // Absent: off, and nothing to say but that.
            state = StandingRead.Found;
        }
        else
        {
            (state, presses, since, appointments) = Read(text, _machine);
            newer = state == StandingRead.Newer;
        }

        lock (_gate)
        {
            _presses = state == StandingRead.Found ? presses : [];
            _since = state == StandingRead.Found && presses.Count > 0 ? since : null;
            _unread = state == StandingRead.Found ? null : state;
            _newer = newer;
            _held.Clear();
            // SUPERVISOR1a-ii (§2.3): every appointment read at a start is dormant, since no key is known for it in this run.
            _appointments = state == StandingRead.Found ? appointments : [];
            _holders.Clear();
            _holderOf.Clear();
            _answers.Clear();
        }

        _log.Info(LogEvent, ("state", state), ("presses", Names(state == StandingRead.Found ? presses : [])),
            ("appointments", (state == StandingRead.Found ? appointments.Count : 0).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>The record's text read as the instance reads it: its state, its presses in the build's order (unknown names kept out), and its moment.</summary>
    internal static (string State, IReadOnlyList<string> Presses, DateTimeOffset? Since) Parse(string text, string machine)
    {
        var (state, presses, since, _) = Read(text, machine);
        return (state, presses, since);
    }

    /// <summary>
    /// The record whole, its appointments too (SUPERVISOR1a-ii): each well formed one kept as written, its role, repository, holder,
    /// generation and moment, whatever else it carries (a key or a hash an agent wrote there is never read).
    /// </summary>
    public static (string State, IReadOnlyList<string> Presses, DateTimeOffset? Since, IReadOnlyList<Appointment> Appointments) Read(
        string text, string machine)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (StandingRead.Unread, [], null, []);
            if (!root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number < 1)
            {
                return (StandingRead.Unread, [], null, []);
            }

            if (number > SchemaVersion) return (StandingRead.Newer, [], null, []);
            if (Json.Text(root, "machine") is not { } bound || !string.Equals(bound, machine, StringComparison.OrdinalIgnoreCase))
            {
                return (StandingRead.OtherMachine, [], null, []);
            }

            if (!root.TryGetProperty("presses", out var named) || named.ValueKind != JsonValueKind.Array) return (StandingRead.Unread, [], null, []);
            var names = named.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).ToHashSet(StringComparer.Ordinal);
            var since = Moments.Read(Json.Text(root, "since"));
            var appointments = new List<Appointment>();
            foreach (var each in Json.Items(root, "appointments"))
            {
                // D209 §2.3: a record the built door wrote, `repository` and no `scope`, is read as `repository:<name>`.
                var scope = Json.Text(each, "scope") is { Length: > 0 } written ? written
                    : Json.Text(each, "repository") is { Length: > 0 } repository ? RoleScopes.Repository(repository) : null;
                if (Json.Text(each, "role") is { Length: > 0 } role && scope is not null && RoleScopes.Known(scope)
                    && Json.Text(each, "holder") is { Length: > 0 } holder && Json.Text(each, "generation") is { Length: > 0 } generation
                    && Moments.Read(Json.Text(each, "since")) is { } at
                    && !appointments.Any(kept => kept.Role == role && RoleScopes.Same(kept.Scope, scope)))
                {
                    // Its acts and its tick as written, which only show it: Appoint again never reads them back (§2.3).
                    IReadOnlyList<string> acts = each.TryGetProperty("acts", out var listed) && listed.ValueKind == JsonValueKind.Array
                        ? [.. listed.EnumerateArray().Where(act => act.ValueKind == JsonValueKind.String).Select(act => act.GetString()!)]
                        : [];
                    var setup = each.TryGetProperty("setup", out var ticked) && ticked.ValueKind == JsonValueKind.True;
                    appointments.Add(new Appointment(role, scope, holder, generation, at, acts, setup));
                }
            }

            // A later build's press this build does not know is kept out: its table cannot judge it.
            return (StandingRead.Found, [.. StandingPresses.Built.Where(names.Contains)], since, appointments);
        }
        catch (JsonException)
        {
            return (StandingRead.Unread, [], null, []);
        }
    }

    /// <summary>What the window's row reads now.</summary>
    public StandingNow Now()
    {
        var stewardActs = StewardActsNow();
        lock (_gate)
        {
            return new StandingNow(
                Offered, IsOn, _presses, StandingPresses.Built, _since, _unread, _unwritten, _used,
                [.. _held.Keys.Order(StringComparer.OrdinalIgnoreCase)], AppointsOffered,
                [.. _appointments.Select(each => new AppointmentNow(
                    each.Role, each.Repository, each.Holder, each.Since, !_holderOf.ContainsKey(each.Generation),
                    _answers.GetValueOrDefault(each.Generation), each.Scope, _holderOf.GetValueOrDefault(each.Generation))
                {
                    Acts = each.Acts, Setup = each.Setup,
                })],
                SupervisorOffered, StewardOffered,
                [.. _holders.Values.OrderBy(each => each.Since).Select(each => new HolderNow(each.Id, each.Since, RolesOf(each.Id)))])
            {
                StewardActsOffered = StewardOffered ? OffersActs(Roles.Steward) : [],
                StewardActs = stewardActs,
            };
        }
    }

    // Under the lock: on where this build offers it and the record names a press.
    private bool IsOn => Offered && _presses.Count > 0;

    /// <summary>
    /// The window's grant or revoke (<c>SET_STANDING_CONFIRMATION</c>, §4.1): <paramref name="presses"/> the presses ticked, none to turn it
    /// off. A revoke clears the memory under the lock before its record is written, and stands for this run where the write fails; a
    /// grant whose write fails turns nothing on. A grant this build does not offer, or one naming a press its table does not, is refused
    /// as data and changes nothing. A press waiting on a card keeps its card: a grant confirms no press already asked.
    /// </summary>
    public StandingChange Set(IReadOnlyList<string> presses)
    {
        if (presses.FirstOrDefault(press => !StandingPresses.Built.Contains(press, StringComparer.Ordinal)) is { } unknown)
        {
            return new StandingChange(false, UnknownPress, $"This Daoris has no press `{unknown}` for its standing confirmation.");
        }

        var granted = StandingPresses.Built.Where(press => presses.Contains(press, StringComparer.Ordinal)).ToList();
        if (granted.Count == 0) return Revoke();
        if (!Offered)
        {
            return new StandingChange(false, NotOffered, "This Daoris does not offer the standing confirmation yet.");
        }

        // One mutation step (SUPERVISOR1a-ii, §2.3): the whole record built from memory, written and installed under the lock, so a
        // grant never interleaves with an appointment's change or a revoke.
        lock (_gate)
        {
            if (_newer)
            {
                return new StandingChange(false, StandingRead.Newer, "Its record is from a newer Daoris, which this one does not write over.");
            }

            var since = _clock.GetUtcNow();
            try
            {
                Write(granted, since, _appointments);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _log.Warn(LogEvent, ("state", Unwritten), ("presses", Names(granted)));
                return new StandingChange(false, Unwritten, "Its record could not be written, so nothing was turned on.");
            }

            _presses = granted;
            _since = since;
            _unread = null;
            _unwritten = false;
        }

        _log.Info(LogEvent, ("state", "granted"), ("presses", Names(granted)));
        return new StandingChange(true, null, null);
    }

    private StandingChange Revoke()
    {
        bool unwritten;
        lock (_gate)
        {
            // At once, before the write (§4.3): a press judged after this asks. The appointments stay: each is its own tick.
            _presses = [];
            _since = null;
            _held.Clear();
            unwritten = !TryWrite([], null, _appointments, onlyWhereFound: true);
            _unwritten = unwritten;
        }

        _log.Info(LogEvent, ("state", unwritten ? Unwritten : "revoked"), ("presses", ""));
        return unwritten
            ? new StandingChange(true, Unwritten, "Turned off; its record could not be written, so the next start may read it as on.")
            : new StandingChange(true, null, null);
    }

    /// <summary>
    /// Under the lock: the record written as memory now holds it, false where the write failed. A newer record, or another machine's,
    /// is left as it is (§4.2): it is off here already. <paramref name="onlyWhereFound"/> writes nothing where there is no record yet.
    /// </summary>
    private bool TryWrite(IReadOnlyList<string> presses, DateTimeOffset? since, IReadOnlyList<Appointment> appointments, bool onlyWhereFound)
    {
        if (_newer || _unread == StandingRead.OtherMachine) return true;
        try
        {
            if (!onlyWhereFound || File.Exists(RecordPath)) Write(presses, since, appointments);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Write(IReadOnlyList<string> presses, DateTimeOffset? since, IReadOnlyList<Appointment> appointments) => JsonFile.Write(RecordPath, writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteStartArray("presses");
        foreach (var press in presses) writer.WriteStringValue(press);
        writer.WriteEndArray();
        if (since is { } at) writer.WriteString("since", Moments.Write(at));
        writer.WriteString("machine", _machine);
        // SUPERVISOR1a-ii (§2.3): additive, schema 1; never the key nor its hash. Carried through every change of the presses.
        if (appointments.Count > 0)
        {
            writer.WriteStartArray("appointments");
            foreach (var each in appointments)
            {
                // STEWARDROLE1a-i (D209 §2.3): `scope`, `acts` and `setup` in place of the built `repository`, schema 1 still.
                writer.WriteStartObject();
                writer.WriteString("role", each.Role);
                writer.WriteString("scope", each.Scope);
                writer.WriteString("holder", each.Holder);
                writer.WriteString("generation", each.Generation);
                writer.WriteString("since", Moments.Write(each.Since));
                writer.WriteStartArray("acts");
                foreach (var act in each.Acts) writer.WriteStringValue(act);
                writer.WriteEndArray();
                writer.WriteBoolean("setup", each.Setup);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    });

    // ——— The owner's appointments (SUPERVISOR1a-ii, D208; amended by STEWARDROLE1a-i, D209 §2.2 to §2.4, §3.3, §3.4, §5.1a).

    /// <summary>The same as <see cref="AppointAsync"/>, its holder's tab opened synchronously (a test's stand-in).</summary>
    public AppointmentChange Appoint(AppointRequest request, Func<HolderOpen, HolderOpened> open) =>
        AppointAsync(request, each => Task.FromResult(open(each))).GetAwaiter().GetResult();

    /// <summary>
    /// The window's appointment (<c>SET_APPOINTMENT { role, scope, holder, on, share?, setup? }</c>, D209 §2.3): with <c>on</c>, a new
    /// generation, held by a new holder whose tab <paramref name="open"/> starts with a new key before the mutation step installs its
    /// hash, or, where the owner chose it, by a living holder the role is added to (§2.2); nothing kept where the tab could not start or
    /// its harness is not Daoris's own (<see cref="HolderHarness.Refused"/>). Without <c>on</c>, the appointment dismissed alone. Either way
    /// the generations it ended are answered for the caller to sweep after this step (§5), never inside it.
    /// </summary>
    /// <param name="open">Opens a new holder's tab with the key in its harness's environment; called outside the lock.</param>
    public async Task<AppointmentChange> AppointAsync(AppointRequest request, Func<HolderOpen, Task<HolderOpened>> open)
    {
        var (role, scope, holder, on) = (request.Role, request.Scope, request.Holder, request.On);
        if (!on) return Dismiss(role, scope);
        if (!Roles.Known.Contains(role, StringComparer.Ordinal)) return Refuse(UnknownRole, $"This Daoris has no role `{role}` to appoint.");
        // D209 §2.4: each role by its own flag.
        if (!RoleOffered(role)) return Refuse(NotOffered, $"This Daoris does not offer the {role}'s appointment yet.");
        // The actor holds a role only once it is measured (SUPERVISOR1c, 1d): until then a terminal alone.
        if (holder != RoleHolders.Terminal) return Refuse(NotOffered, $"This Daoris does not offer the `{holder}` holder yet.");
        if (role == Roles.Steward ? scope != RoleScopes.Machine : RoleScopes.RepositoryOf(scope) is null)
        {
            return Refuse(Scope, role == Roles.Steward
                ? "The steward is appointed for this machine, every repository its registry names."
                : "The supervisor is appointed for one repository.");
        }

        if (RoleScopes.RepositoryOf(scope) is { } repository && !Drives(repository))
        {
            return Refuse(UnknownRepository, $"Daoris does not drive a repository `{repository}` here.");
        }

        // §2.3: the acts the build offers, and the owner's own tick, never what a file says.
        IReadOnlyList<string> acts = OffersActs(role);
        var setup = role == Roles.Steward && request.Setup;
        string? shared = null;
        IReadOnlyList<string> living;
        lock (_gate)
        {
            if (_newer) return Refuse(StandingRead.Newer, "Its record is from a newer Daoris, which this one does not write over.");
            if (_holders.Count > 0 && request.Share is null)
            {
                return Refuse(ChooseTerminal,
                    "A role's terminal Daoris started is open: add this role to it, or give it a terminal of its own.");
            }

            if (request.Share is { } named && named != "own")
            {
                if (!_holders.TryGetValue(named, out var target)) return Refuse(NoHolder, "That terminal no longer holds a role: give this one a terminal of its own.");
                if (target.Revoked.Contains(role) || HeldBy(target.Id).Any(each => each.Role == role && RoleScopes.Same(each.Scope, scope)))
                {
                    return Refuse(RevokedHolder, $"The {role} was turned off in that terminal this run: it is appointed again only to a terminal of its own.");
                }

                shared = named;
            }

            living = [.. _holders.Keys];
        }

        var now = _clock.GetUtcNow();
        string? key = null;
        HolderOpened? opened = null;
        var id = "h" + Guid.NewGuid().ToString("N")[..11];
        if (shared is null)
        {
            // The key is drawn here, handed to the holder's harness alone, and never kept: only its hash is (§2.2).
            key = HolderKey.Draw();
            try
            {
                opened = await open(new HolderOpen(id, key, [role], now, living)).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                              or System.ComponentModel.Win32Exception)
            {
                opened = new HolderOpened(null, null, Unstarted, null);
            }

            if (opened.Tab is null)
            {
                _log.Warn(RoleLogEvent, ("state", opened.Why ?? Unstarted), ("role", role));
                return opened.Why == HolderHarness.Refused
                    ? Refuse(HolderHarness.Refused,
                        $"Daoris starts only its own pinned claude-code as a role's terminal, and {opened.Message ?? "it has none"}: pin claude-code "
                        + "and install it from its maker's channel (`daoris agent pin claude-code <version>`), then appoint again. Nothing was appointed.")
                    : Refuse(Unstarted, $"The {role}'s terminal could not be started, so nothing was appointed.");
            }
        }

        var appointed = new Appointment(role, scope, holder, Guid.NewGuid().ToString("N"), now, acts, setup);
        var told = new List<(string Tab, string Line, string? Title)>();
        IReadOnlyList<string> ended;
        AppointmentChange? refused = null;
        int cancelled;
        lock (_gate)
        {
            if (_newer)
            {
                refused = Refuse(StandingRead.Newer, "Its record is from a newer Daoris, which this one does not write over.");
            }
            else if (opened?.Tab is { } tab && _endedTabs.Contains(tab))
            {
                // §2.2: a tab that ended while its appointment was still opening it: the appointment is never written.
                refused = Refuse(Unstarted, $"The {role}'s terminal ended as it opened, so nothing was appointed.");
            }
            else if (shared is not null && !_holders.ContainsKey(shared))
            {
                refused = Refuse(NoHolder, "That terminal no longer holds a role: give this one a terminal of its own.");
            }

            if (refused is not null)
            {
                ended = [];
                cancelled = 0;
            }
            else
            {
                var replaced = _appointments.Where(each => each.Role == role && RoleScopes.Same(each.Scope, scope)).ToList();
                var next = _appointments.Where(each => !(each.Role == role && RoleScopes.Same(each.Scope, scope))).Append(appointed).ToList();
                try
                {
                    // As a grant does, written over a record this build does not honour but one a newer Daoris wrote.
                    Write(_presses, _since, next);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    _log.Warn(RoleLogEvent, ("state", Unwritten), ("role", role));
                    refused = Refuse(Unwritten, "Its record could not be written, so nothing was appointed.");
                    next = null;
                }

                if (next is null)
                {
                    ended = [];
                    cancelled = 0;
                }
                else
                {
                    _appointments = next;
                    cancelled = Revoked(replaced, told);
                    Holding target;
                    if (shared is null)
                    {
                        target = new Holding(id, HolderKey.Hash(key)!, opened!.Tab, opened.Process, now);
                        _holders[id] = target;
                    }
                    else
                    {
                        target = _holders[shared];
                    }

                    _holderOf[appointed.Generation] = target.Id;
                    if (shared is not null && target.Tab is { } tab)
                    {
                        var roles = RolesOf(target.Id);
                        told.Add((tab, HolderTerminal.Added(role, now, roles), HolderTerminal.Title(roles)));
                    }

                    _unread = null;
                    ended = [.. replaced.Select(each => each.Generation)];
                }
            }
        }

        if (refused is not null)
        {
            // A tab opened for an appointment that is not kept is closed: its key admits nothing.
            if (opened?.Tab is { } orphan) Close?.Invoke(orphan);
            return refused;
        }

        Said(told);
        if (cancelled > 0) _log.Info("role.cancelled", ("role", role), ("count", Count(cancelled)));
        _log.Info(LogEvent, ("state", "appointed"), ("role", role), ("holder", holder));
        _log.Info(RoleLogEvent, ("state", shared is null ? "appointed" : "added"), ("role", role), ("scope", RoleScopes.RepositoryOf(scope) is null ? scope : "repository"));
        return new AppointmentChange(new StandingChange(true, null, null), ended);
    }

    /// <summary>
    /// Turn one appointment off (§5; D209 §2.2, Codex's final read, finding 10): its record, its admitted acts and its pending operations
    /// and bindings, by its generation, in one mutation step; the holder's hash stays while the holder holds another live appointment,
    /// and goes with its last. Taken whether or not its role is offered.
    /// </summary>
    private AppointmentChange Dismiss(string role, string scope)
    {
        IReadOnlyList<string> ended;
        bool unwritten;
        var told = new List<(string Tab, string Line, string? Title)>();
        int cancelled;
        lock (_gate)
        {
            var gone = _appointments.Where(each => each.Role == role && RoleScopes.Same(each.Scope, scope)).ToList();
            // At once, before the write: an act judged after this is refused.
            cancelled = Revoked(gone, told);
            _appointments = [.. _appointments.Where(each => !(each.Role == role && RoleScopes.Same(each.Scope, scope)))];
            unwritten = gone.Count > 0 && !TryWrite(_presses, _since, _appointments, onlyWhereFound: true);
            ended = [.. gone.Select(each => each.Generation)];
        }

        Said(told);
        if (cancelled > 0) _log.Info("role.cancelled", ("role", role), ("count", Count(cancelled)));
        _log.Info(LogEvent, ("state", unwritten ? Unwritten : "dismissed"), ("role", role));
        _log.Info(RoleLogEvent, ("state", unwritten ? Unwritten : "revoked"), ("role", role));
        return new AppointmentChange(
            unwritten
                ? new StandingChange(true, Unwritten, "Turned off; its record could not be written, so the next start may read it as dormant.")
                : new StandingChange(true, null, null),
            ended);
    }

    /// <summary>
    /// A holder's tab ended (D209 §2.2's <c>HolderEnded</c>), by any of its paths: the holder's hash and every appointment it holds dropped
    /// in one mutation step, their acts and operations cancelled; the generations it ended answered for the sweep, and told to
    /// <see cref="Ended"/>. A tab that ends while its appointment still opens it is remembered, so that appointment is never written.
    /// </summary>
    public IReadOnlyList<string> HolderEnded(string tab)
    {
        IReadOnlyList<string> ended;
        int cancelled;
        lock (_gate)
        {
            _endedTabs.Add(tab);
            if (_holders.Values.FirstOrDefault(each => each.Tab == tab) is not { } holding) return [];
            (ended, cancelled) = EndHolder(holding);
        }

        _log.Info(RoleLogEvent, ("state", "holder-ended"), ("count", Count(ended.Count)));
        if (cancelled > 0) _log.Info("role.cancelled", ("count", Count(cancelled)));
        if (ended.Count > 0) Ended?.Invoke(ended);
        return ended;
    }

    // Under the lock: a holder dropped with every appointment it holds, written once.
    private (IReadOnlyList<string> Ended, int Cancelled) EndHolder(Holding holding)
    {
        var generations = _holderOf.Where(each => each.Value == holding.Id).Select(each => each.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var generation in generations) _holderOf.Remove(generation);
        _holders.Remove(holding.Id);
        var cancelled = _acts.Cancel(generations);
        if (generations.Count > 0)
        {
            _appointments = [.. _appointments.Where(each => !generations.Contains(each.Generation))];
            TryWrite(_presses, _since, _appointments, onlyWhereFound: true);
        }

        return ([.. generations], cancelled);
    }

    // Under the lock: the appointments ended by a dismissal or a replacement taken out of memory. Each one's holder marks the role
    // revoked; a holder left holding nothing goes, its tab told so. Their acts and operations are cancelled.
    private int Revoked(IReadOnlyList<Appointment> gone, List<(string Tab, string Line, string? Title)> told)
    {
        var generations = gone.Select(each => each.Generation).ToHashSet(StringComparer.Ordinal);
        foreach (var each in gone)
        {
            if (!_holderOf.Remove(each.Generation, out var id) || !_holders.TryGetValue(id, out var holding)) continue;
            holding.Revoked.Add(each.Role);
            if (_holderOf.ContainsValue(id)) continue;
            _holders.Remove(id);
            if (holding.Tab is { } tab) told.Add((tab, HolderTerminal.TurnedOff, null));
        }

        return _acts.Cancel(generations);
    }

    // Under the lock: the appointments a holder holds now.
    private IEnumerable<Appointment> HeldBy(string holder) =>
        _appointments.Where(each => _holderOf.TryGetValue(each.Generation, out var id) && id == holder);

    // Under the lock: the roles a holder holds now, in the build's order.
    private IReadOnlyList<string> RolesOf(string holder)
    {
        var held = HeldBy(holder).Select(each => each.Role).ToHashSet(StringComparer.Ordinal);
        return [.. Roles.Known.Where(held.Contains)];
    }

    // Under the lock: whether a generation is an appointment a living holder holds now.
    private bool Live(string generation) => _holderOf.TryGetValue(generation, out var id) && _holders.ContainsKey(id);

    private void Said(List<(string Tab, string Line, string? Title)> told)
    {
        foreach (var (tab, line, title) in told) Tell?.Invoke(tab, line, title);
    }

    private static string Count(int count) => count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static AppointmentChange Refuse(string why, string message) => new(new StandingChange(false, why, message), []);

    /// <summary>
    /// The decisions' admission (SUPERVISOR1a-ii; §2.3, §3.4): the effective supervisor's appointment for <paramref name="repository"/>,
    /// its generation, read under the lock for this synchronous answer alone; refused <c>not-offered</c> (the supervisor's own flag,
    /// D209 §2.4), <c>not-appointed</c> or <c>dormant</c>.
    /// </summary>
    public DecisionAdmitted Admit(string repository)
    {
        lock (_gate)
        {
            if (!SupervisorOffered) return DecisionAdmitted.Refused(SupervisorWhy.NotOffered);
            if (_appointments.FirstOrDefault(each => each.Role == Roles.Supervisor && RoleScopes.Same(each.Scope, RoleScopes.Repository(repository)))
                is not { } appointed)
            {
                return DecisionAdmitted.Refused(SupervisorWhy.NotAppointed);
            }

            return Live(appointed.Generation)
                ? new DecisionAdmitted(appointed.Generation, null)
                : DecisionAdmitted.Refused(SupervisorWhy.Dormant);
        }
    }

    /// <summary>
    /// The judge's role method (SUPERVISOR1a-ii; amended by STEWARDROLE1a-i, D209 §3.4), for every route of the census's <c>Role</c>
    /// class: the role's own flag; D201's caller look, the first denial, so a contained agent is refused before its proof is read; the
    /// form; the appointment its generation names, live and not dormant; the holder's proof over this connection's unused
    /// <paramref name="nonce"/> and the form, against the appointment's holder's hash in memory; then <b>the configuration walk</b>, one
    /// guard for the whole class (finding 13), which ends a holder it finds started on another configuration; and for <c>ROLE_ACT</c>,
    /// the act in its role's table and the appointment's <c>acts</c>, its own form exact, and the act admitted once, its id drawn here.
    /// Anything uncovered is refused, never asked.
    /// </summary>
    /// <param name="nonce">The connection's hello's nonce, taken for this act (each is used once), or null where it was used or expired.</param>
    public RoleJudgement JudgeRole(string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce)
    {
        RoleJudgement judged;
        Holding? misconfigured = null;
        string? tab = null;
        IReadOnlyList<string> ended = [];
        var cancelled = 0;
        lock (_gate)
        {
            judged = Judged(module, route, payload, client, nonce, out misconfigured);
            if (misconfigured is not null)
            {
                tab = misconfigured.Tab;
                (ended, cancelled) = EndHolder(misconfigured);
            }
        }

        if (misconfigured is not null)
        {
            // §6.1: a holder that started on another configuration may have run what it loaded: it is ended, its tab closed.
            _log.Warn(RoleLogEvent, ("state", RoleWhy.HolderConfig), ("count", Count(ended.Count)));
            if (cancelled > 0) _log.Info("role.cancelled", ("count", Count(cancelled)));
            if (tab is not null) Close?.Invoke(tab);
            if (ended.Count > 0) Ended?.Invoke(ended);
        }

        if (judged is { Covered: true, ActId: not null }) _log.Info("role.act", ("role", judged.Role ?? ""), ("act", judged.Act ?? ""), ("outcome", "admitted"));
        return judged;
    }

    // Under the lock: §3.4's order, steps 1 to 7.
    private RoleJudgement Judged(string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce, out Holding? misconfigured)
    {
        misconfigured = null;
        var roleAct = route == RoleRoutes.Act;
        if (!string.Equals(module, DriverModuleName, StringComparison.OrdinalIgnoreCase) || !(roleAct || SupervisorRoutes.Acts.Contains(route)))
        {
            return RoleJudgement.Refused(StandingWhy.NotNamed);
        }

        var role = roleAct ? RoleActForm.RoleOf(payload) : Roles.Supervisor;
        if (role is null || !Roles.Known.Contains(role, StringComparer.Ordinal)) return RoleJudgement.Refused(SupervisorWhy.Form);
        if (!RoleOffered(role)) return RoleJudgement.Refused(SupervisorWhy.NotOffered);

        var place = Caller.Place(client);
        if (place == StandingPlace.InAgentJob) return RoleJudgement.Refused(StandingWhy.InAgentJob);
        if (place != StandingPlace.Outside) return RoleJudgement.Refused(StandingWhy.Untold);

        // The supervisor's answer or defer (SUPERVISOR1b), each proved over its own route's canonical bytes; or ROLE_ACT's form.
        RoleActForm? act = null;
        string generation, proof;
        byte[] canonical;
        if (roleAct)
        {
            if (RoleActForm.Read(payload) is not { } read || read.Role != role) return RoleJudgement.Refused(SupervisorWhy.Form);
            (act, generation, proof, canonical) = (read, read.Generation, read.Proof, read.Canonical);
        }
        else
        {
            if (SupervisorActs.Read(route, payload) is not { } read) return RoleJudgement.Refused(SupervisorWhy.Form);
            (generation, proof, canonical) = (read.Generation, read.Proof, read.Canonical);
        }

        if (_appointments.FirstOrDefault(each => each.Role == role && each.Generation == generation) is not { } appointed)
        {
            return RoleJudgement.Refused(!roleAct || _appointments.Any(each => each.Role == role)
                ? SupervisorWhy.AppointmentChanged
                : SupervisorWhy.NotAppointed);
        }

        if (!_holderOf.TryGetValue(appointed.Generation, out var id) || !_holders.TryGetValue(id, out var holding))
        {
            return RoleJudgement.Refused(SupervisorWhy.Dormant);
        }

        if (nonce is null || !SupervisorProof.Verify(holding.Hash, nonce, canonical, proof)) return RoleJudgement.Refused(SupervisorWhy.NotHolder);

        // §6.1, finding 13: one guard for every route of the class, never a list of routes.
        if (Walk.Check(client, holding.Process) is not null)
        {
            misconfigured = holding;
            return RoleJudgement.Refused(RoleWhy.HolderConfig);
        }

        var roles = RolesOf(holding.Id);
        if (act is null)
        {
            _answers[appointed.Generation] = _answers.GetValueOrDefault(appointed.Generation) + 1;
            return new RoleJudgement(true, null, appointed.Role, appointed.Repository, appointed.Generation, holding.Id, Roles: roles);
        }

        if (!TableOf(role).TryGetValue(act.Act, out var row)) return RoleJudgement.Refused(RoleWhy.NotRole);
        if (!appointed.Acts.Contains(act.Act, StringComparer.Ordinal) || (act.Act == StewardActs.Setup && !appointed.Setup))
        {
            return RoleJudgement.Refused(RoleWhy.NotGranted);
        }

        if (!row.IsForm(act.Form)) return RoleJudgement.Refused(SupervisorWhy.Form);

        // Step 6's `owner-set` (STEWARDROLE1a-ii, §5.4): what the owner acted on this run is theirs until Daoris restarts.
        var target = new RoleScope(row.TargetKind, row.Target(act.Form));
        if (OwnerHolds(target)) return RoleJudgement.Refused(RoleWhy.OwnerSet);

        // Step 7: admitted once, its id drawn here and registered as admitted; the lock is released as this returns.
        var admitted = _acts.Admit(role, act.Act, appointed.Generation, holding.Id, target.Name, target.Kind);
        _answers[appointed.Generation] = _answers.GetValueOrDefault(appointed.Generation) + 1;
        return new RoleJudgement(true, null, role, appointed.Repository, appointed.Generation, holding.Id, act.Act, admitted, roles);
    }

    // ——— The run's memory of a role's acts (D209 §3.4 steps 7 and 8, §5.1a): every step one hold of the judge's lock, never across an await.

    /// <summary>
    /// §3.4 step 8, under the act's gates: the act still <c>admitted</c> (no revoke and no owner's mark cancelled it), its holder's hash
    /// present and its generation live, and its target not <c>owner-set</c>, moved to <c>running</c>; null where it may run, else
    /// <c>owner-set</c> or <c>appointment-changed</c>.
    /// </summary>
    public string? RunAct(string id)
    {
        lock (_gate)
        {
            if (_acts.TargetOf(id) is { } target && OwnerHolds(target))
            {
                _acts.Refuse(id);
                return RoleWhy.OwnerSet;
            }

            if (_acts.Run(id, Live)) return null;
            return _acts.CancelledByOwner(id) ? RoleWhy.OwnerSet : SupervisorWhy.AppointmentChanged;
        }
    }

    /// <summary>The admitted act's target, which its gates are taken for (§5.4); null where no act has the id.</summary>
    public RoleScope? ActTarget(string id)
    {
        lock (_gate) return _acts.TargetOf(id);
    }

    /// <summary>
    /// The owner's mark (STEWARDROLE1a-ii, D209 §5.4), set inside the gates of every scope it resolved to: each repository and quest
    /// marked <c>owner-set</c> for the run, every admitted act, pending stage and binding on it cancelled in this one step; an owner's
    /// target that could not be resolved holds every quest. A repository marked by anything but the standing confirmation's covered
    /// press forgets the hold that confirmation remembered (D201's <c>Forget</c>, moved into the mark), so its covered resume asks.
    /// </summary>
    public void MarkOwner(OwnerResolved resolved, bool standing)
    {
        if (resolved.Marks.Count == 0 && !resolved.Unresolved) return;
        var cancelled = 0;
        lock (_gate)
        {
            foreach (var scope in resolved.Marks)
            {
                _ownerSet.Add(scope.Key);
                cancelled += _acts.CancelTarget(scope.Kind, scope.Name);
                if (scope.Kind == OwnerKinds.Repository && !standing) _held.Remove(scope.Name);
            }

            if (resolved.Unresolved)
            {
                _ownerUnresolved = true;
                cancelled += _acts.CancelKind(OwnerKinds.Quest);
            }
        }

        _log.Info("role.owner-set", ("count", Count(resolved.Marks.Count)), ("unresolved", resolved.Unresolved ? "true" : "false"));
        if (cancelled > 0) _log.Info("role.cancelled", ("count", Count(cancelled)), ("why", RoleWhy.OwnerSet));
    }

    /// <summary>Whether the owner acted on <paramref name="target"/> this run (§5.4).</summary>
    public bool IsOwnerSet(RoleScope target)
    {
        lock (_gate) return OwnerHolds(target);
    }

    // Under the lock.
    private bool OwnerHolds(RoleScope target) =>
        _ownerSet.Contains(target.Key) || (_ownerUnresolved && target.Kind == OwnerKinds.Quest);

    /// <summary>The act's handler returned.</summary>
    public void ActDone(string id)
    {
        lock (_gate) _acts.Done(id);
    }

    /// <summary>The act's state in memory (<see cref="RoleActStates"/>), or null where none has the id.</summary>
    public string? ActState(string id)
    {
        lock (_gate) return _acts.StateOf(id);
    }

    /// <summary>
    /// A pending operation registered for an act that ran (§5.1a): its kind, its stages in order, whether its last is a binding, and the
    /// complete operation its consumers act on. Cancelled at once where its appointment ended since the act ran.
    /// </summary>
    public RoleOperation? Pend(string id, string kind, IReadOnlyList<string> stages, bool binds, object? operation)
    {
        RoleOperation? pending;
        lock (_gate) pending = _acts.Pend(id, Roles.Steward, kind, stages, binds, operation, Live);
        if (pending is { State: RoleOperationStates.Cancelled }) _log.Info("role.cancelled", ("role", pending.Role), ("count", "1"));
        return pending;
    }

    /// <summary>A consumer's first step (§5.1a): the live operation on <paramref name="target"/> at <paramref name="stage"/>, from memory first; null where none.</summary>
    public RoleOperation? FindOperation(string target, string stage)
    {
        lock (_gate) return _acts.Find(target, stage, Live);
    }

    /// <summary>
    /// A consumer's take, and after its awaits its second step, the execution cutoff (§5.1a, finding 5): the operation still at
    /// <paramref name="stage"/> and live, moved past it; null where a revoke or an owner's act cancelled it, or another took it.
    /// </summary>
    public RoleOperation? TakeOperation(string id, string stage)
    {
        lock (_gate) return _acts.Take(id, stage, Live);
    }

    /// <summary>The quest's live hand-over binding, read by each later start this run (§5.1a); null where none.</summary>
    public RoleOperation? Binding(string target)
    {
        lock (_gate) return _acts.Binding(target, Live);
    }

    /// <summary>An operation by its id, whatever its state.</summary>
    public RoleOperation? Operation(string id)
    {
        lock (_gate) return _acts.Of(id);
    }

    // ——— The steward's own record of this run (STEWARDROLE1b, D209 §5.1, §5.3): what each act did, for the window's line and for the
    // conditions that read the steward's own acts (resume's and Try again's `not-own`, §3.1a's drive). Memory alone: a restart forgets it.

    private readonly List<StewardDone> _stewardDone = [];

    private sealed record StewardDone(string Id, string Act, string? Repository, string? Quest, string? Session, DateTimeOffset At);

    /// <summary>A session's act re-keyed to the quest its session resolved to (§5.4), before its gates' step.</summary>
    public void Retarget(string id, RoleScope target)
    {
        lock (_gate) _acts.Retarget(id, target);
    }

    /// <summary>A steward's act that ran, recorded as the steward's with the moment it ran (§5.1): the line's entry and its conditions' memory.</summary>
    public void StewardDid(string id, string act, string? repository = null, string? quest = null, string? session = null)
    {
        lock (_gate) _stewardDone.Add(new StewardDone(id, act, repository, quest, session, _clock.GetUtcNow()));
        _log.Info("role.act", ("role", Roles.Steward), ("act", act), ("outcome", "done"));
    }

    /// <summary>
    /// Whether the steward's own hold of <paramref name="repository"/> this run still stands (§3.1 row 5's <c>not-own</c>, D201's rule with
    /// its memory moved to the role): its last hold or resume there a hold, and nothing of the owner's on it since.
    /// </summary>
    public bool StewardHolds(string repository)
    {
        lock (_gate) return Latest(repository, StewardActNames.Hold, StewardActNames.Resume)?.Act == StewardActNames.Hold
                             && !OwnerHolds(new RoleScope(OwnerKinds.Repository, repository));
    }

    /// <summary>Whether the steward drove <paramref name="repository"/> this run and nothing of the owner's touched it since (§3.1a).</summary>
    public bool StewardDrove(string repository)
    {
        lock (_gate) return Latest(repository, StewardActNames.Drive, StewardActNames.Undrive)?.Act == StewardActNames.Drive
                             && !OwnerHolds(new RoleScope(OwnerKinds.Repository, repository));
    }

    /// <summary>
    /// The session of the steward's own stop of <paramref name="quest"/> this run that nothing has touched since (§3.1 row 8's
    /// <c>not-own</c>): its last stop or Try again there a stop, and nothing of the owner's on it since; else null.
    /// </summary>
    public string? StewardStopOf(string quest)
    {
        lock (_gate)
        {
            var last = _stewardDone.LastOrDefault(each => each.Quest is { } named && string.Equals(named, quest, StringComparison.OrdinalIgnoreCase)
                                                          && each.Act is StewardActNames.Stop or StewardActNames.Retry);
            return last is { Act: StewardActNames.Stop } && !OwnerHolds(new RoleScope(OwnerKinds.Quest, quest)) ? last.Session : null;
        }
    }

    /// <summary>
    /// Whether a steward's act caused a start of <paramref name="quest"/> in <paramref name="repository"/> (§3.1a): the repository driven by
    /// the steward this run and not the owner's since, or a live pending operation of the steward's on the quest.
    /// </summary>
    public bool StewardCaused(string repository, string quest)
    {
        lock (_gate)
        {
            if (OwnerHolds(new RoleScope(OwnerKinds.Quest, quest))) return false;
            return _acts.Pending(quest, Live)
                   || (Latest(repository, StewardActNames.Drive, StewardActNames.Undrive)?.Act == StewardActNames.Drive
                       && !OwnerHolds(new RoleScope(OwnerKinds.Repository, repository)));
        }
    }

    /// <summary>Whether this run's memory holds the steward's release <paramref name="act"/> of <paramref name="quest"/> at <c>released</c>, live (§5.1a).</summary>
    public bool StewardReleased(string quest, string act)
    {
        lock (_gate)
        {
            return _acts.Of(act) is { Stage: StewardStages.Released } operation
                   && string.Equals(operation.Target, quest, StringComparison.OrdinalIgnoreCase) && Live(operation.Generation)
                   && !OwnerHolds(new RoleScope(OwnerKinds.Quest, quest));
        }
    }

    /// <summary>The start's take of the steward's release (§5.1a): moved past <c>released</c> in one step, or null where it was cancelled.</summary>
    public RoleOperation? TakeRelease(string quest, string act)
    {
        RoleOperation? taken;
        lock (_gate)
        {
            taken = _acts.Of(act) is { } operation && string.Equals(operation.Target, quest, StringComparison.OrdinalIgnoreCase)
                    && !OwnerHolds(new RoleScope(OwnerKinds.Quest, quest))
                ? _acts.Take(act, StewardStages.Released, Live)
                : null;
        }

        _log.Info(taken is null ? "role.cancelled" : "role.act", ("role", Roles.Steward), ("act", StewardActNames.Retry),
            ("outcome", taken is null ? "withdrawn" : "started"));
        return taken;
    }

    /// <summary>A start a steward's act caused held for the owner (§3.1a, <c>held.checkout</c>): every pending operation on the quest ended.</summary>
    public void HeldForOwner(string quest)
    {
        int cancelled;
        lock (_gate) cancelled = _acts.CancelTarget(OwnerKinds.Quest, quest);
        _log.Info("role.cancelled", ("role", Roles.Steward), ("count", Count(cancelled)), ("why", "held.checkout"));
    }

    /// <summary>The steward's acts this run, newest first, as the window's line reads them (§5.3): each standing while the owner has not acted on its target since.</summary>
    public IReadOnlyList<StewardActNow> StewardActsNow()
    {
        lock (_gate)
        {
            var acts = new List<StewardActNow>();
            for (var at = _stewardDone.Count - 1; at >= 0; at--)
            {
                var done = _stewardDone[at];
                var target = done.Quest is { } quest ? new RoleScope(OwnerKinds.Quest, quest) : new RoleScope(OwnerKinds.Repository, done.Repository ?? "");
                var standing = !OwnerHolds(target);
                // A stop waits on a Try again while no later Try again of the steward's on its quest followed it and it still stands (§5.3).
                var waiting = done.Act == StewardActNames.Stop && standing
                              && !_stewardDone.Skip(at + 1).Any(later => later.Act == StewardActNames.Retry
                                                                         && string.Equals(later.Quest, done.Quest, StringComparison.OrdinalIgnoreCase));
                acts.Add(new StewardActNow(done.Id, done.Act, done.Repository, done.Quest, done.Session, done.At, standing, waiting));
            }

            return acts;
        }
    }

    // Under the lock: the steward's last act of the two named on a repository.
    private StewardDone? Latest(string repository, string one, string other) =>
        _stewardDone.LastOrDefault(each => each.Repository is { } named && string.Equals(named, repository, StringComparison.OrdinalIgnoreCase)
                                           && (each.Act == one || each.Act == other));

    /// <summary>
    /// Any change of a repository's hold but a covered one (the window's press, a confirmed press): the run forgets it held it, so a
    /// covered resume of it asks (§4.2).
    /// </summary>
    public void Forget(string repository)
    {
        lock (_gate) _held.Remove(repository);
    }

    /// <summary>
    /// Judge a terminal's press of the person's (§5 step 3) and, where it is covered, admit it under the lock: its use counted, a hold
    /// remembered, a resume's memory taken. Codes only reach the log, never a value.
    /// </summary>
    /// <param name="module">The press's module.</param>
    /// <param name="route">Its route.</param>
    /// <param name="payload">Its payload as the terminal sent it.</param>
    /// <param name="client">The terminal's process, held by the pipe from the connection's taking.</param>
    public StandingJudgement Judge(string module, string route, JsonElement? payload, PipeClientProcess? client)
    {
        var press = PressOf(module, route, payload);
        long seq;
        DateTimeOffset at;
        lock (_gate)
        {
            if (!IsOn) return StandingJudgement.Asks(StandingWhy.Off, press, said: false);
            if (press is null || !_presses.Contains(press, StringComparer.Ordinal)) return StandingJudgement.Asks(StandingWhy.NotNamed, press, said: false);
            if (!IsForm(press, payload)) return StandingJudgement.Asks(StandingWhy.Form, press, said: true);

            // Read under the lock, so no revoke slips between the judgement and the admission; the look reads the system, never a file.
            var place = Caller.Place(client);
            if (place == StandingPlace.InAgentJob) return StandingJudgement.Asks(StandingWhy.InAgentJob, press, said: true);
            if (place != StandingPlace.Outside) return StandingJudgement.Asks(StandingWhy.Untold, press, said: true);

            var subject = Text(payload, press is StandingPresses.Hold or StandingPresses.Resume ? "repository" : "id")!;
            switch (press)
            {
                case StandingPresses.Finish when RunsHere(subject):
                    return StandingJudgement.Asks(StandingWhy.RunningHere, press, said: true);
                case StandingPresses.Resume when !_held.ContainsKey(subject) || !HeldNow(subject):
                    // A hold the file no longer says was changed by someone else: forgotten, and the resume asks.
                    _held.Remove(subject);
                    return StandingJudgement.Asks(StandingWhy.NotHeldHere, press, said: true);
                case StandingPresses.Resume:
                    _held.Remove(subject);
                    break;
                case StandingPresses.Hold when !HeldNow(subject):
                    at = _clock.GetUtcNow();
                    _held[subject] = at;
                    break;
            }

            _used++;
            seq = ++_seq;
            at = _clock.GetUtcNow();
        }

        Used?.Invoke(seq, at);
        return new StandingJudgement(true, null, press, Said: true);
    }

    /// <summary>The press of the table a route is, whatever its form, or null: a route the table does not name.</summary>
    internal static string? PressOf(string module, string route, JsonElement? payload)
    {
        if (!string.Equals(module, DriverModuleName, StringComparison.OrdinalIgnoreCase)) return null;
        return route switch
        {
            "STOP_SESSION" => StandingPresses.Stop,
            "RESOLVE_SESSION" => StandingPresses.Finish,
            "SET_HOLD" => Field(payload, "held") is { ValueKind: JsonValueKind.False } ? StandingPresses.Resume : StandingPresses.Hold,
            _ => null,
        };
    }

    /// <summary>The bridge's driver module, whose routes the table names (the census's <c>DAORIS.DRIVER</c>).</summary>
    internal const string DriverModuleName = "DAORIS.DRIVER";

    /// <summary>
    /// Whether the payload is exactly the press's form (§2.1): every name, read as the bridge reads names (ignoring case, each once), one
    /// the form allows, of its type and value, and every name the form needs there. Anything more or less is not the form.
    /// </summary>
    internal static bool IsForm(string press, JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var form = press switch
        {
            StandingPresses.Stop => new Dictionary<string, Func<JsonElement, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = NamedText, ["said"] = value => value.ValueKind == JsonValueKind.True,
            },
            StandingPresses.Finish => new(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = NamedText,
                ["state"] = value => value.ValueKind == JsonValueKind.String && value.GetString() == "completed",
                ["said"] = value => value.ValueKind == JsonValueKind.True,
            },
            StandingPresses.Hold => new(StringComparer.OrdinalIgnoreCase)
            {
                ["repository"] = NamedText, ["held"] = value => value.ValueKind == JsonValueKind.True,
            },
            StandingPresses.Resume => new(StringComparer.OrdinalIgnoreCase)
            {
                ["repository"] = NamedText, ["held"] = value => value.ValueKind == JsonValueKind.False,
            },
            _ => null,
        };
        if (form is null) return false;
        foreach (var property in body.EnumerateObject())
        {
            if (!seen.Add(property.Name)) return false;
            if (!form.TryGetValue(property.Name, out var allowed) || !allowed(property.Value)) return false;
        }

        return seen.Count == form.Count;

        static bool NamedText(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            && text.Trim().Length == text.Length;
    }

    private static JsonElement? Field(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return null;
        foreach (var property in body.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }

        return null;
    }

    private static string? Text(JsonElement? payload, string name) =>
        Field(payload, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static string Names(IReadOnlyList<string> presses) => string.Join(",", presses);

    /// <summary>
    /// This machine and account, as the record binds them (§4.2): a SHA-256, in hex, of the machine's name and the account's SID (its name
    /// off Windows), so a <c>data/</c> folder copied to another machine or account is not honoured there.
    /// </summary>
    public static string ThisMachine()
    {
        string account;
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            account = identity.User?.Value ?? Environment.UserName;
        }
        else
        {
            account = Environment.UserName;
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Environment.MachineName}\n{account}")));
    }
}
