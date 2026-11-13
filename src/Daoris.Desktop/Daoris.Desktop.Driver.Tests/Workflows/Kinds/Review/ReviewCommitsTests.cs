using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A branch's or a checkout's file drawn at its review record's commits (<see cref="ReviewCommits"/>): the tip or the newest turn's end
/// after, the line's merge base or the first turn's start before, read from the commits themselves whatever the checkout holds now.
/// </summary>
public sealed class ReviewCommitsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "review-commits", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    public void Dispose() => GitSeed.Discard(_root);

    private async Task<string> Git(params string[] arguments)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(_root, arguments, CancellationToken.None);
        Assert.True(code == 0, stderr);
        return stdout.Trim();
    }

    private async Task<string> Commit(string file, string text)
    {
        await File.WriteAllTextAsync(Path.Combine(_root, file), text);
        await Git("add", file);
        await Git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", text.Trim());
        return await Git("rev-parse", "HEAD");
    }

    [Fact]
    public async Task A_branchs_file_is_drawn_at_its_tip_and_at_its_lines_base_whatever_the_checkout_holds_now()
    {
        Directory.CreateDirectory(_root);
        await Git("init", "--quiet", "-b", "main");
        var start = await Commit("notes.md", "one\n");
        await Git("checkout", "--quiet", "-b", "work");
        var tip = await Commit("notes.md", "two\n");
        await Commit("notes.md", "three, after the review\n");
        var branch = new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "r1", T) { Reference = new() { Branch = "work", Line = "main", Tip = tip, Base = start } };
        var unbased = new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "r1", T) { Reference = new() { Branch = "work", Line = "main", Tip = tip } };
        var checkout = new ReviewRecord("q1", "review", 1, ReviewKinds.Checkout, "r2", T)
        {
            Reference = new() { Turns = [new ReviewInterval("t1", T, T, start, tip)] },
        };

        Assert.Equal("two\n", (await ReviewCommits.ReadAsync(_root, branch, "notes.md", ReviewContent.After)).Text);
        Assert.Equal("one\n", (await ReviewCommits.ReadAsync(_root, branch, "notes.md", ReviewContent.Before)).Text);
        Assert.Equal("one\n", (await ReviewCommits.ReadAsync(_root, checkout, "notes.md", ReviewContent.Before)).Text);
        Assert.Null(await ReviewCommits.CommitAsync(_root, unbased, ReviewContent.Before, default));
        Assert.Equal(ReviewContent.NotRow, (await ReviewCommits.ReadAsync(_root, branch, "gone.md", ReviewContent.After)).Refused);
        Assert.Equal(ReviewContent.NotRow, (await ReviewCommits.ReadAsync(_root, branch, "../outside.md", ReviewContent.After)).Refused);
    }
}
