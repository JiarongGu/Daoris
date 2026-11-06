using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A kind's reading handed the one read it takes, as the pass hands it (<see cref="NodeReading"/>), for the tests of the kinds that observe a gate's or a record's read.</summary>
internal static class Reads
{
    public static NodeObservation Look(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, ReviewGateState? gate) =>
        LookReading.Observe(new NodeReading(unit, node, binding) { Review = gate });

    public static NodeObservation GoAhead(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, IReadOnlyList<GoAheadView>? asked) =>
        GoAheadReading.Observe(new NodeReading(unit, node, binding) { GoAheads = asked });

    public static NodeObservation Opinion(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, OpinionGateState? gate) =>
        OpinionGateReading.Observe(new NodeReading(unit, node, binding) { Opinion = gate });

    public static NodeObservation WorkRequests(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, IReadOnlyList<QuestView>? published) =>
        WorkRequestsReading.Observe(new NodeReading(unit, node, binding) { Requests = published });
}
