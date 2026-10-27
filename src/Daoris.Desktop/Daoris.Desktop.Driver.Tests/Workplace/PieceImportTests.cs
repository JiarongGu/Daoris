using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 point 2; the co-agent design §7.9, Codex's read 24): the import's rules over what the keeper's walk found and
/// what the base holds. Regular files only; a link, a nested repository and a submodule refused with the base's files under them
/// left as they were; a new ignored file left out; a file and a piece over their bounds; deletions; paths beyond the piece named,
/// never refused; and the object id the keeper compares by is git's.
/// </summary>
public sealed class PieceImportTests
{
    private static readonly Dictionary<string, PieceImport.BaseFile> Base = new(StringComparer.Ordinal)
    {
        ["README.md"] = new("100644", "r0"),
        ["src/a.ts"] = new("100644", "a0"),
        ["src/run.sh"] = new("100755", "x0"),
        ["vendor/lib/x.c"] = new("100644", "v0"),
        ["modules/sub"] = new("160000", "m0"),
        ["docs/old.md"] = new("100644", "d0"),
    };

    private static PieceEntry File(string path, long size = 10) => new(path, PieceEntryKind.File, size, path);

    /// <summary>Each found file's object id is its path with the content marker the test names, so "unchanged" is a base id.</summary>
    private static PieceImport.Judged Judge(IEnumerable<PieceEntry> found, Dictionary<string, string>? ids = null, params string[] ignored) =>
        PieceImport.Judge([.. found], Base, entry => ids?.GetValueOrDefault(entry.Path) ?? "new-" + entry.Path, ignored.Contains);

    [Fact]
    public void A_changed_file_keeps_its_base_mode_a_new_one_is_regular_an_unchanged_one_is_not_imported_and_a_gone_one_is_deleted()
    {
        var judged = Judge(
            [File("README.md"), File("src/a.ts"), File("src/run.sh"), File("src/b.ts"), File("vendor/lib/x.c")],
            new() { ["README.md"] = "r0", ["vendor/lib/x.c"] = "v0" });

        Assert.Equal(
            [("src/a.ts", "100644"), ("src/b.ts", "100644"), ("src/run.sh", "100755")],
            judged.Changed.Select(pair => (pair.Entry.Path, pair.Mode)).OrderBy(pair => pair.Path, StringComparer.Ordinal));
        Assert.Equal(["docs/old.md"], judged.Deleted);
        Assert.Empty(judged.LeftOut);
        Assert.False(judged.Refused);
    }

    [Fact]
    public void A_link_and_a_nested_repository_are_refused_and_what_the_base_held_under_them_is_left_as_it_was()
    {
        var judged = Judge(
            [File("README.md"), File("src/a.ts"), File("src/run.sh"), new("vendor", PieceEntryKind.Link, 0, null),
             new("docs", PieceEntryKind.Repository, 0, null)],
            new() { ["README.md"] = "r0", ["src/a.ts"] = "a0", ["src/run.sh"] = "x0" });

        Assert.Equal([new PieceLeftOut("docs", PieceImport.WhyRepository), new PieceLeftOut("vendor", PieceImport.WhyLink)], judged.LeftOut);
        // Nothing under either is deleted, and the gitlink is never touched: the import changed nothing.
        Assert.Empty(judged.Deleted);
        Assert.True(judged.Empty);
    }

    [Fact]
    public void A_file_written_at_a_submodules_path_is_refused_and_the_gitlink_kept()
    {
        var judged = Judge([File("README.md"), File("modules/sub/inner.c")], new() { ["README.md"] = "r0" });

        Assert.Contains(new PieceLeftOut("modules/sub/inner.c", PieceImport.WhySubmodule), judged.LeftOut);
        Assert.DoesNotContain("modules/sub", judged.Deleted);
        Assert.DoesNotContain(judged.Changed, pair => pair.Entry.Path.StartsWith("modules/", StringComparison.Ordinal));
    }

    [Fact]
    public void A_new_ignored_file_is_left_out_while_a_tracked_one_the_rules_would_ignore_is_still_imported()
    {
        var judged = Judge([File("README.md"), File("debug.log"), File("src/a.ts")], new() { ["README.md"] = "r0" }, "debug.log", "src/a.ts");

        Assert.Equal([new PieceLeftOut("debug.log", PieceImport.WhyIgnored)], judged.LeftOut);
        Assert.Contains(judged.Changed, pair => pair.Entry.Path == "src/a.ts");
    }

    [Fact]
    public void A_file_over_its_bound_is_refused_with_its_base_kept_and_a_piece_over_its_bound_is_refused_whole()
    {
        var big = Judge([File("README.md", PieceImport.FileBound + 1), File("src/a.ts")]);
        Assert.Contains(new PieceLeftOut("README.md", PieceImport.WhyTooBig), big.LeftOut);
        Assert.DoesNotContain("README.md", big.Deleted);
        Assert.False(big.Refused);

        var each = PieceImport.FileBound - 1;
        var many = Judge(Enumerable.Range(0, 6).Select(n => File($"new/{n}.bin", each)));
        Assert.True(many.Refused);
        Assert.True(many.Empty);
        Assert.Contains(new PieceLeftOut("", PieceImport.WhyPieceTooBig), many.LeftOut);
    }

    [Fact]
    public void The_paths_beyond_the_piece_are_named_and_none_where_the_ask_named_no_paths()
    {
        string[] touched = ["src/a.ts", "src/deep/b.ts", "srcs/c.ts", "README.md"];

        Assert.Equal(["README.md", "srcs/c.ts"], PieceImport.Beyond(touched, ["src"]));
        Assert.Equal(["src/deep/b.ts", "srcs/c.ts"], PieceImport.Beyond(touched, ["src/a.ts", "README.md/"]));
        Assert.Empty(PieceImport.Beyond(touched, []));
    }

    [Fact]
    public void The_blob_id_is_gits_for_the_same_bytes()
    {
        // `printf 'hello\n' | git hash-object --stdin`, and the empty blob.
        Assert.Equal("ce013625030ba8dba906f756967f9e9ca394464a", PieceImport.BlobId(Encoding.ASCII.GetBytes("hello\n")));
        Assert.Equal("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", PieceImport.BlobId([]));
    }
}
