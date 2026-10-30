namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a look declares to the windows beside its tick (FOUNDATION1s, D182 point 7): the page's tick no longer asks the rules, the
/// repositories and the registry again, so what a look moved of them is told as a change of its own scope.
/// </summary>
public sealed class TickScopesTests
{
    [Fact]
    public void A_settled_proposal_moves_the_rules_and_a_moved_registry_the_registry()
    {
        Assert.Equal(StateScopes.None, DriverLoop.LookScopes(["start  q1 → engine"], registryMoved: false));
        Assert.Equal(StateScopes.Rules, DriverLoop.LookScopes(["rules  applied #p1 from helper: narrows — it narrows."], registryMoved: false));
        Assert.Equal(StateScopes.Registry, DriverLoop.LookScopes([], registryMoved: true));
        Assert.Equal(StateScopes.Rules | StateScopes.Registry, DriverLoop.LookScopes(["rules  refused #p2 from helper: no"], registryMoved: true));
    }
}
