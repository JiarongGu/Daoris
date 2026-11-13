using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The lead and the scout are seeded working roles like the others: the lead takes plan work on Design's tier, the scout orient work on
/// Exact's, each staffed through <c>role_staffing</c> and walked by <c>StartPlan</c>; with one removed its work falls to Design's or Exact's role,
/// and the census admits the lead on an intake's marks and the scout on a helper's.
/// </summary>
public sealed class TeamRolesTests
{
    private static readonly RolePlace Here = new("w1", "r1");

    [Fact]
    public void The_seed_holds_a_lead_taking_plan_work_and_a_scout_taking_orient_work_on_the_machine()
    {
        var lead = Assert.Single(RoleCatalog.Seed, role => role.Id == "lead");
        var scout = Assert.Single(RoleCatalog.Seed, role => role.Id == "scout");

        Assert.Equal([RoleWork.Plan], lead.Takes);
        Assert.Equal([RoleWork.Orient], scout.Takes);
        Assert.All(new[] { lead, scout }, role => Assert.Equal(RoleScope.Everywhere, role.Scope));
        Assert.Equal(("Lead", "负责人", "Scout", "侦察"), (lead.Name.In("en"), lead.Name.In("zh"), scout.Name.In("en"), scout.Name.In("zh")));
        Assert.All(new[] { "lead", "scout" }, id =>
        {
            Assert.Contains(id, ActorChoices.Ids);
            Assert.Contains(id, ActorChoices.Ordered);
        });
        Assert.Equal("lead", StartPlan.RoleOf(RoleWork.Plan));
        Assert.Equal("scout", StartPlan.RoleOf(RoleWork.Orient));
    }

    [Fact]
    public void The_lead_is_staffed_on_Designs_tier_and_the_scout_on_Exacts()
    {
        Assert.Equal([ActorChoices.DesignSeed], ActorChoices.Seed("lead"));
        Assert.Equal([ActorChoices.ExactSeed], ActorChoices.Seed("scout"));
        Assert.Equal("opus", ActorChoices.Effective("lead", DriverConfig.Empty with { Actors = new Dictionary<string, IReadOnlyList<ActorChoice>> { ["lead"] = ActorChoices.Seed("lead")! } }).Single().Model);
    }

    [Fact]
    public void A_home_older_than_the_lead_and_the_scout_holds_no_row_for_them_and_their_seeds_stand_until_the_person_sets_an_order()
    {
        var none = new Dictionary<string, string>();
        var older = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>>(), none, none));
        var own = new ActorChoice("claude-code", "sonnet", "low");
        var set = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>> { ["lead"] = [own] }, none, none));
        var unread = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>>(), new Dictionary<string, string> { ["scout"] = "bad" }, none));

        Assert.Equal([ActorChoices.DesignSeed], older.Actors["lead"]);
        Assert.Equal([ActorChoices.ExactSeed], older.Actors["scout"]);
        Assert.Equal([own], set.Actors["lead"]);
        Assert.False(unread.Actors.ContainsKey("scout"));
        Assert.Equal("bad", unread.ActorErrors["scout"]);
    }

    [Fact]
    public void Plan_and_orient_work_take_the_seeded_roles_and_walk_their_own_orders()
    {
        var catalogue = RoleCatalog.Of([]);
        var config = DriverConfig.Empty with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>
            {
                ["design"] = [new ActorChoice("claude-code", "opus", "max")],
                ["lead"] = [new ActorChoice("claude-code", "opus", "high")],
                ["scout"] = [new ActorChoice("claude-code", "haiku")],
            },
        };

        Assert.Equal("lead", catalogue.RoleFor(RoleWork.Plan, Here).Role!.Id);
        Assert.Equal("scout", catalogue.RoleFor(RoleWork.Orient, Here).Role!.Id);
        Assert.Equal("high", catalogue.Order(RoleWork.Plan, Here, config).Single().Effort);
        Assert.Equal("haiku", catalogue.Order(RoleWork.Orient, Here, config).Single().Model);
    }

    [Fact]
    public void With_the_lead_removed_plan_work_falls_to_Designs_role_and_with_the_scout_removed_orient_work_to_Exacts()
    {
        var removed = RoleCatalog.Of([new StoredRole("lead", "removed", "{}", 1), new StoredRole("scout", "removed", "{}", 1)]);

        Assert.Equal("design", removed.RoleFor(RoleWork.Plan, Here).Role!.Id);
        Assert.Equal("exact", removed.RoleFor(RoleWork.Orient, Here).Role!.Id);
        Assert.Null(removed.Judge(RoleChange.Remove("lead")));
        Assert.Null(RoleCatalog.Of([]).Judge(RoleChange.Remove("scout")));
        Assert.Equal(("design", "exact"), (RoleCatalog.Fallback(RoleWork.Plan), RoleCatalog.Fallback(RoleWork.Orient)));
        Assert.Null(RoleCatalog.Fallback(RoleWork.Build));
    }

    [Fact]
    public void A_role_named_for_a_plan_start_must_take_plan_or_the_work_it_falls_to()
    {
        var catalogue = RoleCatalog.Of([]);

        Assert.Null(catalogue.Named("lead", RoleWork.Plan, Here).Held);
        Assert.Null(catalogue.Named("design", RoleWork.Plan, Here).Held);
        Assert.NotNull(catalogue.Named("intake", RoleWork.Plan, Here).Held);
        Assert.Null(catalogue.Named("scout", RoleWork.Orient, Here).Held);
    }

    [Fact]
    public void The_census_admits_the_lead_on_an_intake_s_marks_and_the_scout_on_a_helper_s()
    {
        RoleRecord? Seeded(string role, long revision) => RoleCatalog.Seed.SingleOrDefault(each => each.Id == role);

        Assert.Null(RoleCensus.Check(ParticipantWork.Intake, "lead", null, Seeded));
        Assert.Null(RoleCensus.Check(ParticipantWork.Help, "scout", null, Seeded));
        Assert.NotNull(RoleCensus.Check(ParticipantWork.Intake, "scout", null, Seeded));
        Assert.NotNull(RoleCensus.Check(ParticipantWork.Build, "lead", null, Seeded));
    }
}
