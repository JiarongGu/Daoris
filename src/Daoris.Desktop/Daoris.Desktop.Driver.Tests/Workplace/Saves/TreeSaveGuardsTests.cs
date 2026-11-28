using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What keeps a tree's work from being lost through its save: a removal behind a save refuses every file the save
/// does not hold, the save refuses state it cannot carry, the landed proof never calls unlanded work landed, and a restore writes
/// any shape a tree had and leaves nothing registered when it fails. Real git, in scratch repositories under <c>_fixtures/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSaveGuardsTests : LandedFixture
{
    private readonly MovedClock _clock = new();

    private readonly SavedTreeService _service = new();

    private static string Sha(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

    private async Task<(string Root, SessionTrees Trees, string Tree, string Branch)> TreeAsync()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        return (root, trees, tree.Path, tree.Branch);
    }

    private async Task<(int Code, string Said)> DoorAsync(SessionTrees trees, params string[] args)
    {
        var said = new StringWriter();
        var code = await TreeSaveDoors.RunAsync(args, trees, () => _service.Client(), _clock, said, said);
        return (code, said.ToString());
    }

    [Fact]
    public async Task A_removal_behind_a_save_keeps_a_tree_holding_ignored_files_its_save_does_not_list()
    {
        var (root, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree, ".gitignore", "local/\n", "ignore local");
        Directory.CreateDirectory(Path.Combine(tree, "local"));
        await File.WriteAllTextAsync(Path.Combine(tree, "local", "state.db"), "only this tree holds it\n");

        Assert.Equal(0, (await DoorAsync(trees, "save", tree)).Code);
        var kept = await DoorAsync(trees, "remove", tree, "--saved");

        Assert.Equal(1, kept.Code);
        Assert.Contains("local/state.db", kept.Said);
        Assert.True(File.Exists(Path.Combine(tree, "local", "state.db")));

        Assert.Equal(0, (await DoorAsync(trees, "save", tree, "--work", "local")).Code);
        var removed = await DoorAsync(trees, "remove", tree, "--saved");
        Assert.True(removed.Code == 0, removed.Said);
        Assert.False(Directory.Exists(tree));
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public async Task Work_that_names_no_ignored_file_refuses_the_save_and_a_name_is_read_literally()
    {
        var (_, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree, ".gitignore", "/notes/\n", "ignore notes");
        Directory.CreateDirectory(Path.Combine(tree, "notes"));
        await File.WriteAllTextAsync(Path.Combine(tree, "notes", "x[1].txt"), "bracketed\n");
        await File.WriteAllTextAsync(Path.Combine(tree, "notes", "x1.txt"), "plain\n");

        var mistyped = await DoorAsync(trees, "save", tree, "--work", "note");
        Assert.Equal(1, mistyped.Code);
        Assert.Contains("note", mistyped.Said);
        Assert.Equal("", (await GitAsync(tree, "for-each-ref", "refs/daoris/saves/")).Trim());

        var saved = await new TreeSaves(Home, _clock).SaveAsync(tree, "aurora", "engine", ["notes/x[1].txt"]);
        Assert.True(saved.Save is not null, saved.Said);
        Assert.Equal(["notes/x[1].txt"], saved.Save!.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task A_save_refuses_each_state_of_its_tree_it_cannot_carry_naming_it()
    {
        var (root, _, tree, _) = await TreeAsync();
        var saves = new TreeSaves(Home, _clock);
        await CommitAsync(root, "line.txt", "the line moved\n", "line");

        await GitAsync(tree, "merge", "--no-commit", "--no-ff", "main");
        var merging = await saves.SaveAsync(tree, "aurora", "engine", []);
        Assert.Equal(SaveRefusal.Unsavable, merging.Refused);
        Assert.Contains("MERGE_HEAD", merging.Said);
        await GitAsync(tree, "merge", "--abort");

        await GitAsync(tree, "update-index", "--skip-worktree", "README.md");
        var hidden = await saves.SaveAsync(tree, "aurora", "engine", []);
        Assert.Equal(SaveRefusal.Unsavable, hidden.Refused);
        Assert.Contains("README.md", hidden.Said);
        await GitAsync(tree, "update-index", "--no-skip-worktree", "README.md");

        await GitAsync(tree, "update-ref", "refs/worktree/mine", "HEAD");
        var own = await saves.SaveAsync(tree, "aurora", "engine", []);
        Assert.Equal(SaveRefusal.Unsavable, own.Refused);
        Assert.Contains("refs/worktree/mine", own.Said);
        await GitAsync(tree, "update-ref", "-d", "refs/worktree/mine");

        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/saves/")).Trim());
        Assert.NotNull((await saves.SaveAsync(tree, "aurora", "engine", [])).Save);
    }

    [Fact]
    public async Task A_save_keeps_the_executable_bit_in_the_files_as_written()
    {
        var (_, _, tree, _) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree, "shared.txt"), "#!/bin/sh\necho run\n");
        await GitAsync(tree, "add", "shared.txt");
        await GitAsync(tree, "update-index", "--chmod=+x", "shared.txt");

        var saved = await new TreeSaves(Home, _clock).SaveAsync(tree, "aurora", "engine", []);

        Assert.True(saved.Save is not null, saved.Said);
        Assert.StartsWith("100755 ", await GitAsync(tree, "ls-tree", saved.Save!.Raw, "--", "shared.txt"));
    }

    [Fact]
    public async Task A_removal_behind_a_save_refuses_once_its_ref_is_gone_or_the_index_moved()
    {
        var (root, trees, tree, _) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree, "shared.txt"), "unstaged\n");

        Assert.Equal(0, (await DoorAsync(trees, "save", tree)).Code);
        await GitAsync(tree, "add", "shared.txt");
        var restaged = await DoorAsync(trees, "remove", tree, "--saved");
        Assert.Equal(1, restaged.Code);
        Assert.Contains("index", restaged.Said);

        Assert.Equal(0, (await DoorAsync(trees, "save", tree)).Code);
        var save = new TreeSaveRecords(Home).LatestOf(tree)!;
        await GitAsync(root, "update-ref", "-d", save.Id);
        var unpinned = await DoorAsync(trees, "remove", tree, "--saved");
        Assert.Equal(1, unpinned.Code);
        Assert.Contains(save.Id, unpinned.Said);
        Assert.True(Directory.Exists(tree));
    }

    [Fact]
    public async Task A_removal_behind_a_save_refuses_the_tree_of_a_quest_still_taken_and_says_how_to_restore_it()
    {
        var (_, trees, tree, _) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "work\n");
        _service.Records.Add(("s1", tree, "q1"));
        _service.Quests.Add(new QuestView("q1", "app", "engine", "Make it", "", "Taken"));

        Assert.Equal(0, (await DoorAsync(trees, "save", tree)).Code);
        var kept = await DoorAsync(trees, "remove", tree, "--saved");

        Assert.Equal(1, kept.Code);
        Assert.Contains("q1", kept.Said);
        Assert.Contains("restore", kept.Said);
        Assert.True(Directory.Exists(tree));
    }

    [Fact]
    public async Task A_branch_named_where_an_older_branch_kept_its_saves_saves_under_a_ref_of_its_own()
    {
        var (root, _, tree, _) = await TreeAsync();
        var saves = new TreeSaves(Home, _clock);
        await GitAsync(tree, "switch", "-c", "feature/1");
        var first = await saves.SaveAsync(tree, "aurora", "engine", []);
        // git keeps `feature` and `feature/1` apart in time only: the older branch goes, its save stays, and the newer is named over it.
        await GitAsync(tree, "switch", "-c", "between");
        await GitAsync(tree, "branch", "-D", "feature/1");
        await GitAsync(tree, "switch", "-c", "feature");
        var second = await saves.SaveAsync(tree, "aurora", "engine", []);

        Assert.True(first.Save is not null, first.Said);
        Assert.True(second.Save is not null, second.Said);
        Assert.NotEqual(first.Save!.Id, second.Save!.Id);
        Assert.Equal(("feature/1", "feature"), (first.Save.Branch, second.Save.Branch));
        Assert.Equal(first.Save.Commit, (await GitAsync(root, "rev-parse", first.Save.Id)).Trim());
        Assert.Equal(second.Save.Commit, (await GitAsync(root, "rev-parse", second.Save.Id)).Trim());
    }

    [Fact]
    public async Task A_restore_writes_a_file_where_a_folder_was_and_a_folder_where_a_file_was_staged_only_and_odd_names_included()
    {
        var (root, _, tree, _) = await TreeAsync();
        Directory.CreateDirectory(Path.Combine(tree, "docs"));
        await CommitAsync(tree, Path.Combine("docs", "a.txt"), "a\n", "a folder");
        File.Delete(Path.Combine(tree, "docs", "a.txt"));
        Directory.Delete(Path.Combine(tree, "docs"));
        await File.WriteAllTextAsync(Path.Combine(tree, "docs"), "now a file\n");
        File.Delete(Path.Combine(tree, "shared.txt"));
        Directory.CreateDirectory(Path.Combine(tree, "shared.txt"));
        await File.WriteAllTextAsync(Path.Combine(tree, "shared.txt", "inner.txt"), "now a folder\n");
        await File.WriteAllTextAsync(Path.Combine(tree, "staged only.txt"), "staged\n");
        await GitAsync(tree, "add", "staged only.txt");
        await File.WriteAllTextAsync(Path.Combine(tree, "名字 ü.txt"), "non-ASCII\n");

        var saved = await new TreeSaves(Home, _clock).SaveAsync(tree, "aurora", "engine", []);
        Assert.True(saved.Save is not null, saved.Said);
        var save = saved.Save!;
        var folder = Path.Combine(Scratch, "restored");
        var proof = await TreeRestores.RestoreAsync(save, folder);

        Assert.True(proof.Whole, proof.Said);
        Assert.Equal("now a file\n", await File.ReadAllTextAsync(Path.Combine(folder, "docs")));
        Assert.Equal("now a folder\n", await File.ReadAllTextAsync(Path.Combine(folder, "shared.txt", "inner.txt")));
        Assert.Equal(Sha(Path.Combine(tree, "名字 ü.txt")), Sha(Path.Combine(folder, "名字 ü.txt")));
        Assert.Equal("A  staged only.txt", (await GitAsync(folder, "status", "--porcelain=v1", "-z", "--", "staged only.txt")).TrimEnd('\0'));
        Assert.Equal(save.Head, (await GitAsync(root, "rev-parse", save.Id + "^1")).Trim());
    }

    [Fact]
    public async Task A_restore_that_fails_leaves_no_tree_registered()
    {
        var (root, _, tree, _) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "only in the save\n");
        var save = (await new TreeSaves(Home, _clock).SaveAsync(tree, "aurora", "engine", [])).Save!;
        var blob = save.Files.Single(file => file.Path == "loose.txt").Blob!;
        var loose = Path.Combine(root, ".git", "objects", blob[..2], blob[2..]);
        File.SetAttributes(loose, FileAttributes.Normal);
        File.Delete(loose);
        var folder = Path.Combine(Scratch, "restored");

        var proof = await TreeRestores.RestoreAsync(save, folder);

        Assert.False(proof.Whole);
        Assert.DoesNotContain("restored", await GitAsync(root, "worktree", "list", "--porcelain"));
        Assert.False(Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any());
    }

    [Fact]
    public async Task A_merge_carrying_its_own_resolution_and_a_change_of_whitespace_alone_read_unlanded()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var saves = new TreeSaves(Home, _clock);

        // The tree resolves a conflict in its merge; the line takes the tree's own commit by patch, not its resolution.
        var resolving = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(resolving.Path, "shared.txt", "the tree's\n", "tree side");
        await CommitAsync(root, "shared.txt", "the line's\n", "line side");
        await GitAsync(resolving.Path, "merge", "main");
        await File.WriteAllTextAsync(Path.Combine(resolving.Path, "shared.txt"), "resolved\n");
        await GitAsync(resolving.Path, "add", "shared.txt");
        await GitAsync(resolving.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--no-edit");
        var resolved = (await saves.SaveAsync(resolving.Path, "aurora", "engine", [])).Save!;
        await CommitAsync(root, "shared.txt", "one\n", "the line goes back");
        await CommitAsync(root, "shared.txt", "the tree's\n", "the tree's change, picked by hand");

        // The tree indents with a tab; the line indents the same line with two spaces, the same patch to git's patch id.
        var spacing = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(spacing.Path, "make.mk", "all:\n    echo\n", "a rule");
        await CommitAsync(spacing.Path, "make.mk", "all:\n\techo\n", "a tab");
        var tabbed = (await saves.SaveAsync(spacing.Path, "aurora", "engine", [])).Save!;
        await CommitAsync(root, "make.mk", "all:\n    echo\n", "a rule, picked");
        await CommitAsync(root, "make.mk", "all:\n  echo\n", "two spaces");

        foreach (var save in new[] { resolved, tabbed })
        {
            var judged = await LandedProofKinds.JudgeAsync(new(save.Root, save.Commit, "main"));
            Assert.Equal(LandedVerdict.Unlanded, judged.Verdict);
        }
    }

    [Fact]
    public async Task A_save_that_fails_or_stops_after_its_pin_leaves_no_ref_and_the_tree_as_it_was()
    {
        var (root, _, tree, _) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "work\n");
        var status = await GitAsync(tree, "status", "--porcelain=v1", "--untracked-files=all");
        using var stop = new CancellationTokenSource();

        var failed = await new TreeSaves(Home, _clock) { AfterPin = _ => throw new IOException("the disk said no") }.SaveAsync(tree, "aurora", "engine", []);
        var stopped = await new TreeSaves(Home, _clock) { AfterPin = _ => stop.CancelAsync() }.SaveAsync(tree, "aurora", "engine", [], ct: stop.Token);

        Assert.Equal((SaveRefusal.Unread, SaveRefusal.Stopped), (failed.Refused, stopped.Refused));
        Assert.Contains("the disk said no", failed.Said);
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/saves/")).Trim());
        Assert.Equal(status, await GitAsync(tree, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Null(new TreeSaveRecords(Home).LatestOf(tree));
    }

    [Fact]
    public async Task Saves_made_at_once_each_take_a_number_of_their_own()
    {
        var (_, _, tree, _) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "work\n");

        var saved = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => new TreeSaves(Home, _clock).SaveAsync(tree, "aurora", "engine", [])));

        Assert.All(saved, each => Assert.True(each.Save is not null, each.Said));
        Assert.Equal(3, saved.Select(each => each.Save!.Id).Distinct().Count());
    }

    [Fact]
    public async Task Saves_of_one_branch_name_in_two_repositories_are_two_records_and_a_door_asks_which()
    {
        var engine = await RepositoryAsync("engine");
        var tools = await RepositoryAsync("tools");
        var trees = new SessionTrees(Home);
        var saves = new TreeSaves(Home, _clock);
        var made = new List<TreeSave>();
        foreach (var (root, name) in new[] { (engine, "engine"), (tools, "tools") })
        {
            var tree = await trees.OpenAsync(root, name, "aurora");
            await GitAsync(tree.Path, "switch", "-c", "same");
            await File.WriteAllTextAsync(Path.Combine(tree.Path, "loose.txt"), name);
            made.Add((await saves.SaveAsync(tree.Path, "aurora", name, [])).Save!);
        }

        Assert.Equal(made[0].Id, made[1].Id);
        Assert.Equal(2, new TreeSaveRecords(Home).Matching(made[0].Id).Count);
        var asked = await DoorAsync(trees, "drop", made[0].Id);
        Assert.Equal(1, asked.Code);
        Assert.Contains("--repository", asked.Said);

        var dropped = await DoorAsync(trees, "drop", made[0].Id, "--repository", "engine");
        Assert.True(dropped.Code == 0, dropped.Said);
        Assert.Equal("", (await GitAsync(engine, "for-each-ref", "refs/daoris/saves/")).Trim());
        Assert.Equal(made[1].Commit, (await GitAsync(tools, "rev-parse", made[1].Id)).Trim());
        Assert.Equal("tools", new TreeSaveRecords(Home).Matching(made[0].Id).Single().Repository);
    }
}

/// <summary>
/// The service standing in for a removal behind a save: no session running, the records given (each with its tree and quest, ended),
/// and the quests given, as <c>/api/sessions</c>, <c>/api/sessions?includeClosed=true</c> and <c>/api/quests</c> answer them.
/// </summary>
internal sealed class SavedTreeService : HttpMessageHandler
{
    public List<(string Id, string Tree, string Quest)> Records { get; } = [];

    public List<QuestView> Quests { get; } = [];

    public ServiceClient Client() => new("http://stand-in", null, new HttpClient(new SessionChanges(this), disposeHandler: false));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        var body = uri.AbsolutePath switch
        {
            "/api/sessions" when uri.Query.Contains("includeClosed", StringComparison.Ordinal) => JsonSerializer.Serialize(
                Records.Select(each => new { id = each.Id, repository = "engine", state = "completed", tree = each.Tree, quest = each.Quest })),
            "/api/quests" => QuestStandIn.List(Quests),
            _ => "[]",
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
