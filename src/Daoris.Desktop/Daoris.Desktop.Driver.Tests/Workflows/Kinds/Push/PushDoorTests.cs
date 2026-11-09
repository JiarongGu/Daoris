using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a push act's end comes to on the node: a refusal fails the attempt its push started for and no other, and an end that is
/// neither done nor refused is said, with the node left active.
/// </summary>
public sealed class PushDoorTests : IDisposable
{
    private static readonly NodeDeclaration Push = new(NodeKinds.Push, NodeKinds.Push, 1, NodeReach.Performed);
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "push-door", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _said = [];

    public PushDoorTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => GitSeed.Discard(_scratch);

    private PushStart Start(string tip) => new("unit-a", Push, "engine", "default", _scratch, "feature/x", tip) { Authority = StepAuthority.OfDoor("unit-a", Push.Id, 1) };

    private void Active(string tip) =>
        new NodeRecordFiles(_scratch, () => true).Write(new NodeRecord("unit-a")
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                [Push.Id] = new("unit-a", Push.Id, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance("unit-a", tip, "graph-1", Push.Id, 1, 1) },
            },
        });

    private string? State() => new NodeRecordFiles(_scratch, () => true).Read("unit-a")!.Statuses[Push.Id].State;

    [Fact]
    public void A_refusal_fails_the_attempt_bound_to_the_tip_it_pushed()
    {
        Active("t1");

        new PushDoor(_scratch, _said.Add, holdsLease: () => true).Settle(Start("t1"), new ActRun("push", null, "Write is denied", true));

        Assert.Equal(NodeStates.Failed, State());
    }

    [Fact]
    public void A_refusal_of_an_older_tip_leaves_the_attempt_bound_to_a_newer_one_active()
    {
        Active("t2");

        new PushDoor(_scratch, _said.Add, holdsLease: () => true).Settle(Start("t1"), new ActRun("push", null, "Write is denied", true));

        Assert.Equal(NodeStates.Active, State());
    }

    [Fact]
    public void An_end_that_is_neither_done_nor_refused_is_said_and_leaves_the_node_active()
    {
        Active("t1");

        new PushDoor(_scratch, _said.Add, holdsLease: () => true).Settle(Start("t1"), new ActRun("push", ActOutcomes.NotDone, "nothing stands", false));

        Assert.Equal(NodeStates.Active, State());
        Assert.Contains(_said, line => line.Contains("not-done", StringComparison.Ordinal) && line.Contains("nothing stands", StringComparison.Ordinal));
    }
}
