using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A run is never ended by its own judgement while a branch its home records for it still holds commits the line does not hold: its landing is
/// still owed, and the end (which closes its quest and ends its session) would drop the work with nothing said. The in-place end
/// (<see cref="InPlaceRunEnds"/>) reads "no branch of it stands" from what one look read of the run's branches, which is empty while a session's
/// tree is being rebased or its unit is not read, so it asks the home's record of the run's session branches as well, whose commits the line
/// lacks (<see cref="NodeRuntimeLook.Lacks"/>). A held end is said as an error line (<see cref="Event"/>) naming the branch and the commits, once per
/// change, and is judged again at the next look, which lands the work once the branch reads again.
/// </summary>
internal static class UnlandedRunEnds
{
    /// <summary>The machine log's event a held end is written under, at the error level.</summary>
    public const string Event = "run.end.unlanded";

    // The last sentence said for each run, by home and run.
    private static readonly ConcurrentDictionary<string, string> Last = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The standing branches the home records for the run, whether or not this look read their units.</summary>
    public static IReadOnlyList<GrownBranch> Owned(string home, RunScope scope) =>
        [.. new SessionBranches(home).All().Where(entry => string.Equals(entry.Run, scope.Run.Run, StringComparison.Ordinal)
            && string.Equals(entry.Repository, scope.Run.Binding.Repository, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// The branches among <paramref name="owned"/> whose commits the line lacks, with those commits. A branch git says is not there holds nothing to
    /// land; one whose tip git could not read, or whose lacking commits it could not say, is held with none named, since an end that cannot tell
    /// loses work where a held one is judged again.
    /// </summary>
    public static async Task<IReadOnlyList<(string Branch, IReadOnlyList<LandingCommit>? Commits)>> HeldAsync(
        IReadOnlyList<GrownBranch> owned, IReadOnlyDictionary<string, string> heads,
        Func<string, CancellationToken, Task<IReadOnlyList<LandingCommit>?>> lacks, CancellationToken ct)
    {
        var held = new List<(string, IReadOnlyList<LandingCommit>?)>();
        foreach (var entry in owned)
        {
            if (!heads.TryGetValue(entry.Branch, out var tip))
            {
                held.Add((entry.Branch, null));
                continue;
            }

            if (tip.Length == 0) continue;
            var commits = await lacks(tip, ct).ConfigureAwait(false);
            if (commits is null || commits.Count > 0) held.Add((entry.Branch, commits));
        }

        return held;
    }

    /// <summary>
    /// Whether the end is held: a branch the home records for the run holds commits the line lacks. The branches' tips are read fresh
    /// (<paramref name="heads"/>, never the pass's cached read, which holds only the branches the look governed). It is said to <paramref name="say"/>
    /// as an error line once per change, where the caller holds the home's lease.
    /// </summary>
    public static async Task<bool> HoldsAsync(
        string home, RunScope scope, Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> heads,
        Func<string, CancellationToken, Task<IReadOnlyList<LandingCommit>?>> lacks, Action<string, string>? say, CancellationToken ct)
    {
        var owned = Owned(home, scope);
        if (owned.Count == 0) return false;
        var tips = await heads(scope.Root, [.. owned.Select(entry => entry.Branch)], ct).ConfigureAwait(false);
        var held = await HeldAsync(owned, tips, lacks, ct).ConfigureAwait(false);
        if (held.Count == 0) return false;
        var sentence = Sentence(scope.Run.Run, held);
        if (say is not null && Changed(home, scope.Run.Run, sentence)) say(Event, sentence);
        return true;
    }

    /// <summary>The error line for a held end: the run, and each branch with the commits it holds that the line does not.</summary>
    public static string Sentence(string run, IReadOnlyList<(string Branch, IReadOnlyList<LandingCommit>? Commits)> held) =>
        $"run {run}: its end was not written, since its landing is still owed, so its quest stays taken and its session goes on: "
        + string.Join("; ", held.Select(each => QuestDoneRuns.Left(each.Branch, each.Commits)))
        + ". It is judged again at the next look and lands once its branch reads again.";

    /// <summary>Whether <paramref name="sentence"/> is not the last one said for the run, which it then is.</summary>
    public static bool Changed(string home, string run, string sentence)
    {
        var key = $"{home}\n{run}";
        if (string.Equals(Last.GetValueOrDefault(key), sentence, StringComparison.Ordinal)) return false;
        Last[key] = sentence;
        return true;
    }
}
