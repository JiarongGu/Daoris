using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Risk;

/// <summary>
/// The judge: one closed census of act kinds, each with its seed class; one record <c>risk</c> in <c>driver.json</c>, read
/// narrowest place first; an unreadable record reads every act at the owner; today's <c>placeRisk</c> carried through (a place at high reads
/// every kind at the owner, a production mark its destination's row); <c>policy.change</c> fixed at the owner whatever a row says.
/// </summary>
public sealed class RiskGateTests
{
    private static RiskPolicy Policy(string json) => RiskPolicy.Read(JsonDocument.Parse(json).RootElement);

    private static readonly RiskPlace Restricted = new("work", "app", Full: false);

    private static readonly RiskPlace Full = new("work", "app", Full: true);

    private static RiskVerdict Judge(RiskPolicy policy, string kind, RiskPlace place) => RiskGate.Judge(policy, RiskAct.Of(kind), "agent:alpha", place, "run:q7/holds");

    [Theory]
    [InlineData(RiskActs.WorkDone, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.WorkAccept, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.TaskApprove, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.WorkRecover, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.LineLand, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.RemotePush, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.ReleaseStart, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.PublishOutside, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.SpendMoney, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.DirectionSet, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.CheckNarrow, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.ReachWiden, RiskClasses.Role, RiskClasses.None)]
    [InlineData(RiskActs.CommandChange, RiskClasses.Owner, RiskClasses.None)]
    [InlineData(RiskActs.AgentVersion, RiskClasses.None, RiskClasses.None)]
    [InlineData(RiskActs.HistoryRewrite, RiskClasses.Owner, RiskClasses.Owner)]
    [InlineData(RiskActs.DataDelete, RiskClasses.Owner, RiskClasses.Owner)]
    [InlineData(RiskActs.PolicyChange, RiskClasses.Owner, RiskClasses.Owner)]
    public void Each_kind_reads_its_seed_under_each_mode_where_nothing_is_written(string kind, string restricted, string full)
    {
        var none = Policy("{}");

        var underRestricted = Judge(none, kind, Restricted);
        var underFull = Judge(none, kind, Full);

        Assert.Equal((restricted, full), (underRestricted.Class, underFull.Class));
        Assert.Equal(RiskVerdict.OutcomeOf(restricted), underRestricted.Outcome);
        Assert.Equal(kind == RiskActs.PolicyChange ? RiskVerdict.Fixed : RiskVerdict.Seed, underRestricted.Source);
        Assert.Equal(RiskPolicy.SeedRevision, underRestricted.Revision);
    }

    [Fact]
    public void The_census_is_closed_seventeen_kinds_and_a_kind_outside_it_is_refused()
    {
        Assert.Equal(17, RiskActs.All.Count);
        Assert.Throws<ArgumentException>(() => RiskAct.Of("work.vanish"));
        Assert.Equal(("release.start", "production"), (RiskAct.Of("release.start/production").Kind, RiskAct.Of("release.start/production").Qualifier));
    }

    [Fact]
    public void A_held_permission_under_restricted_access_goes_to_the_places_approver_and_a_role_row_names_its_role()
    {
        Assert.Equal((RiskVerdict.Gate, "approver"), (Judge(Policy("{}"), RiskActs.ReachWiden, Restricted).Outcome, Judge(Policy("{}"), RiskActs.ReachWiden, Restricted).By));

        var policy = Policy("""{ "risk": { "machine": [ { "kind": "work.done", "class": "role" }, { "kind": "work.accept", "class": "role", "by": "reviewer" } ] } }""");

        Assert.Equal((RiskVerdict.Gate, AppointedRoles.Supervisor), (Judge(policy, RiskActs.WorkDone, Full).Outcome, Judge(policy, RiskActs.WorkDone, Full).By));
        Assert.Equal("reviewer", Judge(policy, RiskActs.WorkAccept, Full).By);
    }

    [Fact]
    public void The_narrowest_place_with_a_row_decides_and_a_qualified_or_mode_keyed_row_stands_over_its_kinds()
    {
        var policy = Policy("""
            { "risk": {
              "machine": [ { "kind": "line.land", "class": "owner" }, { "kind": "release.start", "class": "role" } ],
              "workspace/work": [ { "kind": "line.land", "class": "role" }, { "kind": "release.start/production", "class": "owner" } ],
              "repository/app": [ { "kind": "line.land", "class": "none" }, { "kind": "command.change", "class": "role", "mode": "restricted" } ] } }
            """);

        var land = Judge(policy, RiskActs.LineLand, Restricted);
        Assert.Equal((RiskClasses.None, "repository/app"), (land.Class, land.Source));
        Assert.Equal((RiskClasses.Role, "workspace/work"), (Judge(policy, RiskActs.LineLand, new RiskPlace("work", null, false)).Class, Judge(policy, RiskActs.LineLand, new RiskPlace("work", null, false)).Source));
        Assert.Equal(RiskClasses.Owner, Judge(policy, RiskActs.LineLand, new RiskPlace("other", "else", false)).Class);
        Assert.Equal((RiskClasses.Owner, "workspace/work"), (Judge(policy, "release.start/production", Restricted).Class, Judge(policy, "release.start/production", Restricted).Source));
        Assert.Equal((RiskClasses.Role, "machine"), (Judge(policy, "release.start/staging", Restricted).Class, Judge(policy, "release.start/staging", Restricted).Source));
        Assert.Equal(RiskClasses.Role, Judge(policy, RiskActs.CommandChange, Restricted).Class);
        // Under full access the mode-keyed row does not apply, and the seed stands.
        Assert.Equal((RiskClasses.None, RiskVerdict.Seed), (Judge(policy, RiskActs.CommandChange, Full).Class, Judge(policy, RiskActs.CommandChange, Full).Source));
        Assert.NotEqual(RiskPolicy.SeedRevision, land.Revision);
    }

    [Fact]
    public void An_unreadable_record_reads_every_act_at_the_owner_and_says_its_fault()
    {
        foreach (var broken in new[] { """{ "risk": [] }""", """{ "risk": { "machine": { "kind": "work.done" } } }""", """{ "risk": { "machine": [ { "kind": "work.done" } ] } }""", """{ "risk": { "galaxy/far": [] } }""", """{ "risk": { "machine": [ { "kind": "work.done", "class": "none", "mode": "sideways" } ] } }""" })
        {
            var policy = Policy(broken);
            Assert.NotNull(policy.Fault);
            var verdict = Judge(policy, RiskActs.CheckNarrow, Full);
            Assert.Equal((RiskVerdict.OwnerOutcome, RiskClasses.Owner, RiskVerdict.Unreadable), (verdict.Outcome, verdict.Class, verdict.Source));
            Assert.Contains(policy.Fault!, verdict.Reason, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_class_this_build_does_not_know_reads_the_owner_and_a_row_naming_no_known_kind_is_named_never_read()
    {
        var policy = Policy("""{ "risk": { "machine": [ { "kind": "work.done", "class": "maybe" }, { "kind": "work.vanish", "class": "none" } ] } }""");

        Assert.Null(policy.Fault);
        Assert.Equal(RiskClasses.Owner, Judge(policy, RiskActs.WorkDone, Full).Class);
        Assert.Equal(["machine work.vanish"], policy.Unknown);
    }

    [Fact]
    public void The_policys_own_change_is_fixed_at_the_owner_whatever_a_row_says()
    {
        var policy = Policy("""{ "risk": { "machine": [ { "kind": "policy.change", "class": "none" } ] } }""");

        var verdict = Judge(policy, RiskActs.PolicyChange, Full);

        Assert.Equal((RiskVerdict.OwnerOutcome, RiskVerdict.Fixed), (verdict.Outcome, verdict.Source));
    }

    [Fact]
    public void A_place_at_high_carried_from_place_risk_reads_every_kind_at_the_owner_and_a_production_mark_its_destinations_row()
    {
        var policy = Policy("""
            { "placeRisk": {
                "workspace/work": { "risk": "high", "marks": [] },
                "workspace/shop": { "risk": "low", "marks": ["release.production"] },
                "repository/calm": { "risk": "low", "marks": [] } },
              "risk": { "repository/app": [ { "kind": "line.land", "class": "none" } ] } }
            """);

        var done = Judge(policy, RiskActs.WorkDone, Full);
        Assert.Equal((RiskClasses.Owner, "workspace/work"), (done.Class, done.Source));
        Assert.Contains("high", done.Reason, StringComparison.Ordinal);
        // A row written at a narrower place stands over the wider place's level.
        Assert.Equal(RiskClasses.None, Judge(policy, RiskActs.LineLand, Full).Class);
        // A repository whose own level is set low stands over its workspace's high, as the level reads today.
        Assert.Equal(RiskClasses.None, Judge(policy, RiskActs.WorkDone, new RiskPlace("work", "calm", true)).Class);
        var shop = new RiskPlace("shop", "store", true);
        Assert.Equal(RiskClasses.Owner, Judge(policy, "release.start/production", shop).Class);
        Assert.Equal(RiskClasses.Owner, Judge(policy, RiskActs.ReleaseStart, shop).Class);
        Assert.Equal(RiskClasses.None, Judge(policy, RiskActs.WorkDone, shop).Class);
    }

    [Fact]
    public void The_resolved_table_lists_every_kind_once_in_census_order()
    {
        var table = RiskGate.Table(Policy("{}"), Restricted);

        Assert.Equal(RiskActs.All, table.Select(row => row.Kind));
        Assert.Equal(RiskClasses.Owner, table.Single(row => row.Kind == RiskActs.CommandChange).Class);
    }
}
