namespace Daoris.Driver;

/// <summary>
/// The verification runs the history clear may take (the pipeline design §4.3, D153): a landed run's file, folder and node record once a newer
/// run on its line has a verdict, and a person's run once it has settled. Never the newest run with a verdict, which the next batch's base and
/// the line's read stand on, and never one unsettled. No quest holds a run under <c>lines/</c>, so the clear that takes a chain's binding
/// when its quest goes would never take these.
/// </summary>
internal static class LandedRunsLeft
{
    /// <summary>Each file or folder of a run the clear may take: its binding, its folder, and its node record.</summary>
    internal static IReadOnlyList<(string Path, bool Folder)> Of(string home)
    {
        var runs = VerifyRuns.All(home);
        if (runs.Count == 0) return [];
        var records = new NodeRecordFiles(home);
        var files = new WorkflowFiles(home);
        string? Settled(VerifyRun run) => LineBatching.Settled(records.Read(RunUnits.IdOf(run.Run)), LineBatching.ChecksOf(files, run));

        var going = runs.Where(run => run.Level == VerifyRuns.Person && Settled(run) is not null).ToList();
        foreach (var line in runs.Where(run => run.Level != VerifyRuns.Person && run.Line is not null)
                     .GroupBy(run => (run.Repository.ToLowerInvariant(), run.Line)))
        {
            var verdict = false;
            foreach (var run in LineBatching.On(home, line.First().Repository, line.Key.Line!))
            {
                var settled = Settled(run);
                if (verdict) { if (settled is not null) going.Add(run); }
                else if (settled is not null && settled != LineVerdicts.NotNow) verdict = true;
            }
        }

        var found = new List<(string Path, bool Folder)>();
        foreach (var run in going)
        {
            AddIf(found, VerifyRuns.PathOf(home, run.Run), folder: false);
            AddIf(found, VerifyFiles.RunFolder(home, run.Run), folder: true);
            AddIf(found, NodeRecordFiles.PathOf(home, RunUnits.IdOf(run.Run)), folder: false);
        }

        return found;
    }

    private static void AddIf(List<(string Path, bool Folder)> found, string path, bool folder)
    {
        if (folder ? Directory.Exists(path) : File.Exists(path)) found.Add((path, folder));
    }
}
