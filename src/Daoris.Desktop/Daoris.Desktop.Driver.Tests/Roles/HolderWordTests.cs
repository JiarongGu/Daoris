using System.Text.Json;
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

    /// <summary>A named step reads <c>you</c> as the runtime does: the holder its words cast is the one its attempt's tenure begins with.</summary>
    [Theory]
    [InlineData("you")]
    [InlineData("automatic")]
    public void A_named_steps_you_is_held_by_the_person_through_its_tenure(string start)
    {
        new PlaceCastFiles(_home, () => true).Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        var graph = Start(start);

        var named = Assert.Single(Involvement.StepsOf(graph), step => step.Kind == NodeKinds.Request);
        var begun = Assert.Single(AttemptTenures.Keep(Records, RunUnits.IdOf(Run), graph.Nodes, "work", null, Risk(), _now));

        Assert.Equal(begun.Holder, HolderWords.First(named) ?? "agent:alpha");
        Assert.Equal(start == "you" ? CastHolders.Person : "agent:alpha", begun.Holder);
    }

    /// <summary>The named steps' reading, gate and diff each say the person's part where a step's words cast the person, and none where they do not.</summary>
    [Fact]
    public void The_named_readers_read_you_as_the_holder_it_casts()
    {
        static NamedStep Stage(string start) =>
            new("deploy", WorkflowKinds.Stage, [new("plugin", "example.cloud"), new("stage", "staging"), new("start", start)]);
        static NamedStep Landing(string accept) => new("landing", WorkflowKinds.Landing, [new("form", LandingForm.Merge), new("accept", accept)]);
        IReadOnlyList<NamedStep> Version(string word) => [new("work", WorkflowKinds.Work, []), Landing(word), Stage(word)];

        Assert.Equal(CastHolders.Person, HolderWords.First(Stage("you")));
        Assert.Null(HolderWords.First(Landing("automatic")));

        Assert.Equal(
            ["it lands with no press of yours", "it starts `staging` with no press of yours"],
            Involvement.Lowers(Version("automatic"), Version("you")));
        Assert.Empty(Involvement.Lowers(Version("you"), Version("automatic")));

        var diff = WorkflowDiff.Of(Version("you"), Version("automatic")).Part;
        Assert.Contains("− landing: you no longer accept each piece of work before it lands.", diff);
        Assert.Contains("− deploy: you no longer start `staging`.", diff);
        Assert.Empty(WorkflowDiff.Of(Version("automatic"), Version("automatic")).Part);

        using var you = JsonDocument.Parse("""
            [{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"branch","accept":"you"},
             {"id":"ahead","kind":"go-ahead","act":"deploy to staging","on":"staging"},
             {"id":"deploy","kind":"stage","plugin":"example.cloud","stage":"staging","start":"you"}]
            """);
        Assert.Equal(
            "v1, step `ahead`: a go-ahead straight before a stage you start is not needed: starting it is your go-ahead.",
            NamedStepReading.ReadShape(you.RootElement, 1).Problem);
        using var automatic = JsonDocument.Parse(you.RootElement.GetRawText().Replace("\"start\":\"you\"", "\"start\":\"automatic\"", StringComparison.Ordinal));
        Assert.Null(NamedStepReading.ReadShape(automatic.RootElement, 1).Problem);
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
