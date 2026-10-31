using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>GITTXN1 over real git: each expectation alone refuses movement, a symbolic ref is refused, and the rollbacks are conditional.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RefTransactionGitTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ref-txn", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public RefTransactionGitTests()
    {
        _root = Path.Combine(_home, "engine");
        GitSeed.PlainAsync(_root, "engine").GetAwaiter().GetResult();
    }

    public void Dispose() => GitSeed.Remove(_home);

    private async Task<string> CommitAsync(string name)
    {
        await File.WriteAllTextAsync(Path.Combine(_root, name), name);
        await GitAsync(_root, "add", "-A");
        await GitAsync(_root, "commit", "--quiet", "-m", name);
        return (await GitAsync(_root, "rev-parse", "HEAD")).Trim();
    }

    private async Task<string> TipAsync(string reference) => (await GitAsync(_root, "for-each-ref", "--format=%(objectname)", reference)).Trim();

    [Fact]
    public async Task A_transaction_with_every_act_moves_all_of_them()
    {
        var one = (await GitAsync(_root, "rev-parse", "HEAD")).Trim();
        var two = await CommitAsync("two");
        await GitAsync(_root, "update-ref", "refs/heads/old", one);
        await GitAsync(_root, "update-ref", "refs/heads/gone", one);
        var run = await new RefTransaction().Verify("refs/heads/main", two).Create("refs/heads/made", two)
            .Update("refs/heads/old", one, two).Delete("refs/heads/gone", one).RunAsync(_root, default);
        Assert.True(run.Done, run.Said);
        Assert.Equal(two, await TipAsync("refs/heads/made"));
        Assert.Equal(two, await TipAsync("refs/heads/old"));
        Assert.Equal("", await TipAsync("refs/heads/gone"));
    }

    [Fact]
    public async Task Each_expectation_alone_refuses_and_nothing_moves()
    {
        var one = (await GitAsync(_root, "rev-parse", "HEAD")).Trim();
        var two = await CommitAsync("two");
        await GitAsync(_root, "update-ref", "refs/heads/there", one);
        await GitAsync(_root, "update-ref", "refs/heads/witness", one);
        var cases = new (string Name, RefTransaction Refused)[]
        {
            ("create over a ref that stands", new RefTransaction().Create("refs/heads/there", two)),
            ("update from a wrong old value", new RefTransaction().Update("refs/heads/there", two, two)),
            ("delete at a wrong old value", new RefTransaction().Delete("refs/heads/there", two)),
            ("a dependency that moved", new RefTransaction().Verify("refs/heads/there", two)),
            ("a dependency that must be absent but stands", new RefTransaction().Verify("refs/heads/there", null)),
        };
        foreach (var (name, refused) in cases)
        {
            var run = await refused.Update("refs/heads/witness", one, two).RunAsync(_root, default);
            Assert.False(run.Done, name);
            Assert.Equal(one, await TipAsync("refs/heads/witness"));
            Assert.Equal(one, await TipAsync("refs/heads/there"));
        }
    }

    [Fact]
    public async Task A_symbolic_ref_is_refused_before_git_runs_it()
    {
        var one = (await GitAsync(_root, "rev-parse", "HEAD")).Trim();
        await GitAsync(_root, "symbolic-ref", "refs/heads/alias", "refs/heads/main");
        var run = await new RefTransaction().Delete("refs/heads/alias", one).RunAsync(_root, default);
        Assert.False(run.Done);
        Assert.Contains("symbolic", run.Said);
        Assert.Equal(one, await TipAsync("refs/heads/main"));
    }

    [Fact]
    public async Task The_rollbacks_each_hold_only_while_their_condition_does()
    {
        var one = (await GitAsync(_root, "rev-parse", "HEAD")).Trim();
        var two = await CommitAsync("two");

        // A delete is restored only where the name is absent.
        await GitAsync(_root, "update-ref", "refs/heads/a", two);
        Assert.False((await RefTransaction.RestoreDeleted("refs/heads/a", one).RollBackAsync(_root, "the delete", default)).Done);
        Assert.Equal(two, await TipAsync("refs/heads/a"));
        await GitAsync(_root, "update-ref", "-d", "refs/heads/a");
        Assert.True((await RefTransaction.RestoreDeleted("refs/heads/a", one).RollBackAsync(_root, "the delete", default)).Done);
        Assert.Equal(one, await TipAsync("refs/heads/a"));

        // A rewrite is restored only from the tip it produced.
        var stale = await RefTransaction.RestoreRewritten("refs/heads/a", two, one).RollBackAsync(_root, "the rewrite", default);
        Assert.False(stale.Done);
        Assert.Contains("nothing was moved", stale.Said);
        Assert.Equal(one, await TipAsync("refs/heads/a"));
        Assert.True((await RefTransaction.RestoreRewritten("refs/heads/a", one, two).RollBackAsync(_root, "the rewrite", default)).Done);
        Assert.Equal(two, await TipAsync("refs/heads/a"));

        // A rename is reversed checking both names: the new name moved on, or the old name made meanwhile, each refuses alone.
        Assert.True((await RefTransaction.Rename("refs/heads/a", "refs/heads/b", two).RunAsync(_root, default)).Done);
        await GitAsync(_root, "update-ref", "refs/heads/a", one);
        Assert.False((await RefTransaction.ReverseRename("refs/heads/a", "refs/heads/b", two).RollBackAsync(_root, "the rename", default)).Done);
        Assert.Equal(two, await TipAsync("refs/heads/b"));
        await GitAsync(_root, "update-ref", "-d", "refs/heads/a");
        await GitAsync(_root, "update-ref", "refs/heads/b", one);
        Assert.False((await RefTransaction.ReverseRename("refs/heads/a", "refs/heads/b", two).RollBackAsync(_root, "the rename", default)).Done);
        Assert.Equal("", await TipAsync("refs/heads/a"));
        await GitAsync(_root, "update-ref", "refs/heads/b", two);
        Assert.True((await RefTransaction.ReverseRename("refs/heads/a", "refs/heads/b", two).RollBackAsync(_root, "the rename", default)).Done);
        Assert.Equal(two, await TipAsync("refs/heads/a"));
        Assert.Equal("", await TipAsync("refs/heads/b"));
    }
}
