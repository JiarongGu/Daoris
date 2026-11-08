using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation's line reads the supervisor's rules as a driven line does. Its episode
/// makes its tries rule's number of tries; past them a move rule moves it by weight, counted in <c>moves</c>, the seeded hold ends its turn as
/// always, and the moves rule's bound holds it; its limit's <c>wait</c> and <c>ask</c> keep it on its own account. Each act is kept.
/// </summary>
public sealed class ConversationRulesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "conversation-rules", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 14, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private StaffLedger Ruled(params SupervisorRule[] rules)
    {
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.True(staffing.ImportFile().How is StaffingImported.Done or StaffingImported.Already);
        var store = new SupervisorRuleStore(_home);
        Assert.True(store.Write(
            SupervisorRules.Machine, store.Of(SupervisorRules.Machine).Revision, [.. SupervisorRuleBook.Seeded(3), .. rules],
            SupervisorRuleBook.SeedDoor, SupervisorRuleDoor.World(_home)).Stored);
        return new StaffLedger(_home, () => StoreSettled.Yes);
    }

    private static SupervisorRule Rule(string count, int at, string does, int? upTo = null) =>
        new($"chat-{count}", count, SupervisorRules.Windows[count], at, does, upTo, SupervisorRules.Machine, [RoleWork.Chat]);

    private static PriorSession Chat(string session = "c1") => new(session, "trees/default/engine/c-1", "running")
    {
        Kind = SessionKinds.Chat, Actor = RoleWork.Chat, Adapter = "acp-stub", Profile = "default", Repository = "engine", Said = [],
    };

    private ConversationRules Rules(StaffLedger staff) => new(staff, new SupervisorRuleActs(_home), new FixedClock(Now));

    [Fact]
    public void A_conversation_under_the_seeded_rules_makes_five_tries_and_ends_its_turn_past_them_as_always()
    {
        var rules = Rules(Ruled());

        Assert.Equal(TransientRetry.Bound, rules.Tries(RoleWork.Chat, "engine", null));
        Assert.Null(rules.Judge(Chat(), "c1", StaffConditions.Transient));
    }

    [Fact]
    public void A_tries_rule_at_two_by_weight_makes_two_tries_then_moves_the_conversation_and_counts_the_move()
    {
        var staff = Ruled(Rule(SupervisorRules.Tries, 2, SupervisorRules.Weighted), Rule(SupervisorRules.Moves, 3, SupervisorRules.Ask));
        var rules = Rules(staff);

        Assert.Equal(2, rules.Tries(RoleWork.Chat, "engine", null));
        var ruled = rules.Judge(Chat(), "c1", StaffConditions.Transient);

        Assert.NotNull(ruled);
        Assert.IsType<LineCourse.Move>(ruled.Course);
        Assert.True(rules.Counted(ruled, "c1", StaffConditions.Transient, "acp-stub"));
        Assert.Equal(1, staff.Moves.Of("c1").Moves);
        rules.Said(ruled, "c1", SupervisorRules.Tries, SupervisorRules.Weighted, 2, "episode");
        var kept = Assert.Single(new SupervisorRuleActs(_home).Of("c1"));
        Assert.Equal(SupervisorRules.Tries, kept.Rule.Count);
        Assert.Equal("episode", kept.Point);
    }

    [Fact]
    public void The_moves_rules_bound_holds_a_conversation_its_tries_rule_would_move()
    {
        var staff = Ruled(Rule(SupervisorRules.Tries, 2, SupervisorRules.Weighted), Rule(SupervisorRules.Moves, 1, SupervisorRules.Ask));
        Assert.True(staff.Moves.Record("c1", RoleWork.Chat, "c0", StaffConditions.Transient, SupervisorRules.Weighted, "acp-stub"));

        var ruled = Rules(staff).Judge(Chat(), "c1", StaffConditions.Transient);

        Assert.NotNull(ruled);
        Assert.Equal(CourseHoldWhy.MovesBound, Assert.IsType<LineCourse.Hold>(ruled.Course).Why);
    }

    [Fact]
    public void A_tries_rule_that_asks_holds_the_conversation_for_the_person()
    {
        var ruled = Rules(Ruled(Rule(SupervisorRules.Tries, 3, SupervisorRules.Ask))).Judge(Chat(), "c1", StaffConditions.Transient);

        Assert.NotNull(ruled);
        Assert.Equal(CourseHoldWhy.PolicyAsk, Assert.IsType<LineCourse.Hold>(ruled.Course).Why);
    }

    [Fact]
    public void A_limits_rule_that_waits_keeps_the_conversation_until_its_account_is_back()
    {
        var ruled = Rules(Ruled(Rule(SupervisorRules.Limits, 1, SupervisorRules.Wait, upTo: 60)))
            .Judge(Chat(), "c1", StaffConditions.Limit, until: Now.AddMinutes(30));

        Assert.NotNull(ruled);
        var wait = Assert.IsType<LineCourse.Wait>(ruled.Course);
        Assert.Equal(Now.AddMinutes(30), wait.Until);
        Assert.Equal(CourseWaitWhy.PolicyWait, wait.Why);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    public void A_limits_wait_is_the_courses_answer_whatever_its_upTo_and_the_cap_is_left_to_the_limits_own_reading(int upTo)
    {
        var ruled = Rules(Ruled(Rule(SupervisorRules.Limits, 1, SupervisorRules.Wait, upTo: upTo)))
            .Judge(Chat(), "c1", StaffConditions.Limit, until: Now.AddMinutes(90));

        var wait = Assert.IsType<LineCourse.Wait>(Assert.IsType<ConversationRuled>(ruled).Course);
        Assert.Equal(Now.AddMinutes(90), wait.Until);
    }

    [Fact]
    public void A_limits_rule_that_asks_holds_the_conversation_and_one_that_moves_leaves_the_limit_its_own_walk()
    {
        var asks = Rules(Ruled(Rule(SupervisorRules.Limits, 1, SupervisorRules.Ask))).Judge(Chat(), "c1", StaffConditions.Limit, until: Now.AddHours(2));
        Assert.Equal(CourseHoldWhy.PolicyAsk, Assert.IsType<LineCourse.Hold>(asks!.Course).Why);

        var moves = Rules(Ruled(Rule(SupervisorRules.Limits, 1, SupervisorRules.Next), Rule(SupervisorRules.Moves, 3, SupervisorRules.Ask))).Judge(Chat("c2"), "c2", StaffConditions.Limit, until: Now.AddHours(2));
        Assert.Null(moves);
    }

    [Fact]
    public void A_conversations_retry_line_says_the_tries_its_rule_makes()
    {
        var line = TransientRetry.Retrying("acp-stub", null, Now, tries: 2);

        Assert.Contains("up to 2 times", line.Note);
        Assert.Contains("after 3 minutes", line.Note);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
