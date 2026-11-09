using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's ref listing held to git's on real git, value for value (packed, loose and symbolic refs, a tag a branch stores, each
/// kind of upstream, an ambiguous short name, every folder), a fan-out's open repositories, and a missing tip answered by git.
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

    [GitLegFact]
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
            Assert.Null(GitLibraryAdmission.RefusalOf(folder, GitLibraryQuestion.Refs));
            Assert.Equal(git, await GitLibraryRefs.ListingAsync(folder, CancellationToken.None));
            Assert.Equal(git, await RefSnapshot.ReadAsync(folder, CancellationToken.None));
        }

        Assert.Contains("refs/heads/main ", (await GitListingAsync(root))!);
        Assert.Contains(" refs/remotes/origin/main remotes/origin/main ", (await GitListingAsync(root))!);
        Assert.Equal(0, GitLibrary.Open);
    }

    [GitLegFact]
    public async Task A_fan_out_of_forty_calls_holds_at_most_eight_repositories_open_and_none_after()
    {
        var root = await BuildRepositoryAsync("fanned");
        var most = 0;
        var calls = Enumerable.Range(0, 40).Select(_ => GitLibrary.AskAsync(root, GitLibraryQuestion.Refs, call =>
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

    [GitLegFact]
    public async Task A_tip_whose_object_is_missing_is_answered_by_git()
    {
        var root = await BuildRepositoryAsync("missing");
        await File.WriteAllTextAsync(Path.Combine(root, ".git", "refs", "heads", "absent"), new string('7', 40) + "\n");

        Assert.Null(await GitLibraryRefs.ListingAsync(root, CancellationToken.None));
        Assert.Equal(await GitListingAsync(root), await RefSnapshot.ReadAsync(root, CancellationToken.None));
    }
}
