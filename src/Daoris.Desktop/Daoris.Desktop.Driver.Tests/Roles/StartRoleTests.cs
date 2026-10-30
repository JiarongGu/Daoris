using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1x step 1 (the foundation design §2.4, §2.7): the start plan finds a start's role by its work and place from the role
/// catalogue, keeps the seeded role for <c>actor</c>, and a continuation of a record an added role started runs as that seeded work.
/// </summary>
public sealed class StartRoleTests
{
    private static readonly RolePlace Machine = new();

    private static readonly DriverConfig Config = DriverConfig.Empty with
    {
        Adapter = "claude",
        Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>
        {
            ["build"] = [new ActorChoice("claude", "sonnet")],
            ["designer"] = [new ActorChoice("codex", "opus", "high"), new ActorChoice("claude", "opus")],
        },
    };

    private static StartPlan Plan(string work, RoleCatalog catalogue, RolePlace place = default, IReadOnlyList<string>? fence = null) =>
        new(work, Config, null!, null!, new PassMemory(), fence: fence, catalogue: catalogue, place: place) { Subject = "quest:q1" };

    private static RoleCatalog WithDesigner(string work = "design", RoleScope? scope = null, long revision = 3) =>
        RoleCatalog.Of([RoleCatalogTests.Stored(RoleCatalogTests.Working("designer", work, scope), revision)]);

    [Fact]
    public void A_quest_marked_design_starts_on_Build_s_order_while_no_role_takes_design_and_on_the_role_that_takes_it_once_one_does()
    {
        var before = Plan("design", RoleCatalog.Of([]), Machine);
        Assert.Equal(("build", "build", 0L), (before.Role, before.Seeded, before.Revision));
        Assert.Equal([new ActorEffective("claude", "sonnet", null, "1")], before.Order);

        var after = Plan("design", WithDesigner(), Machine);
        Assert.Equal(("designer", "build", 3L), (after.Role, after.Seeded, after.Revision));
        Assert.Equal([new ActorEffective("codex", "opus", "high", "1"), new ActorEffective("claude", "opus", null, "2")], after.Order);
    }

    [Fact]
    public void A_quest_s_weight_marks_its_work_and_a_design_start_says_whose_order_it_ran_on()
    {
        var quest = new QuestView("q1", "a", "b", "t", "body", "Open");
        Assert.Equal("build", Daoris.Driver.Driver.StartWork(quest));
        Assert.Equal("build", Daoris.Driver.Driver.StartWork(quest with { Weight = "exact" }));
        Assert.Equal("design", Daoris.Driver.Driver.StartWork(quest with { Weight = "design" }));

        Assert.Equal("Design work: it ran on Build's order.", RotatedOpening.DesignLine(ActorWords.Name("build")).Note);
        Assert.Equal("Design work: it ran on designer's order.", RotatedOpening.DesignLine("designer").Note);
    }

    [Fact]
    public void An_unmarked_quest_starts_on_Build_s_order_though_a_role_takes_design()
    {
        var plan = Plan("build", WithDesigner(), Machine);

        Assert.Equal(("build", 0L), (plan.Role, plan.Revision));
        Assert.Equal([new ActorEffective("claude", "sonnet", null, "1")], plan.Order);
    }

    [Fact]
    public void A_scoped_role_naming_an_agent_its_place_s_rule_does_not_permit_walks_only_the_permitted()
    {
        var catalogue = WithDesigner(scope: new RoleScope(RoleScope.Repository, "engine"));
        var inside = new RolePlace(Repository: "engine");

        var plan = Plan("design", catalogue, inside, fence: ["claude"]);

        Assert.Equal("designer", plan.Role);
        Assert.Equal([new ActorEffective("claude", "opus", null, "2")], plan.Order);
        Assert.Equal("build", Plan("design", catalogue, new RolePlace(Repository: "other")).Role);
    }

    [Fact]
    public void An_older_build_s_continuation_of_a_record_started_on_an_added_role_runs_as_its_seeded_work()
    {
        var plan = Plan("design", WithDesigner(), Machine);
        Assert.Contains(plan.Seeded, ActorChoices.Ids);

        // What an older build reads: the record's `actor`, the seeded id the start wrote, never the added role's.
        var record = new PriorSession("s1", null) { Actor = plan.Seeded };
        Assert.Equal("claude", StartPlan.ContinuedOn(record, Config));

        // The newer build reads the binding's role first, and where the role is since removed, the role that takes the work now.
        Assert.Equal("codex", StartPlan.ContinuedOn(record, Config, "design", bound: "designer", catalogue: WithDesigner(), place: Machine));
        Assert.Equal("claude", StartPlan.ContinuedOn(record, Config, "design", bound: "designer", catalogue: RoleCatalog.Of([]), place: Machine));
    }
}
