using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>hand</c> proposal (WSR5b): a branch a landing made, handed to a landing plugin afterwards.</summary>
public sealed class HelpHandProposalsTests : HelpProposalsFixture
{
    private static readonly LandedBranch Second = new(
        "engine", "work", "feature/q2-second", "main", "abc1234", "s2a3b4c5", "q2", "Second part", DateTimeOffset.UnixEpoch);

    /// <summary>A machine with a landed branch in `engine`, one of the same name in `game`, and a plugin that lands work.</summary>
    private HelpMachineFacts HandMachine(bool installed = true)
    {
        if (installed)
        {
            var folder = Path.Combine(_home, PluginCatalog.Folder, "example.lands");
            Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
                """{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");
        }

        return Facts with
        {
            Plugins = PluginCatalog.Load(_home),
            Landed =
            [
                Second,
                Second with { Repository = "game", Branch = "feature/shared", Session = "s9", Tip = "def5678" },
                Second with { Branch = "feature/shared", Session = "s8", Tip = "fed8765" },
            ],
        };
    }

    private static readonly DriverConfig HandsToPlugin = DriverConfig.Empty
        .WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "example.lands"));

    [Fact]
    public void A_landed_branch_is_handed_to_the_rules_plugin_and_the_plan_says_what_it_does()
    {
        var facts = HandMachine();

        var bySession = HelpProposals.Plan(Of("hand", "hand", "s2a3b4c5"), HandsToPlugin, facts);
        var byBranch = HelpProposals.Plan(Of("hand", "hand", "feature/q2-second"), HandsToPlugin, facts);

        Assert.Null(bySession.Refusal);
        Assert.Equal("daoris-driver trees hand feature/q2-second --repository engine", bySession.Terminal);
        Assert.Contains("Hand `feature/q2-second` (in `engine`) to plugin `example.lands`", bySession.Describe);
        Assert.Contains("pushes the branch and opens the pull request", bySession.Describe);
        Assert.Contains("“Second part”", bySession.Describe);
        Assert.Null(bySession.Apply);
        Assert.Equal(new HelpHandOff("engine", "feature/q2-second", null), bySession.Hand);
        Assert.Equal(bySession.Terminal, byBranch.Terminal);
    }

    /// <summary>A plugin named on the card is the one spoken to, where the rule names none.</summary>
    [Fact]
    public void A_plugin_named_on_the_card_is_the_one_it_goes_to()
    {
        var plan = HelpProposals.Plan(Of("hand", "hand", "s2a3b4c5", "example.lands"), DriverConfig.Empty, HandMachine());

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver trees hand feature/q2-second --repository engine --plugin example.lands", plan.Terminal);
        Assert.Equal("example.lands", plan.Hand!.Plugin);
    }

    [Theory]
    [InlineData("nobody", null, null, true, "names no branch a landing made")]
    [InlineData("s2a3b4c5", null, null, false, "names none")]
    [InlineData("s2a3b4c5", "example.gone", null, true, "not installed")]
    [InlineData("feature/shared", null, null, true, "in `engine`, `game`")]
    [InlineData("feature/shared", null, "nobody", true, "names no branch a landing made")]
    public void A_hand_off_the_door_would_refuse_is_never_proposed(string target, string? plugin, string? repository, bool ruled, string says)
    {
        var proposal = Of("hand", "hand", target, plugin) with { Repository = repository };

        var plan = HelpProposals.Plan(proposal, ruled ? HandsToPlugin : DriverConfig.Empty, HandMachine());

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Hand);
    }

    [Fact]
    public void A_branch_named_in_two_repositories_is_handed_where_the_card_says()
    {
        var plan = HelpProposals.Plan(Of("hand", "hand", "feature/shared") with { Repository = "engine" }, HandsToPlugin, HandMachine());

        Assert.Null(plan.Refusal);
        Assert.Equal(new HelpHandOff("engine", "feature/shared", null), plan.Hand);
    }

    [Fact]
    public async Task A_hand_off_is_applied_through_the_reviews_own_door_and_its_answer_said()
    {
        var doors = new HelpStandInDoors();
        doors.Change(_ => HandsToPlugin);
        doors.Calls.Clear();

        var (applied, _, _) = await ApplyAsync(Of("hand", "hand", "s2a3b4c5"), doors, HandMachine());

        Assert.True(applied.Applied);
        Assert.Equal(["HANDOFF engine feature/q2-second (the rule's)"], doors.Calls);
        Assert.StartsWith("Applied: `#p6` (`daoris-driver trees hand feature/q2-second --repository engine`)", applied.Told);
        Assert.Contains("Plugin `example.lands`: pushed it.", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>What the door finds at the press — the branch moved on, the plugin did not push — settles it refused, in its words.</summary>
    [Fact]
    public async Task A_hand_off_the_door_refuses_or_the_plugin_did_not_push_settles_it_refused()
    {
        var doors = new HelpStandInDoors { HandAnswer = new TreeHand(false, "`feature/q2-second` was not handed on. Plugin `example.lands` did not push it: gh is not signed in.", "feature/q2-second") };

        var (applied, _, _) = await ApplyAsync(Of("hand", "hand", "s2a3b4c5", "example.lands"), doors, HandMachine());

        Assert.False(applied.Applied);
        Assert.Contains("Not applied", applied.Told);
        Assert.Contains("gh is not signed in", applied.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p6")!.State);
    }
}

public sealed partial class HelpStandInDoors
{
    /// <summary>What the hand-off's door answers (WSR5b): pushed by default, or its refusal in its own words.</summary>
    public TreeHand? HandAnswer { get; init; }

    public Task<TreeHand> HandAsync(string repository, string branch, string? plugin, CancellationToken ct)
    {
        Calls.Add($"HANDOFF {repository} {branch} {plugin ?? "(the rule's)"}".TrimEnd());
        return Task.FromResult(HandAnswer ?? new TreeHand(true, $"handed `{branch}` to plugin `example.lands`. Plugin `example.lands`: pushed it.", branch));
    }
}
