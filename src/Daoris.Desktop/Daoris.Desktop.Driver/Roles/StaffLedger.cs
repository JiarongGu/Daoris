using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// A role's policy as the store holds it (the staff policy design §2.1): its state, <see cref="Default"/> with no row, <see cref="Set"/>
/// while its digest is the role's choices', <see cref="Stale"/> once they changed or it does not read; the record, kept for the editor
/// even when stale; its revision, the deletion's where there is no row; and the moment its window starts from.
/// </summary>
public sealed record StaffPolicyRead(string State, StaffPolicy? Policy, long Revision, string? Since, string? Why = null)
{
    public const string Default = "default";
    public const string Set = "set";
    public const string Stale = "stale";

    /// <summary>The policy a start applies: a set one; with none or a stale one the role runs in order, every move as today.</summary>
    public StaffPolicy? Applied => State == Set ? Policy : null;
}

/// <summary>
/// The store's half of a role's policy (STAFFPOLICY1a, D226; the staff policy design §2.1): <c>role_policy</c> and its revisions, read and
/// written inside a caller's transaction, so the choices' writer can save or re-key the policy in its own (row b).
/// </summary>
/// <remarks>Its callers write a store at <see cref="Step"/> or later; a store held before it, by a start not yet settled, has no table.</remarks>
public static class StaffPolicies
{
    /// <summary>The family a policy's write moves.</summary>
    public const string Family = "role_policy";

    /// <summary>The store's version the policy's tables arrive at.</summary>
    public const int Step = 16;

    private const string Row =
        """
        SELECT p.policy, p.staffing, p.revision, p.since, s.state, s.choices,
               (SELECT MAX(r.revision) FROM role_policy_revisions r WHERE r.role = $role)
        FROM (SELECT $role AS role) k
        LEFT JOIN role_policy p ON p.role = k.role
        LEFT JOIN role_staffing s ON s.role = k.role
        """;

    /// <summary><paramref name="role"/>'s policy as a read-only store holds it; default before the policy's step.</summary>
    public static StaffPolicyRead Read(DesktopStoreReader store, string role) =>
        store.Version < Step ? new StaffPolicyRead(StaffPolicyRead.Default, null, 0, null) : Judged(store.Query(Row, Raw, ("$role", role))[0]);

    /// <summary><paramref name="role"/>'s policy as the write's transaction sees it.</summary>
    public static StaffPolicyRead Read(DesktopWrite write, string role) => Judged(write.Query(Row, Raw, ("$role", role))[0]);

    /// <summary>
    /// Save <paramref name="policy"/> as <paramref name="role"/>'s, set for <paramref name="choices"/>, where the revision the person read is
    /// still the store's, and keep the revision; the window starts afresh where the rule or a choice's weight changed (§3.2). The caller
    /// judged it (<see cref="StaffPolicy.Judge"/>). The new revision.
    /// </summary>
    /// <exception cref="RoleRefusedException">The policy changed since <paramref name="read"/>: nothing is written, and the draft is the caller's to keep.</exception>
    public static long Save(DesktopWrite write, string role, StaffPolicy policy, IReadOnlyList<ActorChoice> choices, long read, string at)
    {
        var now = Read(write, role);
        if (read != now.Revision)
        {
            throw new RoleRefusedException(new RoleRefusal(StaffPolicy.Code,
                $"`{role}`'s policy changed since you read it (revision {read}, now {now.Revision}), so nothing was saved: read it again and save your draft over it."));
        }

        var kept = now.State != StaffPolicyRead.Default && now.Policy is { } old && old.Rule == policy.Rule
            && choices.All(choice => (old.Of(StaffKey.Of(choice))?.Weight ?? 1) == (policy.Of(StaffKey.Of(choice))?.Weight ?? 1));
        var revision = now.Revision + 1;
        var text = policy.Write();
        var digest = StaffPolicy.Digest(choices);
        write.Execute(
            """
            INSERT INTO role_policy (role, policy, staffing, revision, since, at) VALUES ($role, $policy, $staffing, $revision, $since, $at)
            ON CONFLICT (role) DO UPDATE SET policy = excluded.policy, staffing = excluded.staffing, revision = excluded.revision,
              since = excluded.since, at = excluded.at
            """,
            ("$role", role), ("$policy", text), ("$staffing", digest), ("$revision", revision), ("$since", kept ? now.Since : at), ("$at", at));
        write.Execute(
            "INSERT INTO role_policy_revisions (role, revision, policy, staffing, at) VALUES ($role, $revision, $policy, $staffing, $at)",
            ("$role", role), ("$revision", revision), ("$policy", text), ("$staffing", digest), ("$at", at));
        return revision;
    }

    /// <summary>
    /// Delete <paramref name="role"/>'s policy, as <i>Restore role defaults</i>, <i>Clear staffing</i> and a removal do (§2.4): its revisions
    /// kept, a revision with no policy marking the deletion. Whether there was one.
    /// </summary>
    public static bool Delete(DesktopWrite write, string role, string at)
    {
        var now = Read(write, role);
        if (now.State == StaffPolicyRead.Default) return false;
        write.Execute("DELETE FROM role_policy WHERE role = $role", ("$role", role));
        write.Execute(
            "INSERT INTO role_policy_revisions (role, revision, policy, staffing, at) VALUES ($role, $revision, NULL, NULL, $at)",
            ("$role", role), ("$revision", now.Revision + 1), ("$at", at));
        return true;
    }

    /// <summary>Whether the store this write is in has the policy's tables: a store held before <see cref="Step"/>, by a start not yet settled, has none.</summary>
    public static bool Has(DesktopWrite write) =>
        write.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'role_policy'") > 0;

    /// <summary>
    /// Bind <paramref name="policy"/> to <paramref name="choices"/>, the staffing a write of Daoris's own just made (§2.1): the digest set and the
    /// record re-keyed in the revision it has, which is rewritten to say the same. Nothing where the digest is already the choices'.
    /// </summary>
    public static void Rekey(DesktopWrite write, string role, StaffPolicy policy, IReadOnlyList<ActorChoice> choices)
    {
        var digest = StaffPolicy.Digest(choices);
        if (write.Scalar<string>("SELECT staffing FROM role_policy WHERE role = $role", ("$role", role)) == digest) return;
        var text = policy.Rekeyed(choices).Write();
        write.Execute("UPDATE role_policy SET policy = $policy, staffing = $staffing WHERE role = $role", ("$role", role), ("$policy", text), ("$staffing", digest));
        write.Execute(
            "UPDATE role_policy_revisions SET policy = $policy, staffing = $staffing WHERE role = $role AND revision = (SELECT revision FROM role_policy WHERE role = $role)",
            ("$role", role), ("$policy", text), ("$staffing", digest));
    }

    private sealed record Stored(string? Policy, string? Staffing, long? Revision, string? Since, string? State, string? Choices, long? Last);

    private static Stored Raw(SqliteDataReader row) => new(
        Text(row, 0), Text(row, 1), row.IsDBNull(2) ? null : row.GetInt64(2), Text(row, 3), Text(row, 4), Text(row, 5), row.IsDBNull(6) ? null : row.GetInt64(6));

    private static string? Text(SqliteDataReader row, int column) => row.IsDBNull(column) ? null : row.GetString(column);

    /// <summary>The state of a stored row: set while its digest is the role's choices' as they stand.</summary>
    private static StaffPolicyRead Judged(Stored stored)
    {
        if (stored.Policy is null) return new StaffPolicyRead(StaffPolicyRead.Default, null, stored.Last ?? 0, null);
        var revision = stored.Revision ?? 0;
        var (policy, refused) = StaffPolicy.Parse(stored.Policy);
        if (policy is null) return new StaffPolicyRead(StaffPolicyRead.Stale, null, revision, stored.Since, $"its policy does not read ({refused!.Said.TrimEnd('.')}), so it runs in order");
        if (stored.State != "set" || RoleStaffing.Choices(stored.Choices) is not { Count: > 0 } choices)
        {
            return new StaffPolicyRead(StaffPolicyRead.Stale, policy, revision, stored.Since, "its role has no choices of its own now, so it keeps its policy unapplied");
        }

        return StaffPolicy.Digest(choices) == stored.Staffing
            ? new StaffPolicyRead(StaffPolicyRead.Set, policy, revision, stored.Since)
            : new StaffPolicyRead(StaffPolicyRead.Stale, policy, revision, stored.Since, "its policy was set for other choices; it runs them in order until it is saved again");
    }
}

/// <summary>A start's hold on a choice in this process (§3.2): the role and the choice it counts on until the start opens or is refused.</summary>
public sealed class StaffReservation
{
    internal StaffReservation(string role, StaffKey choice, DateTimeOffset at)
    {
        Role = role;
        Choice = choice;
        At = at;
    }

    /// <summary>When it was taken, by which a reservation no open or refusal ever answered is dropped (<see cref="StaffLedger.ReservationLife"/>).</summary>
    internal DateTimeOffset At { get; }

    public string Role { get; }

    /// <summary>The choice it counts on: the arrangement's top pick, then the entry the settled selection took.</summary>
    public StaffKey Choice { get; internal set; }
}

/// <summary>
/// A look at a role's staffing (§3.1): the policy as read, the facts it was arranged by, the arrangement, and the reservation of its top pick
/// where the look takes one (<see cref="StaffLedger.Take"/>); a preview takes none.
/// </summary>
public sealed record StaffTake(StaffPolicyRead Policy, StaffFacts Facts, StaffArrangement Arrangement, StaffReservation? Reservation);

/// <summary>
/// What a start that took an account count holds until it opens or is refused (STAFFPOLICY1c): the role and choice, the rule and why
/// (<see cref="StaffBy"/>) with that said, and the reservation where a set policy arranged the walk.
/// </summary>
public sealed record StaffBinding(string Role, StaffKey Choice, string Rule, string By, string Said, StaffReservation? Reservation);

/// <summary>A new start's row of <c>role_starts</c> (§3.2): its session, role, choice, why it was chosen, and the condition where it moved.</summary>
public sealed record StaffStart(string Session, string Role, StaffKey Choice, string By, string? Condition = null);

/// <summary>
/// The ledger of a home's role starts (STAFFPOLICY1a, D226; the staff policy design §3.2, §4.2): <c>role_starts</c>, a row per new start at
/// its open; the reservations of this process's looks under one lock; the day's counts; and the health mark. It coordinates a look's
/// snapshot, its arrangement by <see cref="StaffJudge.Arrange"/> and its reservation under its lock, so two starts in one look count each
/// other. Nothing reads it at a start yet (row c).
/// </summary>
/// <remarks>
/// <para><b>In memory, this process's:</b> the reservations and the marks. A restart drops them and keeps <c>role_starts</c>.</para>
/// <para><b>The day</b> is the machine's local day, in its zone as D125 reads a reset; an edit of the policy never refills it.</para>
/// <para>The store is opened for one use and closed, a read never creating or writing it.</para>
/// </remarks>
/// <param name="home">The home whose store this is.</param>
/// <param name="settled">The reading of the start's confirmation a write opens the store under; null reads this install's swap journal.</param>
/// <param name="clock">The clock of every moment read and written.</param>
/// <param name="zone">The zone the day is read in; null for the machine's.</param>
public sealed class StaffLedger(string home, Func<StoreSettled>? settled = null, TimeProvider? clock = null, TimeZoneInfo? zone = null) : IStartLedger
{
    /// <summary>The family a start's write moves.</summary>
    public const string Family = "role_starts";

    /// <summary>How long an agent marked not answering stays marked, unless a prompt on it answers sooner (§4.2).</summary>
    public static readonly TimeSpan Mark = TimeSpan.FromMinutes(15);

    /// <summary>How many of the latest starts since a policy's <c>since</c> the balances are replayed from; held within −W and 2W they forget the rest.</summary>
    private const int Sequence = 1000;

    private readonly Lock _gate = new();

    private readonly List<StaffReservation> _reserved = [];

    private readonly Dictionary<string, DateTimeOffset> _marks = new(StringComparer.OrdinalIgnoreCase);

    private TimeProvider Clock => clock ?? TimeProvider.System;

    /// <summary>The store's file in this home.</summary>
    public string Location => DesktopStore.LocationIn(home);

    /// <summary>The home this ledger keeps its tables in.</summary>
    public string Home => home;

    /// <summary>The lines' moves to another choice, kept beside the starts (STAFFPOLICY1e).</summary>
    public StaffMoves Moves => new(home, settled, clock);

    /// <summary>The supervisor's rules as this home's store holds them now (CONTEXTDRIVE1e).</summary>
    public SupervisorRuleBook Rules() => SupervisorRuleBook.Read(home);

    /// <summary><paramref name="role"/>'s policy as the store holds it; default where there is no store or it is before the policy's step.</summary>
    public StaffPolicyRead Policy(string role)
    {
        using var store = DesktopStoreReader.Open(Location);
        return store is null ? new StaffPolicyRead(StaffPolicyRead.Default, null, 0, null) : StaffPolicies.Read(store, role);
    }

    /// <summary>
    /// A look that reserves its top pick: under the ledger's lock, the policy and the facts read once, <paramref name="line"/>'s tokens and
    /// moves off kept beside them, the walk arranged and, where a set policy arranged one, its top entry reserved. A refusal gives it back
    /// (<see cref="Release"/>), the settled selection moves it (<see cref="Settle"/>) and the open records it (<see cref="Record"/>).
    /// </summary>
    public StaffTake Take(string role, string work, IReadOnlyList<ActorEffective> order, StaffFacts? line = null) => Look(role, work, order, line, reserve: true);

    /// <summary>The same look reserving nothing and asking no account: <c>ROLES</c>' next start.</summary>
    public StaffTake Preview(string role, string work, IReadOnlyList<ActorEffective> order, StaffFacts? line = null) => Look(role, work, order, line, reserve: false);

    private StaffTake Look(string role, string work, IReadOnlyList<ActorEffective> order, StaffFacts? line, bool reserve)
    {
        lock (_gate)
        {
            var policy = Policy(role);
            var facts = Facts(role, policy, line ?? new StaffFacts());
            var arranged = StaffJudge.Arrange(policy.Applied, work, order, facts);
            StaffReservation? reservation = null;
            if (reserve && policy.Applied is not null && arranged.Walk.Count > 0)
            {
                reservation = new StaffReservation(role, StaffKey.Of(arranged.Walk[0].Entry), facts.Now);
                _reserved.Add(reservation);
            }

            return new StaffTake(policy, facts, arranged, reservation);
        }
    }

    /// <summary>How long a reservation stands unanswered by an open or a refusal (an agent with no account count has no release): past it, it counts no more.</summary>
    public static readonly TimeSpan ReservationLife = TimeSpan.FromMinutes(10);

    private readonly Dictionary<long, StaffBinding> _bound = [];

    /// <summary>
    /// Hold <paramref name="binding"/> on the account count (<see cref="HarnessSelection.Number"/>) the start took: the roster's <c>Release</c> gives
    /// it back with the count (<see cref="ReleaseBound"/>), and the open writes it down (<see cref="Opened"/>). A start with no count (0) is not bound.
    /// </summary>
    public void Bind(long number, StaffBinding binding)
    {
        if (number <= 0) return;
        lock (_gate)
        {
            _bound[number] = binding;
            if (_bound.Count > 256) _bound.Remove(_bound.Keys.Min());
        }
    }

    /// <summary>The start's account count was given back: its reservation goes with it, once.</summary>
    public void ReleaseBound(long number)
    {
        lock (_gate)
        {
            if (_bound.Remove(number, out var binding) && binding.Reservation is { } reservation) _reserved.Remove(reservation);
        }
    }

    /// <summary>
    /// The start on account count <paramref name="number"/> opened as <paramref name="session"/>: its row of <c>role_starts</c> is written, once
    /// for the session, and its reservation dropped. A store that will not take the row does not fail the start; the binding is answered all the same.
    /// Null where nothing was bound.
    /// </summary>
    public StaffBinding? Opened(long number, string session, string? condition = null)
    {
        StaffBinding? binding;
        lock (_gate)
        {
            if (!_bound.Remove(number, out binding)) return null;
        }

        return Written(binding, session, condition);
    }

    /// <summary>
    /// <see cref="Opened(long, string, string?)"/> for the selection the start opened on: a start with no account count (0) carries its binding
    /// on the selection, so its row is written all the same.
    /// </summary>
    public StaffBinding? Opened(HarnessSelection selection, string session, string? condition = null) =>
        Opened(selection.Number, session, condition)
        ?? (selection.Number <= 0 && selection.Staffed is StaffBinding carried ? Written(carried, session, condition) : null);

    private StaffBinding Written(StaffBinding binding, string session, string? condition)
    {
        // A start under no policy is written down where the home keeps a store already, never made for it.
        if (binding.Reservation is null && !File.Exists(Location)) return binding;
        try
        {
            Record(new StaffStart(session, binding.Role, binding.Choice, binding.By, condition), binding.Reservation);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            lock (_gate) if (binding.Reservation is { } reservation) _reserved.Remove(reservation);
        }

        return binding;
    }

    /// <summary>Move <paramref name="reservation"/> to the entry the settled selection took.</summary>
    public void Settle(StaffReservation reservation, ActorEffective taken)
    {
        lock (_gate) reservation.Choice = StaffKey.Of(taken);
    }

    /// <summary>Give <paramref name="reservation"/> back, once: the start was refused before it opened.</summary>
    public void Release(StaffReservation reservation)
    {
        lock (_gate) _reserved.Remove(reservation);
    }

    /// <summary>
    /// Write <paramref name="start"/>'s row at its open, once for its session, and drop <paramref name="reservation"/>, which the row now counts.
    /// False, nothing written, where the store is before the policy's step; the reservation is dropped either way.
    /// </summary>
    public bool Record(StaffStart start, StaffReservation? reservation = null)
    {
        lock (_gate)
        {
            try
            {
                using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
                if (store.Stamp.Version < StaffPolicies.Step) return false;
                store.Write(Family, write => write.Execute(
                    """
                    INSERT INTO role_starts (session, role, choice, by, condition, at) VALUES ($session, $role, $choice, $by, $condition, $at)
                    ON CONFLICT (session) DO NOTHING
                    """,
                    ("$session", start.Session), ("$role", start.Role), ("$choice", start.Choice.Text), ("$by", start.By), ("$condition", start.Condition),
                    ("$at", StaffJson.Moment(Clock.GetUtcNow()))));
                return true;
            }
            finally
            {
                if (reservation is not null) _reserved.Remove(reservation);
            }
        }
    }

    /// <summary>Mark <paramref name="agent"/> not answering for <see cref="Mark"/>: a transient episode past its bound, or a driven line's <c>after</c>.</summary>
    public void MarkNotAnswering(string agent)
    {
        lock (_gate) _marks[agent] = Clock.GetUtcNow() + Mark;
    }

    /// <summary>A prompt on <paramref name="agent"/> answered: its mark ends.</summary>
    public void Answered(string agent)
    {
        lock (_gate) _marks.Remove(agent);
    }

    /// <summary>
    /// The facts of a look, under the lock: <paramref name="line"/>'s tokens and moves off, the window since the policy's <c>since</c> and
    /// today's starts where a set policy reads them, this process's reservations and marks, and now.
    /// </summary>
    private StaffFacts Facts(string role, StaffPolicyRead policy, StaffFacts line)
    {
        var now = Clock.GetUtcNow();
        IReadOnlyList<StaffKey> window = [];
        var today = new Dictionary<StaffKey, int>();
        if (policy.Applied is not null)
        {
            using var store = DesktopStoreReader.Open(Location);
            if (store is not null && store.Version >= StaffPolicies.Step)
            {
                window = [.. store.Query(
                        "SELECT choice FROM role_starts WHERE role = $role AND at >= $since ORDER BY at DESC, rowid DESC LIMIT $most",
                        row => StaffKey.Read(row.GetString(0)), ("$role", role), ("$since", policy.Since), ("$most", Sequence))
                    .OfType<StaffKey>().Reverse()];
                foreach (var (key, count) in store.Query(
                             "SELECT choice, COUNT(*) FROM role_starts WHERE role = $role AND at >= $day GROUP BY choice",
                             row => (StaffKey.Read(row.GetString(0)), row.GetInt32(1)), ("$role", role), ("$day", StaffJson.Moment(DayStart(now)))))
                {
                    if (key is { } choice) today[choice] = today.GetValueOrDefault(choice) + count;
                }
            }
        }

        var tokens = policy.Applied is { } applied ? TokensToday(role, applied, now) : new Dictionary<StaffKey, StaffTokens>();
        var reserved = new Dictionary<StaffKey, int>();
        _reserved.RemoveAll(each => now - each.At >= ReservationLife);
        foreach (var each in _reserved.Where(each => each.Role == role)) reserved[each.Choice] = reserved.GetValueOrDefault(each.Choice) + 1;
        return line with
        {
            Now = now,
            Window = window,
            Reserved = reserved,
            Today = today,
            Tokens = tokens,
            NotAnswering = new Dictionary<string, DateTimeOffset>(_marks.Where(mark => mark.Value > now), StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// The tokens <paramref name="role"/>'s turns reported today, by the choice each turn's line names (STAFFPOLICY1e2): input plus output of every
    /// turn of the local day, read only where a choice of the policy has a daily budget of tokens. A turn whose door reported no count leaves its
    /// choice <see cref="StaffTokens.Unreported"/>, which holds the choice, never zero. A turn that names no choice is nobody's budget.
    /// </summary>
    private Dictionary<StaffKey, StaffTokens> TokensToday(string role, StaffPolicy policy, DateTimeOffset now)
    {
        var counted = new Dictionary<StaffKey, StaffTokens>();
        if (policy.Choices.All(choice => choice.Budget?.Tokens is null)) return counted;
        var day = DayStart(now);
        var turns = new List<KeptTurn>();
        foreach (var month in UsageHistory.TurnMonths(home).Where(each => each.Month.AddMonths(1) > day)) UsageHistory.TurnsOn(home, month, 0, turns);
        foreach (var turn in turns.Where(each => each.Actor == role && each.Ended >= day && each.Choice is not null))
        {
            if (StaffKey.Read(turn.Choice!) is not { } key) continue;
            var seen = counted.GetValueOrDefault(key, new StaffTokens(0, false));
            counted[key] = turn.Tokens.Input is null && turn.Tokens.Output is null
                ? seen with { Unreported = true }
                : seen with { Reported = seen.Reported + (turn.Tokens.Input ?? 0) + (turn.Tokens.Output ?? 0) };
        }

        return counted;
    }

    /// <summary>The start of <paramref name="now"/>'s local day in the ledger's zone.</summary>
    private DateTimeOffset DayStart(DateTimeOffset now)
    {
        var where = zone ?? TimeZoneInfo.Local;
        var local = TimeZoneInfo.ConvertTime(now, where);
        return new DateTimeOffset(local.Date, where.GetUtcOffset(local.Date));
    }
}
