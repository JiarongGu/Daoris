using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>Why a role's act or a card's ask is refused or noted where the terminal's place is not told as outside, by code.</summary>
public static class StandingWhy
{
    /// <summary>The act is not one the roles' methods name.</summary>
    public const string NotNamed = "not-named";

    /// <summary>The terminal's process is in a job a Daoris on this Windows session made for its agents.</summary>
    public const string InAgentJob = "in-agent-job";

    /// <summary>The judge cannot tell where the terminal's process stands, or an agent of some Daoris here runs uncontained.</summary>
    public const string Untold = "untold";
}

/// <summary>What the window reads of the roles' judge (<c>STATE</c>'s <c>standingConfirmation</c>): the terminals that hold a role, the steward's acts and its desk.</summary>
/// <param name="Holds">The roles a terminal holds this run, each by its catalogue id; a role staffed by the terminal and not here is not started.</param>
/// <param name="Holders">The holders living this run, each with its roles and when its tab is opened, which a later start may add a role to.</param>
public sealed record StandingNow(IReadOnlyList<RoleHoldNow> Holds, IReadOnlyList<HolderNow> Holders)
{
    /// <summary>The steward's acts this run, newest first, from memory alone: the line's entries.</summary>
    public IReadOnlyList<StewardActNow> StewardActs { get; init; } = [];

    /// <summary>The steward's desk, newest first: what the line shows under the role's acts.</summary>
    public IReadOnlyList<DeskRequestNow> StewardDesk { get; init; } = [];

    /// <summary>The steward's <c>land</c>s a supervisor allowed that wait their window, which the pipe holds: the line says when each lands.</summary>
    public IReadOnlyList<StewardLandNow> StewardLands { get; init; } = [];
}

/// <summary>
/// One of the steward's acts this run as the window's line reads it: its id, the act, what it acted on,
/// when; <paramref name="Standing"/> while the owner has not acted on its target since, so its reverse press is offered; and, for a stop,
/// <paramref name="Waiting"/> while its quest waits on a Try again, which <i>What needs you</i> shows as <i>Stopped by the steward</i>; and
/// <paramref name="Allowed"/> where a supervisor allowed this act outside the steward's grants.
/// </summary>
public sealed record StewardActNow(
    string Id, string Act, string? Repository, string? Quest, string? Session, DateTimeOffset At, bool Standing, bool Waiting, bool Allowed = false);

/// <summary>A start of a role's terminal answered: done, or refused as data with a code the page words.</summary>
public sealed record StandingChange(bool Done, string? Why, string? Message);

/// <summary>
/// A role a terminal holds this run, as the window reads it: the role's catalogue id, its kind, the living holder, since when, and how
/// many acts it made. Never its generation, its key nor its hash.
/// </summary>
public sealed record RoleHoldNow(string Role, string Kind, string Holder, DateTimeOffset Since, int Answers);

/// <summary>A living holder as the window reads it: its id, when its tab is opened, its roles. Never its key, its hash nor its process.</summary>
public sealed record HolderNow(string Id, DateTimeOffset Since, IReadOnlyList<string> Roles);

/// <summary>A start of a role's terminal: its answer, and the generations it ended, which the caller sweeps after its step.</summary>
public sealed record HoldChange(StandingChange Answer, IReadOnlyList<string> Ended);

/// <summary>
/// The window's start of the terminal that holds a role staffed by the terminal (<c>START_ROLE_TERMINAL { role, share? }</c>):
/// <paramref name="Role"/> the role's catalogue id; <paramref name="Share"/> the choice while a holder lives, <c>own</c> for a terminal of
/// its own or a living holder's id to add the role to it.
/// </summary>
public sealed record HoldRequest(string Role, string? Share = null);

/// <summary>
/// The judge's role method's answer: covered, with the role's kind and the hold it acted
/// under, its holder and, for <c>ROLE_ACT</c>, the act and the act's id Daoris drew at admission; or refused with why, never asked.
/// </summary>
/// <param name="Roles">The roles the holder holds now, which every answer of a role's verb names.</param>
public sealed record RoleJudgement(
    bool Covered, string? Why, string? Role = null, string? Repository = null, string? Generation = null, string? Holder = null,
    string? Act = null, string? ActId = null, IReadOnlyList<string>? Roles = null)
{
    internal static RoleJudgement Refused(string why) => new(false, why);

    public string? HolderKind { get; init; }

    /// <summary>
    /// A proved holder's act outside the acts the person granted, in its exact form, that asks the person as <see cref="Asks"/>'s press:
    /// the route, and the form as its payload. Null for every other judgement.
    /// </summary>
    public (string Route, JsonElement Form)? Asks { get; init; }
}

/// <summary>
/// The roles' judge: the supervisor and the steward are catalogue roles, staffed through
/// the role door, read live from the catalogue (<see cref="Appointed"/>). A role staffed by the terminal is held this run once the window
/// starts its terminal (<see cref="StartAsync"/>), a generation drawn for that hold and the holder key's SHA-256 kept in this instance's
/// memory alone, so after a restart the role is not started until the window starts it again. A role's act is judged by its own method
/// (<see cref="JudgeRole"/>) under one lock and the caller's look, against the role's record as it stands, then by the holder's proof over
/// the connection's nonce; and the decisions' admission (<see cref="IDecisionAdmission"/>) answers the effective supervisor's hold and
/// window from the same memory and the catalogue.
/// </summary>
/// <remarks>
/// <para><b>One mutation step</b>: every read-modify-write of the memory runs whole under the lock. The lock is never held across
/// an await nor while a decision's lock is taken: the sweep of an ended hold's decisions runs after its step
/// (<see cref="HoldChange.Ended"/>).</para>
///
/// <para><b>Its parts and their state.</b> The memory and its one lock are <see cref="StandingState"/>'s, which every part reaches: the
/// roles' terminals <see cref="RoleTerminals"/>'s, the role method and the decisions' admission <see cref="RoleJudge"/>'s, the acts'
/// memory and the owner's marks <see cref="RoleActsLedger"/>'s and the steward's own record <see cref="StewardRecord"/>'s. This type keeps
/// the doors, the window's reading and what the loop hands in.</para>
/// </remarks>
public sealed class StandingConfirmation : IDecisionAdmission
{
    /// <summary>A start naming no appointed role of the catalogue.</summary>
    public const string UnknownRole = "unknown-role";

    /// <summary>A start of a role the terminal does not hold: its record names another holder, or none.</summary>
    public const string NotTerminal = "not-terminal";

    /// <summary>A role's terminal that could not be started: nothing is kept.</summary>
    public const string Unstarted = "unstarted";

    /// <summary>
    /// A start while a holder's tab lives that names no choice: the owner adds the role to that terminal or gives it one of
    /// its own; neither is preselected.
    /// </summary>
    public const string ChooseTerminal = "choose-terminal";

    /// <summary>A share naming no living holder.</summary>
    public const string NoHolder = "no-holder";

    /// <summary>A role ended in a holder this run added back to it: only a terminal of its own.</summary>
    public const string RevokedHolder = "revoked-holder";

    /// <summary>The machine log's event for a role's terminal started, a hold ended and a holder's end.</summary>
    public const string RoleLogEvent = "role.hold";

    /// <summary>The bridge's driver module, whose routes the table names (the census's <c>DAORIS.DRIVER</c>).</summary>
    internal const string DriverModuleName = "DAORIS.DRIVER";

    private readonly StandingState _state = new();
    private readonly RoleTerminals _terminals;
    private readonly RoleJudge _judge;
    private readonly RoleActsLedger _ledger;
    private readonly StewardRecord _steward;
    private readonly StewardAllowedAct _allowed;

    /// <param name="home">The home whose catalogue and desk these are.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="caller">Where a terminal's process stands (<see cref="StandingCaller"/> on Windows); a test hands its own.</param>
    /// <param name="clock">The clock a hold's moment is read by.</param>
    /// <param name="walk">The configuration walk every <c>Role</c> route's admission runs (<see cref="HolderWalk.System"/> where none is named).</param>
    /// <param name="desk">Who a desk request is counted against (<see cref="DeskCaller.System"/> where none is named).</param>
    public StandingConfirmation(
        string home, MachineLog? log = null, IStandingCaller? caller = null, TimeProvider? clock = null, IHolderWalk? walk = null,
        IDeskCaller? desk = null)
    {
        Log = log ?? MachineLog.None;
        Caller = caller ?? new StandingCaller();
        Clock = clock ?? TimeProvider.System;
        Walk = walk ?? HolderWalk.System;
        RunStarted = Clock.GetUtcNow();
        DeskCaller = desk ?? (Caller is StandingCaller jobs ? Desktop.DeskCaller.System(jobs) : new DeskCaller(_ => null, new SystemProcesses(), Clock, Environment.ProcessId));
        Desk = new StewardDesk(Clock, DaorisHome.Entry(home, HomeEntry.StewardDesk));
        Wider = new RuleWiderPlaceStore(home).Read;
        Slots = () =>
        {
            try
            {
                return RoleCatalog.ReadHome(home).Slots;
            }
            catch (DriverException)
            {
                // A catalogue that cannot be read staffs no role: every act and answer is refused until it reads.
                return [];
            }
        };
        _terminals = new RoleTerminals(this, _state);
        _judge = new RoleJudge(this, _state);
        _ledger = new RoleActsLedger(this, _state);
        _steward = new StewardRecord(this, _state);
        _allowed = new StewardAllowedAct(this, _state);
    }

    internal MachineLog Log { get; }

    internal TimeProvider Clock { get; }

    /// <summary>The catalogue's slots as they stand now, read at each judgement, its unreadable records among them; a test hands its own.</summary>
    public Func<IReadOnlyList<RoleSlot>> Slots { get; set; }

    /// <summary>
    /// Each repository's workspace as the store keeps it for the rules in every process, read at each judgement; a map that did not read
    /// says so, and the decisions it cannot place are the person's. A test hands its own.
    /// </summary>
    public Func<RuleWiderPlaces> Wider { get; set; }

    /// <summary>The catalogue's appointed roles that read, the supervisors and the steward, as they stand now.</summary>
    public IReadOnlyList<RoleRecord> Appointed() => [.. Slots().Select(slot => slot.Role).OfType<RoleRecord>().Where(role => role.Hold is not null)];

    /// <summary>Whether a holder holds the steward: what the desk, the steward's starts and the owner's marks wait on.</summary>
    public bool StewardStaffed => AppointedRoles.StewardOf(Appointed()) is { Hold.Holder: not null };

    internal SupervisorFound Supervising(string kind, RolePlace place) => SupervisorResolution.Find(Slots(), kind, place, Wider());

    internal RolePlace Placed(string repository) => new(Wider().WorkspaceOf.GetValueOrDefault(repository), repository);

    /// <summary>The steward's desk: the requests, which grant nothing.</summary>
    public StewardDesk Desk { get; }

    /// <summary>Who a desk request is counted against.</summary>
    public IDeskCaller DeskCaller { get; }

    /// <summary>
    /// <c>STEWARD_REQUEST</c>: any caller's request kept on the desk, counted against the caller <paramref name="client"/> is
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

    /// <summary>The configuration walk, run for every route of the census's <c>Role</c> class.</summary>
    public IHolderWalk Walk { get; }

    /// <summary>Each role's table for <c>ROLE_ACT</c> (<see cref="StewardActs.TableOf"/>), unless a test hands a stand-in act.</summary>
    public Func<string, IReadOnlyDictionary<string, IRoleAct>> TableOf { get; set; } = StewardActs.TableOf;

    /// <summary>Tells a holder's tab a line, and its title where one is named (the door hands the panel's): a role added, its last role off.</summary>
    public Action<string, string, string?>? Tell { get; set; }

    /// <summary>Closes a holder's tab Daoris ended (the walk found its configuration changed); the door hands the panel's.</summary>
    public Action<string>? Close { get; set; }

    /// <summary>
    /// Told the generations a holder's end or the walk ended outside a start's own press, after the step that ended them, for the door's
    /// sweep as a start's change answers them.
    /// </summary>
    public Action<IReadOnlyList<string>>? Ended { get; set; }

    /// <summary>Where a terminal's process stands.</summary>
    public IStandingCaller Caller { get; }

    /// <summary>
    /// The run's gates, one per workspace, repository and quest: a role's act holds its target's and its
    /// dependencies' alone; an owner's press holds what it resolves to beside the owner's other presses. Never the judge's lock.
    /// </summary>
    public RoleGates Gates { get; } = new();

    /// <summary>When this run began, after which a person's door's write on a quest's records marks it (<see cref="OwnerRecords"/>).</summary>
    public DateTimeOffset RunStarted { get; }

    /// <summary>What the window's row reads now.</summary>
    public StandingNow Now()
    {
        var stewardActs = StewardActsNow();
        lock (_state.Gate)
        {
            return new StandingNow(
                [.. _state.Holds.Select(each => new RoleHoldNow(each.Role, each.Kind, each.Holder, each.Since, _state.Answers.GetValueOrDefault(each.Generation)))],
                [.. _state.Holders.Values.OrderBy(each => each.Since).Select(each => new HolderNow(each.Id, each.Since, _state.RolesOf(each.Id)))])
            {
                StewardActs = stewardActs,
                StewardDesk = Desk.Now(),
            };
        }
    }

    // ——— The roles' terminals.

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

    /// <summary>The decisions' admission: <see cref="RoleJudge.Admit"/>.</summary>
    public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => _judge.Admit(kind, place, act);

    /// <summary>The allow supervisor that answers a steward's act at <paramref name="place"/>, never the steward's own holder: <see cref="StewardAllowedAct.Admit"/>.</summary>
    public DecisionAdmitted AdmitStewardAllow(RolePlace place, string? stewardHolder) => _allowed.Admit(place, stewardHolder);

    /// <summary>A steward's act an allow supervisor allowed, admitted as the steward's own: <see cref="StewardAllowedAct.Allowed"/>.</summary>
    internal RoleJudgement AdmitAllowedAct(RoleJudgement asked, JsonElement? payload, string decision) => _allowed.Allowed(asked, payload, decision);

    /// <summary>Whether a supervisor's hold is the effective one for a kind at a repository: <see cref="RoleJudge.Covers"/>.</summary>
    public string? Covers(string generation, string kind, string repository) => _judge.Covers(generation, kind, repository);

    /// <summary>The judge's role method, for every route of the census's <c>Role</c> class: <see cref="RoleJudge.JudgeRole"/>.</summary>
    /// <param name="nonce">The connection's hello's nonce, taken for this act (each is used once), or null where it is used or expired.</param>
    public RoleJudgement JudgeRole(string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce) =>
        _judge.JudgeRole(module, route, payload, client, nonce);

    // ——— The run's memory of a role's acts: every step one hold of the judge's lock, never across an await.

    /// <summary>Under the act's gates: <see cref="RoleActsLedger.RunAct"/>.</summary>
    public string? RunAct(string id) => _ledger.RunAct(id);

    /// <summary>The admitted act's target, which its gates are taken for; null where no act has the id.</summary>
    public RoleScope? ActTarget(string id) => _ledger.ActTarget(id);

    /// <summary>The owner's mark: <see cref="RoleActsLedger.MarkOwner"/>.</summary>
    public void MarkOwner(OwnerResolved resolved) => _ledger.MarkOwner(resolved);

    /// <summary>Whether the owner acted on <paramref name="target"/> this run.</summary>
    public bool IsOwnerSet(RoleScope target) => _ledger.IsOwnerSet(target);

    /// <summary>The act's handler returned.</summary>
    public void ActDone(string id) => _ledger.ActDone(id);

    /// <summary>The act's state in memory (<see cref="RoleActStates"/>), or null where none has the id.</summary>
    public string? ActState(string id) => _ledger.ActState(id);

    /// <summary>A pending operation registered for an act that ran: <see cref="RoleActsLedger.Pend"/>.</summary>
    public RoleOperation? Pend(string id, string kind, IReadOnlyList<string> stages, bool binds, object? operation) =>
        _ledger.Pend(id, kind, stages, binds, operation);

    /// <summary>A consumer's first step: the live operation on <paramref name="target"/> at <paramref name="stage"/>, from memory first; null where none.</summary>
    public RoleOperation? FindOperation(string target, string stage) => _ledger.FindOperation(target, stage);

    /// <summary>A consumer's take, and after its awaits its execution cutoff: <see cref="RoleActsLedger.TakeOperation"/>.</summary>
    public RoleOperation? TakeOperation(string id, string stage) => _ledger.TakeOperation(id, stage);

    /// <summary>The quest's live hand-over binding, read by each later start this run; null where none.</summary>
    public RoleOperation? Binding(string target) => _ledger.Binding(target);

    /// <summary>An operation by its id, whatever its state.</summary>
    public RoleOperation? Operation(string id) => _ledger.Operation(id);

    /// <summary>The generation of a live hold of <paramref name="kind"/>, or null: <see cref="RoleActsLedger.LiveGeneration"/>.</summary>
    public string? LiveGeneration(string kind) => _ledger.LiveGeneration(kind);

    /// <summary>A session's act re-keyed to the quest its session resolved to, before its gates' step.</summary>
    public void Retarget(string id, RoleScope target) => _ledger.Retarget(id, target);

    // ——— The steward's own record of this run: <see cref="StewardRecord"/>.

    /// <summary>A steward's act that ran, recorded as the steward's with the moment it ran: the line's entry and its conditions' memory.</summary>
    public void StewardDid(string id, string act, string? repository = null, string? quest = null, string? session = null) =>
        _steward.StewardDid(id, act, repository, quest, session);

    /// <summary>Whether the steward's own hold of <paramref name="repository"/> this run still stands: <see cref="StewardRecord.StewardHolds"/>.</summary>
    public bool StewardHolds(string repository) => _steward.StewardHolds(repository);

    /// <summary>Whether the steward drove <paramref name="repository"/> this run and nothing of the owner's touched it since.</summary>
    public bool StewardDrove(string repository) => _steward.StewardDrove(repository);

    /// <summary>The session of the steward's own stop of <paramref name="quest"/> that still stands: <see cref="StewardRecord.StewardStopOf"/>.</summary>
    public string? StewardStopOf(string quest) => _steward.StewardStopOf(quest);

    /// <summary>Whether the steward's own pause of <paramref name="quest"/> still stands: <see cref="StewardRecord.StewardPaused"/>.</summary>
    public bool StewardPaused(string quest) => _steward.StewardPaused(quest);

    /// <summary>Whether a steward's act caused a start of <paramref name="quest"/>: <see cref="StewardRecord.StewardCaused"/>.</summary>
    public bool StewardCaused(string repository, string quest) => _steward.StewardCaused(repository, quest);

    /// <summary>Whether this run's memory holds the steward's release <paramref name="act"/> of <paramref name="quest"/> at <c>released</c>, live.</summary>
    public bool StewardReleased(string quest, string act) => _steward.StewardReleased(quest, act);

    /// <summary>The start's take of the steward's release: moved past <c>released</c> in one step, or null where it is cancelled.</summary>
    public RoleOperation? TakeRelease(string quest, string act) => _steward.TakeRelease(quest, act);

    /// <summary>A start a steward's act caused held for the owner (<c>held.checkout</c>): every pending operation on the quest ended.</summary>
    public void HeldForOwner(string quest) => _steward.HeldForOwner(quest);

    /// <summary>The steward's acts this run, newest first, as the window's line reads them: <see cref="StewardRecord.StewardActsNow"/>.</summary>
    public IReadOnlyList<StewardActNow> StewardActsNow() => _steward.StewardActsNow();
}
