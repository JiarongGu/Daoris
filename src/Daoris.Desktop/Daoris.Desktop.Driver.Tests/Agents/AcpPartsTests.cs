using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The protocol door hands each frame to the stages its modules register, phase by phase in their order, each built from the
/// session as its wiring; an order the marks cannot make, or a stage the session cannot build, is refused at its first frame and
/// never part-way through one.
/// </summary>
public sealed class AcpPartsTests
{
    [Fact]
    public void The_session_runs_the_registered_stages_in_their_order_each_built_from_the_session()
    {
        var session = new AcpSession(new StringReader(""), TextWriter.Null, _ => { });

        var order = session.TurnStages.Order;
        Assert.Equal(["replay", "watch", "heard", "subagent", "measure", "render", "permission"], order.Select(part => part.Name));
        Assert.Equal(
            [.. Enumerable.Repeat(TurnPhase.Update, 6), TurnPhase.Request], order.Select(part => (TurnPhase)part.Phase));

        // What each anchor runs, in the order it runs them: a replay is passed over first, a subagent's update is routed away before
        // the session measures and renders its own.
        Assert.Collection(
            session.TurnStages.In(TurnPhase.Update),
            stage => Assert.IsType<AcpReplay>(stage),
            stage => Assert.IsType<AcpWatch>(stage),
            stage => Assert.IsType<AcpHeard>(stage),
            stage => Assert.IsType<AcpSubagent>(stage),
            stage => Assert.IsType<AcpMeasure>(stage),
            stage => Assert.IsType<AcpRender>(stage));
        Assert.IsType<AcpPermission>(Assert.Single(session.TurnStages.In(TurnPhase.Request)));

        // Built once per session: the root reads a module's other doors from the same stage the frames pass.
        Assert.Same(session.TurnStages, session.TurnStages);
        Assert.Null(session.Usage);
    }

    [Fact]
    public void The_units_the_session_hands_its_work_to_are_built_on_first_use_and_stand_idle_before_a_run()
    {
        var session = new AcpSession(new StringReader(""), TextWriter.Null, _ => { });

        Assert.False(session.WaitsOnOwnWork);
        Assert.False(session.WaitsOnTransient);
        Assert.Empty(session.ConfigOptions);
        Assert.Empty(session.OpeningOptions);
        Assert.Null(session.NotOffered);
        Assert.Null(session.CarriesOn);
        Assert.Null(session.LastAcknowledgedOptions);
    }

    [Fact]
    public void A_phase_runs_each_stage_after_the_ones_it_names_and_a_cycle_is_refused_by_its_members()
    {
        var order = PartOrder.Of(
        [
            Declared("permission", TurnPhase.Request),
            Declared("audit", TurnPhase.Update, "render"),
            Declared("render", TurnPhase.Update, "measure"),
            Declared("measure", TurnPhase.Update),
        ]);
        Assert.Equal(["measure", "render", "audit", "permission"], order.Select(part => part.Name));

        var cycle = Assert.Throws<PartOrderException>(() => PartOrder.Of(
            [Declared("measure", TurnPhase.Update, "render"), Declared("render", TurnPhase.Update, "measure"), Declared("permission", TurnPhase.Request)]));
        Assert.Equal("the parts measure → render → measure come after each other, so none of them can run first", cycle.Message);
    }

    [Fact]
    public void An_update_stage_after_a_request_stage_and_a_stage_its_session_cannot_build_are_refused()
    {
        Assert.Equal(
            "the part render runs in Update and comes after permission, which runs later, in Request",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("render", TurnPhase.Update, "permission"), Declared("permission", TurnPhase.Request)])).Message);

        // A constructor asking for wiring the session does not hand fails at the first frame, naming the stage.
        var unbuilt = Assert.Throws<PartOrderException>(() => Parts.Build<ITurnStage>(
            [Declared("render", TurnPhase.Update)], type => (ITurnStage)Activator.CreateInstance(type, new object())!));
        Assert.StartsWith("the part render (AcpRender) could not be built from its root's wiring: ", unbuilt.Message);
    }

    private static PartDeclaration Declared(string name, TurnPhase phase, params string[] after) =>
        new(name, phase, after, typeof(AcpRender));
}
