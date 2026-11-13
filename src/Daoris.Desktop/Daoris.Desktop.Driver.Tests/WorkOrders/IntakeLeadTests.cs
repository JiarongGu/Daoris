using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The intake is the lead of the ask's team where its workspace has more than one repository: it starts as plan work on the Lead role,
/// is handed <c>team_plan</c> beside <c>quest_publish</c> and told to decide whether the work is one part or several. A workspace with one
/// repository starts it as intake work exactly as before.
/// </summary>
public sealed class IntakeLeadTests
{
    private static AskView Ask() => new("a1", "work", "make the media service read its field name from config and show it on the settings page", "Proposed", "declarations");

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void An_intake_leads_where_the_workspace_has_more_than_one_repository(int repositories, bool leads)
    {
        Assert.Equal(leads, IntakeLead.Leads([.. Enumerable.Range(1, repositories).Select(each => $"repo-{each}")]));
        Assert.Equal(leads ? ParticipantWork.Plan : ParticipantWork.Intake, IntakeLead.WorkOf(leads));
        Assert.Equal(leads ? "lead" : "intake", StartPlan.RoleOf(IntakeLead.WorkOf(leads)));
    }

    [Fact]
    public void The_lead_is_told_that_one_part_is_a_plan_and_to_write_several_through_team_plan()
    {
        var led = IntakePrompt.Compose(Ask(), lead: true);
        var alone = IntakePrompt.Compose(Ask());

        Assert.Contains("`team_plan`", led);
        Assert.Contains("One part is a valid plan", led);
        Assert.Contains("`quest_publish`", led);
        Assert.Contains("`deployables`", led);
        Assert.Contains("`questions`", led);
        Assert.DoesNotContain("you also lead the ask's team", alone);
        Assert.DoesNotContain("One part is a valid plan", alone);
        Assert.StartsWith("You are the intake for workspace `work`", led);
    }

    [Fact]
    public void The_room_allows_the_plan_door_beside_the_publish_door_and_still_no_command()
    {
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__team_plan", IntakeRoom.Allowed);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__quest_publish", IntakeRoom.Allowed);
        Assert.DoesNotContain("Bash", IntakeRoom.Allowed);
    }
}
