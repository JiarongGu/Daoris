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
    // The roles a terminal holds this run, each with the generation every act under it carries; the holders living this run by their id,
    // each with its key's hash, its tab, its harness's process and the roles ended in it, in memory alone.
    private readonly List<Hold> _holds = [];
    private readonly Dictionary<string, Holding> _holders = new(StringComparer.Ordinal);
    private readonly HashSet<string> _endedTabs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _answers = new(StringComparer.Ordinal);
    private readonly RoleActsMemory _acts = new();
    // STEWARDROLE1a-ii (D209 §5.4): what the owner acted on this run, by its scope's key, through whichever door; and whether a target
    // of theirs could not be resolved, which holds every quest for the run. Memory alone: a restart forgets it.
    private readonly HashSet<string> _ownerSet = new(StringComparer.Ordinal);
    private bool _ownerUnresolved;

    /// <summary>A role a living holder holds this run: its catalogue id, its kind, the generation drawn at its start, its holder.</summary>
    private sealed record Hold(string Role, string Kind, string Generation, string Holder, DateTimeOffset Since);

    /// <summary>A holder of this run (D209 §2.2): apart from its holds, shared by them or its own.</summary>
    private sealed class Holding(string id, byte[] hash, string? tab, HolderProcess? process, DateTimeOffset since)
    {
        public string Id { get; } = id;
        public byte[] Hash { get; } = hash;
        public string? Tab { get; } = tab;
        public HolderProcess? Process { get; } = process;
        public DateTimeOffset Since { get; } = since;

        /// <summary>The roles ended in this holder this run: never added back to it (§2.2).</summary>
        public HashSet<string> Revoked { get; } = new(StringComparer.Ordinal);
    }

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
        _log = log ?? MachineLog.None;
        Caller = caller ?? new StandingCaller();
        _clock = clock ?? TimeProvider.System;
        Offered = offered ?? OfferedInThisBuild;
        _machine = machine ?? ThisMachine();
        Walk = walk ?? HolderWalk.System;
        RunStarted = _clock.GetUtcNow();
        DeskCaller = desk ?? (Caller is StandingCaller jobs ? Desktop.DeskCaller.System(jobs) : new DeskCaller(_ => null, new SystemProcesses(), _clock, Environment.ProcessId));
        Desk = new StewardDesk(_clock, DaorisHome.Entry(home, HomeEntry.StewardDesk));
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
    }

    /// <summary>The catalogue's appointed roles as they stand now, read at each judgement; a test hands its own.</summary>
    public Func<IReadOnlyList<RoleRecord>> Appointed { get; set; }

    /// <summary>Whether a holder holds the steward: what the desk, the steward's starts and the owner's marks wait on.</summary>
    public bool StewardStaffed => Staffed(AppointedRoles.Steward, new RolePlace()) is { Hold.Holder: not null };

    // The role of a kind at a place, as the catalogue says now (the most specific scope's), or null.
    private RoleRecord? Staffed(string kind, RolePlace place) =>
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
        _log.Info("steward.desk", ("outcome", filed.Filed ? "filed" : "refused"), ("why", filed.Why ?? ""));
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

    /// <summary>
    /// Read the record (§4.2): once, as the instance's loop takes the home and before its pipe serves. What it said is logged by code,
    /// <c>person.standing</c> with <see cref="StandingRead"/>'s <c>state</c> and the presses' names, never a value of the record.
    /// </summary>
    public void Load()
    {
        string state;
        IReadOnlyList<string> presses = [];
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
            (state, presses, since) = Parse(text, _machine);
            newer = state == StandingRead.Newer;
        }

        lock (_gate)
        {
            _presses = state == StandingRead.Found ? presses : [];
            _since = state == StandingRead.Found && presses.Count > 0 ? since : null;
            _unread = state == StandingRead.Found ? null : state;
            _newer = newer;
            _held.Clear();
            _holds.Clear();
            _holders.Clear();
            _answers.Clear();
        }

        _log.Info(LogEvent, ("state", state), ("presses", Names(state == StandingRead.Found ? presses : [])));
    }

    /// <summary>The record's text read as the instance reads it: its state, its presses in the build's order (unknown names kept out), and its moment.</summary>
    internal static (string State, IReadOnlyList<string> Presses, DateTimeOffset? Since) Parse(string text, string machine)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (StandingRead.Unread, [], null);
            if (!root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number < 1)
            {
                return (StandingRead.Unread, [], null);
            }

            if (number > SchemaVersion) return (StandingRead.Newer, [], null);
            if (Json.Text(root, "machine") is not { } bound || !string.Equals(bound, machine, StringComparison.OrdinalIgnoreCase))
            {
                return (StandingRead.OtherMachine, [], null);
            }

            if (!root.TryGetProperty("presses", out var named) || named.ValueKind != JsonValueKind.Array) return (StandingRead.Unread, [], null);
            var names = named.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).ToHashSet(StringComparer.Ordinal);
            var since = Moments.Read(Json.Text(root, "since"));

            // A later build's press this build does not know is kept out: its table cannot judge it.
            return (StandingRead.Found, [.. StandingPresses.Built.Where(names.Contains)], since);
        }
        catch (JsonException)
        {
            return (StandingRead.Unread, [], null);
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
                [.. _held.Keys.Order(StringComparer.OrdinalIgnoreCase)],
                [.. _holds.Select(each => new RoleHoldNow(each.Role, each.Kind, each.Holder, each.Since, _answers.GetValueOrDefault(each.Generation)))],
                [.. _holders.Values.OrderBy(each => each.Since).Select(each => new HolderNow(each.Id, each.Since, RolesOf(each.Id)))])
            {
                StewardActs = stewardActs,
                StewardDesk = Desk.Now(),
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

        // One mutation step (§2.3): the whole record built from memory, written and installed under the lock, so a grant never
        // interleaves with a revoke.
        lock (_gate)
        {
            if (_newer)
            {
                return new StandingChange(false, StandingRead.Newer, "Its record is from a newer Daoris, which this one does not write over.");
            }

            var since = _clock.GetUtcNow();
            try
            {
                Write(granted, since);
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
            // At once, before the write (§4.3): a press judged after this asks.
            _presses = [];
            _since = null;
            _held.Clear();
            unwritten = !TryWrite([], null, onlyWhereFound: true);
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
    private bool TryWrite(IReadOnlyList<string> presses, DateTimeOffset? since, bool onlyWhereFound)
    {
        if (_newer || _unread == StandingRead.OtherMachine) return true;
        try
        {
            if (!onlyWhereFound || File.Exists(RecordPath)) Write(presses, since);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Write(IReadOnlyList<string> presses, DateTimeOffset? since) => JsonFile.Write(RecordPath, writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteStartArray("presses");
        foreach (var press in presses) writer.WriteStringValue(press);
        writer.WriteEndArray();
        if (since is { } at) writer.WriteString("since", Moments.Write(at));
        writer.WriteString("machine", _machine);
        writer.WriteEndObject();
    });

    // ——— The roles' terminals (D233 point 2; D209 §2.2, §3.3, §3.4, §5.1a).

    /// <summary>The same as <see cref="StartAsync"/>, its holder's tab opened synchronously (a test's stand-in).</summary>
    public HoldChange Start(HoldRequest request, Func<HolderOpen, HolderOpened> open) =>
        StartAsync(request, each => Task.FromResult(open(each))).GetAwaiter().GetResult();

    /// <summary>
    /// The window's start of a role's terminal (<c>START_ROLE_TERMINAL { role, share? }</c>): the role read from the catalogue as it stands,
    /// staffed by the terminal, held under a new generation by a new holder whose tab <paramref name="open"/> starts with a new key before
    /// the mutation step installs its hash, or, where the owner chose it, by a living holder the role is added to (§2.2); nothing kept
    /// where the tab could not start or its harness is not Daoris's own (<see cref="HolderHarness.Refused"/>). The generation of a hold it
    /// replaced is answered for the caller to sweep after this step (§5), never inside it.
    /// </summary>
    /// <param name="open">Opens a new holder's tab with the key in its harness's environment; called outside the lock.</param>
    public async Task<HoldChange> StartAsync(HoldRequest request, Func<HolderOpen, Task<HolderOpened>> open)
    {
        var role = request.Role;
        if (Appointed().FirstOrDefault(each => each.Id == role) is not { Hold.Kind: { } kind } staffed)
        {
            return Refuse(UnknownRole, $"This Daoris has no appointed role `{role}`.");
        }

        if (!staffed.Hold!.Terminal) return Refuse(NotTerminal, $"`{role}` is not held by a terminal: staff it with the terminal among the roles first.");
        string? shared = null;
        IReadOnlyList<string> living;
        lock (_gate)
        {
            if (_holders.Count > 0 && request.Share is null)
            {
                return Refuse(ChooseTerminal,
                    "A role's terminal Daoris started is open: add this role to it, or give it a terminal of its own.");
            }

            if (request.Share is { } named && named != "own")
            {
                if (!_holders.TryGetValue(named, out var target)) return Refuse(NoHolder, "That terminal no longer holds a role: give this one a terminal of its own.");
                if (target.Revoked.Contains(role) || _holds.Any(each => each.Holder == target.Id && each.Role == role))
                {
                    return Refuse(RevokedHolder, $"The {kind} was turned off in that terminal this run: it is started again only in a terminal of its own.");
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
                opened = await open(new HolderOpen(id, key, [kind], now, living)).ConfigureAwait(false);
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
                        + "and install it from its maker's channel (`daoris agent pin claude-code <version>`), then start it again. Nothing was started.")
                    : Refuse(Unstarted, $"The {kind}'s terminal could not be started, so nothing was kept.");
            }
        }

        var hold = new Hold(role, kind, Guid.NewGuid().ToString("N"), shared ?? id, now);
        var told = new List<(string Tab, string Line, string? Title)>();
        IReadOnlyList<string> ended = [];
        HoldChange? refused = null;
        var cancelled = 0;
        lock (_gate)
        {
            if (opened?.Tab is { } tab && _endedTabs.Contains(tab))
            {
                // §2.2: a tab that ended while its start was still opening it: the hold is never kept.
                refused = Refuse(Unstarted, $"The {kind}'s terminal ended as it opened, so nothing was kept.");
            }
            else if (shared is not null && !_holders.ContainsKey(shared))
            {
                refused = Refuse(NoHolder, "That terminal no longer holds a role: give this one a terminal of its own.");
            }
            else
            {
                var replaced = _holds.Where(each => each.Role == role).ToList();
                cancelled = Revoked(replaced, told);
                if (shared is null) _holders[id] = new Holding(id, HolderKey.Hash(key)!, opened!.Tab, opened.Process, now);
                _holds.Add(hold);
                if (shared is not null && _holders[shared].Tab is { } sharedTab)
                {
                    var roles = RolesOf(shared);
                    told.Add((sharedTab, HolderTerminal.Added(kind, now, roles), HolderTerminal.Title(roles)));
                }

                ended = [.. replaced.Select(each => each.Generation)];
            }
        }

        if (refused is not null)
        {
            // A tab opened for a hold that is not kept is closed: its key admits nothing.
            if (opened?.Tab is { } orphan) Close?.Invoke(orphan);
            return refused;
        }

        Said(told);
        if (cancelled > 0) _log.Info("role.cancelled", ("role", kind), ("count", Count(cancelled)));
        _log.Info(RoleLogEvent, ("state", shared is null ? "started" : "added"), ("role", kind), ("scope", staffed.Scope.Kind));
        return new HoldChange(new StandingChange(true, null, null), ended);
    }

    /// <summary>
    /// The catalogue changed (a role's press through the role door): every hold whose role no longer reads as held by the terminal, or
    /// as the kind it was started as, ended in one mutation step, its admitted acts, pending operations and bindings cancelled; a holder
    /// left holding nothing goes, its tab told so. Its generations are answered for the caller to sweep after this step (§5).
    /// </summary>
    public IReadOnlyList<string> RolesChanged()
    {
        var staffed = Appointed();
        var told = new List<(string Tab, string Line, string? Title)>();
        IReadOnlyList<string> ended;
        int cancelled;
        lock (_gate)
        {
            var gone = _holds.Where(each => staffed.FirstOrDefault(role => role.Id == each.Role) is not { Hold: { Terminal: true } held } || held.Kind != each.Kind).ToList();
            cancelled = Revoked(gone, told);
            ended = [.. gone.Select(each => each.Generation)];
        }

        Said(told);
        if (ended.Count > 0) _log.Info(RoleLogEvent, ("state", "ended"), ("count", Count(ended.Count)));
        if (cancelled > 0) _log.Info("role.cancelled", ("count", Count(cancelled)));
        return ended;
    }

    /// <summary>
    /// A holder's tab ended (D209 §2.2's <c>HolderEnded</c>), by any of its paths: the holder's hash and every hold it holds dropped in one
    /// mutation step, their acts and operations cancelled; the generations it ended answered for the sweep, and told to
    /// <see cref="Ended"/>. A tab that ends while its start still opens it is remembered, so that hold is never kept.
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

    // Under the lock: a holder dropped with every hold it holds.
    private (IReadOnlyList<string> Ended, int Cancelled) EndHolder(Holding holding)
    {
        var generations = _holds.Where(each => each.Holder == holding.Id).Select(each => each.Generation).ToHashSet(StringComparer.Ordinal);
        _holds.RemoveAll(each => each.Holder == holding.Id);
        _holders.Remove(holding.Id);
        return ([.. generations], _acts.Cancel(generations));
    }

    // Under the lock: the holds ended by a role's change or a replacement taken out of memory. Each one's holder marks the role ended in
    // it; a holder left holding nothing goes, its tab told so. Their acts and operations are cancelled.
    private int Revoked(IReadOnlyList<Hold> gone, List<(string Tab, string Line, string? Title)> told)
    {
        var generations = gone.Select(each => each.Generation).ToHashSet(StringComparer.Ordinal);
        _holds.RemoveAll(each => generations.Contains(each.Generation));
        foreach (var each in gone)
        {
            if (!_holders.TryGetValue(each.Holder, out var holding)) continue;
            holding.Revoked.Add(each.Role);
            if (_holds.Any(other => other.Holder == each.Holder)) continue;
            _holders.Remove(each.Holder);
            if (holding.Tab is { } tab) told.Add((tab, HolderTerminal.TurnedOff, null));
        }

        return _acts.Cancel(generations);
    }

    // Under the lock: the kinds of the roles a holder holds now, in the build's order.
    private IReadOnlyList<string> RolesOf(string holder)
    {
        var held = _holds.Where(each => each.Holder == holder).Select(each => each.Kind).ToHashSet(StringComparer.Ordinal);
        return [.. AppointedRoles.Kinds.Where(held.Contains)];
    }

    // Under the lock: whether a generation is a hold a living holder holds now.
    private bool Live(string generation) => _holds.Any(each => each.Generation == generation && _holders.ContainsKey(each.Holder));

    private void Said(List<(string Tab, string Line, string? Title)> told)
    {
        foreach (var (tab, line, title) in told) Tell?.Invoke(tab, line, title);
    }

    private static string Count(int count) => count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static HoldChange Refuse(string why, string message) => new(new StandingChange(false, why, message), []);

    /// <summary>
    /// The decisions' admission (SUPERVISOR1a-ii; §2.3, §3.4): the effective supervisor's hold for <paramref name="repository"/>, its
    /// generation and its role's window, read under the lock for this synchronous answer alone; refused <c>not-staffed</c> where the
    /// supervisor there is held by the person's rules alone, or <c>not-started</c> where its terminal is not started this run.
    /// </summary>
    /// <remarks>
    /// <b>One effective supervisor per question</b>, as a work's role is found: the most specific scope's, a repository's own over its
    /// workspace's over the machine's, the workspace the one <see cref="WorkspaceOf"/> places the repository in now.
    /// </remarks>
    public DecisionAdmitted Admit(string repository)
    {
        var effective = Supervising(repository);
        lock (_gate)
        {
            if (effective is not { Hold.Terminal: true }) return DecisionAdmitted.Refused(SupervisorWhy.NotStaffed);
            return HoldOf(effective.Id) is { } hold
                ? new DecisionAdmitted(hold.Generation, null) { Window = TimeSpan.FromMinutes(effective.Hold!.Window) }
                : DecisionAdmitted.Refused(SupervisorWhy.NotStarted);
        }
    }

    /// <summary>
    /// Whether the supervisor's hold of <paramref name="generation"/> is the effective one for <paramref name="repository"/>, which the
    /// holder's answer and defer check against the park's repository: null where it is; <c>role-changed</c> where it holds a supervisor
    /// that covers the repository but another is effective there (a repository's own made over its workspace's); <c>not-staffed</c>
    /// where it covers nothing there.
    /// </summary>
    public string? Covers(string generation, string repository)
    {
        var staffed = Appointed();
        var place = new RolePlace(WorkspaceOf(repository), repository);
        var effective = Supervising(repository);
        lock (_gate)
        {
            if (effective is { Hold.Terminal: true } && HoldOf(effective.Id) is { } live && live.Generation == generation) return null;
            var named = _holds.FirstOrDefault(each => each.Kind == AppointedRoles.Supervisor && each.Generation == generation);
            var covering = named is not null && staffed.Any(role => role.Id == named.Role && role.Scope.Covers(place));
            return covering ? SupervisorWhy.RoleChanged : SupervisorWhy.NotStaffed;
        }
    }

    // The supervisor that acts for a repository now (§5): the most specific scope's that covers it; null where none.
    private RoleRecord? Supervising(string repository) => Staffed(AppointedRoles.Supervisor, new RolePlace(WorkspaceOf(repository), repository));

    // Under the lock: the live hold of a role by its catalogue id, or null.
    private Hold? HoldOf(string role) => _holds.FirstOrDefault(each => each.Role == role && _holders.ContainsKey(each.Holder));

    /// <summary>
    /// The judge's role method (SUPERVISOR1a-ii; amended by STEWARDROLE1a-i, D209 §3.4), for every route of the census's <c>Role</c>
    /// class: D201's caller look, the first denial, so a contained agent is refused before its proof is read; the form; the hold its
    /// generation names, live, its role still held by the terminal as the catalogue says now; the holder's proof over this connection's
    /// unused <paramref name="nonce"/> and the form, against the holder's hash in memory; then <b>the configuration walk</b>, one guard for
    /// the whole class (finding 13), which ends a holder it finds started on another configuration; and for <c>ROLE_ACT</c>, the act in
    /// its role's table and the role's <c>authority</c>, its own form exact, and the act admitted once, its id drawn here. Anything
    /// uncovered is refused, never asked.
    /// </summary>
    /// <param name="nonce">The connection's hello's nonce, taken for this act (each is used once), or null where it was used or expired.</param>
    public RoleJudgement JudgeRole(string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce)
    {
        RoleJudgement judged;
        Holding? misconfigured = null;
        string? tab = null;
        IReadOnlyList<string> ended = [];
        var cancelled = 0;
        var staffed = Appointed();
        lock (_gate)
        {
            judged = Judged(module, route, payload, client, nonce, staffed, out misconfigured);
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
    private RoleJudgement Judged(
        string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce, IReadOnlyList<RoleRecord> staffed, out Holding? misconfigured)
    {
        misconfigured = null;
        var roleAct = route == RoleRoutes.Act;
        if (!string.Equals(module, DriverModuleName, StringComparison.OrdinalIgnoreCase) || !(roleAct || SupervisorRoutes.Acts.Contains(route)))
        {
            return RoleJudgement.Refused(StandingWhy.NotNamed);
        }

        var role = roleAct ? RoleActForm.RoleOf(payload) : AppointedRoles.Supervisor;
        if (role is null || !AppointedRoles.Kinds.Contains(role, StringComparer.Ordinal)) return RoleJudgement.Refused(SupervisorWhy.Form);

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

        if (_holds.FirstOrDefault(each => each.Kind == role && each.Generation == generation) is not { } held)
        {
            // A role of this kind staffed by the terminal and not started this run says so; another hold of it is a changed role.
            return RoleJudgement.Refused(_holds.Any(each => each.Kind == role) ? SupervisorWhy.RoleChanged
                : staffed.Any(each => each.Hold is { Terminal: true } hold && hold.Kind == role) ? SupervisorWhy.NotStarted
                : SupervisorWhy.NotStaffed);
        }

        // The role as the catalogue says now: a hold whose role no longer reads as the terminal's is not the role's.
        if (staffed.FirstOrDefault(each => each.Id == held.Role) is not { Hold: { Terminal: true } grant } found || grant.Kind != held.Kind)
        {
            return RoleJudgement.Refused(SupervisorWhy.RoleChanged);
        }

        if (!_holders.TryGetValue(held.Holder, out var holding)) return RoleJudgement.Refused(SupervisorWhy.NotStarted);

        if (nonce is null || !SupervisorProof.Verify(holding.Hash, nonce, canonical, proof)) return RoleJudgement.Refused(SupervisorWhy.NotHolder);

        // §6.1, finding 13: one guard for every route of the class, never a list of routes.
        if (Walk.Check(client, holding.Process) is not null)
        {
            misconfigured = holding;
            return RoleJudgement.Refused(RoleWhy.HolderConfig);
        }

        var roles = RolesOf(holding.Id);
        var repository = found.Scope.Kind == Daoris.Driver.RoleScope.Repository ? found.Scope.Id : null;
        if (act is null)
        {
            _answers[held.Generation] = _answers.GetValueOrDefault(held.Generation) + 1;
            return new RoleJudgement(true, null, held.Kind, repository, held.Generation, holding.Id, Roles: roles);
        }

        if (!TableOf(role).TryGetValue(act.Act, out var row)) return RoleJudgement.Refused(RoleWhy.NotRole);
        // STEWARDROLE1c (§6.2): the desk's decline changes nothing of Daoris's controls, so every steward may make it.
        var desk = role == AppointedRoles.Steward && StewardActs.Desk.Contains(act.Act, StringComparer.Ordinal);
        if (!desk && !grant.Acts.Contains(act.Act, StringComparer.Ordinal)) return RoleJudgement.Refused(RoleWhy.NotGranted);

        if (!row.IsForm(act.Form)) return RoleJudgement.Refused(SupervisorWhy.Form);

        // Step 6's `owner-set` (STEWARDROLE1a-ii, §5.4): what the owner acted on this run is theirs until Daoris restarts.
        var target = new RoleScope(row.TargetKind, row.Target(act.Form));
        if (OwnerHolds(target)) return RoleJudgement.Refused(RoleWhy.OwnerSet);

        // Step 7: admitted once, its id drawn here and registered as admitted; the lock is released as this returns.
        var admitted = _acts.Admit(role, act.Act, held.Generation, holding.Id, target.Name, target.Kind);
        _answers[held.Generation] = _answers.GetValueOrDefault(held.Generation) + 1;
        return new RoleJudgement(true, null, role, repository, held.Generation, holding.Id, act.Act, admitted, roles);
    }

    // ——— The run's memory of a role's acts (D209 §3.4 steps 7 and 8, §5.1a): every step one hold of the judge's lock, never across an await.

    /// <summary>
    /// §3.4 step 8, under the act's gates: the act still <c>admitted</c> (no revoke and no owner's mark cancelled it), its holder's hash
    /// present and its generation live, and its target not <c>owner-set</c>, moved to <c>running</c>; null where it may run, else
    /// <c>owner-set</c> or <c>role-changed</c>.
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
            return _acts.CancelledByOwner(id) ? RoleWhy.OwnerSet : SupervisorWhy.RoleChanged;
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
    /// complete operation its consumers act on. Cancelled at once where its hold ended since the act ran.
    /// </summary>
    public RoleOperation? Pend(string id, string kind, IReadOnlyList<string> stages, bool binds, object? operation)
    {
        RoleOperation? pending;
        lock (_gate) pending = _acts.Pend(id, AppointedRoles.Steward, kind, stages, binds, operation, Live);
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

    /// <summary>
    /// The generation of a live hold of <paramref name="kind"/>, which its holder's verb proves over (D209 §3.3), or null where none is
    /// live (not staffed by the terminal, or not started). The supervisor's listing names its own the same way.
    /// </summary>
    public string? LiveGeneration(string kind)
    {
        lock (_gate) return _holds.FirstOrDefault(each => each.Kind == kind && Live(each.Generation))?.Generation;
    }

    /// <summary>A session's act re-keyed to the quest its session resolved to (§5.4), before its gates' step.</summary>
    public void Retarget(string id, RoleScope target)
    {
        lock (_gate) _acts.Retarget(id, target);
    }

    /// <summary>A steward's act that ran, recorded as the steward's with the moment it ran (§5.1): the line's entry and its conditions' memory.</summary>
    public void StewardDid(string id, string act, string? repository = null, string? quest = null, string? session = null)
    {
        lock (_gate) _stewardDone.Add(new StewardDone(id, act, repository, quest, session, _clock.GetUtcNow()));
        _log.Info("role.act", ("role", AppointedRoles.Steward), ("act", act), ("outcome", "done"));
        // STEWARDROLE1c (§6.2): the act closes every open request on the desk that asked for it, by its act and what it acted on.
        Desk.Answered(id, act, repository, quest, session);
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

        _log.Info(taken is null ? "role.cancelled" : "role.act", ("role", AppointedRoles.Steward), ("act", StewardActNames.Retry),
            ("outcome", taken is null ? "withdrawn" : "started"));
        return taken;
    }

    /// <summary>A start a steward's act caused held for the owner (§3.1a, <c>held.checkout</c>): every pending operation on the quest ended.</summary>
    public void HeldForOwner(string quest)
    {
        int cancelled;
        lock (_gate) cancelled = _acts.CancelTarget(OwnerKinds.Quest, quest);
        _log.Info("role.cancelled", ("role", AppointedRoles.Steward), ("count", Count(cancelled)), ("why", "held.checkout"));
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
