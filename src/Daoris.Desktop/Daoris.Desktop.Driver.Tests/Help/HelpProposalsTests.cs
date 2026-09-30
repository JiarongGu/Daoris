using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's proposals, the driver's half (HELP1c, D89): the file the connector wrote, read here and
/// judged with the route's own code before the person sees it — what it changes, the command that does
/// the same (D50), or the route's refusal — then settled by the person's press.
/// </summary>
/// <remarks>
/// What holds for every kind: reading, settling, dispatch. Each kind's judgement and Apply are held beside
/// it under <c>Help/Proposals/</c> (MOD6).
/// </remarks>
public sealed class HelpProposalsTests : HelpProposalsFixture
{
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

    /// <summary>A kind no class judges is refused in the words it always was, and never shown (MOD6 kept them word for word).</summary>
    [Theory]
    [InlineData("permission")]
    [InlineData("Setting")]
    [InlineData("")]
    public void A_kind_no_class_judges_is_refused(string kind)
    {
        var plan = HelpProposals.Plan(Of(kind, "drive", "engine"), DriverConfig.Empty, Machine);

        Assert.Equal($"`{kind}` is not a change Ask Daoris proposes.", plan.Refusal);
        Assert.Equal(("", "", null), (plan.Describe, plan.Terminal, plan.Apply));
    }

    [Fact]
    public async Task A_refused_plan_calls_no_door_and_is_settled_refused()
    {
        var (refused, doors, _) = await ApplyAsync(Of("delete", "quest", "q2taken0"));

        Assert.False(refused.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal("refused", HelpProposals.Find(_home, "p6")!.State);
    }
}
