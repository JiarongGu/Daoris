using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the driver asks git before it feeds (D48 §6). Over a real checkout, because the whole point
/// of this half is that git's answer is the one that matters — a stubbed one would prove nothing
/// about the arguments, the format, or the parse.
/// </summary>
public sealed class WorkingTreeProvenanceTests : IDisposable
{
    private readonly GitTree _tree = new("worktree");

    public void Dispose() => _tree.Dispose();

    [Fact]
    public async Task Provenance_names_the_commit_its_time_and_the_line()
    {
        var provenance = await WorkingTree.ProvenanceAsync(_tree.Root);

        Assert.NotNull(provenance);
        Assert.Equal(40, provenance!.Commit.Length);
        Assert.Equal("main", provenance.Branch);
        // A real commit time, not a default: the ordering the deployment does is on this field.
        Assert.True(provenance.CommittedAt > DateTimeOffset.UtcNow.AddMinutes(-10));
        Assert.True(provenance.CommittedAt < DateTimeOffset.UtcNow.AddMinutes(10));
    }

    /// <summary>The canonical line, with no `origin/HEAD` to ask: the conventional names, in turn.</summary>
    [Fact]
    public async Task The_canonical_line_falls_back_to_the_conventional_names()
    {
        Assert.Equal("main", await WorkingTree.DefaultBranchAsync(_tree.Root));
    }

    /// <summary>
    /// A branch is work in flight, and the provenance says which — this is the field the deployment
    /// refuses on, so it has to be the checked-out branch and not the canonical one.
    /// </summary>
    [Fact]
    public async Task A_checkout_on_another_line_says_so_while_the_canonical_line_stays_put()
    {
        _tree.Git("checkout -q -b feature/streaming");

        Assert.Equal("feature/streaming", (await WorkingTree.ProvenanceAsync(_tree.Root))!.Branch);
        Assert.Equal("main", await WorkingTree.DefaultBranchAsync(_tree.Root));
    }

    /// <summary>A detached HEAD is on no line at all, and is NAMED — a blank quotes back as nonsense.</summary>
    [Fact]
    public async Task A_detached_head_is_named_rather_than_blank()
    {
        _tree.Git("checkout -q --detach HEAD");

        Assert.Equal("(detached)", (await WorkingTree.ProvenanceAsync(_tree.Root))!.Branch);
    }

    /// <summary>
    /// A tree git cannot answer for feeds nothing — null rather than an invented commit, because an
    /// invented one is a claim about a point in the history that does not exist.
    /// </summary>
    [Fact]
    public async Task A_folder_that_is_not_a_repository_answers_nothing()
    {
        var plain = Path.Combine(Path.GetTempPath(), "daoris-plain-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(plain);
        try
        {
            Assert.Null(await WorkingTree.ProvenanceAsync(plain));
            Assert.Null(await WorkingTree.DefaultBranchAsync(plain));
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
        }
    }

    // ——— How this checkout's commit stands to one a deployment holds (SYNC5a): only git can say.

    [Fact]
    public async Task A_later_commit_descends_and_an_earlier_one_is_behind()
    {
        var first = _tree.Output("rev-parse HEAD");
        _tree.Commit("second.md");
        var second = _tree.Output("rev-parse HEAD");

        Assert.Equal(TreeRelation.Descends, await WorkingTree.RelationAsync(_tree.Root, held: first, head: second));
        Assert.Equal(TreeRelation.Behind, await WorkingTree.RelationAsync(_tree.Root, held: second, head: first));
    }

    [Fact]
    public async Task Two_lines_from_one_parent_have_diverged()
    {
        var parent = _tree.Output("rev-parse HEAD");
        _tree.Commit("ours.md");
        var ours = _tree.Output("rev-parse HEAD");
        _tree.Git($"checkout -q -b theirs {parent}");
        _tree.Commit("theirs.md");
        var theirs = _tree.Output("rev-parse HEAD");

        Assert.Equal(TreeRelation.Diverged, await WorkingTree.RelationAsync(_tree.Root, held: theirs, head: ours));
    }

    /// <summary>A commit this checkout has never fetched is unknown — not diverged, which would be a claim about it.</summary>
    [Fact]
    public async Task A_commit_this_checkout_does_not_have_is_unknown()
    {
        Assert.Equal(
            TreeRelation.Unknown,
            await WorkingTree.RelationAsync(_tree.Root, held: "0123456789abcdef0123456789abcdef01234567", head: _tree.Output("rev-parse HEAD")));
    }
}
