using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The plan of a merge whose tree is gone and whose line no choice and no branch record names says why in its problem, never as a plugin.</summary>
public sealed class TreeGonePlanTests : IDisposable
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-gone-plan", Guid.NewGuid().ToString("N")[..8]);

    public TreeGonePlanTests() => Directory.CreateDirectory(Path.Combine(_folder, "home"));

    public void Dispose() => GitSeed.Discard(_folder);

    [Fact]
    public async Task A_gone_tree_with_no_line_names_the_reason_as_its_problem_and_no_plugin()
    {
        var plan = await new SessionTrees(Path.Combine(_folder, "home")).PlanAsync(Path.Combine(_folder, "gone"), new LandingSubject("s1", "q1", "Fix the gap"));

        Assert.Equal(LandingForm.Merge, plan.Form);
        Assert.Equal(TreeGoneLines.Unnamed, plan.Target);
        Assert.Null(plan.Plugin);
        Assert.Contains("its tree is gone", plan.Problem);
    }
}
