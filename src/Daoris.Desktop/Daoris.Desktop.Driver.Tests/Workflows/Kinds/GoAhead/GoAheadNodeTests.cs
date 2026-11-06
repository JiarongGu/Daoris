using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GateFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>FABRIC-NODE1b: the <c>go-ahead</c> is reached by the person's yes to its act and rejected by their refusal (D135's answers); anything else waits.</summary>
public sealed class GoAheadNodeTests
{
    [Fact]
    public void A_go_ahead_is_reached_by_the_persons_yes_to_its_act_and_rejected_by_their_refusal()
    {
        var goAhead = Node(NodeKinds.GoAhead);
        NodeObservation Seen(params GoAheadView[] asked) => Reads.GoAhead(Branch(), goAhead, Binding(NodeKinds.GoAhead), asked);

        var yes = Applied(goAhead, Seen(Asked("another act", false, "no"), Asked(Act, true, number: 2)));
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (yes.Status.State, yes.Status.By));

        var no = Applied(goAhead, Seen(Asked(Act, false, "not before the freeze ends")));
        Assert.Equal((NodeStates.Rejected, "not before the freeze ends"), (no.Status.State, no.Status.Words));

        // Asked and unanswered, refused with no reason, another act's answer, or none read: it waits, never reached or rejected on a guess.
        foreach (var (why, asked) in new (string, GoAheadView[]?)[]
        {
            ("unanswered", [Asked(Act, null)]),
            ("refused with no words", [Asked(Act, false)]),
            ("another act", [Asked("another act", true)]),
            ("none asked", []),
            ("none read", null),
        })
        {
            var seen = Reads.GoAhead(Branch(), goAhead, Binding(NodeKinds.GoAhead), asked);
            Assert.Equal((why, (NodeMove?)null), (why, seen.Move));
        }

        // A go-ahead that names no act is answered by none.
        Assert.Null(Reads.GoAhead(Branch(), goAhead with { Options = new Dictionary<string, string>() }, Binding(NodeKinds.GoAhead), [Asked(Act, true)]).Move);
    }
}
