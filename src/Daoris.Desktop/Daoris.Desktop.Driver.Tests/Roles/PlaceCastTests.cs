using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The place casts every role: each role's holder now and why, the cast journaled with its reason; and a step's holder: a tenure's first
/// epoch written when an attempt starts, <i>Take</i>, <i>Hand to…</i> and <i>Give back</i> each a new
/// epoch journaled with its reason, and a word from an ended epoch refused by the door that settles the step.
/// </summary>
public sealed class PlaceCastTests : IDisposable
{
    private const string Run = "q-cast";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-cast-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private PlaceCastFiles Casts => new(_home, () => true);

    private NodeRecordFiles Records => new(_home, () => true);

    /// <summary>A run bound to a graph whose person's task names the release manager, its task under way at its first attempt.</summary>
    private NodeGraph Start()
    {
        var digest = RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"release",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"release","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"release-manager"}],
             "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"}]}
            """);
        RunHome.Bind(_home, Run, digest, _now);
        var unit = RunUnits.IdOf(Run);
        Records.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = NodeLifecycle.Start(unit, "claimed") with { State = NodeStates.Reached, Attempt = 1, Since = _now },
                ["release"] = NodeLifecycle.Start(unit, "release") with { State = NodeStates.Active, Attempt = 1, Since = _now },
            },
        });
        return new GraphRuns(Records).Find(Run)!.Version.Graph!;
    }

    private static PlaceRiskBook Risk(string risk = PlaceRisks.Low, params string[] marks) =>
        PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = risk, Mark = marks });

    [Fact]
    public void Each_role_reads_its_holder_now_and_why_and_a_cast_is_journaled_with_its_reason()
    {
        var low = Risk().At("work", "reports");
        var empty = Casts.Book();
        Assert.Equal((CastHolders.Person, CastWhy.Default), (empty.At(StepRoles.Acceptor, "work", "reports", low) is var a ? (a.Holder, a.Why) : default));
        Assert.Equal((CastHolders.Agent, CastWhy.Staffed), (empty.At(StepRoles.Design, "work", "reports", low) is var d ? (d.Holder, d.Why) : default));
        Assert.Equal(CastWhy.Owner, empty.At(StepRoles.Owner, "work", "reports", low).Why);

        Assert.Null(Casts.Cast("workspace/work", StepRoles.Acceptor, "agent:codex", "low-risk docs", _now));
        var cast = Casts.Book().At(StepRoles.Acceptor, "work", "reports", low);
        Assert.Equal(("agent:codex", CastWhy.Cast, PlaceRiskBook.WorkspaceSource, "low-risk docs"), (cast.Holder, cast.Why, cast.Source, cast.Words));
        var entry = Assert.Single(Casts.Journal());
        Assert.Equal((CastActs.Cast, "workspace/work", StepRoles.Acceptor, "agent:codex", "low-risk docs"), (entry.Act, entry.Place, entry.Role, entry.Holder, entry.Why));

        // High risk, and the owner's mark on the approver, keep the role the person's whatever the cast says.
        Assert.Null(Casts.Cast("workspace/work", StepRoles.Approver, "agent:codex", null, _now));
        Assert.Equal(CastWhy.Risk, Casts.Book().At(StepRoles.Acceptor, "work", null, Risk(PlaceRisks.High).At("work", null)).Why);
        Assert.Equal(CastWhy.Marked, Casts.Book().At(StepRoles.Approver, "work", null, Risk(PlaceRisks.Low, PlaceMarks.ProductionReleases).At("work", null)).Why);

        // A holder no channel names and the owner role are refused, nothing journaled; no holder takes the cast away.
        Assert.Equal(PlaceCastFiles.NotHolder, Casts.Cast("workspace/work", StepRoles.Tester, "you", null, _now));
        Assert.Equal(PlaceCastFiles.OwnerRole, Casts.Cast("workspace/work", StepRoles.Owner, CastHolders.Agent, null, _now));
        Assert.Null(Casts.Cast("workspace/work", StepRoles.Acceptor, null, "back to me", _now));
        Assert.Equal(CastWhy.Default, Casts.Book().At(StepRoles.Acceptor, "work", "reports", low).Why);
        Assert.Equal(3, Casts.Journal().Count);
    }

    [Fact]
    public void The_view_says_every_role_the_risk_the_marks_the_roster_and_the_line()
    {
        Casts.Cast("machine", StepRoles.Tester, "agent:codex", "tries it first", _now);
        var view = PlaceCastView.Of(Casts.Book(), Risk(PlaceRisks.Medium, PlaceMarks.ProductionReleases), "work", null, ["codex", "claude-code"], slots: null);

        Assert.Equal(("workspace/work", PlaceRisks.Medium, PlaceRiskBook.OwnSource), (view.Place, view.Risk, view.RiskSource));
        Assert.Equal([PlaceMarks.ProductionReleases], view.Marks);
        Assert.Equal(StepRoles.All, view.Roles.Select(role => role.Role));
        Assert.Equal(("agent:codex", PlaceRiskBook.MachineSource), view.Roles.Single(role => role.Role == StepRoles.Tester) is var t ? (t.Holder, t.Source) : default);
        Assert.Equal([CastHolders.Person, CastHolders.Agent, "agent:claude-code", "agent:codex"], view.Roster);
        Assert.Equal(LineLevels.Owner, Assert.Single(view.Line).Level);
    }

    [Fact]
    public async Task An_attempt_starts_held_by_its_cast_and_take_hand_and_give_back_each_begin_an_epoch_journaled_and_refuse_the_ended_word()
    {
        var graph = Start();
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", "the release path", _now);
        var unit = RunUnits.IdOf(Run);

        // The attempt's start: its first epoch, cast to the place's holder; again, nothing new.
        var begun = Assert.Single(AttemptTenures.Keep(Records, unit, graph.Nodes, "work", null, Risk(), _now));
        Assert.Equal((1, "agent:alpha", TenureWhy.Cast, StepRoles.ReleaseManager), (begun.Epoch, begun.Holder, begun.Why, begun.Role));
        Assert.Empty(AttemptTenures.Keep(Records, unit, graph.Nodes, "work", null, Risk(), _now));
        await Task.CompletedTask;

        var taken = StepHolding.Act(Records, Casts, Risk(), Run, "release", CastActs.Take, null, "I'll do this one", _now.AddMinutes(1));
        Assert.Equal((true, 2, CastHolders.Person), (taken.Done, taken.Tenure!.Epoch, taken.Tenure.Holder));
        Assert.Equal(StepHolding.Already, StepHolding.Act(Records, Casts, Risk(), Run, "release", CastActs.Take, null, null, _now).Refused);

        // A word from the cast holder's ended epoch is refused through the door that settles the step, naming who holds it now.
        var refused = RunNodeWords.Word(Records, Run, "release", NodeWordActs.Stop, "from alpha", () => _now, epoch: 1, settle: new SettleBy("agent:alpha", 1));
        Assert.Equal(StepTenureFiles.EndedEpoch, refused.Refused);
        Assert.Contains("person", refused.Detail, StringComparison.Ordinal);

        var handed = StepHolding.Act(Records, Casts, Risk(), Run, "release", CastActs.Hand, "agent:beta", "beta knows the pipeline", _now.AddMinutes(2));
        var back = StepHolding.Act(Records, Casts, Risk(), Run, "release", CastActs.GiveBack, null, null, _now.AddMinutes(3));
        Assert.Equal((3, "agent:beta", 4, "agent:alpha", TenureWhy.GiveBack), (handed.Tenure!.Epoch, handed.Tenure.Holder, back.Tenure!.Epoch, back.Tenure.Holder, back.Tenure.Why));
        Assert.Equal([CastActs.Take, CastActs.Hand, CastActs.GiveBack], StepHoldActs.Of(handed.Tenure));
        Assert.Equal([CastActs.Hand, CastActs.GiveBack], StepHoldActs.Of(taken.Tenure));

        var journal = Casts.Journal().Where(entry => entry.Run == Run).ToList();
        Assert.Equal([(CastActs.Take, 2, "I'll do this one"), (CastActs.Hand, 3, "beta knows the pipeline"), (CastActs.GiveBack, 4, (string?)null)],
            journal.Select(entry => (entry.Act, entry.Epoch!.Value, entry.Why)));

        Assert.Equal(PlaceCastFiles.NotHolder, StepHolding.Act(Records, Casts, Risk(), Run, "release", CastActs.Hand, "you", null, _now).Refused);
        Assert.True(RunNodeWords.Word(Records, Run, "release", NodeWordActs.Stop, "from alpha", () => _now, epoch: 4).Done);
    }
}
