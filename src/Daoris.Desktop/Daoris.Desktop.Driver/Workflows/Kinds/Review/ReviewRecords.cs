using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A run's review records, each at <c>workflows/units/&lt;key&gt;.reviews/&lt;node&gt;-&lt;attempt&gt;.json</c> beside the run's record, as the
/// return's journal is: written once, when the review's attempt begins, and never edited, so an Accept names exactly what its review read.
/// </summary>
public static class ReviewRecords
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string FolderOf(string home, string run) =>
        Path.Combine(NodeRecordFiles.FolderOf(home), NodeRecordFiles.KeyOf(RunUnits.IdOf(run)) + ".reviews");

    public static string PathOf(string home, string run, string node, int attempt) =>
        Path.Combine(FolderOf(home, run), $"{string.Concat(node.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_'))}-{attempt}.json");

    /// <summary>Keep <paramref name="record"/> where its attempt has none; false where it has one already, which is never replaced.</summary>
    public static bool Keep(string home, ReviewRecord record)
    {
        Directory.CreateDirectory(FolderOf(home, record.Run));
        return AtomicFile.TryCreateText(PathOf(home, record.Run, record.Node, record.Attempt), JsonSerializer.Serialize(record, Json) + "\n");
    }

    public static ReviewRecord? Read(string home, string run, string node, int attempt)
    {
        var file = PathOf(home, run, node, attempt);
        return File.Exists(file) ? Parse(file) : null;
    }

    /// <summary>Every record of <paramref name="run"/> that reads, by node and then attempt.</summary>
    public static IReadOnlyList<ReviewRecord> Of(string home, string run)
    {
        var folder = FolderOf(home, run);
        return Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.json").Select(Parse).OfType<ReviewRecord>().OrderBy(record => record.Node, StringComparer.Ordinal).ThenBy(record => record.Attempt)]
            : [];
    }

    /// <summary>
    /// The manifest records every review record in <paramref name="home"/> reads from, its baseline or base and its newest end: the names a
    /// place's manifest tidy keeps (<see cref="ManifestStore.Tidy"/>), so their blobs survive its sweep.
    /// </summary>
    public static IReadOnlySet<string> Manifests(string home)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var units = NodeRecordFiles.FolderOf(home);
        if (!Directory.Exists(units)) return names;
        foreach (var file in Directory.EnumerateFiles(units, "*.json", SearchOption.AllDirectories).Where(file => Path.GetFileName(Path.GetDirectoryName(file)!).EndsWith(".reviews", StringComparison.Ordinal)))
        {
            if (Parse(file)?.Reference is not { } reference) continue;
            foreach (var name in new[] { reference.Baseline, reference.End }.OfType<string>()) names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// After a run's rounds, each review active on an attempt that has no record yet is given one, read now: kept only where what is read is
    /// still what its binding holds, so a record never names work its attempt is not bound to; a moved one lapses the binding instead.
    /// </summary>
    public static void KeepActive(string home, GovernedUnit governed, RunScope scope, NodeRecord record, DateTimeOffset at)
    {
        foreach (var node in governed.Declared.Values.Where(node => node.Kind == NodeKinds.Review))
        {
            if (WorkPerformer.StatusOf(record, node.Id) is not { State: NodeStates.Active, Binding: { } binding }) continue;
            if (ReviewWork.OnBranch(scope) && scope.Branch is null) continue;
            var run = scope.Run.Binding.Run;
            if (File.Exists(PathOf(home, run, node.Id, binding.Attempt))) continue;
            var work = ReviewWork.Read(home, scope, binding.SubjectRevision);
            if (work.Kind != ReviewKinds.Branch && work.Revision != binding.SubjectRevision) continue;
            Keep(home, work.Compose(home, run, node.Id, binding.Attempt, at));
        }
    }

    private static ReviewRecord? Parse(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<ReviewRecord>(AtomicFile.ReadText(file), Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
