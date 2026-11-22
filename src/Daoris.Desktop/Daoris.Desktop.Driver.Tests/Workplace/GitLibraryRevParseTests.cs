using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>rev-parse</c>: the single layout reads (<c>--abbrev-ref HEAD</c>, <c>--git-common-dir</c>) and the object forms (a full id,
/// <c>ID^{commit}</c>, <c>ID^{tree}</c>, <c>ID:path</c>) answered by the library as git says them, on real git, with no git started; what
/// the library cannot say (a missing object, an odd path, a gitlink, a subfolder) stays git's.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryRevParseTests : LandedFixture
{
    [GitLegFact]
    public async Task The_single_layout_reads_are_git_s_in_the_checkout_and_in_linked_trees()
    {
        var (root, _) = await RepositoryWithOriginAsync("revparse-layout");
        var linked = Path.Combine(Scratch, "revparse-layout-tree");
        var detached = Path.Combine(Scratch, "revparse-layout-detached");
        await GitAsync(root, "worktree", "add", "-b", "topic", linked);
        await GitAsync(root, "worktree", "add", "--detach", detached);

        foreach (var folder in new[] { root, linked, detached })
        {
            await SameAsync(folder, "rev-parse", "--abbrev-ref", "HEAD");
            await SameAsync(folder, "--no-optional-locks", "rev-parse", "--abbrev-ref", "HEAD");
            await SameAsync(folder, "rev-parse", "--path-format=absolute", "--git-common-dir");
        }

        // A branch another ref makes ambiguous is spelt "heads/x" by git: its to say.
        await GitAsync(root, "tag", "topic");
        await GitsAsync(linked, "rev-parse", "--abbrev-ref", "HEAD");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        await GitsAsync(Path.Combine(root, "sub"), "rev-parse", "--path-format=absolute", "--git-common-dir");
    }

    [GitLegFact]
    public async Task An_object_by_its_full_id_is_named_as_git_names_it()
    {
        var (root, _) = await RepositoryWithOriginAsync("revparse-ids");
        Directory.CreateDirectory(Path.Combine(root, "docs", "deep"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "deep", "note.md"), "note\n");
        await GitAsync(root, "add", ".");
        string[] who = ["-c", "user.email=fixture@example.test", "-c", "user.name=Fixture"];
        await GitAsync(root, [.. who, "commit", "-m", "notes"]);
        await GitAsync(root, [.. who, "tag", "-a", "-m", "annotated", "annotated"]);
        var commit = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var tree = (await GitAsync(root, "rev-parse", "HEAD^{tree}")).Trim();
        var blob = (await GitAsync(root, "rev-parse", "HEAD:docs/deep/note.md")).Trim();
        var tag = (await GitAsync(root, "rev-parse", "refs/tags/annotated")).Trim();

        // Each git start is a second on a loaded machine, so the cases are few: each kind of object, each form, a path found and one not.
        foreach (var id in new[] { commit, blob, tag })
        {
            await SameAsync(root, "rev-parse", "--verify", "--quiet", id);
            await SameAsync(root, "rev-parse", "--verify", "--quiet", id + "^{commit}");
        }

        foreach (var id in new[] { commit, tree, tag })
        {
            await SameAsync(root, "rev-parse", "--verify", "--quiet", id + "^{tree}");
            await SameAsync(root, "rev-parse", "--verify", "-q", $"{id}:docs/deep/note.md");
        }

        await SameAsync(root, "rev-parse", "--verify", "--quiet", $"{commit}:docs");
        await SameAsync(root, "rev-parse", "--verify", "--quiet", $"{commit}:docs/absent");
        // A blob has no tree to look in.
        await SameAsync(root, "rev-parse", "--verify", "--quiet", $"{blob}:docs");
        await SameAsync(root, "--no-optional-locks", "rev-parse", "--verify", "--quiet", commit + "^{commit}");
    }

    [GitLegFact]
    public async Task What_the_library_cannot_say_exactly_is_git_s()
    {
        var (root, _) = await RepositoryWithOriginAsync("revparse-git");
        var commit = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var missing = new string('1', 40);
        await GitsAsync(root, "rev-parse", "--verify", "--quiet", missing + "^{commit}");
        await GitsAsync(root, "rev-parse", "--verify", "--quiet", $"{commit}:docs/../x");
        await GitsAsync(root, "rev-parse", "--verify", "--quiet", $"{commit}:");
    }
}
