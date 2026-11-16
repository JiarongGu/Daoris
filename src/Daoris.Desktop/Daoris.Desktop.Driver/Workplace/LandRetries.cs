using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The waits of runs' landings refused for a cause that passes (<see cref="LandingCode.Busy"/>), and the repositories whose landing is due
/// (LANDRETRY1). Kept in memory, as the performer's tries are: a restart reads as no try, and the next pass judges the landing afresh.
/// </summary>
/// <remarks>
/// <para><b>Tried again by itself, never asked again.</b> The yes the landing was refused under stands. The performer tries it at the first look
/// the repository is free (<see cref="StillHeld"/>), and that try is the landing a second Accept would make, behind the same delivery key and
/// landing record, so it lands once.</para>
/// <para><b>Each wait has a deadline.</b> Past <see cref="Deadline"/> from its first refusal the wait climbs the reporting line above the landing
/// (<see cref="Climb"/>), said once in the conversation and on the run's step, and is still tried at each look.</para>
/// <para><b>The tidy yields to a due landing</b> (<see cref="Due"/>): one being judged and made now, or one waiting here.</para>
/// </remarks>
public static class LandRetries
{
    /// <summary>A wait within its deadline.</summary>
    public const string Busy = "busy";

    /// <summary>A wait past its deadline, which climbed the reporting line.</summary>
    public const string Climbed = "climbed";

    /// <summary>A wait that ended in a refusal that does not pass: nothing is said of it any more, and it holds no tidy back.</summary>
    public const string Ended = "ended";

    /// <summary>How long a landing waits on a cause that passes before the wait climbs the reporting line.</summary>
    public static TimeSpan Deadline { get; } = TimeSpan.FromMinutes(15);

    /// <summary>How long a wait no look has read again still holds the tidy back: a run stopped meanwhile lets its repository go.</summary>
    public static TimeSpan Fresh { get; } = TimeSpan.FromMinutes(10);

    /// <summary>One landing's wait: where, since when, when a look last read it, and the line it climbed to once it did.</summary>
    public sealed record Wait(string Home, string Repository, string Code, DateTimeOffset Since, DateTimeOffset Seen)
    {
        /// <summary><see cref="LineWait.Said"/> once the wait climbed; null before.</summary>
        public string? Line { get; init; }

        /// <summary>What the run's step says while its <c>land</c> waits (<see cref="NodeObservation.Holds"/>): a code, and the line after a bar once climbed.</summary>
        public string Holds => Code switch
        {
            Climbed => $"{Climbed}|{Line}",
            Ended => "",
            _ => Code,
        };
    }

    private static readonly ConcurrentDictionary<string, Wait> Waits = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> Landing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The wait of <paramref name="unit"/>'s landing at <paramref name="tip"/>, or null where it waits on nothing that passes.</summary>
    public static Wait? Waiting(string unit, string tip) => Waits.GetValueOrDefault(Key(unit, tip));

    /// <summary>
    /// A try refused for a cause that passes: its wait begins now, or goes on from its first refusal and is judged against its deadline as a
    /// look's is (<see cref="Looked"/>), so a cause no look reads (the tree's landing hold, its delivery held here) climbs the line too.
    /// </summary>
    /// <returns>Whether the wait began now, and whether it climbed at this refusal.</returns>
    internal static (bool Began, bool Climbed) Refused(string home, string repository, string unit, string tip, DateTimeOffset now, Func<LineWait> climb)
    {
        var began = false;
        Waits.AddOrUpdate(Key(unit, tip),
            _ => { began = true; return new(Homes.Full(home), repository, Busy, now, now); },
            (_, was) => was.Code == Ended ? Begin() : was);
        return began ? (true, false) : (false, Looked(home, repository, unit, tip, now, climb).Climbed);

        Wait Begin()
        {
            began = true;
            return new(Homes.Full(home), repository, Busy, now, now);
        }
    }

    /// <summary>
    /// A look that found the cause still there: the wait read again now, and past its deadline climbed once (<paramref name="climb"/> asked only
    /// then). True where it climbed at this look.
    /// </summary>
    internal static (Wait Wait, bool Climbed) Looked(string home, string repository, string unit, string tip, DateTimeOffset now, Func<LineWait> climb)
    {
        var wait = Waits.GetValueOrDefault(Key(unit, tip)) ?? new(Homes.Full(home), repository, Busy, now, now);
        var climbs = wait.Code == Busy && now - wait.Since >= Deadline;
        wait = climbs ? wait with { Code = Climbed, Seen = now, Line = climb().Said() } : wait with { Seen = now };
        Waits[Key(unit, tip)] = wait;
        return (wait, climbs);
    }

    /// <summary>The landing came to anything else: landed, or refused for a cause that does not pass, which today's ways carry.</summary>
    internal static void End(string unit, string tip, bool landed)
    {
        if (landed) Waits.TryRemove(Key(unit, tip), out _);
        else if (Waits.TryGetValue(Key(unit, tip), out var was)) Waits[Key(unit, tip)] = was with { Code = Ended };
    }

    /// <summary>What still holds the repository <paramref name="tree"/> belongs to alone, as its holder said it; null where it is free (or only starts hold it).</summary>
    internal static string? StillHeld(string home, string tree) => TreeLock.RefusedBy(home, tree);

    /// <summary>
    /// The reporting line above a landing at <paramref name="repository"/> (<see cref="ReportingLine"/>), climbed for its <c>accept</c>. A role
    /// record that cannot be read holds the place's level for the person, as the line reads one.
    /// </summary>
    internal static LineWait Climb(string home, string repository, string? workspace, PlaceRiskBook risk)
    {
        try
        {
            return ReportingLine.Of(RoleCatalog.ReadHome(home).Slots, SupervisorKinds.Landing, new RolePlace(workspace, repository),
                new RuleWiderPlaceStore(home).Read(), "accept", risk).Climb(ownerClass: false);
        }
        catch (DriverException error)
        {
            return new(new LineLevel(LineLevels.Repository, null, LineHolders.Person, error.Message), []);
        }
    }

    /// <summary>Hold <paramref name="repository"/> as due while a landing in it is judged and made, so no tidy starts there meanwhile.</summary>
    public static IDisposable Lands(string home, string repository)
    {
        var key = $"{Homes.Full(home)}|{repository}";
        Landing.AddOrUpdate(key, 1, (_, count) => count + 1);
        return new Let(() => Landing.AddOrUpdate(key, 0, (_, count) => count - 1));
    }

    /// <summary>
    /// Whether a landing is due in <paramref name="repository"/>: one being judged and made now, or one waiting on a cause that passes that a look
    /// read within <see cref="Fresh"/>. The look's tidy does not start there while it is.
    /// </summary>
    public static bool Due(string home, string repository, DateTimeOffset now)
    {
        var full = Homes.Full(home);
        return Landing.GetValueOrDefault($"{full}|{repository}") > 0
            || Waits.Values.Any(wait => wait.Code != Ended && now - wait.Seen < Fresh
                && string.Equals(wait.Home, full, StringComparison.OrdinalIgnoreCase) && string.Equals(wait.Repository, repository, StringComparison.OrdinalIgnoreCase));
    }

    private static string Key(string unit, string tip) => $"{unit}\n{tip}";

    private sealed class Let(Action let) : IDisposable
    {
        private int _gone;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _gone, 1) == 0) let();
        }
    }
}
