using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xv, the Work orders cuts: what they keep. The words the review's doors say and log, the workflow choice's words, what the
/// intake's room tells of a repository that declared nothing, and the work inventory's obligations read from a home with no records, each
/// through the surface that stood on both sides of the cut. The seams the cuts add are <c>WorkOrdersPortsTests</c>'.
/// </summary>
public sealed class WorkOrdersBoundaryTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "work-orders-boundary", Guid.NewGuid().ToString("N")[..8]);

    public WorkOrdersBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void The_environments_a_rule_declares_are_told_in_backticks_and_a_verdict_is_logged_by_codes_alone()
    {
        Assert.Equal("", ReviewRules.Either([]));
        Assert.Equal("`staging`", ReviewRules.Either(["staging"]));
        Assert.Equal("`staging` or `local`", ReviewRules.Either(["staging", "local"]));
        Assert.Equal("`a`, `b` or `c`", ReviewRules.Either(["a", "b", "c"]));

        var line = ReviewLines.Verdict("q1", "reviewed", ReviewDoors.Terminal);
        Assert.Equal("review.verdict", line.Event);
        Assert.Equal([("quest", (object?)"q1"), ("said", "reviewed"), ("door", "terminal")], line.Data);
    }

    [Fact]
    public void The_workflow_choices_words_are_the_settings_and_a_workflows_id_has_the_ids_shape()
    {
        Assert.Equal("current", WorkflowSelection.Current);
        Assert.Equal("`Bad Id` is not a workflow's id: lower-case letters, digits and dashes, at most 40, such as `docs-to-pr`; or `current`.",
            WorkflowSelection.WorkflowProblem("Bad Id"));
        Assert.Equal("workspace `work` declares no kind `docs`: a kind is declared first, with its label.",
            WorkflowSelection.UndeclaredInWorkspace("work", "docs"));
        Assert.True(WorkflowSelection.IsWorkflow("docs-to-pr"));
        Assert.True(WorkflowSelection.IsWorkflow("current"));
        Assert.False(WorkflowSelection.IsWorkflow("Docs"));
        Assert.False(WorkflowSelection.IsWorkflow(new string('a', 41)));
        Assert.True(WorkflowSelection.IsKindId("docs"));
        Assert.False(WorkflowSelection.IsKindId(new string('a', 25)));
    }

    [Fact]
    public void A_repository_that_declared_nothing_is_told_by_what_its_own_files_say()
    {
        var root = Path.Combine(_home, "newcomer");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "README.md"), "# Atlas\n\nA tool that sorts maps.\n");
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), "[package]\nname = \"atlas\"\n");
        IReadOnlyList<DeclarationView> circle =
        [
            new("engine", Adopted: true, Registered: true, "The engine.", ["the core"], [], Root: null),
            new("newcomer", Adopted: false, Registered: true, null, [], [], Root: root),
        ];

        var room = File.ReadAllText(Path.Combine(IntakeRoom.Prepare(_home, "work", circle), "AGENTS.md"));

        Assert.Contains("its README.md says: ", room);
        Assert.Contains("A tool that sorts maps.", room);
        Assert.DoesNotContain("its README.md says: The engine.", room);
    }

    [Fact]
    public void A_home_with_no_records_owes_nothing_and_a_unit_that_owes_nothing_is_as_it_was()
    {
        Assert.Empty(WorkObligations.Read(_home, DateTimeOffset.UtcNow));
        Assert.Empty(WorkObligations.Owing(_home));
        IReadOnlyList<WorkUnit> units = [];
        Assert.Same(units, WorkObligations.Attach(units, WorkObligations.Read(_home, DateTimeOffset.UtcNow)));
    }
}
