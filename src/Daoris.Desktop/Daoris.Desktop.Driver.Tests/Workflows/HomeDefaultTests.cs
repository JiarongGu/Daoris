using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOWCLEAN1 1a (the cleanup design §1.1, §1.4, §1.5): the home's default, the seed a fresh home is given once, the line's policy, the
/// consent a home-default edit asks, and the rollback refusal a converted home makes.
/// </summary>
public sealed class HomeDefaultTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "home-default-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_fresh_home_is_seeded_once_with_the_merge_copy_and_a_home_that_has_a_file_never()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.DriverConfig);

        Assert.True(HomeSeed.Once(path, _home));

        var config = DriverConfig.Load(path);
        Assert.Equal("merge-after-accept", config.DefaultWorkflow);
        var saved = WorkflowPorts.Store(_home).Load("merge-after-accept")!.Read.Versions;
        Assert.Equal(1, saved[0].Version);
        Assert.Equal(WorkflowDefaults.Of("merge-after-accept")!.Digest, saved[0].Digest);
        // A fresh repository follows it with nothing written, at level `home`.
        var selected = WorkflowSelection.Resolve(config, "engine", "aurora", null);
        Assert.Equal(("merge-after-accept", WorkflowLevels.Home), (selected.Workflow, selected.Level));
        Assert.False(HomeSeed.Once(path, _home));
        Assert.Equal(1, WorkflowPorts.Store(_home).Load("merge-after-accept")!.Read.Versions.Count);
    }

    [Fact]
    public void A_home_that_already_has_a_file_is_not_seeded()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}\n");

        Assert.False(HomeSeed.Once(path, _home));
        Assert.Null(DriverConfig.Load(path).DefaultWorkflow);
        Assert.Null(WorkflowPorts.Store(_home).Load("merge-after-accept"));
    }

    [Fact]
    public void The_new_keys_are_kept_by_a_save_that_changes_something_else()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"defaultWorkflow":"merge-after-accept","linePolicies":{"engine":{"batch":4,"gates":"declared"}},"futureKey":{"a":1}}""");

        DriverConfig.Load(path).WithCap(3).Save(path);

        var kept = DriverConfig.Load(path);
        Assert.Equal("merge-after-accept", kept.DefaultWorkflow);
        Assert.Equal(4, kept.LinePolicyOf("engine", null)!.Batch);
        Assert.Contains("futureKey", File.ReadAllText(path));
    }

    [Fact]
    public void A_development_route_is_never_given_current_and_the_landed_route_keeps_its_current()
    {
        var refused = Assert.Throws<DriverException>(() => WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowUse("current", "engine", null, null)));
        Assert.Equal(WorkflowSelection.CurrentRefused, refused.Message);

        var landed = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowUse("current", "engine", null, null) { Landed = true });
        Assert.Equal("current", landed.Workflows["engine"].Landed!.Workflow);
        // A choice cleared follows the home's default again.
        var cleared = WorkflowSelection.Apply(
            WorkflowSelection.Apply(DriverConfig.Empty.WithDefaultWorkflow("merge-after-accept"), new WorkflowUse("documents", "engine", null, null)),
            new WorkflowUse(null, "engine", null, null));
        Assert.Equal(WorkflowLevels.Home, WorkflowSelection.Resolve(cleared, "engine", null, null).Level);
    }

    [Fact]
    public void A_line_policy_is_judged_set_and_cleared_and_reaches_what_integration_reads()
    {
        var config = DriverConfig.Empty.WithLinePolicy("engine", new LandingIntegrate(Batch: 4, Gates: LandingIntegrate.Declared));

        Assert.Equal(4, LandingRules.Choose(config, "engine", null).Rule.Integrate!.Batch);
        Assert.Equal(4, LandingRules.Choose(config, "engine", null).Rule.Integrate!.Batch);
        Assert.Throws<DriverException>(() => config.WithLinePolicy("engine", new LandingIntegrate(Batch: 99)));
        Assert.Throws<DriverException>(() => config.WithLinePolicy("engine", new LandingIntegrate(Request: "owner")));
        Assert.Null(config.WithLinePolicy("engine", null).LinePolicyOf("engine", null));
        // A workspace's policy reaches its repositories that set none; a repository's own replaces it whole.
        var shared = config.WithWorkspaceLinePolicy("aurora", new LandingIntegrate(Batch: 2, Gates: LandingIntegrate.Declared));
        Assert.Equal(2, shared.LinePolicyOf("web", "aurora")!.Batch);
        Assert.Equal(4, shared.LinePolicyOf("engine", "aurora")!.Batch);
    }

    [Fact]
    public void An_unconverted_home_keeps_integrating_by_the_rule_s_integrate_where_no_policy_is_set()
    {
        var config = DriverConfig.Parse("""{"landings":{"engine":{"form":"branch","pattern":"w/{quest}","integrate":{"batch":3,"gates":"declared"}}}}""");

        Assert.Equal(3, LandingRules.Choose(config, "engine", null).Rule.Integrate!.Batch);
        Assert.Equal(5, LandingRules.Choose(config.WithLinePolicy("engine", new LandingIntegrate(Batch: 5, Gates: LandingIntegrate.Declared)), "engine", null).Rule.Integrate!.Batch);
    }

    [Fact]
    public void Editing_the_home_default_s_copy_asks_consent_for_every_place_that_follows_it_with_nothing_written()
    {
        var config = DriverConfig.Empty.WithDefaultWorkflow("merge-after-accept");

        var scopes = WorkflowOperations.ScopesOf(config, "merge-after-accept");

        var home = Assert.Single(scopes);
        Assert.Equal((WorkflowOperations.HomeLevel, "default"), (home.Level, home.Role));
        Assert.Empty(WorkflowOperations.ScopesOf(config, "other"));
    }

    [Fact]
    public void An_applied_conversion_refuses_a_going_back_with_the_workflows_code_and_a_delivery_with_its_own()
    {
        Assert.Null(InstallRollback.Refused(_home, "0.41.0", "0.42.0"));

        var refused = InstallRollback.Refused(_home, "0.41.0", "0.42.0", conversionStands: _ => true);

        Assert.Equal(InstallRollback.Workflows, refused!.Value.Code);
        Assert.Null(InstallRollback.Refused(_home, "0.42.1", "0.42.0", conversionStands: _ => true));
    }
}
