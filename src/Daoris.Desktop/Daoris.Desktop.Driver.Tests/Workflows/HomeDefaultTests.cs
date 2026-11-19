using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The home's default, the seed a fresh home is given once, the line's policy, the
/// consent a home-default edit asks, and the rollback refusal a converted home makes.
/// </summary>
public sealed class HomeDefaultTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "home-default-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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

    /// <summary>A fresh home's seed gives <i>Second check</i> its first choices with their day's budget; no conversion of a home does.</summary>
    [Fact]
    public void A_fresh_home_s_seed_gives_the_second_check_its_first_choices_with_their_day_s_budget()
    {
        Assert.True(HomeSeed.Once(DaorisHome.Entry(_home, HomeEntry.DriverConfig), _home));

        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(SecondCheck.Choices, staffing.Read()!.Actors[SecondCheck.Role]);
        var policy = staffing.Policy(SecondCheck.Role).Policy!;
        Assert.Equal(StaffRules.Order, policy.Rule);
        Assert.All(SecondCheck.Choices, choice => Assert.Equal(SecondCheck.StartsADay, policy.Of(StaffKey.Of(choice))!.Budget!.Starts));
        Assert.False(SecondCheck.SeedFresh(_home, () => StoreSettled.Yes));
        Assert.Equal(1, staffing.Policy(SecondCheck.Role).Revision);
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

    /// <summary>
    /// A home whose <c>driver.json</c> came before its first start (a terminal's verb wrote it) gets no workflow's seed, and still gets the
    /// supervisor's rules and <i>Second check</i>'s first choices, once: the rules' record says it is done.
    /// </summary>
    [Fact]
    public void A_home_whose_file_came_first_still_gets_its_rules_and_second_check_once()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}\n");

        Assert.False(HomeSeed.Once(path, _home));

        Assert.True(new SupervisorRuleStore(_home, () => StoreSettled.Yes).Seeded());
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(SecondCheck.Choices, staffing.Read()!.Actors[SecondCheck.Role]);
        Assert.False(HomeSeed.Once(path, _home));
        Assert.Equal(1, staffing.Policy(SecondCheck.Role).Revision);
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

        Assert.Equal(4, config.LinePolicyOf("engine", null)!.Batch);
        Assert.Throws<DriverException>(() => config.WithLinePolicy("engine", new LandingIntegrate(Batch: 99)));
        Assert.Throws<DriverException>(() => config.WithLinePolicy("engine", new LandingIntegrate(Request: "owner")));
        Assert.Null(config.WithLinePolicy("engine", null).LinePolicyOf("engine", null));
        // A workspace's policy reaches its repositories that set none; a repository's own replaces it whole.
        var shared = config.WithWorkspaceLinePolicy("aurora", new LandingIntegrate(Batch: 2, Gates: LandingIntegrate.Declared));
        Assert.Equal(2, shared.LinePolicyOf("web", "aurora")!.Batch);
        Assert.Equal(4, shared.LinePolicyOf("engine", "aurora")!.Batch);
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
    public void A_going_back_with_no_delivery_unresolved_is_not_refused()
    {
        Assert.Null(InstallRollback.Refused(_home, "0.41.0", "0.42.0"));
        Assert.Null(InstallRollback.Refused(_home, "0.42.1", "0.42.0"));
    }
}
