using Xunit;

namespace Daoris.Testing;

public abstract partial class RepositoryContract
{
    [Fact]
    public async Task Git_answers()
    {
        Assert.True(await Backend.AvailableAsync(Ct));
    }

    [Fact]
    public async Task An_init_makes_a_repository_on_the_line_with_no_commit()
    {
        var made = Beside(await NewAsync(), "made");

        Assert.True((await Backend.InitAsync(made, "trunk", Ct)).Ok);

        Assert.Equal("trunk", await Backend.BranchAsync(made, Ct));
        Assert.Null(await Backend.HeadAsync(made, Ct));
        Assert.Empty((await Backend.RefsAsync(made, ["refs"], null, null, Ct))!);
    }

    [Fact]
    public async Task An_empty_commit_is_one_commit_on_the_line_holding_no_file()
    {
        var made = Beside(await NewAsync(), "made");
        Assert.True((await Backend.InitAsync(made, "trunk", Ct)).Ok);

        Assert.True((await Backend.CommitEmptyAsync(made, "Fixture", "fixture@example.test", "begin", Ct)).Ok);

        var head = (await Backend.HeadAsync(made, Ct))!;
        Assert.Equal(1, await Backend.CountAsync(made, "HEAD", [], Ct));
        Assert.Empty((await Backend.EntriesAsync(made, head, [], true, Ct))!);
        Assert.Equal([head], (await Backend.RefsAsync(made, ["refs"], null, null, Ct))!.Select(each => each.Commit));
        Assert.Equal("refs/heads/trunk", Assert.Single(await Backend.RefsAsync(made, ["refs"], null, null, Ct) ?? []).Name);
    }
}
