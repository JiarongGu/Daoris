using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The rules decide where each executor read its number or its policy's move. Today's numbers seeded; a policy's moves read
/// back as rules and the rules read back as the course's moves; the old doors refused on a reached home; the save writing no `strikes` the
/// file did not hold.
/// </summary>
public sealed class SupervisorRuleBookTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-rule-book", Guid.NewGuid().ToString("N")[..8]);

    public SupervisorRuleBookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static SupervisorRule Rule(string count, int at, string does, string place = "machine", IReadOnlyList<string>? roles = null, int? upTo = null) =>
        new($"{count}-{at}", count, SupervisorRules.Windows[count], at, does, upTo, place, roles);

    private static SupervisorRuleBook Book(params SupervisorRuleSet[] sets) => new(sets);

    [Fact]
    public void CONTEXTDRIVE1l_a_store_with_no_machine_rules_decides_by_the_seeded_rules_as_today_s_readers_did()
    {
        var unwritten = SupervisorRuleBook.Read(_home).For("build", "daoris", "w1");
        Assert.Equal((2, 3, 5), (SupervisorRuleBook.AnswersAloneAt(unwritten), SupervisorRuleBook.FailuresAt(unwritten), SupervisorRuleBook.TriesLimit(unwritten)));
        Assert.Equal((2, 3, 5), (SupervisorRuleBook.AnswersAloneAt(SupervisorRuleBook.Seed.For("chat", null, null)),
            SupervisorRuleBook.FailuresAt(SupervisorRuleBook.Seed.For("chat", null, null)), SupervisorRuleBook.TriesLimit(SupervisorRuleBook.Seed.For("chat", null, null))));

        Assert.True(SupervisorRuleBook.SeedFresh(_home,() => StoreSettled.Yes));
        var seeded = SupervisorRuleBook.Read(_home);
        Assert.Single(seeded.Sets, set => set.Place == SupervisorRules.Machine && set.Revision == 1);
    }

    [Fact]
    public void CONTEXTDRIVE1e_a_workspace_is_a_repository_s_wider_place_at_the_judge_as_at_the_lines()
    {
        var wider = RuleWiderPlaces.From([("daoris", "w1"), ("atlas", null)]);
        Assert.Equal(["workspace:w1"], wider.Of("repository:daoris"));
        Assert.Empty(wider.Of("repository:atlas"));
        Assert.Empty(wider.Of("workspace:w1"));
        Assert.Empty(RuleWiderPlaces.None.Of("repository:daoris"));

        // The workspace moves on quiet with a bound; the repository's own tries rule leaves it no moves rule of its own.
        var held = new List<SupervisorRuleSet>
        {
            new("machine", 1, [.. SupervisorRuleBook.Seeded(3).Where(rule => rule.Count != SupervisorRules.Moves)]),
            new("workspace:w1", 1, [Rule("quiet", 1, "next", "workspace:w1"), Rule("moves", 3, "ask", "workspace:w1")]),
        };
        var world = new SupervisorRuleWorld(["build", "chat"], Wider: wider.Of);
        Assert.Null(SupervisorRules.Judge("repository:daoris", 0, [Rule("tries", 4, "hold", "repository:daoris")], held, world));

        var refused = SupervisorRules.Judge("workspace:w1", 1, [Rule("quiet", 1, "next", "workspace:w1")], held, world);
        Assert.Equal("moves-unbounded", refused?.Code);

        var line = new SupervisorRuleBook(held).For("build", "daoris", "w1");
        Assert.Equal("next", line[SupervisorRules.Quiet].Single().Do);
    }

    [Fact]
    public void CONTEXTDRIVE1e_the_seeded_rules_are_today_s_numbers_and_none_for_failures_at_0()
    {
        var seeded = SupervisorRuleBook.Seeded(3);
        Assert.Equal(
            ["answers-alone at 2: hold", "failures at 3: hold", "tries at 5: hold", "moves at 3: ask"], seeded.Select(rule => $"{rule.Count} at {rule.At}: {rule.Do}"));
        Assert.All(seeded, rule => Assert.Equal(("machine", null), (rule.Place, rule.Roles)));
        Assert.DoesNotContain(SupervisorRuleBook.Seeded(0), rule => rule.Count == SupervisorRules.Failures);
        Assert.Null(SupervisorRules.Judge("machine", 0, seeded, [], new SupervisorRuleWorld(["build", "chat"])));

        var effective = Book(new SupervisorRuleSet("machine", 1, seeded)).For("build", "daoris", "w1");
        Assert.Equal((2, 3, 5), (SupervisorRuleBook.AnswersAloneAt(effective), SupervisorRuleBook.FailuresAt(effective), SupervisorRuleBook.TriesLimit(effective)));
        Assert.Null(SupervisorRuleBook.Course(null, effective));
    }

    [Fact]
    public void CONTEXTDRIVE1e_an_applied_policy_s_moves_become_rules_naming_its_role_and_read_back_as_the_same_moves()
    {
        var policy = new StaffPolicy(StaffRules.Weight, [], new Dictionary<string, StaffMove>
        {
            [StaffConditions.Transient] = new(StaffActions.Weighted, 2),
            [StaffConditions.Failed] = new(StaffActions.Next, 2),
            [StaffConditions.Limit] = new(StaffActions.Wait, UpTo: 60),
            [StaffConditions.Quiet] = new(StaffActions.Retry),
        });

        var rules = PolicyRules.From(policy, "build", "machine");

        Assert.Equal(
            ["failures-on-choice at 2: next", "limits at 1: wait", "tries at 2: weighted", "moves at 3: ask"],
            rules.Select(rule => $"{rule.Count} at {rule.At}: {rule.Do}"));
        Assert.Equal(60, rules.Single(rule => rule.Count == SupervisorRules.Limits).UpTo);
        Assert.All(rules, rule => Assert.Equal("build", Assert.Single(rule.Roles!)));

        var effective = Book(new SupervisorRuleSet("machine", 1, [.. SupervisorRuleBook.Seeded(3), .. rules])).For("build", null, null);
        var course = SupervisorRuleBook.Course(policy with { Moves = new Dictionary<string, StaffMove>() }, effective)!;
        Assert.Equal(new StaffMove(StaffActions.Weighted, 2), course.Moves[StaffConditions.Transient]);
        Assert.Equal(new StaffMove(StaffActions.Next, 2), course.Moves[StaffConditions.Failed]);
        Assert.Equal(new StaffMove(StaffActions.Wait, UpTo: 60), course.Moves[StaffConditions.Limit]);
        Assert.False(course.Moves.ContainsKey(StaffConditions.Quiet));
        Assert.Equal((3, StaffRules.Weight), (course.Bound, course.Rule));
        Assert.Equal(2, SupervisorRuleBook.TriesLimit(effective));
    }

    /// <summary>The design's per-role inheritance: a Chat line under the machine's rule where its repository holds only a Build rule.</summary>
    [Fact]
    public void CONTEXTDRIVE1e_a_chat_line_reads_the_machine_s_rule_where_its_repository_holds_only_a_build_rule()
    {
        var book = Book(
            new SupervisorRuleSet("machine", 1, SupervisorRuleBook.Seeded(3)),
            new SupervisorRuleSet("repository:daoris", 1, [Rule(SupervisorRules.AnswersAlone, 4, SupervisorRules.Hold, "repository:daoris", ["build"])]));

        Assert.Equal(4, SupervisorRuleBook.AnswersAloneAt(book.For("build", "daoris", null)));
        Assert.Equal(2, SupervisorRuleBook.AnswersAloneAt(book.For("chat", "daoris", null)));
    }

    [Fact]
    public void CONTEXTDRIVE1e_a_count_whose_rule_is_off_holds_nothing_and_a_tries_hold_is_no_move()
    {
        var book = Book(
            new SupervisorRuleSet("machine", 1, SupervisorRuleBook.Seeded(3)),
            new SupervisorRuleSet("repository:daoris", 1, [Rule(SupervisorRules.AnswersAlone, 1, SupervisorRules.Off, "repository:daoris"), Rule(SupervisorRules.Tries, 3, SupervisorRules.Hold, "repository:daoris")]));

        var effective = book.For("build", "daoris", null);
        Assert.Null(SupervisorRuleBook.AnswersAloneAt(effective));
        Assert.Equal(3, SupervisorRuleBook.TriesLimit(effective));
        Assert.Null(SupervisorRuleBook.Course(null, effective));
    }

    [Fact]
    public void CONTEXTDRIVE1l_a_preset_s_limit_move_and_bound_become_rules_naming_the_role_and_the_policy_holds_neither()
    {
        var role = new RoleRecord("build", AuthoredText.Of("Build"), RoleRecord.Always, [RoleWork.Build], RoleScope.Everywhere);
        var choices = new List<ActorChoice> { new("claude", "sonnet", "high"), new("claude", "opus", "high") };

        foreach (var name in new[] { StaffPresets.Even, StaffPresets.Weighted })
        {
            var preset = StaffPolicy.Preset(name, choices, null);
            Assert.Empty(preset.Moves);
            Assert.Null(preset.Most);
            var rules = RuledPolicy.Of(role, new RolePolicyPress(preset, name));
            Assert.Equal(["limits at 1: weighted", "moves at 3: ask"], rules.Select(rule => $"{rule.Count} at {rule.At}: {rule.Do}"));
            Assert.All(rules, rule => Assert.Equal(("machine", "build"), (rule.Place, Assert.Single(rule.Roles!))));
        }

        var order = StaffPolicy.Preset(StaffPresets.First, choices, null);
        Assert.Empty(RuledPolicy.Of(role, new RolePolicyPress(order, StaffPresets.First)));
        Assert.Empty(RuledPolicy.Of(role, new RolePolicyPress(order)));
        Assert.Empty(RuledPolicy.Of(role, new RolePolicyPress(Clear: true)));
    }

    [Fact]
    public void CONTEXTDRIVE1e_a_fresh_home_s_first_start_writes_its_rules_and_a_home_with_a_file_is_left_to_the_conversion()
    {
        Assert.True(SupervisorRuleBook.SeedFresh(_home,() => StoreSettled.Yes));
        Assert.Equal(SupervisorRuleBook.Seeded(3), new SupervisorRuleStore(_home).Of("machine").Rules);
        Assert.False(SupervisorRuleBook.SeedFresh(_home,() => StoreSettled.Yes));
    }

    /// <summary>A fresh home and a migrated one hold one machine set, its moves rule among it, so a first move rule stands.</summary>
    [Fact]
    public void CONTEXTDRIVE1t_one_seed_holds_the_moves_rule_and_a_first_move_rule_stands()
    {
        Assert.Contains(SupervisorRuleBook.Seeded(), rule => rule is { Count: SupervisorRules.Moves, At: 3, Do: SupervisorRules.Ask, Roles: null });
        Assert.True(SupervisorRuleBook.SeedFresh(_home, () => StoreSettled.Yes));
        var store = new SupervisorRuleStore(_home);
        var move = new SupervisorRule("q", SupervisorRules.Quiet, SupervisorRules.Windows[SupervisorRules.Quiet], 1, SupervisorRules.Next, null, "repository:daoris", null);
        Assert.True(store.Write("repository:daoris", 0, [move], "test", SupervisorRuleDoor.World(_home)).Stored);
    }
}
