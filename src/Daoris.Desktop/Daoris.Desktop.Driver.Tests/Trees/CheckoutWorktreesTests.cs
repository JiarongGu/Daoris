namespace Daoris.Desktop.Driver.Tests;

public sealed class CheckoutWorktreesTests
{
    private static string Listing(params string[] trees) => string.Concat(trees.Select(tree => $"worktree {tree}\nHEAD abc\nbranch refs/heads/x\n\n"));

    [Fact]
    public void A_worktree_listed_under_the_fixtures_folder_is_named_and_one_elsewhere_is_not()
    {
        var fixtures = Path.Combine(Path.GetTempPath(), "checkout", "_fixtures");
        var listing = Listing(
            Path.Combine(Path.GetTempPath(), "checkout").Replace('\\', '/'),
            Path.Combine(fixtures, "x", "engine", "s-1").Replace('\\', '/'),
            Path.Combine(Path.GetTempPath(), "elsewhere", "s-2").Replace('\\', '/'));

        var found = CheckoutWorktrees.Under(listing, fixtures);

        Assert.Single(found);
        Assert.EndsWith("/_fixtures/x/engine/s-1", found[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_checkout_with_only_its_own_tree_names_none()
    {
        Assert.Empty(CheckoutWorktrees.Under(Listing(Path.Combine(Path.GetTempPath(), "checkout")), Path.Combine(Path.GetTempPath(), "checkout", "_fixtures")));
    }
}
