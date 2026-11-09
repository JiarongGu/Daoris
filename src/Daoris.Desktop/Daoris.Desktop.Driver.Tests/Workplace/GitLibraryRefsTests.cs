using System.Diagnostics;
using Daoris.Driver;
using LibGit2Sharp;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's ref listing held to git's for the same question on real git, value for value: packed and loose refs, a symbolic
/// ref, an annotated tag a branch stores, upstreams through origin, through <c>.</c> and gone, a short name another rule makes
/// ambiguous, asked from the checkout, a linked tree holding a branch and a detached one. And each refusal answered by git, every
/// one held by a case that fails once the pinned library reads it as git does, naming the refusal that can go; and the git in use,
/// which retires the library the day it makes reftable or SHA-256 repositories by default.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryRefsTests : LandedFixture
{
    private static async Task<(int Code, string Stdout)> RunAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo { FileName = "git", WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await stderr;
        return (process.ExitCode, await stdout);
    }

    /// <summary>What git lists for the question the library answers; null where git could not list.</summary>
    private static async Task<string?> GitListingAsync(string folder)
    {
        var (code, stdout) = await RunAsync(folder, "for-each-ref", $"--format={RefSnapshot.Format}", "refs/heads", "refs/remotes/origin");
        return code == 0 ? stdout : null;
    }

    /// <summary>The library's listing with no admission asked: what it would answer were the refusal gone.</summary>
    private static string? Unadmitted(string folder)
    {
        try
        {
            using var repository = new Repository(folder);
            return GitLibraryRefs.Listing(new GitLibraryCall(repository, RefFiles.GitDirOf(folder)!, RefFiles.CommonDirOf(folder)!));
        }
        catch (LibGit2SharpException)
        {
            return null;
        }
    }

    [Fact]
    public async Task The_library_lists_what_git_lists_in_every_folder_of_a_repository()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await GitAsync(root, "branch", "topic");
        await GitAsync(root, "branch", "feature/a");
        await GitAsync(root, "branch", "feature-a");
        await GitAsync(root, "branch", "--track", "follow", "main");
        await GitAsync(root, "branch", "gone");
        await GitAsync(root, "config", "branch.gone.remote", "origin");
        await GitAsync(root, "config", "branch.gone.merge", "refs/heads/gone");
        // A branch named as origin's copy of the line: the line's upstream shortens to `remotes/origin/main`, as git's strict rule says.
        await GitAsync(root, "branch", "origin/main");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "tag", "-a", "marked", "-m", "a tag");
        await GitAsync(root, "update-ref", "refs/heads/tagged", (await GitAsync(root, "rev-parse", "marked")).Trim());
        await GitAsync(root, "remote", "set-head", "origin", "main");
        await GitAsync(root, "pack-refs", "--all");
        await GitAsync(root, "branch", "loose");
        var linked = Path.Combine(Scratch, "engine-topic");
        await GitAsync(root, "worktree", "add", linked, "topic");
        var detached = Path.Combine(Scratch, "engine-detached");
        await GitAsync(root, "worktree", "add", "--detach", detached);

        foreach (var folder in new[] { root, linked, detached })
        {
            var git = await GitListingAsync(folder);
            Assert.NotNull(git);
            Assert.Contains("refs/heads/main ", git);
            Assert.Null(GitLibraryAdmission.RefusalOf(folder));
            Assert.Equal(git, await GitLibraryRefs.ListingAsync(folder, CancellationToken.None));
            Assert.Equal(git, await RefSnapshot.ReadAsync(folder, CancellationToken.None));
        }

        Assert.Contains("refs/heads/main ", (await GitListingAsync(root))!);
        Assert.Contains(" refs/remotes/origin/main remotes/origin/main ", (await GitListingAsync(root))!);
        Assert.Equal(0, GitLibrary.Open);
    }

    [Fact]
    public async Task A_repository_with_a_replacement_is_answered_by_git_and_the_library_still_reads_it_otherwise()
    {
        var root = await BuildRepositoryAsync("replaced");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "branch", "other");
        await CommitAsync(root, "other.txt", "another tree\n", "another tree");
        var other = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "reset", "--hard", tip);
        Assert.Null(GitLibraryAdmission.RefusalOf(root));
        Assert.Equal(await GitListingAsync(root), await GitLibraryRefs.ListingAsync(root, CancellationToken.None));

        // Added between two questions: the next is git's, which reads main's tree through the replacement.
        await GitAsync(root, "replace", tip, other);
        var git = await GitListingAsync(root);
        Assert.Equal(GitLibraryAdmission.Replacements, GitLibraryAdmission.RefusalOf(root));
        Assert.Null(await GitLibraryRefs.ListingAsync(root, CancellationToken.None));
        Assert.Equal(git, await RefSnapshot.ReadAsync(root, CancellationToken.None));
        Assert.True(Unadmitted(root) != git, "the pinned library reads a replaced tip as git does: the replacements refusal can go (GITLIB1g)");

        // Packed, it is refused the same.
        await GitAsync(root, "pack-refs", "--all");
        Assert.Equal(GitLibraryAdmission.Replacements, GitLibraryAdmission.RefusalOf(root));
    }

    [Fact]
    public async Task Reftable_sha256_and_an_unknown_extension_are_answered_by_git_while_the_library_cannot_read_them()
    {
        var reftable = Path.Combine(Scratch, "reftable");
        Directory.CreateDirectory(reftable);
        if ((await RunAsync(reftable, "init", "--quiet", "--ref-format=reftable", "-b", "main")).Code == 0)
        {
            await CommitAsync(reftable, "a.txt", "a\n", "first");
            Assert.Equal(GitLibraryAdmission.Reftable, GitLibraryAdmission.RefusalOf(reftable));
            Assert.Equal(await GitListingAsync(reftable), await RefSnapshot.ReadAsync(reftable, CancellationToken.None));
            Assert.True(Unadmitted(reftable) != await GitListingAsync(reftable), "the pinned library reads reftable refs as git does: the reftable refusal can go (GITLIB1g)");
        }

        var sha256 = Path.Combine(Scratch, "sha256");
        Directory.CreateDirectory(sha256);
        Assert.Equal(0, (await RunAsync(sha256, "init", "--quiet", "--object-format=sha256", "-b", "main")).Code);
        await CommitAsync(sha256, "a.txt", "a\n", "first");
        Assert.Null(await GitLibraryRefs.ListingAsync(sha256, CancellationToken.None));
        Assert.Equal(await GitListingAsync(sha256), await RefSnapshot.ReadAsync(sha256, CancellationToken.None));
        Assert.True(Unadmitted(sha256) is null, "the pinned library opens SHA-256 objects: its refusal at open can go (GITLIB1g)");

        var unknown = await BuildRepositoryAsync("unknown");
        await GitAsync(unknown, "config", "core.repositoryformatversion", "1");
        await GitAsync(unknown, "config", "extensions.daorisunknown", "true");
        Assert.Null(await GitLibraryRefs.ListingAsync(unknown, CancellationToken.None));
        Assert.Equal(await GitListingAsync(unknown), await RefSnapshot.ReadAsync(unknown, CancellationToken.None));
        Assert.True(Unadmitted(unknown) is null, "the pinned library opens an extension it does not know: its refusal at open can go (GITLIB1g)");
        Assert.Equal(0, GitLibrary.Open);
    }

    [Fact]
    public async Task A_fan_out_of_forty_calls_holds_at_most_eight_repositories_open_and_none_after()
    {
        var root = await BuildRepositoryAsync("fanned");
        var most = 0;
        var calls = Enumerable.Range(0, 40).Select(_ => GitLibrary.AskAsync(root, call =>
        {
            var open = GitLibrary.Open;
            InterlockedMax(ref most, open);
            Thread.Sleep(20);
            return call.Repository.Head.CanonicalName;
        }, CancellationToken.None));

        Assert.All(await Task.WhenAll(calls), head => Assert.Equal("refs/heads/main", head));
        Assert.InRange(most, 2, GitLibrary.AtOnce);
        Assert.Equal(0, GitLibrary.Open);

        static void InterlockedMax(ref int most, int seen)
        {
            for (var was = Volatile.Read(ref most); seen > was; was = Volatile.Read(ref most))
            {
                if (Interlocked.CompareExchange(ref most, seen, was) == was) return;
            }
        }
    }

    [Fact]
    public async Task A_tip_whose_object_is_missing_is_answered_by_git()
    {
        var root = await BuildRepositoryAsync("missing");
        await File.WriteAllTextAsync(Path.Combine(root, ".git", "refs", "heads", "absent"), new string('7', 40) + "\n");

        Assert.Null(await GitLibraryRefs.ListingAsync(root, CancellationToken.None));
        Assert.Equal(await GitListingAsync(root), await RefSnapshot.ReadAsync(root, CancellationToken.None));
    }

    [Fact]
    public async Task The_git_in_use_makes_neither_reftable_nor_sha256_repositories_by_default()
    {
        var scratch = Path.Combine(Scratch, "defaults");
        Directory.CreateDirectory(scratch);
        Assert.Equal(0, (await RunAsync(scratch, "init", "--quiet")).Code);
        var (objects, format) = await RunAsync(scratch, "rev-parse", "--show-object-format");
        Assert.True(objects == 0 && format.Trim() == "sha1", $"the git in use makes {format.Trim()} repositories by default: retire the library (GITLIB1g)");
        var (refs, storage) = await RunAsync(scratch, "rev-parse", "--show-ref-format");
        Assert.True(refs != 0 || storage.Trim() == "files", $"the git in use makes {storage.Trim()} refs by default: retire the library (GITLIB1g)");
    }
}
