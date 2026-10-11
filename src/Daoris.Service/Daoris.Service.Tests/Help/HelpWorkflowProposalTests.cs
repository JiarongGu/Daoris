namespace Daoris.Service.Tests;

/// <summary>
/// The <c>workflow</c> kind's writer (WORKFLOW1h-a, D157, design §8.1): a whole version of a workflow for a repository or a
/// workspace, and optionally its choice. The box judges the shape only; the driver judges the rest.
/// </summary>
public sealed class HelpWorkflowProposalTests : HelpProposalBoxFixture
{
    private const string Steps = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","accept":"automatic"}]""";

    [Fact]
    public void A_new_workflow_is_written_with_its_steps_as_nested_json()
    {
        var (id, message) = Box().ProposeWorkflow(" web-app ", "docs-to-pr", null, Steps, false, null, null, null, "the person asked", "h1", Now);

        var written = Written(id!);
        Assert.Equal("workflow", written.GetProperty("kind").GetString());
        Assert.Equal("web-app", written.GetProperty("scope").GetString());
        Assert.Equal("docs-to-pr", written.GetProperty("workflow").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("base").ValueKind);
        Assert.False(written.GetProperty("choose").GetBoolean());
        var steps = written.GetProperty("steps");
        Assert.Equal(System.Text.Json.JsonValueKind.Array, steps.ValueKind);
        Assert.Equal(["work", "land"], steps.EnumerateArray().Select(step => step.GetProperty("id").GetString()));
        Assert.Equal("automatic", steps[1].GetProperty("accept").GetString());
        Assert.Equal("the person asked", written.GetProperty("why").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    [Fact]
    public void An_edit_names_its_base_and_may_choose_the_workflow()
    {
        var (id, _) = Box().ProposeWorkflow("web-app", "docs-to-pr", 3, Steps, true, null, null, null, "why", "h1", Now);

        var written = Written(id!);
        Assert.Equal(3, written.GetProperty("base").GetInt32());
        Assert.True(written.GetProperty("choose").GetBoolean());
    }

    [Theory]
    [InlineData("", "docs-to-pr", 1, Steps, null, null, null, "a reason", "scope")]
    [InlineData("web app", "docs-to-pr", 1, Steps, null, null, null, "a reason", "one word")]
    [InlineData("web-app", "", 1, Steps, null, null, null, "a reason", "workflow")]
    [InlineData("web-app", "Docs To PR", 1, Steps, null, null, null, "a reason", "lower-case")]
    [InlineData("web-app", "current", 1, Steps, null, null, null, "a reason", "Current")]
    [InlineData("web-app", "docs-to-pr", 0, Steps, null, null, null, "a reason", "whole number from 1")]
    [InlineData("web-app", "docs-to-pr", 1, "", null, null, null, "a reason", "steps")]
    [InlineData("web-app", "docs-to-pr", 1, "not json", null, null, null, "a reason", "JSON")]
    [InlineData("web-app", "docs-to-pr", 1, """{"id":"work"}""", null, null, null, "a reason", "list")]
    [InlineData("web-app", "docs-to-pr", 1, "[]", null, null, null, "a reason", "at least one")]
    [InlineData("web-app", "docs-to-pr", 1, "[1]", null, null, null, "a reason", "a JSON object")]
    [InlineData("web-app", "docs-to-pr", 1, Steps, "remove look", null, null, "a reason", "`changes`")]
    [InlineData("web-app", "docs-to-pr", 1, Steps, null, "landing", null, "a reason", "`kind`")]
    [InlineData("web-app", "docs-to-pr", 1, Steps, null, null, "file.json", "a reason", "`import`")]
    [InlineData("web-app", "docs-to-pr", 1, Steps, null, null, null, " ", "needs its reason")]
    public void A_malformed_workflow_proposal_is_refused_with_nothing_written(
        string scope, string workflow, int? baseVersion, string steps, string? changes, string? kind, string? import, string why, string says)
    {
        var (id, message) = Box().ProposeWorkflow(scope, workflow, baseVersion, steps, false, changes, kind, import, why, "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }

    [Fact]
    public void Changes_kind_and_import_are_refused_as_not_proposed_yet()
    {
        var (_, message) = Box().ProposeWorkflow("web-app", "docs-to-pr", 1, Steps, false, "x", null, null, "why", "h1", Now);

        Assert.Contains("not proposed yet", message);
    }
}
