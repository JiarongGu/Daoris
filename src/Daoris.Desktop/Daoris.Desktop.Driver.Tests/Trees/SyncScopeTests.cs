using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR7 (D112), without git: which repositories bringing up to date takes. By default those holding a branch of
/// Daoris's; every one with `--all`; one the person named; and, at the press, each one a listed row names.
/// </summary>
public sealed class SyncScopeTests
{
    private static readonly SyncRepository Holding = new("engine", "aurora", Holds: true);
    private static readonly SyncRepository Other = new("game", "aurora", Holds: false);

    [Fact]
    public void By_default_a_scope_takes_the_repositories_holding_Daoris_branches_and_no_other()
    {
        Assert.True(SyncScope.Held.Includes(Holding));
        Assert.False(SyncScope.Held.Includes(Other));
    }

    [Fact]
    public void All_takes_every_repository_and_a_name_takes_that_one_whatever_its_case()
    {
        Assert.True(SyncScope.Everything.Includes(Other));
        Assert.True(SyncScope.Named(["GAME"]).Includes(Other));
        Assert.False(SyncScope.Named(["tools"]).Includes(Other));
        // Naming one takes nothing away from the default.
        Assert.True(SyncScope.Named(["tools"]).Includes(Holding));
    }

    /// <summary>The press acts on what the person was shown: a row listed as `repository:branch` includes its repository.</summary>
    [Fact]
    public void A_press_takes_each_repository_a_listed_row_names_and_no_other()
    {
        Assert.True(SyncScope.Held.Listed(new HashSet<string> { "game:main", "engine:daoris/s-1" }).Includes(Other));
        Assert.False(SyncScope.Held.Listed(new HashSet<string> { "tools:main" }).Includes(Other));
        Assert.False(SyncScope.Held.Listed(null).Includes(Other));
        // A branch name with a colon of its own still names the repository before the first.
        Assert.True(SyncScope.Held.Listed(new HashSet<string> { "game:feature/a:b" }).Includes(Other));
    }
}
