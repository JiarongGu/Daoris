namespace Daoris.Desktop.Driver.Tests;

public sealed class CheckoutWorktreesTests : IDisposable
{
    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "checkout-worktrees-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_root);

    private void Register(string name, string tree)
    {
        var entry = Path.Combine(_root, ".git", "worktrees", name);
        Directory.CreateDirectory(entry);
        File.WriteAllText(Path.Combine(entry, "gitdir"), tree.Replace('\\', '/') + "/.git\n");
    }

    [Fact]
    public void A_worktree_registered_under_the_fixtures_folder_is_named_and_one_elsewhere_is_not()
    {
        var fixtures = Path.Combine(_root, "_fixtures");
        Register("leaked", Path.Combine(fixtures, "x", "engine", "s-1"));
        Register("own", Path.Combine(_root, "elsewhere", "s-2"));

        var found = CheckoutWorktrees.Under(Path.Combine(_root, ".git"), fixtures);

        Assert.Single(found);
        Assert.EndsWith("/_fixtures/x/engine/s-1/.git", found[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_checkout_with_no_worktrees_names_none()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        Assert.Empty(CheckoutWorktrees.Under(Path.Combine(_root, ".git"), Path.Combine(_root, "_fixtures")));
    }
}
