using Daoris.Driver;
using Daoris.Ports.Repositories;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The proof by content, case by case, over a repository kept in memory: every file a landed branch changed since it left the line,
/// a deletion and both paths of a rename included, must read on the line as the branch left it. The branch's own commits are
/// made in a working tree of the repository, and the line is its first checkout.
/// </summary>
public sealed class LandedProofMemoryTests : IDisposable
{
    private static readonly string Root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "landed-proof-memory", "engine");

    private readonly MemoryRepository _repository = new(Root);
    private readonly IDisposable _chosen;
    private readonly string _place = Path.Combine(Path.GetDirectoryName(Root)!, "landed-proof-slot");

    public LandedProofMemoryTests()
    {
        _repository.Write(Root, "README.md", "# engine\n");
        _repository.Write(Root, "shared.txt", "one\n");
        Commit(Root, "first");
        _chosen = FlowRepository.Use(_repository);
    }

    public void Dispose() => _chosen.Dispose();

    [Fact]
    public async Task A_branch_with_one_file_still_different_on_the_line_is_unproven_and_the_file_named()
    {
        var tip = Branch(tree =>
        {
            _repository.Write(tree, "shared.txt", "one\ntwo\n");
            _repository.Write(tree, "b.txt", "b\n");
        });
        // The line took b.txt as the branch left it, and shared.txt otherwise.
        _repository.Write(Root, "b.txt", "b\n");
        Commit(Root, "part of it");
        _repository.Write(Root, "shared.txt", "one\nsomething else\n");
        Commit(Root, "not quite");

        var proof = await ProveAsync(tip);

        Assert.Equal(LandedKind.Differs, proof.Kind);
        Assert.Equal(["shared.txt"], proof.Files);
        Assert.Equal(1, proof.Commits);
    }

    [Fact]
    public async Task A_deletion_is_on_the_line_only_where_the_line_deleted_it_too()
    {
        var tip = Branch(tree => _repository.Remove(tree, "shared.txt"));

        var kept = await ProveAsync(tip);
        Assert.Equal(LandedKind.Differs, kept.Kind);
        Assert.Equal(["shared.txt"], kept.Files);

        _repository.Remove(Root, "shared.txt");
        Commit(Root, "Drop (#9)");
        Assert.Equal(LandedKind.OnLine, (await ProveAsync(tip)).Kind);
    }

    [Fact]
    public async Task A_rename_is_on_the_line_only_where_the_old_path_is_gone_and_the_new_one_matches()
    {
        var tip = Branch(tree =>
        {
            _repository.Remove(tree, "shared.txt");
            _repository.Write(tree, "renamed.txt", "one\n");
        });
        // The line holds the new name and still the old one.
        _repository.Write(Root, "renamed.txt", "one\n");
        Commit(Root, "a copy, not a rename");

        var kept = await ProveAsync(tip);
        Assert.Equal(LandedKind.Differs, kept.Kind);
        Assert.Equal(["shared.txt"], kept.Files);

        _repository.Remove(Root, "shared.txt");
        Commit(Root, "Rename (#10)");
        Assert.Equal(LandedKind.OnLine, (await ProveAsync(tip)).Kind);
    }

    [Fact]
    public async Task A_branch_the_line_holds_by_history_is_merged_and_needs_no_content()
    {
        var tip = Branch(tree => _repository.Write(tree, "shared.txt", "one\ntwo\n"));
        Assert.True((await _repository.UpdateRefAsync(Root, "refs/heads/main", tip, null, CancellationToken.None)).Ok);

        var proof = await ProveAsync(tip);

        Assert.Equal(LandedKind.Merged, proof.Kind);
        Assert.Equal("main", proof.Where);
    }

    [Fact]
    public async Task A_line_that_has_no_such_form_to_compare_with_is_unknown()
    {
        var tip = Branch(tree => _repository.Write(tree, "shared.txt", "one\ntwo\n"));

        var proof = await LandedProofs.ProveAsync(Root, tip, "main", [], CancellationToken.None);

        Assert.Equal(LandedKind.Unknown, proof.Kind);
    }

    private Task<LandedProofs.Proof> ProveAsync(string tip) =>
        LandedProofs.ProveAsync(Root, tip, "main", [new LandedProofs.LineForm("main", "refs/heads/main")], CancellationToken.None);

    /// <summary>A working tree on a new branch from the line, <paramref name="change"/> made in it and committed; the branch's tip.</summary>
    private string Branch(Action<string> change)
    {
        var start = _repository.HeadAsync(Root, CancellationToken.None).GetAwaiter().GetResult()!;
        var name = $"feature/work-{Guid.NewGuid():N}"[..20];
        Assert.True(_repository.AddWorktreeAsync(Root, new GitWorktreeAdd(Path.Combine(_place, name.Replace('/', '-')), start, name), CancellationToken.None).GetAwaiter().GetResult().Ok);
        var tree = Path.Combine(_place, name.Replace('/', '-'));
        change(tree);
        var made = _repository.CommitAllAsync(tree, "the work", CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(made.Ok, made.Error);
        return _repository.Ref($"refs/heads/{name}")!;
    }

    private void Commit(string folder, string message) =>
        Assert.True(_repository.CommitAllAsync(folder, message, CancellationToken.None).GetAwaiter().GetResult().Ok);
}
