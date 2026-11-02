using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>A line's moves to another choice in 24 hours, and whether the person's yes released one (<see cref="StaffMoves.Of"/>).</summary>
/// <param name="Moves">The moves made, less the approvals given: what the policy's bound is weighed against.</param>
/// <param name="Approved">An approval stands newer than the line's last move: one move is released.</param>
public sealed record StaffMoved(int Moves, bool Approved);

/// <summary>A move a part's end decided, which the planner carries the line on by: the session it leaves, its condition and the choice it left.</summary>
public sealed record StaffMoving(string Session, string Condition, string? From);

/// <summary>
/// A line's moves to another choice, kept by the line's identity in <c>role_moves</c> (STAFFPOLICY1e, D226; the staff policy design §4.2,
/// §11 point 5). A quest's id or a conversation's names the line, so the count survives a restart and covers the moves that write no part
/// link: a model the agent refused or a sign-in replaced inside one start (<c>kind</c> <see cref="Moved"/> either way). A move is written
/// when it is decided, so two decisions at once cannot both pass the bound.
/// </summary>
/// <remarks>
/// <para>The bound weighs moves made in the last 24 hours less the approvals given in them: one approval of the person's at the bound lets one
/// move go, and that move is counted again, so the next waits for a new yes. A store before the table, or none, counts nothing and
/// records nothing: the line then moves as today's bounds allow.</para>
/// <para>A write the store refuses does not fail the line; it is said false.</para>
/// </remarks>
public sealed class StaffMoves(string home, Func<StoreSettled>? settled = null, TimeProvider? clock = null)
{
    /// <summary>The family a move's write moves.</summary>
    public const string Family = "role_moves";

    /// <summary>The store's version the table arrives at.</summary>
    public const int Step = 17;

    /// <summary>A move to another choice, decided.</summary>
    public const string Moved = "moved";

    /// <summary>The person's yes to a policy's ask: one move released.</summary>
    public const string Approval = "approved";

    /// <summary>A yes closed at the start it released, which moved nothing.</summary>
    public const string Spent = "spent";

    /// <summary>How long a move counts against the bound.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    // One lock for the process: the ledger hands out a fresh reader each time, and a decision reads the count and writes the move under it.
    private static readonly Lock _gate = new();

    private TimeProvider Clock => clock ?? TimeProvider.System;

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>The line's moves in 24 hours and whether a yes stands, read from the store; none before the table.</summary>
    public StaffMoved Of(string line)
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is null || store.Version < Step) return new StaffMoved(0, false);
        var since = StaffJson.Moment(Clock.GetUtcNow() - Window);
        var rows = store.Query(
            "SELECT id, kind FROM role_moves WHERE line = $line AND at >= $since ORDER BY id",
            row => (Id: row.GetInt64(0), Kind: row.GetString(1)), ("$line", line), ("$since", since));
        var moves = rows.Count(row => row.Kind == Moved);
        // A yes closed at the start it released (Spent) was never used for a move: it cancels itself and loosens nothing.
        var approvals = Math.Max(0, rows.Count(row => row.Kind == Approval) - rows.Count(row => row.Kind == Spent));
        var lastClosed = rows.Where(row => row.Kind is Moved or Spent).Select(row => row.Id).DefaultIfEmpty(0).Max();
        return new StaffMoved(Math.Max(0, moves - approvals), rows.Any(row => row.Kind == Approval && row.Id > lastClosed));
    }

    /// <summary>
    /// Close the person's standing yes for <paramref name="line"/> at the start it released, when that start moved nothing: an approval releases
    /// one move at the start that follows it and is not kept for a later one. False where none stood or the store would not take it.
    /// </summary>
    public bool SpendApproval(string line, string role) => Of(line).Approved && Write(line, role, Spent, null, null, null, null, null);

    /// <summary>
    /// Count a move off <paramref name="from"/> on <paramref name="condition"/> by <paramref name="action"/>, made in <paramref name="session"/>
    /// (the part it leaves; none for a move made inside one start, which no later part is carried on by). False where the store would not take it.
    /// </summary>
    public bool Record(string line, string role, string? session, string condition, string action, string? from, string? to = null) =>
        Write(line, role, Moved, session, from, to, condition, action);

    /// <summary>
    /// Decide a move under the ledger's lock: <paramref name="decide"/> is told the line's count and answers whether the move is made (the
    /// policy's bound weighed against it), and a yes is written before the lock is let go, so two decisions at once cannot both pass the bound.
    /// </summary>
    public bool Decided(string line, string role, string session, string condition, string action, string? from, Func<StaffMoved, bool> decide)
    {
        lock (_gate)
        {
            return decide(Of(line)) && Record(line, role, session, condition, action, from);
        }
    }

    /// <summary>
    /// The choices <paramref name="line"/> moved off within <see cref="StaffJudge.Hold"/>, each with when it was left (the judge's
    /// <c>moved-off</c> pass keeps the line from walking back onto one): the line's facts a start arranges by.
    /// </summary>
    public StaffFacts LineFacts(string line)
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is null || store.Version < Step) return new StaffFacts();
        var now = Clock.GetUtcNow();
        var since = StaffJson.Moment(now - StaffJudge.Hold);
        var off = new Dictionary<StaffKey, DateTimeOffset>();
        string? rule = null;
        foreach (var (from, at, action) in store.Query(
                     "SELECT from_choice, at, action FROM role_moves WHERE line = $line AND kind = $kind AND from_choice IS NOT NULL AND at >= $since ORDER BY id",
                     row => (row.GetString(0), row.GetString(1), row.IsDBNull(2) ? null : row.GetString(2)), ("$line", line), ("$kind", Moved), ("$since", since)))
        {
            if (StaffKey.Read(from) is { } key && DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)) off[key] = when;
            // The latest move's action names the rule of the walk it moves.
            rule = action == StaffActions.Weighted ? StaffRules.Weight : action == StaffActions.Next ? StaffRules.Order : rule;
        }

        return new StaffFacts { MovedOff = off, Rule = rule };
    }

    /// <summary>The person's yes to <paramref name="line"/>'s policy ask or bound: one move released. False where the store would not take it.</summary>
    public bool Approve(string line, string role) => Write(line, role, Approval, null, null, null, null, null);

    /// <summary>
    /// The move each line's last part ended on, which the planner carries the line on by: a move decided at a part's end names that part's
    /// session, and is moving until a newer part exists, which the planner knows by the line's last run no longer being that session.
    /// </summary>
    public IReadOnlyDictionary<string, StaffMoving> Moving()
    {
        using var store = DesktopStoreReader.Open(Location);
        var moving = new Dictionary<string, StaffMoving>(StringComparer.OrdinalIgnoreCase);
        if (store is null || store.Version < Step) return moving;
        var since = StaffJson.Moment(Clock.GetUtcNow() - Window);
        foreach (var (line, session, condition, from) in store.Query(
                     "SELECT line, session, condition, from_choice FROM role_moves WHERE kind = $kind AND session IS NOT NULL AND at >= $since ORDER BY id",
                     row => (row.GetString(0), row.GetString(1), row.IsDBNull(2) ? "" : row.GetString(2), row.IsDBNull(3) ? null : row.GetString(3)),
                     ("$kind", Moved), ("$since", since)))
        {
            moving[line] = new StaffMoving(session, condition, from);
        }

        return moving;
    }

    private bool Write(string line, string role, string kind, string? session, string? from, string? to, string? condition, string? action)
    {
        lock (_gate)
        {
            try
            {
                using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
                if (store.Stamp.Version < Step) return false;
                store.Write(Family, write => write.Execute(
                    """
                    INSERT INTO role_moves (line, role, kind, session, from_choice, to_choice, condition, action, at)
                    VALUES ($line, $role, $kind, $session, $from, $to, $condition, $action, $at)
                    """,
                    ("$line", line), ("$role", role), ("$kind", kind), ("$session", session), ("$from", from), ("$to", to),
                    ("$condition", condition), ("$action", action), ("$at", StaffJson.Moment(Clock.GetUtcNow()))));
                return true;
            }
            catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
