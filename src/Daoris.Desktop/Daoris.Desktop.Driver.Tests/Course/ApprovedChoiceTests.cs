using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent's own ask is kept on the arranged order's step for its agent, and a
/// quest handed over starts the choice approved, revalidated against the role's order, never picked again.
/// </summary>
public sealed class ApprovedChoiceTests
{
    private static readonly HandOverDoor[] Doors = [new("claude-code", true, null), new("codex", true, null)];

    private static ActorEffective Entry(string agent, string? model, string? effort, string from) => new(agent, model, effort, from);

    [Fact]
    public void An_ask_for_another_agent_takes_the_arranged_steps_model_and_effort()
    {
        var arranged = new[] { Entry("claude-code", "opus", "high", "1"), Entry("codex", "gpt-5", "medium", "2") };
        var step = ApprovedChoice.StepFor("claude-code", Doors, arranged, "codex");
        Assert.Equal(("codex", "gpt-5", "medium"), (step!.Harness, step.Model, step.Effort));
    }

    [Fact]
    public void An_ask_for_an_agent_the_order_does_not_name_takes_no_model()
    {
        var arranged = new[] { Entry("claude-code", "opus", "high", "1") };
        Assert.Null(ApprovedChoice.StepFor("claude-code", Doors, arranged, "codex"));
    }

    [Fact]
    public void A_roster_step_carries_no_model()
    {
        Assert.Null(ApprovedChoice.StepFor("claude-code", Doors, [], "codex"));
    }

    [Fact]
    public void The_start_takes_the_approved_entry_while_the_role_lists_it()
    {
        var handed = new HandedQuest("codex", null, "s1", HandOverWhy.Agent, HandOverBy.Agent, DateTimeOffset.UnixEpoch) { Model = "gpt-5", Effort = "medium" };
        var order = new[] { Entry("claude-code", "opus", "high", "1"), Entry("codex", "gpt-4", null, "2"), Entry("codex", "gpt-5", "medium", "3") };
        Assert.Equal(Entry("codex", "gpt-5", "medium", "handed"), ApprovedChoice.Of(handed, order));
    }

    [Fact]
    public void The_start_takes_none_where_the_role_dropped_the_choice_or_none_was_approved()
    {
        var handed = new HandedQuest("codex", null, "s1", HandOverWhy.Agent, HandOverBy.Agent, DateTimeOffset.UnixEpoch) { Model = "gpt-5" };
        Assert.Null(ApprovedChoice.Of(handed, [Entry("codex", "gpt-4", null, "1")]));
        Assert.Null(ApprovedChoice.Of(handed with { Model = null }, [Entry("codex", null, null, "1")]));
        Assert.Null(ApprovedChoice.Of(null, [Entry("codex", "gpt-5", null, "1")]));
    }

    [Fact]
    public void A_handed_quests_file_keeps_the_approved_model_and_reads_one_from_before_as_none()
    {
        var folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "handed-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "q1.json");
            var handed = new HandedQuest("codex", null, "s1", HandOverWhy.Agent, HandOverBy.Agent, DateTimeOffset.UnixEpoch) { Model = "gpt-5", Effort = "medium" };
            HandOverFiles.WriteHanded(path, handed);
            var read = HandOverFiles.ReadHanded(path)!;
            Assert.Equal(("gpt-5", "medium"), (read.Model, read.Effort));

            HandOverFiles.WriteHanded(path, handed with { Model = null, Effort = null });
            var before = HandOverFiles.ReadHanded(path)!;
            Assert.Null(before.Model);
            Assert.Null(before.Effort);
        }
        finally
        {
            GitSeed.Discard(folder);
        }
    }
}
