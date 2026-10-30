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

    /// <summary>The service's quest answer (its door carries <c>weight</c>), read by the client and started, with and without a role taking design.</summary>
    [Fact]
    public void A_quest_published_with_weight_design_starts_on_Build_s_order_then_on_the_role_that_takes_it()
    {
        var answered = ServiceClient.ReadQuests(
            """[{"id":"q1","from":"a","to":"b","title":"Decide the cap","body":"Two ways.","status":"Open","weight":"design"},""" +
            """{"id":"q2","from":"a","to":"b","title":"Cap it","body":"b","status":"Open"}]""");

        Assert.Equal("design", answered[0].Weight);
        Assert.Null(answered[1].Weight);

        var marked = Daoris.Driver.Driver.StartWork(answered[0]);
        Assert.Equal("build", Plan(marked, RoleCatalog.Of([]), Machine).Role);
        Assert.Equal("designer", Plan(marked, WithDesigner(), Machine).Role);
        Assert.Equal("build", Plan(Daoris.Driver.Driver.StartWork(answered[1]), WithDesigner(), Machine).Role);
    }

    [Fact]
    public void A_chat_continuation_keeps_its_binding_s_role_and_a_removed_role_falls_to_the_one_that_takes_chat_now()
    {
        var catalogue = RoleCatalog.Of(
        [
            new StoredRole("chat", "removed", "{}", 2), RoleCatalogTests.Stored(RoleCatalogTests.Working("talker", "chat"), 4),
        ]);
        var config = Config with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(Config.Actors)
            {
                ["chat"] = [new ActorChoice("claude")], ["talker"] = [new ActorChoice("codex")],
            },
        };
        var record = new PriorSession("s1", null) { Actor = "chat" };

        Assert.Equal("claude", StartPlan.ContinuedOn(record, config, "chat"));
        Assert.Equal("codex", StartPlan.ContinuedOn(record, config, "chat", bound: "talker", catalogue: catalogue, place: Machine));
        Assert.Equal("talker", StartPlan.BoundRole("gone", "chat", catalogue, Machine));
    }

    [Fact]
    public void A_helper_takes_the_catalogue_s_role_and_the_seeded_catalogue_changes_nothing()
    {
        var config = Config with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(Config.Actors)
            {
                ["helper"] = [new ActorChoice("claude", "haiku")], ["second"] = [new ActorChoice("claude", "opus")],
            },
        };
        var ask = new HelperAsk("h1", "s1", "Read this.", "claude", HelperAsk.ByPerson, DateTimeOffset.UnixEpoch);
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>());
        IReadOnlyList<ActorEffective> Order(RoleCatalog? catalogue) => HelperChoice.Order(ask, HelperRule.Default, adapters, null, config, catalogue);

        var today = Order(null);
        Assert.Equal([new ActorEffective("claude", "haiku", null, "1")], today);
        Assert.Equal(today, Order(RoleCatalog.Of([])));

        var added = RoleCatalog.Of([new StoredRole("helper", "removed", "{}", 2), RoleCatalogTests.Stored(RoleCatalogTests.Working("second", "help"), 5)]);
        Assert.Equal([new ActorEffective("claude", "opus", null, "1")], Order(added));
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
