using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The workflow choices as <c>driver.json</c> keeps them (FOUNDATION1 mod-iii): the settings' own reading and writing, which the
/// selection answers through, so a key is read and kept alike wherever a choice is named.
/// </summary>
public sealed class WorkflowKeyTests
{
    [Fact]
    public void The_selection_answers_through_the_key_for_its_limits_and_its_words()
    {
        Assert.Equal(WorkflowKey.MaxKinds, WorkflowSelection.MaxKinds);
        Assert.Equal(WorkflowKey.PathLimit, WorkflowSelection.PathLimit);
        Assert.Equal(WorkflowKey.LandedProblem, WorkflowSelection.LandedProblem);
        Assert.Equal(WorkflowKey.KindIdProblem("x"), WorkflowSelection.KindIdProblem("x"));
        Assert.Equal(WorkflowKey.PathsProblem("docs"), WorkflowSelection.PathsProblem("docs"));
    }

    [Fact]
    public void A_choice_is_read_and_kept_as_it_was_written_and_the_selection_reads_it_alike()
    {
        const string text = """{"default":"docs-to-pr","kinds":{"docs":{"label":"Docs","paths":["docs/a"],"workflow":"current"}},"landed":{"workflow":"verify","gates":"none"}}""";
        using var document = JsonDocument.Parse(text);

        var (choice, problem) = WorkflowKey.Read(document.RootElement, workspace: true);

        Assert.Null(problem);
        Assert.Equal("docs-to-pr", choice!.Default);
        Assert.Equal(new WorkflowKindChoice("Docs", ["docs/a"], "current"), choice.KindOf("docs"), new KindComparer());
        Assert.Equal(new WorkflowLanded("verify", "none"), choice.Landed);
        Assert.Equal(WorkflowSelection.Read(document.RootElement, workspace: true).Choice!.Default, choice.Default);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) WorkflowKey.Write(writer, choice, workspace: true);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    [Theory]
    [InlineData("""[]""", false)]
    [InlineData("""{"default":"Not An Id"}""", false)]
    [InlineData("""{"kinds":[]}""", false)]
    [InlineData("""{"kinds":{"docs":"x y"}}""", false)]
    [InlineData("""{"landed":{"workflow":"verify","gates":"abc"}}""", false)]
    public void An_entry_that_does_not_hold_says_its_first_problem(string text, bool workspace)
    {
        using var document = JsonDocument.Parse(text);

        var (choice, problem) = WorkflowKey.Read(document.RootElement, workspace);

        Assert.Null(choice);
        Assert.NotNull(problem);
        Assert.Equal(problem, WorkflowSelection.Problem(document.RootElement, workspace));
    }

    [Fact]
    public void A_map_keeps_the_first_readable_entry_of_a_name_written_twice_and_skips_one_naming_nothing()
    {
        using var document = JsonDocument.Parse("""{"workflows":{" a ":{"default":"one"},"A":{"default":"two"},"b":{},"c":{"default":"bad id"}}}""");

        var map = WorkflowKey.Map(document.RootElement, "workflows", workspace: false);

        Assert.Equal(["a"], map.Keys);
        Assert.Equal("one", map["a"].Default);
    }

    private sealed class KindComparer : IEqualityComparer<WorkflowKindChoice?>
    {
        public bool Equals(WorkflowKindChoice? x, WorkflowKindChoice? y) =>
            x is not null && y is not null && x.Label == y.Label && x.Workflow == y.Workflow && x.Paths.SequenceEqual(y.Paths);

        public int GetHashCode(WorkflowKindChoice? obj) => obj?.Label?.GetHashCode() ?? 0;
    }
}
