namespace Daoris.Driver;

/// <summary>An account a running session could move to (SWITCH1c-i): what its agent last said, and whether it can run now.</summary>
/// <param name="Account">The account, one of the scope's list other than the session's.</param>
/// <param name="Said">What it last said about its windows, or null where it said nothing.</param>
/// <param name="Ready">Not cooling, not refused and signed in: an account that is not is never moved to.</param>
public sealed record SwitchCandidate(string Account, AccountSaid? Said, bool Ready);

/// <summary>What <see cref="SwitchDecision.Decide"/> says of a session at its turn's end (SWITCH1c-i; the switch design §4).</summary>
public abstract record SwitchChoice
{
    private SwitchChoice()
    {
    }

    /// <summary>The session stays on its account, for one of <see cref="SwitchStay"/>'s reasons.</summary>
    public sealed record Stay(string Why) : SwitchChoice;

    /// <summary>
    /// Read these accounts before deciding: the session's account is over its curve or within the margin of it, and what these
    /// ready candidates said is older than <see cref="SwitchDecision.Fresh"/> or nothing. In the scope's list order.
    /// </summary>
    public sealed record ReadOthers(IReadOnlyList<string> Accounts) : SwitchChoice;

    /// <summary>
    /// Move the session to <paramref name="Account"/>. The rest is why, as the moved lines say it: the session's account's
    /// window with the least room under its curve, its use there (a share, 0 to 1) and the curve's value there now (points).
    /// </summary>
    public sealed record MoveTo(string Account, string Window, double Used, double Curve) : SwitchChoice;
}

/// <summary>Why a session stays on its account (SWITCH1c-i), by code: the runner's log reads it, no person does.</summary>
public static class SwitchStay
{
    /// <summary>The scope's switch is <c>off</c>: no switch while a session runs (the switch design §2).</summary>
    public const string Off = "off";

    /// <summary>The session's account said no number on a window this build measures: absent is never over (D57).</summary>
    public const string Unmeasured = "unmeasured";

    /// <summary>The session's account is not over its curve; on it is not over it.</summary>
    public const string Room = "room";

    /// <summary>The session moved by the curve less than <see cref="SwitchDecision.Hold"/> ago.</summary>
    public const string Held = "held";

    /// <summary>No ready account of the scope has the margin's room under its curve on every window it said.</summary>
    public const string NoRoom = "no-room";
}

/// <summary>
/// Whether a running session should move to another account by the usage curve, at its turn's end (SWITCH1c-i; the switch
/// design §2, margin and hold, and §4; D165 point 3). Pure: the readings, the readiness, the moment and the last move come in
/// as values. The runner (SWITCH1c-ii) asks it, reads the others where it says so, and asks once more.
/// </summary>
/// <remarks>
/// <para>The order: <c>off</c> stays; an account that said no number stays; one with the margin's room stays and reads nothing;
/// within the margin, a ready candidate said long ago or never is read first, unless the session is held; not over stays;
/// held stays; then, of the ready candidates with at least <see cref="UsageCurves.SwitchMargin"/> on every window they said, the
/// one where the most of a week would lapse per hour to its reset (<see cref="UsageCurves.Lapsing"/>, USAGERESET1, as the walk's
/// step 4), then the most room on its tightest window (step 5, SWITCH1b-ii), the scope's list order breaking a tie; none stays.
/// What would lapse never makes a move: a session under its curve stays whatever another account's reset.</para>
/// <para>A limit reached is not this path: a cut-off session carries on by TOOL4f, held or not.</para>
/// </remarks>
public static class SwitchDecision
{
    /// <summary>How long a session that moved by the curve stays where it went (the switch design §2): no flip-flop between two accounts near their curves.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromMinutes(30);

    /// <summary>A candidate's reading older than this is read again before a move (Claude's read floor, <c>ClaudeUsage.Floor</c>).</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromMinutes(1);

    /// <param name="use">The scope's settings: its switch and curve.</param>
    /// <param name="account">The session's account.</param>
    /// <param name="said">What the session's account last said, or null for nothing.</param>
    /// <param name="candidates">The scope's other accounts, in its list's order; the session's own, if handed, is passed over.</param>
    /// <param name="now">The moment the curve and freshness are read at.</param>
    /// <param name="lastMove">When this session last moved by the curve, or null where it never did.</param>
    /// <param name="othersRead">
    /// The others were read for this turn's end already: a reading still old (a read floored, or an account busy) is weighed as it
    /// stands rather than read again, so the second ask always decides.
    /// </param>
    public static SwitchChoice Decide(
        RotationUse use, string account, AccountSaid? said, IReadOnlyList<SwitchCandidate> candidates, DateTimeOffset now,
        DateTimeOffset? lastMove, bool othersRead = false)
    {
        if (use.Switch == "off") return new SwitchChoice.Stay(SwitchStay.Off);
        if (UsageCurves.TightestSaid(use, said, now) is not { } tightest) return new SwitchChoice.Stay(SwitchStay.Unmeasured);
        if (tightest.Room >= UsageCurves.SwitchMargin) return new SwitchChoice.Stay(SwitchStay.Room);

        var others = candidates.Where(each => each.Account != account).ToList();
        var held = lastMove is { } at && now - at < Hold;
        if (!held && !othersRead
            && others.Where(each => each.Ready && Stale(each.Said, now)).Select(each => each.Account).ToList() is { Count: > 0 } stale)
        {
            return new SwitchChoice.ReadOthers(stale);
        }

        if (tightest.Room >= 0) return new SwitchChoice.Stay(SwitchStay.Room);
        if (held) return new SwitchChoice.Stay(SwitchStay.Held);

        // The move is required by now; what would lapse only chooses where it goes (USAGERESET1, the switch design §4): the most
        // of a week that would lapse per hour first, as the walk's step 4, then the most room. OrderByDescending and ThenBy
        // are stable: equal keeps the scope's list order.
        var target = others
            .Where(each => each.Ready)
            .Select(each => (
                each.Account,
                Room: UsageCurves.TightestSaid(use, each.Said, now)?.Room,
                Lapse: UsageCurves.Lapsing(use, each.Said, now)?.Rate ?? 0))
            .Where(each => each.Room >= UsageCurves.SwitchMargin)
            .OrderByDescending(each => each.Lapse)
            .ThenByDescending(each => each.Room)
            .Select(each => each.Account)
            .FirstOrDefault();
        return target is null
            ? new SwitchChoice.Stay(SwitchStay.NoRoom)
            : new SwitchChoice.MoveTo(target, tightest.Window.Window, tightest.Window.Used!.Value, tightest.Curve);
    }

    /// <summary>Whether a reading is older than <see cref="Fresh"/>, or nothing was said.</summary>
    private static bool Stale(AccountSaid? said, DateTimeOffset now) =>
        said is null || said.Windows.Count == 0 || now - said.Seen > Fresh;
}
