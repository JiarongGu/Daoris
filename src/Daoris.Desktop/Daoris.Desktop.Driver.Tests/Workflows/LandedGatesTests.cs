using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1a (the pipeline design §4.2): where a landed route's acceptance stands against the declaration at the line's tip,
/// read by blob alone: accepted where they are one, changed where the tip's differs, not accepted where none was, and nothing to accept
/// where the route names no verification.
/// </summary>
public sealed class LandedGatesTests
{
    private const string Blob = "3b18e512dba79e4c8300dd08aeb37f8e728b8dad";

    private const string Other = "0e5751c026e543b2e8ab2eb06099daa1d1e5df47";

    private static WorkflowLandedSelected Route(string workflow, string? gates) => new(workflow, gates, WorkflowLevels.RepositoryLanded);

    private static LandedTip Tip(string gates) => new("main", "aa11bb22cc33dd44ee55ff6677889900aabbccdd", gates);

    [Fact]
    public void An_acceptance_stands_by_the_blob_alone()
    {
        Assert.Equal(LandedGates.Accepted, LandedGates.StateOf(Route("web-verify", Blob), Tip(Blob)));
        Assert.Equal(LandedGates.Changed, LandedGates.StateOf(Route("web-verify", Blob), Tip(Other)));
        Assert.Equal(LandedGates.Changed, LandedGates.StateOf(Route("web-verify", "none"), Tip(Blob)));
        Assert.Equal(LandedGates.Accepted, LandedGates.StateOf(Route("web-verify", "none"), Tip("none")));
        Assert.Equal(LandedGates.Unaccepted, LandedGates.StateOf(Route("web-verify", null), Tip(Blob)));
        Assert.Equal(LandedGates.Unread, LandedGates.StateOf(Route("web-verify", Blob), null));
        Assert.Equal(LandedGates.NoVerification, LandedGates.StateOf(Route("current", null), Tip(Blob)));
        Assert.Equal(LandedGates.NoVerification, LandedGates.StateOf(new WorkflowLandedSelected("current", null, WorkflowLevels.Current), null));
    }
}
