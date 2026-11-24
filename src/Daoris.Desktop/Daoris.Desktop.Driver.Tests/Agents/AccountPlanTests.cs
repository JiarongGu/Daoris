using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account's plan: read from its agent's own word (a status's or a usage reading's, never a credential), kept beside its windows,
/// and weighed by the walk as room in the plan's own units.
/// </summary>
public sealed class AccountPlanTests : IDisposable
{
    private static readonly DateTimeOffset Now = AccountRotationWalkTests.Now;

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plan-" + Guid.NewGuid().ToString("N")[..8]);

    public AccountPlanTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ——— What an agent says of the plan.

    // `claude auth status --json`, measured on 2.1.331 on three signed-in accounts (fields kept to what is read here).
    private static string Status(string type, string tier) =>
        $$"""{"loggedIn":true,"authMethod":"claude.ai","email":"person@example.invalid","orgName":"Org","subscriptionType":"{{type}}","planTier":"{{tier}}"}""";

    [Theory]
    [InlineData("max", "max_20x", "max_20x")]
    [InlineData("max", "max_5x", "max_5x")]
    [InlineData("team", "other", "team")]
    public void Claude_s_status_names_a_max_plan_by_its_tier_and_any_other_by_its_type(string type, string tier, string plan)
    {
        var read = ClaudeCodeProvider.Login.Read(Status(type, tier));

        Assert.Equal((LoginState.In, plan), (read.Login, read.Plan));
    }

    [Fact]
    public void A_status_that_names_no_tier_leaves_the_type_as_the_plan()
    {
        var read = ClaudeCodeProvider.Login.Read("""{"loggedIn":true,"email":"person@example.invalid","subscriptionType":"pro"}""");

        Assert.Equal("pro", read.Plan);
    }

    [Fact]
    public void Claude_declares_a_size_for_the_plans_whose_word_states_one_and_none_for_the_rest()
    {
        var sizes = ClaudePlans.Sizes;

        Assert.Equal([1d, 5d, 20d], [sizes["pro"], sizes["max_5x"], sizes["MAX_20X"]]);
        Assert.DoesNotContain("max", sizes.Keys);
        Assert.DoesNotContain("team", sizes.Keys);
    }

    // ——— What is kept.

    [Fact]
    public void A_plan_is_kept_beside_the_windows_and_a_type_never_replaces_the_tier_it_names_more_exactly()
    {
        AccountWindows.Planned(_home, "claude-code", "account-1", "max_20x");
        AccountWindows.Planned(_home, "claude-code", "account-1", "max");
        Assert.Equal("max_20x", AccountWindows.PlanOf(_home, "claude-code", "account-1"));

        // Another plan is the plan, whatever is kept.
        AccountWindows.Planned(_home, "claude-code", "account-1", "team");
        Assert.Equal("team", AccountWindows.PlanOf(_home, "claude-code", "account-1"));
        AccountWindows.Planned(_home, "claude-code", "account-1", "max_5x");
        Assert.Equal("max_5x", AccountWindows.PlanOf(_home, "claude-code", "account-1"));
        Assert.Null(AccountWindows.PlanOf(_home, "claude-code", "account-2"));
    }

    [Theory]
    [InlineData("someone@example.com")]
    [InlineData("Acme Holdings")]
    [InlineData("")]
    public void What_is_no_plain_word_is_no_plan_s_word_and_is_kept_nowhere(string said)
    {
        AccountWindows.Planned(_home, "claude-code", "account-1", said);

        Assert.Null(AccountWindows.PlanOf(_home, "claude-code", "account-1"));
        Assert.False(File.Exists(AccountWindows.PathOf(_home)));
    }

    [Fact]
    public void A_plan_beside_the_windows_is_no_window_and_changes_none()
    {
        var reading = new WindowReading("weekly", 0.4, Now.AddDays(3));
        AccountWindows.Said(_home, "claude-code", "account-1", [reading], Now, session: null);
        AccountWindows.Planned(_home, "claude-code", "account-1", "max_20x");

        Assert.Equal(["weekly"], AccountWindows.SaidOf(_home, "claude-code", "account-1", Now)!.Windows.Select(each => each.Window));
        Assert.Equal("max_20x", AccountWindows.PlanOf(_home, "claude-code", "account-1"));
    }

    // ——— What the walk does with it.

    private static WindowSaid Week(double used) =>
        new("weekly", used, Now.AddDays(6), Standing: null, Credits: false, Seen: Now.AddMinutes(-10), Session: "s0");

    private static AccountFacts Account(double used, double? size = null) =>
        new(Said: new AccountSaid([Week(used)]), Size: size);

    private static string Order(Dictionary<string, AccountFacts> facts)
    {
        var scope = AccountRotationWalkTests.Scope("account-1 account-2");
        return string.Join(' ', AccountRotation.Order(scope, StartKind.Driven, [.. scope.List], facts, Now));
    }

    [Fact]
    public void Two_accounts_at_the_same_share_go_by_the_room_each_plan_leaves_the_larger_first()
    {
        var facts = new Dictionary<string, AccountFacts> { ["account-1"] = Account(0.10, size: 5), ["account-2"] = Account(0.10, size: 20) };

        Assert.Equal("account-2 account-1", Order(facts));
        Assert.Equal(WalkStep.Headroom, AccountRotation.Rank("account-1", "account-2", facts, Now, AccountRotationWalkTests.Scope("account-1 account-2").Use).Step);
    }

    [Fact]
    public void Where_no_account_has_a_size_the_walk_is_as_it_was_and_an_unsized_account_ranks_as_the_base_plan()
    {
        Assert.Equal("account-1 account-2", Order(new() { ["account-1"] = Account(0.10), ["account-2"] = Account(0.10) }));
        // The same size on both scales both rooms alike.
        Assert.Equal("account-1 account-2", Order(new() { ["account-1"] = Account(0.10, size: 5), ["account-2"] = Account(0.10, size: 5) }));
        // One sized, one not: the unsized is the base (size 1), so five times the base has the room.
        Assert.Equal("account-2 account-1", Order(new() { ["account-1"] = Account(0.10), ["account-2"] = Account(0.10, size: 5) }));
    }

    [Fact]
    public void A_larger_plan_with_less_of_its_share_left_can_still_have_more_room_than_a_smaller_one()
    {
        // The smaller plan has 29 points of room under the curve, the larger 4: twenty times four is more than one times 29.
        var facts = new Dictionary<string, AccountFacts> { ["account-1"] = Account(0.05, size: 1), ["account-2"] = Account(0.30, size: 20) };

        Assert.Equal("account-2 account-1", Order(facts));
        Assert.Equal("account-1 account-2", Order(new() { ["account-1"] = Account(0.05), ["account-2"] = Account(0.30) }));
    }

    // ——— Through the roster.

    private HarnessRoster Doors() =>
        new(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Now };

    [Fact]
    public void A_roster_with_two_accounts_at_the_same_share_and_different_plans_starts_on_the_one_with_the_room()
    {
        var roster = Doors();
        var frame = System.Text.Json.JsonDocument.Parse(AccountReadingsTests.Placed("""
            {"status":"allowed","resetsAt":<+2 h>,"rateLimitType":"five_hour",
             "unifiedWindows":{"five_hour":{"utilization":0.2,"resetsAt":<+2 h>},"seven_day":{"utilization":0.1,"resetsAt":<+144 h>}}}
            """, Now)).RootElement.Clone();
        roster.Said("claude-code-acp", "account-1", frame, "s1");
        roster.Said("claude-code-acp", "account-2", frame, "s2");
        AccountWindows.Planned(_home, "claude-code", "account-1", "max_5x");
        AccountWindows.Planned(_home, "claude-code", "account-2", "max_20x");

        var facts = roster.Facts("claude-code", ["account-1", "account-2"], Now);

        Assert.Equal(("max_5x", 5d), (facts["account-1"].Plan, facts["account-1"].Size));
        Assert.Equal(("max_20x", 20d), (facts["account-2"].Plan, facts["account-2"].Size));
        Assert.Equal("account-2 account-1", Order(facts));
    }

    [Fact]
    public void A_plan_the_agent_declares_no_size_for_is_shown_and_weighs_as_the_base()
    {
        AccountWindows.Planned(_home, "claude-code", "account-1", "team");
        AccountWindows.Planned(_home, "codex", "account-1", "team");
        var roster = Doors();

        var facts = roster.Facts("claude-code", ["account-1"], Now);

        Assert.Equal(("team", (double?)null, 1d), (facts["account-1"].Plan, facts["account-1"].Size, facts["account-1"].Units));
        Assert.Null(roster.PlanSizeOf("codex", "team"));
        Assert.Null(roster.PlanSizeOf("claude-code", null));
    }
}
