using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 A ref's id and the branches' listing read from git's files answer as git does: loose and packed refs, a symbolic and a detached HEAD, a
/// linked tree's own HEAD, a ref deleted and repacked, an annotated tag, a name that is no ref, and a reftable repository, which is git's.
/// Where the files cannot say, the answer is none and git is asked.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitRefReadsParityTests : LandedFixture
{
    private static readonly string[] Listing = ["for-each-ref", $"--format={RefSnapshot.Format}", "refs/heads", "refs/remotes/origin"];

    private async Task<string> RepositoryAsync()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        await GitAsync(root, "branch", "feature");
        await GitAsync(root, "tag", "-a", "-m", "annotated", "annotated");
        return root;
    }

    /// <summary>Every read the files answer, in git's words, with what git itself says; the files' answer is none or git's.</summary>
    private static async Task<int> ParityAsync(string folder, bool directory = true)
    {
        var answered = 0;
        using var memo = GitObjectMemo.Within(Path.Combine(Path.GetDirectoryName(folder)!, "memo-home"));
        // Each git start is a second on a loaded machine and a class's tests must each finish within the run's inactivity window, so few names.
        // A directory where a ref is looked for is git's, which the library answers each time.
        // An annotated tag is the library's each time, since only a ref's own commit id is learned.
        string[] names = ["HEAD", "refs/heads/main", "refs/heads/feature", "refs/heads/gone", "main", .. directory ? (string[])["refs/heads", "refs/tags/annotated"] : []];
        foreach (var name in names)
        {
            foreach (var form in new[] { name, name + "^{commit}" })
            {
                string[] arguments = name == "HEAD" && form == name ? ["rev-parse", "HEAD"] : ["rev-parse", "--verify", "--quiet", form];
                var git = await RunAsync(folder, arguments);
                // Twice: the second may be answered by what the first learned of a commit's kind.
                for (var again = 0; again < 2; again++)
                {
                    var read = await GitServiceReads.ReadInProcessAsync(GitService.Shared, folder, arguments, TimeSpan.FromSeconds(30), CancellationToken.None);
                    if (read is null) continue;
                    answered++;
                    Assert.True(git.ExitCode == read.Code && git.Stdout == read.Stdout, $"{string.Join(' ', arguments)} in {folder}: git {git.ExitCode} '{git.Stdout.Trim()}', files {read.Code} '{read.Stdout.Trim()}'");
                }
            }
        }

        var refs = await RunAsync(folder, Listing);
        for (var again = 0; again < 2; again++)
        {
            var listed = await GitServiceReads.ReadInProcessAsync(GitService.Shared, folder, Listing, TimeSpan.FromSeconds(30), CancellationToken.None);
            if (listed is null) continue;
            answered++;
            Assert.True(refs.ExitCode == listed.Code && refs.Stdout == listed.Stdout, $"listing in {folder}:\ngit:\n{refs.Stdout}\nfiles:\n{listed.Stdout}");
        }

        return answered;
    }

    [Fact]
    public async Task Loose_refs_and_the_symbolic_head_answer_as_git_does()
    {
        var root = await RepositoryAsync();
        Assert.True(await ParityAsync(root) > 10);
    }

    [Fact]
    public async Task Packed_refs_answer_as_git_does()
    {
        var root = await RepositoryAsync();
        await GitAsync(root, "pack-refs", "--all");
        Assert.True(await ParityAsync(root) > 10);
    }

    [Fact]
    public async Task A_detached_head_answers_as_git_does()
    {
        var root = await RepositoryAsync();
        await GitAsync(root, "checkout", "-q", "--detach", "feature");
        Assert.True(await ParityAsync(root) > 10);
    }

    [Fact]
    public async Task A_linked_trees_own_head_answers_as_git_does_beside_the_repositorys()
    {
        var root = await RepositoryAsync();
        var tree = Path.Combine(Scratch, "tree");
        await GitAsync(root, "worktree", "add", "-q", "-b", "daoris/s-2", tree);
        await File.WriteAllTextAsync(Path.Combine(tree, "t.txt"), "t\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "commit", "-q", "-m", "t");
        Assert.True(await ParityAsync(tree) > 10);
    }

    [Fact]
    public async Task The_repositorys_head_beside_a_linked_tree_with_a_detached_head_answers_as_git_does()
    {
        var root = await RepositoryAsync();
        var tree = Path.Combine(Scratch, "tree");
        await GitAsync(root, "worktree", "add", "-q", "--detach", tree);
        Assert.True(await ParityAsync(root) > 10);
    }

    [Fact]
    public async Task A_ref_deleted_while_packed_is_absent_as_in_git()
    {
        var root = await RepositoryAsync();
        await GitAsync(root, "pack-refs", "--all");
        await GitAsync(root, "branch", "-D", "feature");
        Assert.True(await ParityAsync(root) > 10);
    }

    [Fact]
    public async Task A_packed_ref_moved_and_a_deleted_one_made_again_answer_their_new_ids_as_git_does()
    {
        var root = await RepositoryAsync();
        await GitAsync(root, "pack-refs", "--all");
        await GitAsync(root, "branch", "-D", "feature");
        await File.WriteAllTextAsync(Path.Combine(root, "n.txt"), "n\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-q", "-m", "n");
        await GitAsync(root, "branch", "feature");
        Assert.True(await ParityAsync(root) > 10);
    }

    [Fact]
    public async Task A_second_asking_of_an_unmoved_ref_or_listing_takes_no_library_read()
    {
        var root = await RepositoryAsync();
        await ParityAsync(root, directory: false);
        using var count = GitLaunch.Count();

        await ParityAsync(root, directory: false);

        Assert.Empty(count.Starts);
        Assert.Empty(count.Reads.Where(read => read.Question is "Refs" or "Revisions"));
    }

    [Fact]
    public async Task A_reftable_repository_is_git_s_to_answer()
    {
        var root = Path.Combine(Scratch, "reftable");
        Directory.CreateDirectory(root);
        var init = await RunAsync(root, "init", "-q", "--ref-format=reftable");
        if (init.ExitCode != 0) return;
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "a\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "-c", "user.email=a@example.test", "-c", "user.name=A", "commit", "-q", "-m", "a");

        Assert.Null(GitRefReads.Asked(root, ["rev-parse", "--verify", "--quiet", "refs/heads/master"]));
        Assert.Null(GitRefReads.Asked(root, ["rev-parse", "HEAD"]));
    }
}
