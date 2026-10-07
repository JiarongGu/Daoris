using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRSCOPE1a (D150's BRSCOPE1 note, D50, WSP5): <c>daoris-driver trees clean|sync</c> look at one workspace's checkouts where
/// the words name one, as that workspace's Branches tab does: the words, the checkouts each look is handed, the refusal of a
/// workspace the registry does not name, and the press each list suggests. Read off registry rows alone, since a look at a
/// real checkout runs git; the host hands these rows to its looks and presses (<c>DriverCommandTests</c>).
/// </summary>
public sealed class TreesCommandTests
{
    [Theory]
    [InlineData(new[] { "clean" }, TreesLook.Clean, null, null, false, false)]
    [InlineData(new[] { "clean", "--yes" }, TreesLook.Clean, null, null, false, true)]
    [InlineData(new[] { "clean", "--workspace", "aurora" }, TreesLook.Clean, null, "aurora", false, false)]
    [InlineData(new[] { "clean", "--yes", "--workspace", " aurora " }, TreesLook.Clean, null, "aurora", false, true)]
    [InlineData(new[] { "sync" }, TreesLook.Sync, null, null, false, false)]
    [InlineData(new[] { "sync", "--repository", "newcomer", "--yes" }, TreesLook.Sync, "newcomer", null, false, true)]
    [InlineData(new[] { "sync", "--workspace", "tools", "--all" }, TreesLook.Sync, null, "tools", true, false)]
    [InlineData(new[] { "sync", "--all", "--workspace", "tools", "--repository", "anvil", "--yes" }, TreesLook.Sync, "anvil", "tools", true, true)]
    public void The_words_ask_for_a_look_or_its_press(string[] args, TreesLook look, string? repository, string? workspace, bool all, bool yes)
    {
        var asked = TreesCommand.Read(args, out var problem);

        Assert.Null(problem);
        Assert.Equal(new TreesAsk(look) { Repository = repository, Workspace = workspace, All = all, Yes = yes }, asked);
    }

    /// <summary>
    /// 🔴 Every word is read, and one these looks do not take is the usage: they took the words they knew and passed over
    /// the rest, so a mistyped <c>--workspce aurora --yes</c> would have cleaned every workspace's branches.
    /// </summary>
    [Theory]
    [InlineData(new[] { "clean", "--workspace" }, "`--workspace`")]
    [InlineData(new[] { "clean", "--workspace", "--yes" }, "`--workspace`")]
    [InlineData(new[] { "clean", "--workspace", "  " }, "`--workspace`")]
    [InlineData(new[] { "clean", "--workspace", "aurora", "--workspace", "tools" }, "`--workspace`")]
    [InlineData(new[] { "clean", "--workspce", "aurora", "--yes" }, "`--workspce`")]
    [InlineData(new[] { "clean", "--repository", "engine" }, "`--repository`")]
    [InlineData(new[] { "clean", "--all" }, "`--all`")]
    [InlineData(new[] { "sync", "--repository" }, "`--repository`")]
    [InlineData(new[] { "sync", "--repository", "engine", "--repository", "game" }, "`--repository`")]
    [InlineData(new[] { "sync", "aurora" }, "`aurora`")]
    [InlineData(new[] { "land", "s-1" }, "`clean` or `sync`")]
    public void Anything_else_is_the_usage_and_says_what_was_not_understood(string[] args, string named)
    {
        Assert.Null(TreesCommand.Read(args, out var problem));
        Assert.Contains(named, problem);
    }

    /// <summary>D112's scope, as before: those holding Daoris's branches, every one with <c>--all</c>, and a repository named.</summary>
    [Fact]
    public void The_scope_is_d112s()
    {
        var held = new SyncRepository("engine", "aurora", Holds: true);
        var apart = new SyncRepository("game", "aurora", Holds: false);
        var sync = new TreesAsk(TreesLook.Sync);

        Assert.True(TreesCommand.Scope(sync).Includes(held));
        Assert.False(TreesCommand.Scope(sync).Includes(apart));
        Assert.True(TreesCommand.Scope(sync with { All = true }).Includes(apart));
        Assert.True(TreesCommand.Scope(sync with { Repository = "game" }).Includes(apart));
    }

    /// <summary>
    /// 🔴 A twin of the modules' <c>DriverModule.Checkouts</c>, which the screen's look and press take a workspace by: one table,
    /// <c>fixtures/checkout-scope.json</c>, row for row, so the terminal and a workspace's Branches tab take the same checkouts.
    /// </summary>
    [Fact]
    public void The_checkouts_are_the_screens_row_for_row()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "checkout-scope.json")));
        RepoView[] registry =
        [
            .. table.RootElement.GetProperty("registry").EnumerateArray().Select(row => row.TryGetProperty("workspace", out var space)
                ? new RepoView(Text(row, "repository")!, true, Text(row, "root"), space.GetString()!)
                : new RepoView(Text(row, "repository")!, true, Text(row, "root"))),
        ];

        var asks = table.RootElement.GetProperty("asks").EnumerateArray().ToList();
        Assert.True(asks.Count >= 10, $"the table holds its rows: {asks.Count}");
        foreach (var ask in asks)
        {
            var (repository, workspace) = (Text(ask, "repository"), Text(ask, "workspace"));
            string[] takes = [.. ask.GetProperty("takes").EnumerateArray().Select(name => name.GetString()!)];
            string[] took = [.. TreesCommand.Checkouts(registry, repository, workspace).Select(each => each.Repository)];
            Assert.True(takes.SequenceEqual(took),
                $"repository `{repository}`, workspace `{workspace}`: the table takes [{string.Join(", ", takes)}], the driver [{string.Join(", ", took)}]");
        }
    }

    /// <summary>Two workspaces' checkouts on one machine, and a third's: each look, asked for one, is handed that one's alone.</summary>
    private static readonly RepoView[] TwoWorkspaces =
    [
        new("atelier", true, "C:/family/atelier", "aurora"),
        new("lantern", true, "C:/family/lantern", "Aurora"),
        new("foundry", true, "C:/family/foundry", "tools"),
        new("anvil", true, "C:/family/anvil", "tools"),
        new("newcomer", true, "C:/family/newcomer"),
        // A teammate's registration in aurora: no checkout here, so no look takes it.
        new("borealis", true, null, "aurora"),
    ];

    [Theory]
    [InlineData("clean", "aurora", new[] { "atelier", "lantern" })]
    [InlineData("clean", "TOOLS", new[] { "anvil", "foundry" })]
    [InlineData("clean", "default", new[] { "newcomer" })]
    [InlineData("sync", "Aurora", new[] { "atelier", "lantern" })]
    [InlineData("sync", "tools", new[] { "anvil", "foundry" })]
    [InlineData("sync", "default", new[] { "newcomer" })]
    public void Each_look_given_a_workspace_is_handed_its_own_checkouts_alone(string verb, string workspace, string[] own)
    {
        foreach (var press in new[] { false, true })
        {
            string[] args = press ? [verb, "--workspace", workspace, "--yes"] : [verb, "--workspace", workspace];
            var taken = TreesCommand.Take(TwoWorkspaces, TreesCommand.Read(args, out _)!);

            Assert.Null(taken.Refusal);
            Assert.Equal(own, taken.Repositories.Select(each => each.Repository));
        }
    }

    /// <summary>Asked for none, each look is the machine's, as it was; a repository named in another workspace is none of this one's.</summary>
    [Fact]
    public void Asked_for_no_workspace_each_look_is_the_machines()
    {
        foreach (var verb in new[] { "clean", "sync" })
        {
            var taken = TreesCommand.Take(TwoWorkspaces, TreesCommand.Read([verb], out _)!);
            Assert.Null(taken.Refusal);
            Assert.Equal(["anvil", "atelier", "foundry", "lantern", "newcomer"], taken.Repositories.Select(each => each.Repository));
        }

        Assert.Empty(TreesCommand.Take(TwoWorkspaces, TreesCommand.Read(["sync", "--repository", "atelier", "--workspace", "tools"], out _)!).Repositories);
        Assert.Equal(["atelier"], TreesCommand.Take(TwoWorkspaces, TreesCommand.Read(["sync", "--repository", "ATELIER", "--workspace", "aurora"], out _)!)
            .Repositories.Select(each => each.Repository));
    }

    /// <summary>
    /// A workspace the registry does not name is refused, naming it and the ones it does, and nothing is handed on: a mistyped
    /// name would otherwise look at nothing and say all is well. One whose repositories have no checkout here is no refusal:
    /// it is named, and has nothing here.
    /// </summary>
    [Fact]
    public void A_workspace_the_registry_does_not_name_is_refused_naming_it()
    {
        foreach (var verb in new[] { "clean", "sync" })
        {
            var taken = TreesCommand.Take(TwoWorkspaces, TreesCommand.Read([verb, "--workspace", "nowhere", "--yes"], out _)!);

            Assert.Equal("there is no workspace `nowhere` on this machine — one of `aurora`, `default`, `tools`.", taken.Refusal);
            Assert.Empty(taken.Repositories);
        }

        Assert.Equal("there is no workspace `nowhere` on this machine: no repository is registered here.",
            TreesCommand.Take([], TreesCommand.Read(["clean", "--workspace", "nowhere"], out _)!).Refusal);

        RepoView[] away = [.. TwoWorkspaces, new("faraway", true, null, "elsewhere")];
        var none = TreesCommand.Take(away, TreesCommand.Read(["sync", "--workspace", "Elsewhere"], out _)!);
        Assert.Null(none.Refusal);
        Assert.Empty(none.Repositories);
    }

    /// <summary>What a look's sentences call what it took: every checkout here, or one workspace's, as the person named it.</summary>
    [Fact]
    public void The_sentences_say_whose_checkouts_were_looked_at()
    {
        var clean = new TreesAsk(TreesLook.Clean);
        var sync = new TreesAsk(TreesLook.Sync);

        Assert.Equal("trees: no session branches, and no branch a landing made, in any repository with a checkout here.",
            TreesCommand.NothingToClean(clean));
        Assert.Equal("trees: no session branches, and no branch a landing made, in any repository of workspace `aurora` with a checkout here.",
            TreesCommand.NothingToClean(clean with { Workspace = "aurora" }));
        Assert.Equal("trees: no repository has a checkout here, so there is nothing to bring up to date.", TreesCommand.NothingToSync(sync));
        Assert.Equal("trees: no repository of workspace `tools` has a checkout here, so there is nothing to bring up to date.",
            TreesCommand.NothingToSync(sync with { Workspace = "tools" }));
        Assert.Equal("trees: `newcomer` has no checkout here, so there is nothing to bring up to date.",
            TreesCommand.NothingToSync(sync with { Repository = "newcomer" }));
        Assert.Equal("trees: `atelier` has no checkout here in workspace `tools`, so there is nothing to bring up to date.",
            TreesCommand.NothingToSync(sync with { Repository = "atelier", Workspace = "tools" }));
        Assert.Equal("trees: no repository with a checkout here holds a branch of Daoris's.", TreesCommand.NoneHeld(sync));
        Assert.Equal("trees: no repository of workspace `tools` with a checkout here holds a branch of Daoris's.",
            TreesCommand.NoneHeld(sync with { Workspace = "tools" }));
    }

    /// <summary>The press a list suggests carries the list's own scope (D112), its workspace among it, so <c>--yes</c> takes what was listed.</summary>
    [Fact]
    public void The_press_a_list_suggests_carries_its_scope()
    {
        Assert.Equal("daoris-driver trees clean --yes", TreesCommand.Press(new TreesAsk(TreesLook.Clean)));
        Assert.Equal("daoris-driver trees clean --workspace aurora --yes", TreesCommand.Press(new TreesAsk(TreesLook.Clean) { Workspace = "aurora" }));
        Assert.Equal("daoris-driver trees sync --repository newcomer --yes", TreesCommand.Press(new TreesAsk(TreesLook.Sync) { Repository = "newcomer" }));
        Assert.Equal("daoris-driver trees sync --repository anvil --workspace tools --all --yes",
            TreesCommand.Press(new TreesAsk(TreesLook.Sync) { Repository = "anvil", Workspace = "tools", All = true }));
        // A workspace's name goes through the one spelling every shell keeps whole (ACCTQUOTE1b).
        Assert.Equal("daoris-driver trees clean --workspace \"my circle\" --yes",
            TreesCommand.Press(new TreesAsk(TreesLook.Clean) { Workspace = "my circle" }));
        Assert.Equal("daoris-driver trees clean --workspace <workspace> --yes",
            TreesCommand.Press(new TreesAsk(TreesLook.Clean) { Workspace = "R&D" }));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
