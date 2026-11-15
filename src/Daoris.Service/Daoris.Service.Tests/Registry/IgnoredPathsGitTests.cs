using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// What a checkout's read leaves out, asked of real git over a real checkout: git names each ignored folder without listing
/// what is in it, and a walk opens nothing under one. Git is what these prove, so they are Process.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class IgnoredPathsGitTests() : ScratchFolder("daoris-ignored-")
{
    [Fact]
    public void Git_names_each_ignored_folder_once_and_nothing_in_it()
    {
        Write(".gitignore", "node_modules/\n_fixtures/\n.claude/worktrees/\n*.log\n");
        Write("docs/design.md", "# Design\n");
        Write("docs/build.log", "built\n");
        // A folder holding nothing git would take is named as ignored itself, so the site has a page of its own.
        Write("docs/site/guide.md", "# The site's guide\n");
        Write("docs/site/node_modules/pkg/README.md", "# A package\n");
        Write("_fixtures/install/app/bin/Daoris.dll", "binary");
        Write(".claude/worktrees/side/AGENTS.md", "# A worktree's brief\n");
        Write(".claude/knowledge/twins.md", "# Twins\n");
        GitCheckouts.Init(_root);

        var ignored = IgnoredPaths.Of(_root);

        Assert.Equal(
            [".claude/worktrees/", "_fixtures/", "docs/build.log", "docs/site/node_modules/"],
            ignored.Entries.Order(StringComparer.Ordinal));
        Assert.False(ignored.Covers("docs/design.md"));
        Assert.False(ignored.Covers(".claude/knowledge/twins.md"));

        // Asked under some folders, it answers for those alone.
        Assert.Equal([".claude/worktrees/"], IgnoredPaths.Of(_root, ".claude").Entries);
    }

    [Fact]
    public void A_read_of_a_checkouts_documents_opens_nothing_under_a_folder_git_ignores()
    {
        Write(".gitignore", "node_modules/\nscratch.md\n");
        Write("docs/design.md", "# The probe lock\n\n## Why a lock\n\nOne probe at a time.\n");
        // A large ignored tree inside the documents folder: a docs site's packages, each with a README the walk would index.
        for (var package = 0; package < 40; package++)
        {
            Write($"docs/site/node_modules/pkg{package}/README.md", $"# Package {package}\n\n## Zeppelins\n\nNot this repository's.\n");
        }
        var held = Write("docs/site/node_modules/pkg0/README.md", "# Held\n\n## Zeppelins\n\nHeld with no sharing.\n");
        Write("docs/scratch.md", "# Scratch\n\n## Zeppelins\n\nA person's own, ignored.\n");
        GitCheckouts.Init(_root);

        // Held with no sharing at all, as a swap or a build holds a file: a read that opened it would fail here.
        using var hold = new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var scratch = new FileStream(Path.Combine(_root, "docs", "scratch.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var entries = new RepositoryScanner(documents: "docs").Scan(_root);

        Assert.Contains(entries, entry => entry.RelativePath == "docs/design.md");
        Assert.DoesNotContain(entries, entry => entry.RelativePath.Contains("node_modules", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.RelativePath == "docs/scratch.md");
    }

    [Fact]
    public void A_declared_index_git_ignores_whole_is_read_as_it_stands()
    {
        // A generated index each checkout writes for itself, never committed.
        Write(".gitignore", "docs/index/\n");
        Write("docs/design.md", "# Design\n\n## Why\n\nBecause.\n");
        Write("docs/index/README.md", "# Where things are\n\n| Route | File |\n|---|---|\n| `ZEPPELIN` | `Z.cs:1` |\n");
        Write("docs/index/outlines/Z.cs.md", "# Z.cs\n\n- `class Z` 1-9\n");
        GitCheckouts.Init(_root);

        var entries = new RepositoryScanner(documents: "docs", index: "docs/index").Scan(_root);

        Assert.Contains(entries, entry => entry.RelativePath == "docs/index/README.md" && entry.Kind == EntryKind.Index);
        Assert.Contains(entries, entry => entry.RelativePath == "docs/index/outlines/Z.cs.md" && entry.Kind == EntryKind.Index);
        Assert.Contains(entries, entry => entry.RelativePath == "docs/design.md");
    }
}
