using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The release target's door (RELEASEVALUES1): a node's values set through one judge, the window's route and <c>daoris workflow values</c>
/// alike, the reason recorded beside them; a value that is no JSON, or no reason, is refused with nothing written.
/// </summary>
public sealed class WorkflowValuesDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-values-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkflowValuesDoorTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string File => Path.Combine(_home, "driver.json");

    private int Run(params string[] words)
    {
        var said = new StringWriter();
        var ask = WorkflowValuesCommand.Read(words, out var problem) ?? throw new InvalidOperationException(problem);
        var code = WorkflowValuesCommand.Run(ask, File, said);
        Said = said.ToString();
        return code;
    }

    private string Said { get; set; } = "";

    [Fact]
    public void The_release_manager_sets_a_target_with_its_reason_and_the_run_reads_it()
    {
        Assert.Equal(0, Run("values", "app", "release-azure", "create", "releaseDefinition=43", "stage=403", "--why", "the route for dev is #43"));

        var config = DriverConfig.Load(File);
        Assert.Equal("43", WorkflowValuesKey.Of(config.WorkflowValues, "app", "release-azure", "create")!["releaseDefinition"]);
        Assert.Equal("the route for dev is #43", config.WorkflowValuesWhy["app/release-azure/create"]);
        Assert.Contains("Why: the route for dev is #43", Said, StringComparison.Ordinal);

        Assert.Equal(0, Run("values", "app", "release-azure", "create", "stage=404", "--why", "Test, not Prod"));
        var laid = WorkflowValuesKey.Of(DriverConfig.Load(File).WorkflowValues, "app", "release-azure", "create")!;
        Assert.Equal(["releaseDefinition", "stage"], laid.Keys);
        Assert.Equal("404", laid["stage"]);
    }

    [Fact]
    public void No_reason_or_a_value_that_is_no_json_is_refused_and_nothing_is_written()
    {
        Assert.Equal(1, Run("values", "app", "release-azure", "create", "releaseDefinition=43"));
        Assert.Contains("--why", Said, StringComparison.Ordinal);
        Assert.Equal(1, Run("values", "app", "release-azure", "create", "action=start-stage", "--why", "because"));
        Assert.Contains("no JSON", Said, StringComparison.Ordinal);
        Assert.Empty(DriverConfig.Load(File).WorkflowValues);
    }

    [Fact]
    public void A_clear_takes_the_node_s_values_away_and_keeps_why()
    {
        Run("values", "app", "release-azure", "create", "releaseDefinition=43", "--why", "routed");
        Assert.Equal(0, Run("values", "app", "release-azure", "create", "--clear", "--why", "the definition was retired"));

        var config = DriverConfig.Load(File);
        Assert.Empty(config.WorkflowValues);
        Assert.Equal("the definition was retired", config.WorkflowValuesWhy["app/release-azure/create"]);
    }
}
