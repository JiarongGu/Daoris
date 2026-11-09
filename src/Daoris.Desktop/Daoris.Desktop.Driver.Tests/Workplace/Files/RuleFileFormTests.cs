using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The rule file in git's syntax (the tree-pool design §36.2, §37.2, §38.1): a pattern covers a top as git's ignore does, the file
/// says its form, the person's file is rewritten once and only where it translates exactly, a kept path is read as written until
/// the person words it, and a top holding Daoris's copy is partitioned so the copy no longer makes it the line's own.
/// </summary>
public sealed class RuleFileFormTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "file-rules", Guid.NewGuid().ToString("N")[..8]);

    public RuleFileFormTests() => Directory.CreateDirectory(Path.GetDirectoryName(PersonFiles.PathOf(_home))!);

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_home);

    [Fact]
    public void A_pattern_covers_a_top_as_gits_ignore_does()
    {
        Assert.True(GitPattern.Covers(["out/", "!out/keep/"], "out/keep/", false));
        Assert.False(GitPattern.Covers(["*.log", "!keep.log"], "a/keep.log", false));
        Assert.True(GitPattern.Covers(["bin/"], "src/App/bin/", false));
        Assert.False(GitPattern.Covers(["/bin/"], "src/App/bin/", false));
        Assert.True(GitPattern.Covers(["/bin/"], "bin/Debug/app.dll", false));
        Assert.False(GitPattern.Covers(["/bin/"], "bin", false));
        Assert.False(GitPattern.Covers(["bin/"], "Bin/", false));
        Assert.True(GitPattern.Covers(["bin/"], "Bin/", true));
        Assert.True(GitPattern.Covers(["/logs/**/"], "logs/a/", false));
        Assert.False(GitPattern.Covers(["/logs/**/"], "logs/", false));
        Assert.True(GitPattern.Covers(["/a/**/b"], "a/b", false));
        Assert.True(GitPattern.Covers(["/\\[x]"], "[x]", false));
        Assert.True(GitPattern.Covers(["/[ab]c"], "bc", false));
    }

    [Fact]
    public void A_literal_is_named_only_for_an_anchored_pattern_without_a_wildcard_and_a_top_is_anchored_with_its_escapes()
    {
        Assert.Equal("local/x.txt", GitPattern.LiteralOf("/local/x.txt"));
        Assert.Equal("src/a/node_modules/", GitPattern.LiteralOf("/src/a/node_modules/"));
        Assert.Null(GitPattern.LiteralOf("!/x/"));
        Assert.Null(GitPattern.LiteralOf("bin/"));
        Assert.Null(GitPattern.LiteralOf("/tools/*/bin/"));
        Assert.Null(GitPattern.LiteralOf(new FileRule(FileKind.Output, ["P/*"]) { FirstForm = true }, "P/*"));
        Assert.Equal("/a\\*b\\ /", GitPattern.Anchored("a*b /"));
        Assert.Equal("a*b /", GitPattern.LiteralOf(GitPattern.Anchored("a*b /")));
    }

    [Fact]
    public void The_file_says_its_form_and_a_form_it_does_not_know_reads_as_unreadable()
    {
        var second = RepositoryFilesReader.Read("""{ "form": 2, "rules": [ { "kind": "output", "paths": ["bin/"], "take": "base" } ] }""");
        Assert.Empty(second.Problems);
        Assert.Equal(["bin/"], second.Rules.Single().Paths);
        Assert.True(second.Rules.Single().Matches("src/App/bin/"));

        var first = RepositoryFilesReader.Read("""{ "rules": [ { "kind": "output", "paths": ["bin/"] } ] }""");
        Assert.True(first.FirstForm);
        Assert.False(first.Rules.Single().Matches("src/App/bin/"));

        var third = RepositoryFilesReader.Read("""{ "form": 3, "rules": [] }""");
        Assert.Contains("form", Assert.Single(third.Problems));
        Assert.Empty(RepositoryFilesReader.Read("""{ "form": 2, "rules": [ { "kind": "local", "paths": ["/x"], "copy": true } ] }""").Rules);
    }

    [Fact]
    public void This_repositorys_file_reads_in_gits_form_its_bin_covering_every_projects_and_the_tools_set_aside()
    {
        var read = RepositoryFilesReader.Read(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, DeclarationNames.Rules)));
        Assert.Empty(read.Problems);
        Assert.False(read.FirstForm);
        var rules = FileRules.Of([], read.Rules, PersonFilesRead.Empty, "Daoris");
        var floor = new FileFloorFacts([], []);
        Assert.Equal("base", rules.For("src/Daoris.Desktop/Daoris.Desktop.App/bin/", floor).Rule!.Take);
        Assert.Equal("aside", rules.For("tools/git-library-bench/bin/", floor).Rule!.Take);
        Assert.True(rules.For("local/lyntai-local.props", floor).Copy);
        Assert.Equal(FileKind.Local, rules.For("local/", floor).Kind);
    }

    [Fact]
    public void The_persons_file_is_rewritten_once_in_gits_form_its_first_form_set_aside()
    {
        File.WriteAllText(PersonFiles.PathOf(_home), """{ "machine": { "rules": [ { "kind": "scratch", "paths": ["scratch/", "scratch"] } ], "deny": ["run(rm *)"] } }""");
        var read = PersonFiles.Read(_home);
        Assert.False(read.FirstForm);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(PersonFiles.PathOf(_home))!, PersonFilesForm.SetAsideName)));
        var written = File.ReadAllText(PersonFiles.PathOf(_home));
        Assert.Equal(2, JsonNode.Parse(written)!["form"]!.GetValue<int>());
        Assert.Contains("run(rm *)", written, StringComparison.Ordinal);

        Assert.False(PersonFiles.Read(_home).FirstForm);
        Assert.Equal(written, File.ReadAllText(PersonFiles.PathOf(_home)));
        var rules = FileRules.Of([], [], read, "web");
        Assert.Equal(FileKind.Scratch, rules.For("scratch/", new([], [])).Kind);
        Assert.Equal(FileKind.Scratch, rules.For("scratch", new([], [])).Kind);
    }

    [Fact]
    public void A_kept_path_is_read_as_written_until_the_person_words_it_and_no_other_edit_is_written_before()
    {
        File.WriteAllText(PersonFiles.PathOf(_home), """{ "machine": { "rules": [ { "kind": "local", "paths": ["P/"] }, { "kind": "output", "paths": ["P/*"] } ] } }""");
        var read = PersonFiles.Read(_home);
        Assert.True(read is { FirstForm: true, Keeps: true });
        var rules = FileRules.Of([], [], read, "web");
        Assert.Equal(FileKind.Local, rules.For("P/deep/", new([], [])).Kind);
        Assert.Equal(FileKind.Output, rules.For("P/", new([], [])).Kind);
        Assert.Throws<DriverException>(() => FilesEdits.Set(_home, "web", true, "scratch", ["tmp/"], null));

        FilesEdits.Settle(_home, [new FilesWording("machine", "P/*", Whole: false)]);
        var settled = PersonFiles.Read(_home);
        Assert.False(settled.FirstForm);
        var now = FileRules.Of([], [], settled, "web");
        Assert.Equal(FileKind.Local, now.For("P/deep/", new([], [])).Kind);
        Assert.Equal(FileKind.Output, now.For("P/a.txt", new([], [])).Kind);
        FilesEdits.Set(_home, "web", true, "scratch", ["tmp/"], null);
        Assert.Equal("/tmp/", PersonFiles.Read(_home).Machine.Rules[^1].Paths.Single());
    }

    [Fact]
    public void A_top_holding_daoriss_copy_is_partitioned_so_the_copy_is_daoriss_and_a_file_beside_it_the_lines()
    {
        var slot = Path.Combine(_home, "slot");
        Directory.CreateDirectory(Path.Combine(slot, "local"));
        File.WriteAllText(Path.Combine(slot, "local", "lyntai-local.props"), "<Project />");
        var record = new TreeFilesState(false, false, []) { Copies = new Dictionary<string, string> { ["local/lyntai-local.props"] = TreeCopies.HashOf(Path.Combine(slot, "local", "lyntai-local.props"))! } };

        var alone = TopPartition.Of(record, slot, "local/")!.Value;
        Assert.Equal(["local/lyntai-local.props"], alone.Copies);
        Assert.Empty(alone.Others);

        File.WriteAllText(Path.Combine(slot, "local", "notes.md"), "mine");
        var beside = TopPartition.Of(record, slot, "local/")!.Value;
        Assert.Equal(["local/notes.md"], beside.Others);
        Assert.Null(TopPartition.Of(record, slot, "bin/"));
    }
}
