using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a run's review presents as its outputs (<see cref="ReviewOutputs"/>): each typed by what made it, a branch's commits listed
/// between a base frozen with the tip (<see cref="RunWorkBase"/>, <see cref="ReviewBranchCommits"/>), and a newer revision's delta by output.
/// </summary>
public sealed class ReviewOutputsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "review-outputs", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-13T09:00:00Z");

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

    private static GrownBranch Grown(string branch, string from, string? grewFrom = null) =>
        new("engine", "w", branch, "main", from, grewFrom, T);

    [Fact]
    public void The_base_is_the_commit_the_runs_chain_grew_from_the_line()
    {
        var first = Grown("session/one", "base0");
        var second = Grown("session/two", "tip1", grewFrom: "session/one");

        Assert.Equal("base0", RunWorkBase.Of(second, [first, second]));
        Assert.Equal("base0", RunWorkBase.Of(first, [first]));
        Assert.Null(RunWorkBase.Of(Grown("session/three", "tip2", grewFrom: "session/gone"), []));
    }

    [Fact]
    public async Task A_landed_branch_keeps_its_files_and_commits_between_the_frozen_base_and_its_tip()
    {
        Directory.CreateDirectory(_root);
        await Git("init", "--quiet", "-b", "main");
        var start = await Commit("notes.md", "one\n");
        await Git("checkout", "--quiet", "-b", "work");
        await Commit("notes.md", "two\n");
        var tip = await Commit("added.md", "new\n");
        await Commit("later.md", "after the review\n");
        await Git("checkout", "--quiet", "main");
        await Git("merge", "--quiet", "--ff-only", tip);
        var record = new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "r1", T)
        {
            Reference = new() { Branch = "work", Line = "main", Tip = tip, Base = start },
        };

        var files = await ReviewCommitFiles.ListAsync(_root, record);
        var commits = await ReviewBranchCommits.ListAsync(_root, record);

        Assert.Equal(["added.md", "notes.md"], files.Select(row => row.Path).Order().ToArray());
        Assert.Equal(["new", "two"], commits.Commits.Select(commit => commit.Subject).ToArray());
        Assert.Equal(tip, commits.Commits[0].Sha);
        Assert.Equal(["added.md"], commits.Commits[0].Files);
        Assert.Empty(commits.Uncommitted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_local_line_behind_its_remote_never_adds_the_remotes_merged_work_to_the_runs_changes(bool frozen)
    {
        Directory.CreateDirectory(_root);
        await Git("init", "--quiet", "-b", "main");
        await Commit("notes.md", "one\n");
        await Git("checkout", "--quiet", "-b", "merged-elsewhere");
        var remote = await Commit("theirs.md", "merged by another pull request\n");
        await Git("update-ref", "refs/remotes/origin/main", remote);
        await Git("checkout", "--quiet", "-b", "work");
        await Commit("notes.md", "ours\n");
        var tip = await Commit("added.md", "new\n");
        var record = new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "r1", T)
        {
            Reference = new() { Branch = "work", Line = "main", Tip = tip, Base = frozen ? remote : null },
        };

        var files = await ReviewCommitFiles.ListAsync(_root, record);
        var commits = await ReviewBranchCommits.ListAsync(_root, record);

        Assert.Equal(["added.md", "notes.md"], files.Select(row => row.Path).Order().ToArray());
        Assert.Equal("modified", files.Single(row => row.Path == "notes.md").Change);
        Assert.Equal(["new", "ours"], commits.Commits.Select(commit => commit.Subject).ToArray());
    }

    [Fact]
    public async Task A_checkouts_commits_are_its_turns_own_and_its_uncommitted_paths_stand_apart()
    {
        Directory.CreateDirectory(_root);
        await Git("init", "--quiet", "-b", "main");
        var start = await Commit("notes.md", "one\n");
        var end = await Commit("notes.md", "in the turn\n");
        var between = await Commit("other.md", "between turns\n");
        var last = await Commit("notes.md", "second turn\n");
        var record = new ReviewRecord("q1", "review", 1, ReviewKinds.Checkout, "r2", T)
        {
            Reference = new()
            {
                Turns = [new ReviewInterval("t1", T, T, start, end), new ReviewInterval("t2", T, T, between, last)],
                Uncommitted = [new RunTurnPath("draft.md", "abc")],
            },
        };

        var commits = await ReviewBranchCommits.ListAsync(_root, record);

        Assert.Equal(["second turn", "in the turn"], commits.Commits.Select(commit => commit.Subject).ToArray());
        Assert.Equal(["draft.md"], commits.Uncommitted);
    }

    [Fact]
    public void Each_output_is_typed_by_what_made_it_with_its_display_and_a_digest()
    {
        var parts = new[] { new ReviewPart("q1", "Add the cache") { Machine = "m", Sequence = 1 }, new ReviewPart("q2", "Wire it") { Machine = "m", Sequence = 2 } };
        var branch = new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "r1", T) { Reference = new() { Branch = "work", Tip = "aaa", Base = "bbb" }, Parts = parts };
        var plan = new ReviewRecord("q1", "review", 1, ReviewKinds.Plan, "r2", T) { Reference = new() { PlanRecord = "p", Plan = 3, PlanDigest = "d" } };
        var answer = new ReviewRecord("q1", "review", 1, ReviewKinds.Answer, "r3", T) { Parts = [parts[0]] };

        var outputs = ReviewOutputs.Of(branch);

        Assert.Equal(
            [("handback:q1", ReviewOutputTypes.Handback, ReviewDisplays.Document), ("handback:q2", ReviewOutputTypes.Handback, ReviewDisplays.Document), ("changes", ReviewOutputTypes.Changes, ReviewDisplays.ChangeSet)],
            outputs.Select(output => (output.Id, output.Type, output.Display)).ToArray());
        Assert.Equal("q2", outputs[1].Part);
        Assert.All(outputs, output => Assert.False(string.IsNullOrEmpty(output.Digest)));
        Assert.Equal([("plan", ReviewDisplays.ResourcePlan)], ReviewOutputs.Of(plan).Select(output => (output.Id, output.Display)).ToArray());
        Assert.Equal(["handback:q1"], ReviewOutputs.Of(answer).Select(output => output.Id).ToArray());
    }

    [Fact]
    public void A_newer_revisions_delta_names_a_moved_tip_and_a_corrected_handback_where_no_file_row_changed()
    {
        var part = new ReviewPart("q1", "Add the cache") { Machine = "m", Sequence = 1 };
        var read = new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "r1", T) { Reference = new() { Branch = "work", Tip = "aaa" }, Parts = [part] };
        var moved = read with { Attempt = 2, Revision = "r2", Reference = new() { Branch = "work", Tip = "ccc" } };
        var corrected = read with { Attempt = 2, Revision = "r3", Parts = [part with { Sequence = 2 }] };

        Assert.Empty(RunReviews.Changed(read, moved));
        Assert.Equal(["changes"], ReviewOutputs.Delta(read, moved));
        Assert.Equal(["handback:q1"], ReviewOutputs.Delta(read, corrected));
        Assert.Empty(ReviewOutputs.Delta(read, read));
    }
}
