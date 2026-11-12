using Daoris.Driver;
using LibGit2Sharp;
using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The second way's two ends. Each refusal of the library's, by its admission or at its open, is answered by git and held by a case
/// that fails the day the pinned library reads it as git does, naming the refusal that can go with its path to git; and the git in
/// use and the built-in list's pinned git, either of which retires the library the day it makes reftable or SHA-256 repositories by
/// default.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryEndsTests : LandedFixture
{
    [GitLegFact]
    public async Task A_replacement_is_git_s_for_every_question_while_the_library_reads_it_otherwise()
    {
        var root = await BuildRepositoryAsync("replaced");
        var first = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await CommitAsync(root, "two.txt", "two\n", "two");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        string[] walk = ["rev-list", "--count", "HEAD"];
        string[] names = ["diff", "--name-only", "-z", "--no-renames", first, tip];
        await SameAsync(root, walk);
        await SameAsync(root, names);
        Assert.Equal(await ListingAsync(root), await GitLibraryRefs.ListingAsync(root, CancellationToken.None));

        // Added between two questions: the tip read as a root commit of another tree, and every next question git's.
        var index = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = Path.Combine(Scratch, "replaced-index") };
        await RunAsync(root, index, null, "read-tree", first);
        var blob = (await RunAsync(root, index, "other\n", "hash-object", "-w", "--stdin")).Stdout.Trim();
        await RunAsync(root, index, null, "update-index", "--add", "--cacheinfo", $"100644,{blob},other.txt");
        var tree = (await RunAsync(root, index, null, "write-tree")).Stdout.Trim();
        await GitAsync(root, "replace", tip, (await RunAsync(root, null, null, "commit-tree", tree, "-m", "another root")).Stdout.Trim());

        foreach (var question in Enum.GetValues<GitLibraryQuestion>())
        {
            Assert.Equal(GitLibraryAdmission.Replacements, GitLibraryAdmission.RefusalOf(root, question));
        }

        await GitsAsync(root, walk);
        await GitsAsync(root, names);
        var git = await ListingAsync(root);
        Assert.Equal(git, await RefSnapshot.ReadAsync(root, CancellationToken.None));
        Ends(GitLibraryAdmission.Replacements, "refs", UnadmittedListing(root) != git);
        Ends(GitLibraryAdmission.Replacements, "walks", !Equal(await RunAsync(root, null, null, walk), Unadmitted(root, walk)));
        Ends(GitLibraryAdmission.Replacements, "names", !Equal(await RunAsync(root, null, null, names), Unadmitted(root, names)));

        await GitAsync(root, "pack-refs", "--all");
        Assert.Equal(GitLibraryAdmission.Replacements, GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Walks));
    }

    [GitLegFact]
    public async Task Reftable_sha256_and_an_unknown_extension_are_git_s_while_the_library_cannot_read_them()
    {
        var reftable = Path.Combine(Scratch, "reftable");
        Directory.CreateDirectory(reftable);
        if ((await RunAsync(reftable, null, null, "init", "--quiet", "--ref-format=reftable", "-b", "main")).Code == 0)
        {
            await CommitAsync(reftable, "a.txt", "a\n", "first");
            Assert.Equal(GitLibraryAdmission.Reftable, GitLibraryAdmission.RefusalOf(reftable, GitLibraryQuestion.Refs));
            Assert.Equal(await ListingAsync(reftable), await RefSnapshot.ReadAsync(reftable, CancellationToken.None));
            Ends(GitLibraryAdmission.Reftable, "refs", UnadmittedListing(reftable) != await ListingAsync(reftable));
            await GitsAsync(reftable, "rev-list", "--count", "HEAD");
            Ends(GitLibraryAdmission.Reftable, "walks", Unadmitted(reftable, "rev-list", "--count", "HEAD") is null);
        }

        var sha256 = Path.Combine(Scratch, "sha256");
        Directory.CreateDirectory(sha256);
        Assert.Equal(0, (await RunAsync(sha256, null, null, "init", "--quiet", "--object-format=sha256", "-b", "main")).Code);
        await CommitAsync(sha256, "a.txt", "a\n", "first");
        Assert.Null(await GitLibraryRefs.ListingAsync(sha256, CancellationToken.None));
        Assert.Equal(await ListingAsync(sha256), await RefSnapshot.ReadAsync(sha256, CancellationToken.None));
        await GitsAsync(sha256, "rev-list", "--count", "HEAD");
        Ends("sha256", "every question", UnadmittedListing(sha256) is null && Unadmitted(sha256, "rev-list", "--count", "HEAD") is null);

        var unknown = await BuildRepositoryAsync("unknown");
        await GitAsync(unknown, "config", "core.repositoryformatversion", "1");
        await GitAsync(unknown, "config", "extensions.daorisunknown", "true");
        Assert.Null(await GitLibraryRefs.ListingAsync(unknown, CancellationToken.None));
        Assert.Equal(await ListingAsync(unknown), await RefSnapshot.ReadAsync(unknown, CancellationToken.None));
        Ends("an unknown extension", "every question", UnadmittedListing(unknown) is null && Unadmitted(unknown, "rev-list", "--count", "HEAD") is null);
    }

    [GitLegFact]
    public async Task The_git_in_use_and_the_pinned_git_make_neither_reftable_nor_sha256_repositories_by_default()
    {
        var scratch = Path.Combine(Scratch, "defaults");
        Directory.CreateDirectory(scratch);
        Assert.Equal(0, (await RunAsync(scratch, null, null, "init", "--quiet")).Code);
        var (objects, format) = await RunAsync(scratch, null, null, "rev-parse", "--show-object-format");
        Assert.True(objects == 0 && format.Trim() == "sha1", $"the git in use makes {format.Trim()} repositories by default: retire the library (GITLIB1g)");
        var (refs, storage) = await RunAsync(scratch, null, null, "rev-parse", "--show-ref-format");
        Assert.True(refs != 0 || storage.Trim() == "files", $"the git in use makes {storage.Trim()} refs by default: retire the library (GITLIB1g)");

        // git 3.0 is the first release announced to make both by default.
        var pinned = ToolResources.ReadFile(Path.Combine(AppContext.BaseDirectory, "resources.json"), ToolResources.BuiltIn).Tools["git"].Versions.Keys;
        Assert.NotEmpty(pinned);
        Assert.All(pinned, version => Assert.True(
            System.Version.Parse(version).Major < 3, $"the built-in list pins git {version}, which makes reftable and SHA-256 repositories by default: retire the library (GITLIB1g)"));
    }

    [GitLegFact]
    public void The_leg_runs_the_git_it_names()
    {
        var git = TestGit.InUse;
        Assert.NotNull(git);
        var pinned = Path.GetFullPath(Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pinned-mingit")) + Path.DirectorySeparatorChar;
        var inPin = Path.GetFullPath(git).StartsWith(pinned, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            inPin == PinnedLeg.Requested,
            $"the git a fixture starts is {git}: the {(PinnedLeg.Requested ? "pinned" : "runner's")} leg must run the {(PinnedLeg.Requested ? "pinned MinGit" : "runner's git")}");
    }

    private static void Ends(string refusal, string question, bool held) =>
        Assert.True(held, $"the pinned library reads {question} under {refusal} as git does: that refusal and its path to git can go (GITLIB1g)");

    private static bool Equal((int Code, string Stdout) git, GitLibraryAnswer? library) => library is not null && (library.Code, library.Stdout) == git;

    private static async Task<string?> ListingAsync(string folder)
    {
        var (code, stdout) = await RunAsync(folder, null, null, "for-each-ref", $"--format={RefSnapshot.Format}", "refs/heads", "refs/remotes/origin");
        return code == 0 ? stdout : null;
    }

    private static string? UnadmittedListing(string folder)
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
}
