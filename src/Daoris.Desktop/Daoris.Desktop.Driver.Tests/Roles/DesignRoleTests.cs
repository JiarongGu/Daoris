using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1l: Design is a seeded working role taking design work on Claude Code · opus · high, so a quest its publisher or the intake marks
/// as design work starts on it with no model named; with it removed the work falls to the role that takes build.
/// </summary>
public sealed class DesignRoleTests
{
    private static readonly RolePlace Here = new("w1", "r1");

    [Fact]
    public void The_seed_holds_a_design_role_taking_design_on_the_machine()
    {
        var design = Assert.Single(RoleCatalog.Seed, role => role.Id == "design");
        Assert.Equal([RoleWork.Design], design.Takes);
        Assert.Equal(RoleScope.Everywhere, design.Scope);
        Assert.Equal("Design", design.Name.In("en"));
        Assert.Contains("design", ActorChoices.Ids);
        Assert.Contains("design", ActorChoices.Ordered);
    }

    [Fact]
    public void Design_work_takes_the_design_role_and_its_seed_is_the_references_judgement_row()
    {
        Assert.Equal("design", StartPlan.RoleOf(RoleWork.Design));
        Assert.Equal("build", StartPlan.RoleOf(RoleWork.Build));
        Assert.Equal("design", RoleCatalog.Of([]).RoleFor(RoleWork.Design, Here).Role!.Id);
        Assert.Equal([new ActorChoice("claude-code", "opus", "high")], ActorChoices.Seed("design"));

        var row = File.ReadLines(Path.Combine(WorkspaceRoot.Folder, "docs", "model-roles.md")).Single(line => line.StartsWith("| **build-judgement** |", StringComparison.Ordinal));
        var cells = row.Split('|', StringSplitOptions.TrimEntries);
        Assert.Equal(("Claude Opus 5.5", "high"), (cells[3], cells[4]));
        Assert.Empty(ActorSuggestion.Census(File.ReadLines(Path.Combine(WorkspaceRoot.Folder, "docs", "model-roles.md"))));
    }

    [Fact]
    public void A_design_start_walks_the_design_order_where_a_build_start_walks_builds()
    {
        var config = DriverConfig.Empty with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>
            {
                ["build"] = [new ActorChoice("claude-code", "sonnet", "high")],
                ["design"] = [new ActorChoice("claude-code", "opus", "high")],
            },
        };

        var design = RoleCatalog.Of([]).Order(RoleWork.Design, Here, config);
        var build = RoleCatalog.Of([]).Order(RoleWork.Build, Here, config);

        Assert.Equal(("opus", "high"), (design.Single().Model, design.Single().Effort));
        Assert.Equal(("sonnet", "high"), (build.Single().Model, build.Single().Effort));
    }

    [Fact]
    public void With_the_design_role_removed_design_work_falls_to_the_role_that_takes_build()
    {
        var removed = RoleCatalog.Of([new StoredRole("design", "removed", "{}", 1)]);
        Assert.Equal("build", removed.RoleFor(RoleWork.Design, Here).Role!.Id);
    }

    [Fact]
    public void A_home_older_than_the_role_reads_the_seed_for_it_and_one_that_set_its_own_keeps_it()
    {
        IReadOnlyDictionary<string, string> none = new Dictionary<string, string>();
        var paths = new StaffingPaths(null, null, null, null);
        var older = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>>(), none, none, paths));
        Assert.Equal([ActorChoices.DesignSeed], older.Actors["design"]);

        var own = new ActorChoice("codex", "gpt-5", null);
        var set = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>> { ["design"] = [own] }, none, none, paths));
        Assert.Equal([own], set.Actors["design"]);

        var unread = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>>(), new Dictionary<string, string> { ["design"] = "bad" }, none, paths));
        Assert.False(unread.Actors.ContainsKey("design"));
    }

    [Fact]
    public void A_workflow_step_may_name_the_design_role()
    {
        Assert.Null(RoleCatalog.Of([]).Named("design", RoleWork.Design, Here).Held);
    }
}
