using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A path shared into every tree over a real repository and real links: one place in the checkout, linked by the files stage,
/// trusted only while its entry still reads as that link, and never made over what stands in a tree.
/// </summary>
public sealed class TreeSharesTests : IDisposable
{
    private const string Rules = """
        { "form": 2, "rules": [
          { "kind": "local", "paths": ["/local/rules/"], "reach": "write" },
          { "kind": "local", "paths": ["/local/frozen/"], "reach": "read" },
          { "kind": "local", "paths": ["/local/notes.md"], "reach": "write" }
        ] }
        """;

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-shares", Guid.NewGuid().ToString("N")[..8]);
    private readonly Dictionary<string, string> _trees = [];

    private string Home => Path.Combine(_scratch, "home");

    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose()
    {
        foreach (var tree in _trees.Values) TreeLinks.Unlinked(tree);
        GitSeed.Discard(_scratch);
    }

    private async Task MakeAsync(string? gates = null)
    {
        Directory.CreateDirectory(Path.Combine(Root, "local", "rules"));
        Directory.CreateDirectory(Path.Combine(Root, "local", "frozen"));
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "local/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"), Rules);
        await File.WriteAllTextAsync(Path.Combine(Root, "README.md"), "# engine\n");
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "first");
        await File.WriteAllTextAsync(Path.Combine(Root, "local", "rules", "house.md"), "the house rule\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "local", "frozen", "x.txt"), "frozen\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "local", "notes.md"), "the checkout's notes\n");
        if (gates is not null) await File.WriteAllTextAsync(Path.Combine(Root, "daoris.gates.json"), gates);
    }

    private async Task<TreeFilesOutcome> StageAsync(string name)
    {
        if (!_trees.TryGetValue(name, out var tree))
        {
            tree = Path.Combine(_scratch, name);
            await GitAsync(Root, "worktree", "add", "-q", "-b", $"daoris/{name}", tree, "main");
            TreeFilesRecord.Mark(Home, tree);
            _trees[name] = tree;
        }

        return await new TreeFilesStage(Home, Root, tree, "engine").RunAsync(CancellationToken.None);
    }

    private string In(string name, string path) => Path.Combine(_trees[name], path.Replace('/', Path.DirectorySeparatorChar));

    private string Place(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));

    private static IReadOnlyList<NotePart> Parts(TreeFilesOutcome outcome) => [.. outcome.Said.SelectMany(each => each.Parts)];

    private static string? WhyOf(TreeFilesOutcome outcome, string path) =>
        Parts(outcome).FirstOrDefault(part => part.Code == StartNoteCodes.StartedShareDeclined.Code && Equals(part.Value("path"), path))?.Value("why") as string;

    private IReadOnlyList<string> Shared(string name) => [.. (TreeFilesRecord.Read(Home, _trees[name])?.Shares ?? []).Select(share => share.Path)];

    [Fact]
    public async Task A_folder_shared_read_and_write_is_one_place_a_write_in_one_tree_is_read_in_the_other()
    {
        await MakeAsync();

        var first = await StageAsync("t1");
        await StageAsync("t2");

        Assert.True(File.GetAttributes(In("t1", "local/rules")).HasFlag(FileAttributes.ReparsePoint));
        Assert.Contains("local/rules/", Shared("t1"));
        Assert.Contains("local/frozen/", Shared("t2"));
        await File.WriteAllTextAsync(In("t1", "local/rules/new.md"), "written in t1\n");
        Assert.Equal("written in t1\n", await File.ReadAllTextAsync(In("t2", "local/rules/new.md")));
        Assert.Equal("written in t1\n", await File.ReadAllTextAsync(Place("local/rules/new.md")));
        Assert.Equal("the house rule\n", await File.ReadAllTextAsync(In("t2", "local/rules/house.md")));

        var shared = Assert.Single(Parts(first), part => part.Code == StartNoteCodes.StartedTreeShared.Code);
        Assert.Contains("local/rules/", Assert.IsAssignableFrom<IReadOnlyList<string>>(shared.Value("shared")));
        var line = first.Line()!.Note;
        Assert.Contains("a recursive delete that follows a link deletes the shared place's files", line);
        Assert.Contains("Remove-Item -Recurse", line);
        Assert.Contains("a save by rename replaces", line);
        Assert.Contains("`local/frozen/` is shared read-only", first.Told);
        Assert.Contains("your own shell", first.Told);
    }

    [Fact]
    public async Task A_held_share_is_not_linked_and_is_kept_as_an_ask_linked_at_the_next_start_once_allowed()
    {
        await MakeAsync();
        PersonPermissions.Add(Home, null, "ask", "share(local/rules/)", null);

        var first = await StageAsync("t1");

        Assert.DoesNotContain("local/rules/", Shared("t1"));
        Assert.Contains("local/frozen/", Shared("t1"));
        Assert.NotNull(WhyOf(first, "local/rules/"));
        var ask = Assert.Single(FileAsks.Read(Home));
        Assert.Equal(("engine", FileAsk.Share, "share(local/rules/)"), (ask.Repository, ask.What, ask.Text));

        FileAllows.Give(Home, ask, "supervisor", "d1");
        FileAsks.Settle(Home, "d1", made: true);
        await StageAsync("t2");

        Assert.Contains("local/rules/", Shared("t2"));
        Assert.Empty(FileAsks.Read(Home));
    }

    [Fact]
    public async Task A_read_only_share_takes_no_write_of_Daoris_s_own_and_the_session_is_told_its_own_shell_still_can()
    {
        await MakeAsync(gates: """{ "localInputs": ["local/frozen/x.txt"] }""");

        var opened = await StageAsync("t1");

        Assert.Contains("local/frozen/", Shared("t1"));
        Assert.Contains(Parts(opened), part => part.Code == StartNoteCodes.StartedLocalRefused.Code && Equals(part.Value("input"), "local/frozen/x.txt"));
        Assert.Equal("frozen\n", await File.ReadAllTextAsync(Place("local/frozen/x.txt")));
        Assert.False(File.GetAttributes(Place("local/frozen/x.txt")).HasFlag(FileAttributes.ReadOnly));
        Assert.Contains("nothing stops your own shell or tools writing there", opened.Told);

        // A link cannot make a folder read-only: a session's own write goes through to the checkout, which is what the words say.
        await File.WriteAllTextAsync(In("t1", "local/frozen/y.txt"), "a shell's write\n");
        Assert.True(File.Exists(Place("local/frozen/y.txt")));
    }

    [Fact]
    public void A_dependency_an_output_an_intermediate_or_a_cache_is_never_shared()
    {
        foreach (var kind in new[] { "dependency", "output", "intermediate", "cache" })
        {
            using var document = JsonDocument.Parse($$"""{ "form": 2, "rules": [ { "kind": "{{kind}}", "paths": ["/x/"], "reach": "write" } ] }""");
            var problems = new List<string>();
            var read = FileRuleFile.Rules(document.RootElement, "", problems, FileRuleFile.Form);
            Assert.Empty(read);
            Assert.Contains("is rewritten by what makes it", Assert.Single(problems));
        }
    }

    [Fact]
    public async Task A_single_file_link_a_save_by_rename_replaced_is_the_lines_own_its_record_dropped_and_said_and_the_place_untouched()
    {
        await MakeAsync();
        var first = await StageAsync("t1");
        if (WhyOf(first, "local/notes.md") == ShareWhy.FileLink)
        {
            // A machine that will not make a link to a single file declines the share, and never copies in its stead.
            Assert.False(File.Exists(In("t1", "local/notes.md")));
            return;
        }

        Assert.Contains("local/notes.md", Shared("t1"));
        var saving = In("t1", "local/notes.md.saving");
        await File.WriteAllTextAsync(saving, "the line's notes\n");
        File.Move(saving, In("t1", "local/notes.md"), overwrite: true);
        Assert.False(File.GetAttributes(In("t1", "local/notes.md")).HasFlag(FileAttributes.ReparsePoint));

        var again = await StageAsync("t1");

        Assert.Contains(Parts(again), part => part.Code == StartNoteCodes.TreeShareLost.Code && Equals(part.Value("path"), "local/notes.md"));
        Assert.Equal(ShareWhy.Standing, WhyOf(again, "local/notes.md"));
        Assert.DoesNotContain("local/notes.md", Shared("t1"));
        Assert.Empty(TreeFilesRecord.Read(Home, _trees["t1"])!.Lost);
        Assert.Equal("the line's notes\n", await File.ReadAllTextAsync(In("t1", "local/notes.md")));
        Assert.Equal("the checkout's notes\n", await File.ReadAllTextAsync(Place("local/notes.md")));
    }

    [Fact]
    public async Task A_junction_removed_and_made_again_as_a_folder_is_the_lines_own_and_nothing_of_the_place_is_deleted()
    {
        await MakeAsync();
        await StageAsync("t1");
        Directory.Delete(In("t1", "local/rules"), recursive: false);
        Directory.CreateDirectory(In("t1", "local/rules"));
        await File.WriteAllTextAsync(In("t1", "local/rules/mine.md"), "the line's rule\n");

        var again = await StageAsync("t1");

        Assert.Contains(Parts(again), part => part.Code == StartNoteCodes.TreeShareLost.Code && Equals(part.Value("path"), "local/rules/"));
        Assert.Equal(ShareWhy.Standing, WhyOf(again, "local/rules/"));
        Assert.DoesNotContain("local/rules/", Shared("t1"));
        Assert.False(File.GetAttributes(In("t1", "local/rules")).HasFlag(FileAttributes.ReparsePoint));
        Assert.True(File.Exists(Place("local/rules/house.md")));
        Assert.False(File.Exists(Place("local/rules/mine.md")));
    }

    [Fact]
    public async Task A_link_standing_where_a_share_goes_is_never_made_over_nor_recorded()
    {
        await MakeAsync();
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        await File.WriteAllTextAsync(Path.Combine(elsewhere, "theirs.md"), "not the checkout's\n");
        var tree = Path.Combine(_scratch, "t1");
        await GitAsync(Root, "worktree", "add", "-q", "-b", "daoris/t1", tree, "main");
        _trees["t1"] = tree;
        Directory.CreateDirectory(Path.Combine(tree, "local"));
        DependencyLinks.Link(In("t1", "local/rules"), elsewhere);
        TreeFilesRecord.Mark(Home, tree);

        var opened = await StageAsync("t1");

        Assert.Equal(ShareWhy.Standing, WhyOf(opened, "local/rules/"));
        Assert.DoesNotContain("local/rules/", Shared("t1"));
        Assert.Equal(TreeShareStanding.Elsewhere, TreeShares.Standing(tree, new TreeShare("local/rules/", Place("local/rules"), "write")));
        Assert.True(File.Exists(Path.Combine(elsewhere, "theirs.md")));
    }

    [Fact]
    public async Task An_unlink_cut_off_after_its_link_went_reads_the_entry_again_and_finishes()
    {
        await MakeAsync();
        await StageAsync("t1");
        Directory.Delete(In("t1", "local/rules"), recursive: false);

        Assert.Null(TreeShares.Unlink(Home, _trees["t1"]));

        Assert.Empty(Shared("t1"));
        Assert.Empty(TreeFilesRecord.Read(Home, _trees["t1"])!.Lost);
        Assert.False(Path.Exists(In("t1", "local/frozen")));
        Assert.True(File.Exists(Place("local/rules/house.md")));
        Assert.True(File.Exists(Place("local/frozen/x.txt")));
    }
}
