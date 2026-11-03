using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1ai-i (D228; the oneway design §11): the one command tree's words, declared once in <c>fixtures/command-tree.json</c> and held
/// equal to the desktop program's table here (the CLI's is held to it in <c>cli-tree.test.ts</c>); <c>CommandTreeTests</c> holds the routing.
/// </summary>
public sealed class CommandWordsTests
{
    private static JsonObject Fixture() => (JsonObject)JsonNode.Parse(File.ReadAllText(
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "command-tree.json")))!;

    private static string[] Strings(JsonNode? node) => [.. (node as JsonArray ?? []).Select(item => (string)item!)];

    [Fact]
    public void ONEWAY1ai_i_the_tree_is_the_fixtures()
    {
        var fixture = Fixture();
        Assert.Equal(Strings(fixture["doctrine"]), CommandWords.Doctrine);

        var groups = (JsonArray)fixture["groups"]!;
        Assert.Equal(groups.Select(group => (string)group!["name"]!), CommandWords.Groups.Select(group => group.Name));
        foreach (var node in groups)
        {
            var group = CommandWords.Group((string)node!["name"]!)!;
            Assert.Equal(Strings(node["verbs"]), group.Verbs);
            Assert.Equal((string?)node["takes"], group.Takes);
            Assert.Equal((bool?)node["whole"] ?? false, group.Whole);
            var nested = node["nested"] as JsonObject ?? [];
            Assert.Equal(nested.Select(pair => pair.Key).Order(), group.Nested.Keys.Order());
            foreach (var (verb, under) in nested) Assert.Equal(Strings(under), group.Nested[verb]);
        }

        var moved = ((JsonArray)fixture["moved"]!).Select(node => new TreeMove(
            (string)node!["from"]!, (string)node["to"]!, (string?)node["bare"], (string?)node["append"], (bool?)node["launcher"] ?? false));
        Assert.Equal(moved, CommandWords.Moved);

        var flags = ((JsonArray)fixture["flags"]!).Select(node => new TreeFlag((string)node!["verb"]!, (string)node["from"]!, (string)node["to"]!));
        Assert.Equal(flags, CommandWords.Flags);
    }

    /// <summary>The old launcher runs nothing and names what its words are now.</summary>
    [Theory]
    [InlineData("sessions say s-1 hi", "`daoris-driver sessions` is `daoris session` now")]
    [InlineData("sync status", "`daoris-driver sync` is `daoris remote sync` now")]
    [InlineData("driver drive engine", "`daoris-driver driver drive` is `daoris place drive` now")]
    [InlineData("chat --repository engine", "`daoris-driver chat` is `daoris chat` now")]
    [InlineData("roles brief", "`daoris-driver roles brief` is `daoris role brief` now")]
    [InlineData("", "`daoris-driver` is `daoris` now")]
    public void ONEWAY1ai_i_the_old_launcher_names_the_new_form(string typed, string said) =>
        Assert.Contains(said, CommandWords.OldLauncher(typed.Length == 0 ? [] : typed.Split(' ')));

    /// <summary>A lease names a command by its group and sub-verb as the person typed them, never an argument.</summary>
    [Theory]
    [InlineData("tree land s-1", "daoris tree land")]
    [InlineData("ask pause a-1", "daoris ask pause")]
    [InlineData("tree remove some/tree/path", "daoris tree remove")]
    [InlineData("repository register", "daoris repository register")]
    [InlineData("ask-daoris mode full", "daoris ask-daoris mode")]
    [InlineData("place heavy engine build=x", "daoris place heavy")]
    [InlineData("place drive engine", "daoris place drive")]
    [InlineData("quest retry 12 --at 2", "daoris quest retry")]
    [InlineData("workflow new docs --from merge-after-accept", "daoris workflow new")]
    [InlineData("tool use git system", "daoris tool use")]
    [InlineData("agent account order claude-code account-1", "daoris agent account")]
    [InlineData("plugin acme.gate", "daoris plugin")]
    [InlineData("workflow skip q7 go nobody raises it", "daoris workflow skip")]
    [InlineData("drive --once", "daoris drive")]
    public void ONEWAY1ai_i_a_lease_names_the_command_and_never_its_arguments(string typed, string named) =>
        Assert.Equal(named, CommandWords.Named(typed.Split(' ')));

    [Fact]
    public void ONEWAY1ai_i_the_cli_an_install_carries_is_found_beside_the_program()
    {
        var install = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "daoris-tree-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var program = Path.Combine(install, "app", "daoris-driver");
            Directory.CreateDirectory(program);
            Assert.Null(CommandWords.CarriedCli(program));

            var entry = Path.Combine(install, "app", "cli", "node_modules", "daoris", "bin", "daoris.mjs");
            Directory.CreateDirectory(Path.GetDirectoryName(entry)!);
            File.WriteAllText(entry, "");
            Assert.Equal(Path.GetFullPath(entry), CommandWords.CarriedCli(program));
        }
        finally
        {
            GitSeed.Discard(install);
        }
    }

    /// <summary>Where no Node.js runs, the doctrine command says so with the tools' reason, and does nothing.</summary>
    [Fact]
    public void ONEWAY1ai_i_no_node_says_why_and_that_nothing_was_done()
    {
        Assert.Equal(
            "the doctrine commands run on Node.js 22 or later, and none runs here: `node` is not on this machine's PATH; nothing was done.",
            CommandWords.NoNode(null));
        Assert.EndsWith("none runs here: the file is gone; nothing was done.", CommandWords.NoNode("the file is gone."));
    }
}
