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
/// <param name="Holds">The roles a terminal holds this run, each by its catalogue id; a role staffed by the terminal and not here is not started.</param>
/// <param name="Holders">The holders living this run, each with its roles and when its tab was opened, which a later start may add a role to.</param>
public sealed record StandingNow(
    bool Offered, bool On, IReadOnlyList<string> Presses, IReadOnlyList<string> Built, DateTimeOffset? Since, string? Unread,
    bool Unwritten, int Used, IReadOnlyList<string> Held, IReadOnlyList<RoleHoldNow>? Holds = null, IReadOnlyList<HolderNow>? Holders = null)
{
    /// <summary>The steward's acts this run, newest first (STEWARDROLE1b, D209 §5.3), from memory alone: the line's entries.</summary>
    public IReadOnlyList<StewardActNow> StewardActs { get; init; } = [];

    /// <summary>The steward's desk, newest first: what the line shows under the role's acts.</summary>
    public IReadOnlyList<DeskRequestNow> StewardDesk { get; init; } = [];
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

/// <summary>
/// A role a terminal holds this run, as the window reads it: the role's catalogue id, its kind, the living holder, since when, and how
/// many acts it made. Never its generation, its key nor its hash.
/// </summary>
public sealed record RoleHoldNow(string Role, string Kind, string Holder, DateTimeOffset Since, int Answers);

/// <summary>A living holder as the window reads it (D209 §2.2): its id, when its tab was opened, its roles. Never its key, its hash nor its process.</summary>
public sealed record HolderNow(string Id, DateTimeOffset Since, IReadOnlyList<string> Roles);

/// <summary>A start of a role's terminal: its answer, and the generations it ended, which the caller sweeps after its step (§5).</summary>
public sealed record HoldChange(StandingChange Answer, IReadOnlyList<string> Ended);

/// <summary>
/// The window's start of the terminal that holds a role staffed by the terminal (<c>START_ROLE_TERMINAL { role, share? }</c>):
/// <paramref name="Role"/> the role's catalogue id; <paramref name="Share"/> the choice while a holder lives, <c>own</c> for a terminal of
/// its own or a living holder's id to add the role to it.
/// </summary>
public sealed record HoldRequest(string Role, string? Share = null);

/// <summary>
/// The judge's role method's answer (the supervisor design §2.2, §3.1; D209 §3.3): covered, with the role's kind and the hold it acted
/// under, its holder and, for <c>ROLE_ACT</c>, the act and the act's id Daoris drew at admission; or refused with why, never asked.
/// </summary>
/// <param name="Roles">The roles the holder holds now, which every answer of a role's verb names (D209 §2.2).</param>
public sealed record RoleJudgement(
    bool Covered, string? Why, string? Role = null, string? Repository = null, string? Generation = null, string? Holder = null,
    string? Act = null, string? ActId = null, IReadOnlyList<string>? Roles = null)
{
    internal static RoleJudgement Refused(string why) => new(false, why);

    /// <summary>
    /// A proved holder's act outside the acts the person granted, in its exact form, that asks the person as <see cref="Asks"/>'s press
    /// (STEWARDROLE1d): the route, and the form as its payload. Null for every other judgement.
    /// </summary>
    public (string Route, JsonElement Form)? Asks { get; init; }
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
/// <para><b>The roles' terminals</b> (D233 point 2; the supervisor design §2.2): the supervisor and the steward are catalogue roles,
/// staffed through the role door, read live from the catalogue (<see cref="Appointed"/>). A role staffed by the terminal is held this run
/// once the window starts its terminal (<see cref="StartAsync"/>), a generation drawn for that hold and the holder key's SHA-256 kept in
/// this instance's memory alone, so after a restart the role is not started until the window starts it again. A role's act is judged by
/// its own method (<see cref="JudgeRole"/>) under the same lock and caller look, against the role's record as it stands, then by the
/// holder's proof over the connection's nonce; and the decisions' admission (<see cref="IDecisionAdmission"/>) answers the effective
/// supervisor's hold and window from the same memory and the catalogue.</para>
///
/// <para><b>One mutation step</b> (§2.3): every read-modify-write of the record and the memory it installs runs whole under the lock. The
/// lock is never held across an await nor while a decision's lock is taken: the sweep of an ended hold's decisions runs after its step
/// (<see cref="HoldChange.Ended"/>).</para>
///
/// <para><b>Its parts and their state.</b> The memory and its one lock are <see cref="StandingState"/>'s, which every part reaches: the
/// grant's record and judgement <see cref="StandingGrant"/>'s, the roles' terminals <see cref="RoleTerminals"/>'s, the role method and the
/// decisions' admission <see cref="RoleJudge"/>'s, the acts' memory and the owner's marks <see cref="RoleActsLedger"/>'s and the steward's
/// own record <see cref="StewardRecord"/>'s. This type keeps the doors, the window's reading and what the loop hands in.</para>
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

    /// <summary>A start naming no appointed role of the catalogue.</summary>
    public const string UnknownRole = "unknown-role";

    /// <summary>A start of a role the terminal does not hold: its record names another holder, or none.</summary>
    public const string NotTerminal = "not-terminal";

    /// <summary>A role's terminal that could not be started: nothing was kept.</summary>
    public const string Unstarted = "unstarted";

    /// <summary>
    /// A start while a holder's tab lives that names no choice (D209 §2.2): the owner adds the role to that terminal or gives it one of
    /// its own; neither is preselected.
    /// </summary>
    public const string ChooseTerminal = "choose-terminal";

    /// <summary>A share naming no living holder.</summary>
    public const string NoHolder = "no-holder";

    /// <summary>A role ended in a holder this run added back to it (D209 §2.2): only a terminal of its own.</summary>
    public const string RevokedHolder = "revoked-holder";

    /// <summary>The machine log's event for a role's terminal started, a hold ended and a holder's end (D209 §5.2).</summary>
    public const string RoleLogEvent = "role.hold";

    /// <summary>The bridge's driver module, whose routes the table names (the census's <c>DAORIS.DRIVER</c>).</summary>
    internal const string DriverModuleName = "DAORIS.DRIVER";

    private readonly string _home;
    private readonly StandingState _state = new();
    private readonly StandingGrant _grant;
    private readonly RoleTerminals _terminals;
    private readonly RoleJudge _judge;
    private readonly RoleActsLedger _ledger;
    private readonly StewardRecord _steward;

    /// <param name="home">The home whose record this is.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="caller">Where a terminal's process stands (<see cref="StandingCaller"/> on Windows); a test hands its own.</param>
    /// <param name="clock">The clock a grant's moment is read by.</param>
    /// <param name="offered">Whether the grant is offered (<see cref="OfferedInThisBuild"/> where none is named).</param>
    /// <param name="machine">This machine and account, as the record binds them (<see cref="ThisMachine"/> where none is named).</param>
    /// <param name="walk">The configuration walk every <c>Role</c> route's admission runs (<see cref="HolderWalk.System"/> where none is named).</param>
    /// <param name="desk">Who a desk request is counted against (<see cref="DeskCaller.System"/> where none is named; STEWARDROLE1c).</param>
    public StandingConfirmation(
        string home, MachineLog? log = null, IStandingCaller? caller = null, TimeProvider? clock = null, bool? offered = null,
        string? machine = null, IHolderWalk? walk = null, IDeskCaller? desk = null)
    {
        _home = home;
        Log = log ?? MachineLog.None;
        Caller = caller ?? new StandingCaller();
        Clock = clock ?? TimeProvider.System;
        Offered = offered ?? OfferedInThisBuild;
        Walk = walk ?? HolderWalk.System;
        RunStarted = Clock.GetUtcNow();
        DeskCaller = desk ?? (Caller is StandingCaller jobs ? Desktop.DeskCaller.System(jobs) : new DeskCaller(_ => null, new SystemProcesses(), Clock, Environment.ProcessId));
        Desk = new StewardDesk(Clock, DaorisHome.Entry(home, HomeEntry.StewardDesk));
        Appointed = () =>
        {
            try
            {
                return RoleCatalog.ReadHome(home).HoldingActs;
            }
            catch (DriverException)
            {
                // A catalogue that cannot be read staffs no role: every act and answer is refused until it reads.
                return [];
            }
        };
        _grant = new StandingGrant(this, _state, machine ?? ThisMachine());
        _terminals = new RoleTerminals(this, _state);
        _judge = new RoleJudge(this, _state);
        _ledger = new RoleActsLedger(this, _state);
        _steward = new StewardRecord(this, _state);
    }

    internal MachineLog Log { get; }

    internal TimeProvider Clock { get; }

    /// <summary>The catalogue's appointed roles as they stand now, read at each judgement; a test hands its own.</summary>
    public Func<IReadOnlyList<RoleRecord>> Appointed { get; set; }

    /// <summary>Whether a holder holds the steward: what the desk, the steward's starts and the owner's marks wait on.</summary>
    public bool StewardStaffed => Staffed(AppointedRoles.Steward, new RolePlace()) is { Hold.Holder: not null };

    // The role of a kind at a place, as the catalogue says now (the most specific scope's), or null.
    internal RoleRecord? Staffed(string kind, RolePlace place) =>
        Appointed().Where(role => role.Hold?.Kind == kind && role.Scope.Covers(place)).OrderByDescending(role => role.Scope.Rank).FirstOrDefault();

    /// <summary>The steward's desk (D209 §6.2): the requests, which grant nothing.</summary>
    public StewardDesk Desk { get; }

    /// <summary>Who a desk request is counted against.</summary>
    public IDeskCaller DeskCaller { get; }

    /// <summary>
    /// <c>STEWARD_REQUEST</c> (D209 §6.2): any caller's request kept on the desk, counted against the caller <paramref name="client"/> is
    /// keyed by, the act one of the steward's table; refused <c>not-staffed</c> while no holder holds the steward. It admits nothing and
    /// registers no act.
    /// </summary>
    public DeskFiled FileRequest(JsonElement? payload, PipeClientProcess? client)
    {
        if (!StewardStaffed) return new DeskFiled(false, null, DeskWhy.NotStaffed);
        var filed = Desk.File(payload, DeskCaller.KeyOf(client), StewardActs.Table.Keys);
        Log.Info("steward.desk", ("outcome", filed.Filed ? "filed" : "refused"), ("why", filed.Why ?? ""));
        return filed;
    }

    /// <summary>Whether this instance offers the grant (D201's note on order).</summary>
    public bool Offered { get; }

    /// <summary>The configuration walk (D209 §6.1), run for every route of the census's <c>Role</c> class.</summary>
    public IHolderWalk Walk { get; }

    /// <summary>Each role's table for <c>ROLE_ACT</c> (<see cref="StewardActs.TableOf"/>), unless a test hands a stand-in act.</summary>
    public Func<string, IReadOnlyDictionary<string, IRoleAct>> TableOf { get; set; } = StewardActs.TableOf;

    /// <summary>Tells a holder's tab a line, and its title where one is named (the door hands the panel's): a role added, its last role off.</summary>
    public Action<string, string, string?>? Tell { get; set; }

    /// <summary>Closes a holder's tab Daoris ended (the walk found its configuration changed); the door hands the panel's.</summary>
    public Action<string>? Close { get; set; }

    /// <summary>
    /// Told the generations a holder's end or the walk ended outside a start's own press, after the step that ended them, for the door's
    /// sweep (§5) as a start's change answers them.
    /// </summary>
    public Action<IReadOnlyList<string>>? Ended { get; set; }

    /// <summary>
    /// The workspace the registry places a repository in (SUPERVISOR1e), as the loop last read it; null where it places it in none or the
    /// registry is not read yet, so a workspace's supervisor covers nothing it cannot place. Read under the judge's lock: it never awaits.
    /// </summary>
    public Func<string, string?> WorkspaceOf { get; set; } = _ => null;

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

    /// <summary>Read the record once, before the pipe serves (§4.2): <see cref="StandingGrant.Load"/>.</summary>
    public void Load() => _grant.Load();

    /// <summary>What the window's row reads now.</summary>
    public StandingNow Now()
    {
        var stewardActs = StewardActsNow();
        lock (_state.Gate)
        {
            return new StandingNow(
                Offered, _grant.IsOn, _state.Presses, StandingPresses.Built, _state.Since, _state.Unread, _state.Unwritten, _state.Used,
                [.. _state.Held.Keys.Order(StringComparer.OrdinalIgnoreCase)],
                [.. _state.Holds.Select(each => new RoleHoldNow(each.Role, each.Kind, each.Holder, each.Since, _state.Answers.GetValueOrDefault(each.Generation)))],
                [.. _state.Holders.Values.OrderBy(each => each.Since).Select(each => new HolderNow(each.Id, each.Since, _state.RolesOf(each.Id)))])
            {
                StewardActs = stewardActs,
                StewardDesk = Desk.Now(),
            };
        }
    }

    /// <summary>The window's grant or revoke (<c>SET_STANDING_CONFIRMATION</c>, §4.1): <see cref="StandingGrant.Set"/>.</summary>
    public StandingChange Set(IReadOnlyList<string> presses) => _grant.Set(presses);

    // ——— The roles' terminals (D233 point 2; D209 §2.2, §3.3, §3.4, §5.1a).

    /// <summary>The same as <see cref="StartAsync"/>, its holder's tab opened synchronously (a test's stand-in).</summary>
    public HoldChange Start(HoldRequest request, Func<HolderOpen, HolderOpened> open) =>
        StartAsync(request, each => Task.FromResult(open(each))).GetAwaiter().GetResult();

    /// <summary>The window's start of a role's terminal (<c>START_ROLE_TERMINAL { role, share? }</c>): <see cref="RoleTerminals.StartAsync"/>.</summary>
    /// <param name="open">Opens a new holder's tab with the key in its harness's environment; called outside the lock.</param>
    public Task<HoldChange> StartAsync(HoldRequest request, Func<HolderOpen, Task<HolderOpened>> open) => _terminals.StartAsync(request, open);

    /// <summary>The catalogue changed: the holds it ended, for the caller's sweep (<see cref="RoleTerminals.RolesChanged"/>).</summary>
    public IReadOnlyList<string> RolesChanged() => _terminals.RolesChanged();

    /// <summary>A holder's tab ended: the generations it ended, for the sweep (<see cref="RoleTerminals.HolderEnded"/>).</summary>
    public IReadOnlyList<string> HolderEnded(string tab) => _terminals.HolderEnded(tab);

    /// <summary>The decisions' admission (SUPERVISOR1a-ii): <see cref="RoleJudge.Admit"/>.</summary>
    public DecisionAdmitted Admit(string repository) => _judge.Admit(repository);

    /// <summary>Whether a supervisor's hold is the effective one for a repository: <see cref="RoleJudge.Covers"/>.</summary>
    public string? Covers(string generation, string repository) => _judge.Covers(generation, repository);

    /// <summary>The judge's role method, for every route of the census's <c>Role</c> class: <see cref="RoleJudge.JudgeRole"/>.</summary>
    /// <param name="nonce">The connection's hello's nonce, taken for this act (each is used once), or null where it was used or expired.</param>
    public RoleJudgement JudgeRole(string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce) =>
        _judge.JudgeRole(module, route, payload, client, nonce);

    // ——— The run's memory of a role's acts (D209 §3.4 steps 7 and 8, §5.1a): every step one hold of the judge's lock, never across an await.

    /// <summary>§3.4 step 8, under the act's gates: <see cref="RoleActsLedger.RunAct"/>.</summary>
    public string? RunAct(string id) => _ledger.RunAct(id);

    /// <summary>The admitted act's target, which its gates are taken for (§5.4); null where no act has the id.</summary>
    public RoleScope? ActTarget(string id) => _ledger.ActTarget(id);

    /// <summary>The owner's mark (STEWARDROLE1a-ii, D209 §5.4): <see cref="RoleActsLedger.MarkOwner"/>.</summary>
    public void MarkOwner(OwnerResolved resolved, bool standing) => _ledger.MarkOwner(resolved, standing);

    /// <summary>Whether the owner acted on <paramref name="target"/> this run (§5.4).</summary>
    public bool IsOwnerSet(RoleScope target) => _ledger.IsOwnerSet(target);

    /// <summary>The act's handler returned.</summary>
    public void ActDone(string id) => _ledger.ActDone(id);

    /// <summary>The act's state in memory (<see cref="RoleActStates"/>), or null where none has the id.</summary>
    public string? ActState(string id) => _ledger.ActState(id);

    /// <summary>A pending operation registered for an act that ran (§5.1a): <see cref="RoleActsLedger.Pend"/>.</summary>
    public RoleOperation? Pend(string id, string kind, IReadOnlyList<string> stages, bool binds, object? operation) =>
        _ledger.Pend(id, kind, stages, binds, operation);

    /// <summary>A consumer's first step (§5.1a): the live operation on <paramref name="target"/> at <paramref name="stage"/>, from memory first; null where none.</summary>
    public RoleOperation? FindOperation(string target, string stage) => _ledger.FindOperation(target, stage);

    /// <summary>A consumer's take, and after its awaits its execution cutoff (§5.1a): <see cref="RoleActsLedger.TakeOperation"/>.</summary>
    public RoleOperation? TakeOperation(string id, string stage) => _ledger.TakeOperation(id, stage);

    /// <summary>The quest's live hand-over binding, read by each later start this run (§5.1a); null where none.</summary>
    public RoleOperation? Binding(string target) => _ledger.Binding(target);

    /// <summary>An operation by its id, whatever its state.</summary>
    public RoleOperation? Operation(string id) => _ledger.Operation(id);

    /// <summary>The generation of a live hold of <paramref name="kind"/>, or null (D209 §3.3): <see cref="RoleActsLedger.LiveGeneration"/>.</summary>
    public string? LiveGeneration(string kind) => _ledger.LiveGeneration(kind);

    /// <summary>A session's act re-keyed to the quest its session resolved to (§5.4), before its gates' step.</summary>
    public void Retarget(string id, RoleScope target) => _ledger.Retarget(id, target);

    // ——— The steward's own record of this run (STEWARDROLE1b, D209 §5.1, §5.3): <see cref="StewardRecord"/>.

    /// <summary>A steward's act that ran, recorded as the steward's with the moment it ran (§5.1): the line's entry and its conditions' memory.</summary>
    public void StewardDid(string id, string act, string? repository = null, string? quest = null, string? session = null) =>
        _steward.StewardDid(id, act, repository, quest, session);

    /// <summary>Whether the steward's own hold of <paramref name="repository"/> this run still stands: <see cref="StewardRecord.StewardHolds"/>.</summary>
    public bool StewardHolds(string repository) => _steward.StewardHolds(repository);

    /// <summary>Whether the steward drove <paramref name="repository"/> this run and nothing of the owner's touched it since (§3.1a).</summary>
    public bool StewardDrove(string repository) => _steward.StewardDrove(repository);

    /// <summary>The session of the steward's own stop of <paramref name="quest"/> that still stands: <see cref="StewardRecord.StewardStopOf"/>.</summary>
    public string? StewardStopOf(string quest) => _steward.StewardStopOf(quest);

    /// <summary>Whether the steward's own pause of <paramref name="quest"/> still stands: <see cref="StewardRecord.StewardPaused"/>.</summary>
    public bool StewardPaused(string quest) => _steward.StewardPaused(quest);

    /// <summary>Whether a steward's act caused a start of <paramref name="quest"/>: <see cref="StewardRecord.StewardCaused"/>.</summary>
    public bool StewardCaused(string repository, string quest) => _steward.StewardCaused(repository, quest);

    /// <summary>Whether this run's memory holds the steward's release <paramref name="act"/> of <paramref name="quest"/> at <c>released</c>, live (§5.1a).</summary>
    public bool StewardReleased(string quest, string act) => _steward.StewardReleased(quest, act);

    /// <summary>The start's take of the steward's release (§5.1a): moved past <c>released</c> in one step, or null where it was cancelled.</summary>
    public RoleOperation? TakeRelease(string quest, string act) => _steward.TakeRelease(quest, act);

    /// <summary>A start a steward's act caused held for the owner (§3.1a, <c>held.checkout</c>): every pending operation on the quest ended.</summary>
    public void HeldForOwner(string quest) => _steward.HeldForOwner(quest);

    /// <summary>The steward's acts this run, newest first, as the window's line reads them (§5.3): <see cref="StewardRecord.StewardActsNow"/>.</summary>
    public IReadOnlyList<StewardActNow> StewardActsNow() => _steward.StewardActsNow();

    /// <summary>A change of a repository's hold but a covered one: the run forgets it held it (§4.2, <see cref="StandingGrant.Forget"/>).</summary>
    public void Forget(string repository) => _grant.Forget(repository);

    /// <summary>Judge a terminal's press of the person's (§5 step 3) and admit it where it is covered: <see cref="StandingGrant.Judge"/>.</summary>
    /// <param name="module">The press's module.</param>
    /// <param name="route">Its route.</param>
    /// <param name="payload">Its payload as the terminal sent it.</param>
    /// <param name="client">The terminal's process, held by the pipe from the connection's taking.</param>
    public StandingJudgement Judge(string module, string route, JsonElement? payload, PipeClientProcess? client) =>
        _grant.Judge(module, route, payload, client);

    /// <summary>Whether the payload is exactly the press's form (§2.1): <see cref="StandingGrant.IsForm"/>.</summary>
    internal static bool IsForm(string press, JsonElement? payload) => StandingGrant.IsForm(press, payload);

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
