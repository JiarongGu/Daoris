using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's proposals, the driver's half (HELP1c, D89): the file the connector wrote, read here and
/// judged with the route's own code before the person sees it — what it changes, the command that does
/// the same (D50), or the route's refusal — then settled by the person's press.
/// </summary>
public sealed class HelpProposalsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-plans-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpProposalsTests() => Directory.CreateDirectory(HelpProposals.FolderOf(_home));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly HelpMachineFacts Facts = new(
        Repositories: ["engine", "game"], Workspaces: ["default", "work"], Agents: ["claude-code", "claude-code-acp"]);

    /// <summary>A file as the service's `HelpProposalBox` writes it — the twin's shape.</summary>
    private string File(string id, string kind, string door, string? target = null, string? workspace = null,
        string? value = null, string? sentence = null, string session = "h1", string state = "proposed")
    {
        var node = new JsonObject
        {
            ["id"] = id, ["proposed"] = "2026-09-29T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = session },
            ["kind"] = kind, ["door"] = door, ["target"] = target, ["workspace"] = workspace, ["value"] = value,
            ["sentence"] = sentence, ["why"] = "the person asked", ["state"] = state, ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), $"{id}.json"), node.ToJsonString());
        return id;
    }

    private static HelpProposal Setting(string door, string? target = null, string? workspace = null, string? value = null) =>
        new("p1", "setting", door, target, workspace, value, null, "why", "h1", "proposed");

    [Theory]
    [InlineData("drive", "engine", null, null, "daoris driver drive engine", "Drive `engine`")]
    [InlineData("hold", "game", null, null, "daoris driver hold game", "Hold `game`")]
    [InlineData("trees", "engine", null, "on", "daoris driver trees engine on", "own tree")]
    [InlineData("line", "engine", null, "develop", "daoris driver line engine develop", "`engine`'s line to `develop`")]
    [InlineData("line", null, "work", "--clear", "daoris driver line --workspace work --clear", "Clear workspace `work`'s line")]
    [InlineData("landing", null, "work", "branch feature/{quest}-{slug} --tidy", "daoris driver landing --workspace work branch feature/{quest}-{slug} --tidy", "on a branch `feature/{quest}-{slug}`")]
    [InlineData("intake", null, null, "off", "daoris driver intake off", "Stop answering asks")]
    [InlineData("helper", null, null, "claude-code", "daoris driver helper claude-code", "`claude-code`")]
    [InlineData("strikes", null, null, "5", "daoris driver strikes 5", "5 failed sessions")]
    [InlineData("timeout", null, null, "120", "daoris driver timeout 120", "120 minutes")]
    [InlineData("notify", null, null, "off", "daoris driver notify off", "Stop saying")]
    public void A_setting_is_planned_as_what_it_changes_and_the_command_that_does_the_same(
        string door, string? target, string? workspace, string? value, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
        Assert.NotNull(plan.Apply);
    }

    [Fact]
    public void Applying_a_plan_makes_the_edit_the_screens_route_makes()
    {
        var config = DriverConfig.Empty;
        config = HelpProposals.Plan(Setting("drive", "engine"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("landing", null, "work", "branch feature/{quest}-{slug} --tidy"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("timeout", value: "120"), config, Facts).Apply!(config);

        Assert.Contains("engine", config.Drivable);
        Assert.Equal(new LandingRule("branch", "feature/{quest}-{slug}", Tidy: true), config.WorkspaceLandings["work"]);
        Assert.Equal(120, config.TimeoutMinutes);
    }

    /// <summary>What the route would refuse is refused here in its own words, and never shown to the person.</summary>
    [Theory]
    [InlineData("drive", "nowhere", null, null, "`nowhere` is not registered")]
    [InlineData("line", null, "elsewhere", "develop", "no workspace `elsewhere`")]
    [InlineData("line", "engine", null, "bad..name", "not a branch name git would take")]
    [InlineData("landing", "engine", null, "branch feature/fixed", "`{quest}` or `{session}`")]
    [InlineData("intake", null, null, "gpt-agent", "no agent `gpt-agent`")]
    public void What_the_route_would_refuse_is_refused_in_its_words(
        string door, string? target, string? workspace, string? value, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    [Fact]
    public void An_ask_is_planned_as_the_ask_door_and_its_terminal_twin()
    {
        var ask = new HelpProposal("p2", "ask", "ask", null, "work", null, "fix the cold-cache stall", "why", "h1", "proposed");

        var plan = HelpProposals.Plan(ask, DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver ask --workspace work \"fix the cold-cache stall\"", plan.Terminal);
        Assert.Contains("fix the cold-cache stall", plan.Describe);
        Assert.Null(plan.Apply); // an ask is made through the ask door, not an edit to the file
        Assert.Contains("no workspace `nope`", HelpProposals.Plan(ask with { Workspace = "nope" }, DriverConfig.Empty, Facts).Refusal);
    }

    [Fact]
    public void Pending_proposals_are_read_for_one_conversation_and_a_settled_one_is_not_pending()
    {
        File("a1", "setting", "drive", target: "engine");
        File("a2", "ask", "ask", workspace: "work", sentence: "start it", session: "other");
        File("a3", "setting", "hold", target: "game", state: "applied");
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "broken.json"), "{ not json");

        var pending = HelpProposals.Pending(_home, "h1");

        var only = Assert.Single(pending);
        Assert.Equal(("a1", "drive", "engine"), (only.Id, only.Door, only.Target));

        HelpProposals.Settle(_home, "a1", "dismissed", "not now");
        Assert.Empty(HelpProposals.Pending(_home, "h1"));
        var settled = JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(HelpProposals.FolderOf(_home), "a1.json")))!;
        Assert.Equal("dismissed", settled["state"]!.GetValue<string>());
        Assert.Equal("not now", settled["note"]!.GetValue<string>());
        Assert.Equal("the person asked", settled["why"]!.GetValue<string>());
    }

    /// <summary>A twin (`twins.md`): the service's `HelpProposalBox.FolderOf` names the same folder.</summary>
    [Fact]
    public void The_folder_is_the_services_twin()
    {
        Assert.Equal(Path.Combine(_home, "help", "proposals"), HelpProposals.FolderOf(_home));
    }
}
