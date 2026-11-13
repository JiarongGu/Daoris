using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's option values over the workflow it follows, as <c>driver.json</c> keeps them (<c>workflowValues</c>): by repository, by
/// workflow, by node, each option's JSON value, laid over the node's own options, so one shared workflow serves many repositories.
/// </summary>
public sealed class WorkflowValuesKeyTests
{
    private const string Json = """
        {"workflowValues":{"app":{"merge-after-accept":{"release":{"pipeline":7,"releaseDefinition":42,"stage":402,"action":"start-stage"}}},
         "  ":{"merge-after-accept":{"release":{"pipeline":1}}},"tools":{"merge-after-accept":{"release":"not an object"}}}}
        """;

    private static readonly NodeDeclaration Release = new("release", "daoris-azure-devops/release", 1, NodeReach.Performed)
    {
        Options = new Dictionary<string, string> { ["action"] = "\"observe\"", ["findSeconds"] = "600" },
    };

    [Fact]
    public void A_repositorys_values_are_laid_over_its_workflows_node_and_the_others_keep_the_shared_options()
    {
        var config = DriverConfigReader.Parse(Json);

        var over = NodeValues.Over(config,"APP", "merge-after-accept", Release);

        Assert.Equal("7", over.Options["pipeline"]);
        Assert.Equal("\"start-stage\"", over.Options["action"]);
        Assert.Equal("600", over.Options["findSeconds"]);
        Assert.Same(Release, NodeValues.Over(config,"app", "another-workflow", Release));
        Assert.Same(Release, NodeValues.Over(config,"tools", "merge-after-accept", Release));
        Assert.Same(Release, NodeValues.Over(config,"elsewhere", "merge-after-accept", Release));
    }

    [Fact]
    public void The_values_are_kept_as_written_and_a_blank_repository_or_a_node_in_no_known_shape_is_passed_over()
    {
        var config = DriverConfigReader.Parse(Json);

        Assert.Equal(["app"], config.WorkflowValues.Keys);
        var again = DriverConfigReader.Parse(DriverConfigWriter.Written(config));

        Assert.Equal("42", NodeValues.Over(again, "app","merge-after-accept", Release).Options["releaseDefinition"]);
        Assert.Empty(DriverConfigReader.Parse("{}").WorkflowValues);
        Assert.DoesNotContain(WorkflowValuesKey.Key, DriverConfigWriter.Written(DriverConfigReader.Parse("{}")), StringComparison.Ordinal);
    }
}
