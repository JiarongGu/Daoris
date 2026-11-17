using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>you</c> is a holder word: a step whose option says it (a request's <c>start</c>, a landing's <c>accept</c>) is first held by the person
/// through its tenure whatever the place casts its role to, and the settle door admits the person's word; no runtime code branches on it.
/// </summary>
public sealed partial class HolderWordTests : IDisposable
{
    private const string Run = "q-you";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-you-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private NodeRecordFiles Records => new(_home, () => true);

    private NodeGraph Start(string start)
    {
        var digest = RunHome.SaveGraph(_home, $$$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"ask",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"ask","kind":"request","version":1,"reach":"waited","role":"release-manager",
                       "options":{"to":"other","title":"t","body":"b","start":"{{{start}}}"}}],
             "wires":[{"id":"c>a","kind":"after","from":"claimed","to":"ask"}]}
            """);
        RunHome.Bind(_home, Run, digest, _now);
        var unit = RunUnits.IdOf(Run);
        Records.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = NodeLifecycle.Start(unit, "claimed") with { State = NodeStates.Reached, Attempt = 1, Since = _now },
                ["ask"] = NodeLifecycle.Start(unit, "ask") with { State = NodeStates.Active, Attempt = 1, Since = _now },
            },
        });
        return new GraphRuns(Records).Find(Run)!.Version.Graph!;
    }

    private static PlaceRiskBook Risk() => PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.Low });

    [Theory]
    [InlineData("you", CastHolders.Person)]
    [InlineData("automatic", "agent:alpha")]
    public void A_step_saying_you_is_first_held_by_the_person_and_one_that_does_not_by_its_cast(string start, string holder)
    {
        new PlaceCastFiles(_home, () => true).Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        var graph = Start(start);

        var begun = Assert.Single(AttemptTenures.Keep(Records, RunUnits.IdOf(Run), graph.Nodes, "work", null, Risk(), _now));

        Assert.Equal((holder, StepRoles.ReleaseManager), (begun.Holder, begun.Role));
        var current = new StepTenureFiles(_home, () => true).Current(RunUnits.IdOf(Run), "ask", 1);
        if (start == "you") Assert.Null(SettleDoor.Admit(current, SettleBy.Person(current!.Epoch)));
    }

    /// <summary>The word is read in one place, which sets the first holder; no runtime source compares against it.</summary>
    [Fact]
    public void No_runtime_source_branches_on_you_beyond_the_holder_it_sets()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var branching = new List<string>();
        foreach (var folder in new[] { "Workflows/Kinds", "Workflows/Nodes", "Roles" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(driver, folder), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "HolderWords.cs") continue;
                var text = File.ReadAllText(file);
                if (Compared().IsMatch(text)) branching.Add(Path.GetRelativePath(driver, file));
            }
        }

        Assert.Empty(branching);
        Assert.Contains("HolderWords.First", File.ReadAllText(Path.Combine(driver, "Roles", "Cast", "AttemptTenures.cs")), StringComparison.Ordinal);
    }

    [GeneratedRegex("""(==|!=|is)\s*(RequestOptions\.You|HolderWords\.You|"you")|("you"|RequestOptions\.You)\s*(==|!=)""")]
    private static partial Regex Compared();
}
