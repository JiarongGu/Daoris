using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A home holding a run bound to a named workflow's graph version (FABRIC-CUTOVER1c; the cutover design §2, §3), and the units a pass reads for
/// it: the run's quests here and the branch units their sessions worked, each linked as <see cref="WorkUnits.Linked"/> links them.
/// </summary>
internal static class RunHome
{
    /// <summary>The repository every run here works in.</summary>
    public const string Repository = "engine";

    /// <summary>A named workflow's file holding one graph version, saved under the home; the version's digest, which a run on it binds.</summary>
    public static string SaveGraph(string home, string graph, string workflow = "docs")
    {
        var folder = WorkflowStore.FolderOf(home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(WorkflowStore.PathOf(home, workflow),
            $$"""{"id":"{{workflow}}","name":"Docs","versions":[{"version":1,"at":"2026-10-21T09:00:00Z","door":"terminal","schema":2,"graph":{{graph}}}]}""");
        var version = Assert.Single(new WorkflowFiles(home).Load(workflow)!.Read.Versions);
        Assert.Null(version.Problem);
        return version.Digest!;
    }

    /// <summary>A named workflow's file holding one steps version (schema 1): its digest.</summary>
    public static string SaveSteps(string home, string workflow = "plain")
    {
        Directory.CreateDirectory(WorkflowStore.FolderOf(home));
        File.WriteAllText(WorkflowStore.PathOf(home, workflow),
            $$"""{"id":"{{workflow}}","name":"Plain","versions":[{"version":1,"at":"2026-10-09T09:00:00Z","door":"terminal","steps":[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]}]}""");
        var version = Assert.Single(new WorkflowFiles(home).Load(workflow)!.Read.Versions);
        Assert.Null(version.Problem);
        return version.Digest!;
    }

    /// <summary>The run <paramref name="run"/> bound to v1 of <paramref name="workflow"/> at <paramref name="digest"/>.</summary>
    public static void Bind(string home, string run, string digest, DateTimeOffset at, string workflow = "docs", string? ask = null) =>
        Assert.True(new WorkflowRunFiles(home).Bind(new WorkflowRunBinding(run, Repository, "default", workflow, WorkflowLevels.Repository, at)
        {
            Version = 1, Digest = digest, Ask = ask,
        }));

    /// <summary>A quest of a chain, as the service answers it.</summary>
    public static QuestView Quest(string id, string status = "Taken", string? parent = null, string to = Repository, string? setUpIn = null,
        params QuestStepView[] then) =>
        new(id, "ask:a1", to, "The work", "Body.", status) { Parent = parent, SetUpIn = setUpIn, Then = then };

    /// <summary>A quest's unit, as the quest adapter reads it.</summary>
    public static WorkUnit QuestUnit(QuestView quest) => Assert.Single(QuestUnits.Read([quest], []));

    /// <summary>
    /// A branch unit a session serving <paramref name="quests"/> worked, opened at <paramref name="opened"/> on <paramref name="branch"/>, and the
    /// landings carrying it.
    /// </summary>
    public static WorkUnit Branch(string id, string branch, DateTimeOffset opened, IReadOnlyList<string> quests, params LandedBranch[] landings) =>
        new(id, WorkUnitKind.Branch, new WorkOwner(Repository, "default", SessionBranches.FileName, WorkRecord.DriverWriter), new WorkSubject(branch, null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Links = [.. quests.Select(quest => new WorkLink(WorkRelation.ProducedBy, QuestUnits.IdOf(quest)))],
            Records = new WorkUnitRecords(new GrownBranch(Repository, "default", branch, "main", "base", null, opened) { Unit = id }, landings, null),
        };

    /// <summary>A landing of <paramref name="unit"/>'s work on <paramref name="branch"/> at <paramref name="tip"/>, squashing <paramref name="squashed"/> where given.</summary>
    public static LandedBranch Landing(string unit, string branch, string tip, DateTimeOffset at, string quest, string? squashed = null) =>
        new(Repository, "default", branch, "main", tip, "s1", quest, null, at) { Units = [unit], Squashed = squashed };
}
