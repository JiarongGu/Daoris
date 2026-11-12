using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A branch's or a checkout's changed files listed between its review record's commits (<see cref="ReviewCommitFiles"/>), so a commit made
/// after the review never joins the list the person decides on.
/// </summary>
public sealed class ReviewCommitFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "review-commit-files", Guid.NewGuid().ToString("N")[..8]);

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
    public async Task A_branchs_files_are_those_between_its_lines_base_and_its_recorded_tip_not_those_committed_after()
    {
        Directory.CreateDirectory(_root);
        await Git("init", "--quiet", "-b", "main");
        await Commit("notes.md", "one\n");
        await Git("checkout", "--quiet", "-b", "work");
        await Commit("notes.md", "two\n");
        var tip = await Commit("added.md", "new\n");
        await Commit("later.md", "after the review\n");
        var branch = new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "r1", T) { Reference = new() { Branch = "work", Line = "main", Tip = tip } };

        var files = await ReviewCommitFiles.ListAsync(_root, branch);

        Assert.Equal(["added.md", "notes.md"], files.Select(row => row.Path).Order().ToArray());
        var added = files.Single(row => row.Path == "added.md");
        Assert.Equal(("added", null), (added.Change, added.Before));
        Assert.NotNull(added.After);
        Assert.Equal("modified", files.Single(row => row.Path == "notes.md").Change);
    }

    [Fact]
    public async Task A_checkouts_files_are_those_between_its_first_turns_start_and_its_newest_turns_end()
    {
        Directory.CreateDirectory(_root);
        await Git("init", "--quiet", "-b", "main");
        var start = await Commit("notes.md", "one\n");
        var end = await Commit("notes.md", "two\n");
        await Commit("later.md", "between turns\n");
        var checkout = new ReviewRecord("q1", "review", 1, ReviewKinds.Checkout, "r2", T)
        {
            Reference = new() { Turns = [new ReviewInterval("t1", T, T, start, end)] },
        };

        var files = await ReviewCommitFiles.ListAsync(_root, checkout);

        Assert.Equal(["notes.md"], files.Select(row => row.Path).ToArray());
    }

    [Fact]
    public void A_deleted_file_has_no_blob_after_and_other_kinds_list_nothing()
    {
        var zeros = new string('0', 40);
        var rows = ReviewCommitFiles.Parse($":100644 000000 {new string('a', 40)} {zeros} D\0gone.md\0");

        var gone = Assert.Single(rows);
        Assert.Equal(("deleted", null), (gone.Change, gone.After));
        Assert.NotNull(gone.Before);
        Assert.Empty(ReviewCommitFiles.ListAsync("unused", new ReviewRecord("q1", "review", 1, ReviewKinds.Folder, "r3", T)).Result);
    }
}
