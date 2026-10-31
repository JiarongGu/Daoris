using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1g (D205 point 7 and its 2026-10-25 note): the choices Daoris suggests for the seeded roles are one dated table held to
/// <c>docs/model-roles.md</c>'s roles table, failing both ways.
/// </summary>
public sealed class ActorSuggestionTests
{
    private static string[] Reference() => File.ReadAllLines(Path.Combine(WorkspaceRoot.Folder, "docs", "model-roles.md"));

    [Fact]
    public void The_table_equals_the_reference_s_rows_build_led_by_the_settled_row_then_the_judgement_row()
    {
        Assert.Empty(ActorSuggestion.Census(Reference()));

        var opus = new ActorChoice(RoleSuggestion.ClaudeCode, "opus", "high");
        Assert.Equal([new ActorChoice(RoleSuggestion.ClaudeCode, "sonnet", "high"), opus], ActorSuggestion.Table.Single(each => each.Role == "build").Choices);
        foreach (var role in new[] { "opinion", "helper", "partner", "brainstorm" })
        {
            Assert.Equal([opus], ActorSuggestion.Table.Single(each => each.Role == role).Choices);
        }

        Assert.DoesNotContain(ActorSuggestion.Table, each => each.Role is "intake" or "ask-daoris" or "chat");
    }

    [Theory]
    [InlineData("| **build-settled** |", "Claude Sonnet 5.5", "Claude Haiku 5.5")]
    [InlineData("| **build-settled** |", "| high |", "| default |")]
    [InlineData("| **build-judgement** |", "Claude Opus 5.5", "Claude Fable 5.1")]
    public void A_changed_reference_row_fails_the_census(string row, string was, string now)
    {
        var changed = Reference().Select(line => line.StartsWith(row, StringComparison.Ordinal) ? line.Replace(was, now, StringComparison.Ordinal) : line).ToArray();

        Assert.NotEqual(Reference(), changed);
        Assert.NotEmpty(ActorSuggestion.Census(changed));
    }

    [Fact]
    public void An_agent_not_set_up_is_left_out_and_a_role_left_with_none_goes_whole()
    {
        static IEnumerable<string> Said(IEnumerable<SuggestedChoices> table) => table.Select(each => $"{each.Role}: {string.Join(", ", each.Choices.Select(choice => choice.Said))}");
        Assert.Equal(Said(ActorSuggestion.Table), Said(ActorSuggestion.For(null)));
        Assert.Equal(Said(ActorSuggestion.Table), Said(ActorSuggestion.For(new HashSet<string> { RoleSuggestion.ClaudeCode, "codex-acp" })));
        Assert.Empty(ActorSuggestion.For(new HashSet<string> { "codex-acp" }));
    }
}
