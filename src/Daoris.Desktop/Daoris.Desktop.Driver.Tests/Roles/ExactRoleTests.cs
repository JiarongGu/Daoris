using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Exact build is a seeded working role taking exact work on Claude Code · sonnet at the account's default effort, so a quest its
/// publisher marked exact starts on it with no model named; judgement and plain build work keep their own roles; with it removed the work
/// falls to the role that takes build.
/// </summary>
public sealed class ExactRoleTests
{
    private static readonly RolePlace Here = new("w1", "r1");

    [Fact]
    public void The_seed_holds_an_exact_role_taking_exact_work_on_the_machine()
    {
        var exact = Assert.Single(RoleCatalog.Seed, role => role.Id == "exact");
        Assert.Equal([RoleWork.Exact], exact.Takes);
        Assert.Equal(RoleScope.Everywhere, exact.Scope);
        Assert.Equal(("Exact build", "精确开发"), (exact.Name.In("en"), exact.Name.In("zh")));
        Assert.Contains("exact", ActorChoices.Ids);
        Assert.Contains("exact", ActorChoices.Ordered);
        Assert.Contains(ParticipantWork.Exact, ParticipantWork.Codes);
        Assert.Contains(RoleWork.Exact, RoleWork.Codes);
        Assert.Contains(RoleWork.Exact, RoleWork.Driven);
    }

    [Fact]
    public void Exact_work_takes_the_exact_role_whose_seed_is_the_references_build_exact_row()
    {
        Assert.Equal("exact", StartPlan.RoleOf(RoleWork.Exact));
        Assert.Equal("build", StartPlan.RoleOf(RoleWork.Build));
        Assert.Equal("design", StartPlan.RoleOf(RoleWork.Design));
        Assert.Equal("exact", RoleCatalog.Of([]).RoleFor(RoleWork.Exact, Here).Role!.Id);
        Assert.Equal([new ActorChoice("claude-code", "sonnet", null)], ActorChoices.Seed("exact"));

        var lines = File.ReadLines(Path.Combine(WorkspaceRoot.Folder, "docs", "model-roles.md")).ToList();
        var cells = lines.Single(line => line.StartsWith("| **build-exact** |", StringComparison.Ordinal)).Split('|', StringSplitOptions.TrimEntries);
        Assert.Equal(("Claude Sonnet 5.5", "default"), (cells[3], cells[4]));
        Assert.Empty(ActorSuggestion.Census(lines));
    }

    [Fact]
    public void An_exact_start_walks_the_exact_order_where_judgement_and_build_walk_their_own()
    {
        var config = DriverConfig.Empty with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>
            {
                ["build"] = [new ActorChoice("claude-code", "opus", "high")],
                ["design"] = [new ActorChoice("claude-code", "opus", "max")],
                ["exact"] = [new ActorChoice("claude-code", "sonnet[1m]")],
            },
        };
        var catalogue = RoleCatalog.Of([]);

        Assert.Equal(("sonnet[1m]", null), (catalogue.Order(RoleWork.Exact, Here, config).Single().Model, catalogue.Order(RoleWork.Exact, Here, config).Single().Effort));
        Assert.Equal("opus", catalogue.Order(RoleWork.Build, Here, config).Single().Model);
        Assert.Equal("max", catalogue.Order(RoleWork.Design, Here, config).Single().Effort);
    }

    [Fact]
    public void A_quest_s_weight_marks_its_work_and_an_exact_start_says_whose_order_it_ran_on()
    {
        var quest = new QuestView("q1", "a", "b", "t", "body", "Open");
        Assert.Equal("exact", ParticipantWork.OfWeight("exact"));
        Assert.Equal("design", ParticipantWork.OfWeight("design"));
        Assert.Equal("build", ParticipantWork.OfWeight(quest.Weight));

        Assert.Equal("Exact build: it ran on Exact build's order.", RotatedOpening.ExactLine(ActorWords.Name("exact")).Note);
        Assert.Equal("Exact build: it ran on Build's order.", RotatedOpening.ExactLine(ActorWords.Name("build")).Note);
    }

    [Fact]
    public void With_the_exact_role_removed_exact_work_falls_to_the_role_that_takes_build()
    {
        var removed = RoleCatalog.Of([new StoredRole("exact", "removed", "{}", 1)]);
        Assert.Equal("build", removed.RoleFor(RoleWork.Exact, Here).Role!.Id);
        Assert.Null(removed.Judge(RoleChange.Remove("exact")));
        Assert.Null(RoleCatalog.Of([]).Judge(RoleChange.Remove("exact")));
    }

    [Fact]
    public void A_role_that_takes_build_takes_an_exact_step_a_workflow_names_and_a_line_of_an_exact_quest_is_ruled_as_exact()
    {
        Assert.Null(RoleCatalog.Of([]).Named("build", RoleWork.Exact, Here).Held);
        Assert.Null(RoleCatalog.Of([]).Named("exact", RoleWork.Exact, Here).Held);
        Assert.Equal("exact", RuledLine.Of(null, new PriorSession("s1", null) { Actor = "exact" }).Role);
    }

    [Fact]
    public void A_home_older_than_the_role_reads_the_seed_for_it_and_one_that_set_its_own_keeps_it()
    {
        IReadOnlyDictionary<string, string> none = new Dictionary<string, string>();
        var older = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>>(), none, none));
        Assert.Equal([ActorChoices.ExactSeed], older.Actors["exact"]);

        var own = new ActorChoice("codex", "gpt-5", null);
        var set = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>> { ["exact"] = [own] }, none, none));
        Assert.Equal([own], set.Actors["exact"]);

        var unread = StaffingProjection.Config(new StaffingRead(new Dictionary<string, IReadOnlyList<ActorChoice>>(), new Dictionary<string, string> { ["exact"] = "bad" }, none));
        Assert.False(unread.Actors.ContainsKey("exact"));
    }
}
