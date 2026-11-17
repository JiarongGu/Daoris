using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>you</c> is a holder word: a step whose option says it (a request's <c>start</c>, a landing's <c>accept</c>) is first held by the person
/// through its tenure whatever the place casts its role to, and the settle door admits the person's word; no runtime code branches on it.
/// </summary>
public sealed class HolderWordTests : IDisposable
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

    /// <summary>One rule casts every first epoch, the pass's and a hold act's alike, and <i>Give back</i> returns the step to the holder it names.</summary>
    [Fact]
    public void A_you_step_handed_on_is_given_back_to_the_person_its_word_names()
    {
        var casts = new PlaceCastFiles(_home, () => true);
        casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start("you");

        Assert.True(StepHolding.Act(Records, casts, Risk(), Run, "ask", CastActs.Hand, "agent:beta", null, _now.AddMinutes(1)).Done);
        var tenures = new StepTenureFiles(_home, () => true);
        Assert.Equal(CastHolders.Person, tenures.Of(RunUnits.IdOf(Run), "ask", 1)[0].Holder);

        var back = StepHolding.Act(Records, casts, Risk(), Run, "ask", CastActs.GiveBack, null, null, _now.AddMinutes(2));
        Assert.True(back.Done, back.Refused);
        Assert.Equal((CastHolders.Person, TenureWhy.GiveBack), (back.Tenure!.Holder, back.Tenure.Why));
    }
}
