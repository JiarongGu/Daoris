using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The machine's load at a check's failure, as the admission reads it: the other heavy runs holding a place and the free commit.
/// </summary>
/// <param name="Others">The live heavy runs holding a place other than this check's own.</param>
/// <param name="FreeCommit">The machine's free commit in bytes, null where it cannot be read.</param>
internal sealed record LoadReading(int Others, long? FreeCommit, long Need)
{
    /// <summary>Loaded: another heavy run holds a place, or the free commit is below what one more run needs (<see cref="Need"/>, <see cref="CheckAdmission.QuietNeed"/>).</summary>
    internal bool Loaded => Others > 0 || FreeCommit is { } free && free < Need;

    /// <summary>The reading as a clause: the count, and the free commit where it reads.</summary>
    internal string Words => string.Create(CultureInfo.InvariantCulture,
        $"{Others} other heavy run{(Others == 1 ? "" : "s")}{(FreeCommit is { } free ? $", {Math.Floor(free * 10.0 / (1L << 30)) / 10:F1} GB free commit" : "")}");
}

/// <summary>
/// A failure that may be the machine's load and not the work's: classes of a reached run that failed while the machine ran other
/// heavy runs, or had little free commit. Such a failure is never sent back to the work as its own and never spends a round: its red
/// classes run again alone, at a quiet slot, before the result counts. Passed alone, the check is reached and says load; red alone
/// it is the work's, as any red; no quiet slot within the bound, it is unknown with <see cref="VerifyResult.Load"/>, asked again as
/// a check that could not say is (<see cref="UnrunChecks"/>).
/// </summary>
internal static class LoadFailures
{
    /// <summary>What a place taken to run the classes again alone ends its words with, so the quiet slot's wait can tell its holders from a run that works.</summary>
    internal const string Mark = "(quiet rerun)";

    private static readonly Regex Timeout = new(
        @"timed out in \d+\s*ms|Test timed out|Timeout of \d+\s*ms exceeded|exceeded timeout of \d+\s*ms", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex HostHung = new(@"inactivity time of \d+ seconds has elapsed", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Safe = new(@"^[A-Za-z0-9][A-Za-z0-9._/\\-]*$", RegexOptions.CultureInvariant);

    /// <summary>Whether a text says a test timed out, or its test host hit the hang bound.</summary>
    internal static bool TimedOut(string text) => Timeout.IsMatch(text) || HostHung.IsMatch(text);

    /// <summary>
    /// The names of the red classes a log's failure is load for, or null where it is not: each red class's own failure says a test timed out, or
    /// the test host hit the hang bound, which no class's lines name. One red class that failed on anything else is the work's, and the
    /// failure stands whole.
    /// </summary>
    internal static IReadOnlyList<string>? Timeouts(string log, string marker)
    {
        var red = ReachedVerdict.RedOf(log, marker);
        if (red.Count == 0) return null;
        return HostHung.IsMatch(log) || red.All(id => ReachedVerdict.LinesOf(log, id).Any(Timeout.IsMatch)) ? Names(red) : null;
    }

    internal static IReadOnlyList<string>? Red(string log, string marker) => Names(ReachedVerdict.RedOf(log, marker));

    /// <summary>The names the red classes are run again by, or null where there are none or one is no plain name, which is never put in a command.</summary>
    internal static IReadOnlyList<string>? Names(IReadOnlyList<string> red)
    {
        if (red.Count == 0) return null;
        var names = red.Select(ReachedVerdict.NameOf).Distinct(StringComparer.Ordinal).ToList();
        return names.All(name => Safe.IsMatch(name)) ? names : null;
    }

    /// <summary>The load now: the live places held that do work, but <paramref name="except"/>'s, and the free commit. A place only waiting for its quiet slot is no load.</summary>
    internal static LoadReading Read(string home, VerifyWorld world, string? except = null)
    {
        var look = MachineAdmission.Look(home, world.Admission.Alive, world.Admission.Clock(), world.Capacity);
        return new LoadReading(look.Holds.Count(hold => hold.Id != except && !hold.What.EndsWith(Mark, StringComparison.Ordinal)), world.Memory()?.Commit, CheckAdmission.QuietNeed(home));
    }

    /// <summary>
    /// Whether the run holding <paramref name="mine"/> may run now: no other run holds a place that works, and the free commit is what one run
    /// needs. A run also waiting for its quiet slot goes by the order it asked in, so two never wait on each other, and the earlier goes first.
    /// </summary>
    internal static bool Quiet(AdmissionLook look, AdmissionEntry mine, long? freeCommit, long need) =>
        look.Holds.All(hold => hold.Id == mine.Id || hold.What.EndsWith(Mark, StringComparison.Ordinal) && (hold.Since, hold.Id).CompareTo((mine.Since, mine.Id)) > 0)
        && (freeCommit is not { } free || free >= need);

    private static string Classes(IReadOnlyList<string> names) => string.Join(", ", names);

    /// <summary>What the record says of classes that failed under load and passed run again alone.</summary>
    internal static string Passed(IReadOnlyList<string> names, LoadReading seen, LoadReading quiet) =>
        $"load: {Classes(names)} failed while the machine was loaded ({seen.Words}) and passed run again alone at a quiet slot ({quiet.Words}); "
        + "a load failure is no failure of the work and counted no round";

    /// <summary>What the record says of classes that failed under load and failed again run alone at a quiet slot: the work's.</summary>
    internal static string Failed(IReadOnlyList<string> names, LoadReading seen) =>
        $"{Classes(names)} failed while the machine was loaded ({seen.Words}) and failed again run alone at a quiet slot, so the failure is the work's";

    /// <summary>What the record says of classes that failed under load and timed out again run alone while the machine read loaded again.</summary>
    internal static string Again(IReadOnlyList<string> names, LoadReading seen, LoadReading now) =>
        $"load: {Classes(names)} failed while the machine was loaded ({seen.Words}) and timed out again run alone, the machine loaded again ({now.Words}); "
        + "a load failure is no failure of the work and counts no round, so the check is asked again";

    /// <summary>What the record says where the classes could not be run again as declared.</summary>
    internal static string Empty(IReadOnlyList<string> names, LoadReading seen) =>
        $"load: {Classes(names)} failed while the machine was loaded ({seen.Words}) and their rerun ran no class, so nothing says they pass; "
        + "a load failure is no failure of the work and counts no round, so the check is asked again";

    /// <summary>What the record says of classes that failed under load while no quiet slot came.</summary>
    internal static string Waited(IReadOnlyList<string> names, LoadReading seen, TimeSpan waited) =>
        $"load: {Classes(names)} failed while the machine was loaded ({seen.Words}) and no quiet slot came in {(int)waited.TotalMinutes} min to run them again alone; "
        + "a load failure is no failure of the work and counts no round, so the check is asked again";
}
