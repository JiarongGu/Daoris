namespace Daoris.Driver;

/// <summary>
/// An ask's delivery run as the runtime governs it: its subject is the ask's branch, which no session made, so its nodes observe the
/// branch at the tip a pass reads (<c>check</c>, <c>review</c>, <c>land</c>) and the run itself (<c>parts</c>); it has no quest and no chain.
/// </summary>
public static class AskRunScopes
{
    /// <summary>The unit and the scope of <paramref name="run"/>, a run bound to an ask (<see cref="AskRunKey.Is"/>); null once it ended, since an ask is delivered once.</summary>
    internal static GovernedUnit? Governed(
        GraphRun run, NodeGraph graph, string digest, NodeUnitsRead read, IReadOnlyList<QuestView> quests, IReadOnlyDictionary<string, WorkUnit> units,
        string identity, (Dictionary<string, NodeDeclaration> Declared, HashSet<string> Dispatchable, HashSet<string> Observable) declared)
    {
        if (run.Ended is not null) return null;
        var binding = run.Binding;
        var (ask, repository) = (binding.Ask!, binding.Repository);
        var (root, name) = (read.Roots.GetValueOrDefault(repository) ?? "", TrainBranch.Of(ask));
        var at = read.AskBranches?.GetValueOrDefault(run.Run);
        var grown = new GrownBranch(repository, binding.Workspace, name, at?.Line, at?.Base ?? "", null, binding.At) { Unit = AskRunKey.BranchUnit(run.Run) };
        var branch = new WorkUnit(
            grown.Unit!, WorkUnitKind.Branch, new WorkOwner(repository, binding.Workspace, "ask branch", WorkRecord.DriverWriter), new WorkSubject(name, null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "its ask's delivery"), [])
        {
            Records = new WorkUnitRecords(grown, [], null),
        };
        var self = new WorkUnit(
            run.Unit, WorkUnitKind.Run, new WorkOwner(repository, binding.Workspace, WorkflowRunBindings.Folder, WorkRecord.DriverWriter),
            new WorkSubject(binding.Run, null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "its graph runs"), []);

        // The handbacks its review reads are the quests the ask's plan gave this repository.
        var work = quests.Where(quest => AskWords.AskOf(quest.From) == ask && quest.SetUpIn is null
            && string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase)).ToList();
        var scope = new RunScope(run, self)
        {
            Quests = work,
            Branch = branch,
            Branches = [branch],
            AfterHanded = RunSubjects.AfterHanded(graph),
            Ask = ask,
            Parts = read.Parts?.GetValueOrDefault(ask)?.For(repository),
            Through = units.GetValueOrDefault(AskUnits.IdOf(ask))?.Records.Ask?.Through,
            Root = root,
            Identity = identity,
        };
        return new GovernedUnit(self, graph, digest, run.Run, declared.Declared, declared.Dispatchable,
            new WatchedUnit(repository, binding.Workspace, root, name, "", null, null, []) { Unit = branch.Id, Identity = identity })
        {
            Observable = declared.Observable, Scope = scope,
        };
    }
}
