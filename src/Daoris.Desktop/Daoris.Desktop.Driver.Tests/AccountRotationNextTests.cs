using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.AccountRotationWalkTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6e (D130 §3–§4 as §16.3 amends them, D125 §3.7): which account the next start of a scope would take, the step of
/// the walk that chose it, and what holds each other account, read without starting anything. Pure: the walk's order with
/// each account's state as the roster finds it before any probe, the agent's accounts on this machine, and what Daoris
/// knows of each, written as <see cref="AccountRotationWalkTests"/> writes them.
/// </summary>
/// <remarks>
/// <para>The reason is the step of the walk that chose the account, weighed as <see cref="AccountRotation.Chose"/> weighs it:
/// against where the list begins, when the start was moved off that account while it was ready; otherwise against the
/// next account that is also ready. An account not ready is a hold, said beside it, never the reason. A scope that names
/// one account, or none, is that account, or the tool's own sign-in, and no step chose it.</para>
/// <para>Holds are written <c>name=hold</c>, <c>own</c> for the tool's own sign-in, in the order the walk tried them, then
/// the kept account a driven start drops, then accounts the list names that this machine does not have, then accounts the
/// scope does not use, by name. Nothing counts accounts: one account and six read by the same rules.</para>
/// </remarks>
public sealed class AccountRotationNextTests
{
    private static readonly DateTimeOffset Until = Now.AddHours(4);

    private static NextStart Next(
        string scope, string facts, string unready = "", StartKind kind = StartKind.Driven, string present = "", string? absent = null)
    {
        var read = Scope(scope);
        var known = Facts(facts);
        var marks = unready.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(mark => mark[..mark.IndexOf('=')], mark => Enum.Parse<AccountReadiness>(mark[(mark.IndexOf('=') + 1)..]));
        var here = read.List.Concat(read.Begins is { } begins ? [begins] : [])
            .Concat(present.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(name => name != absent)
            .Distinct()
            .ToList();
        IEnumerable<string?> order = read.List.Count == 0 ? [read.Begins] : AccountRotation.Order(read, kind, here, known, Now);
        var tried = order.Select(name => State(name ?? "own", marks)).ToList();
        return AccountRotation.Next(read, kind, tried, here, known, Now);
    }

    private static AccountState State(string name, IReadOnlyDictionary<string, AccountReadiness> marks)
    {
        var account = name == "own" ? null : name;
        var readiness = marks.GetValueOrDefault(name, AccountReadiness.Ready);
        return new AccountState(
            account, readiness,
            readiness == AccountReadiness.Cooling ? new CoolingEntry("fake", account, Until, true, "weekly", Now.AddHours(-1), null) : null);
    }

    private static string Holds(NextStart next) =>
        string.Join(' ', next.Others.Select(held => $"{held.Account ?? "own"}={JsonCase(held.Hold.ToString())}"));

    private static string JsonCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    // ——— A scope that names one account, or none (D130 §3.1, D125 §3.7): that account, chosen by no step.

    [Fact]
    public void A_scope_that_names_no_account_runs_on_the_tool_s_own_sign_in_and_uses_none_of_the_agent_s_accounts()
    {
        var next = Next("", "", present: "account-1 account-2");

        Assert.Null(next.Account);
        Assert.Equal(NextReason.Own, next.Reason);
        Assert.Equal("account-1=outside account-2=outside", Holds(next));
    }

    [Theory]
    [InlineData("@account-2", "account-1 account-3", "account-2", "account-1=outside account-3=outside")]
    [InlineData("account-2", "account-1", "account-2", "account-1=outside")]
    public void A_scope_that_names_one_account_takes_it(string scope, string present, string account, string holds)
    {
        var next = Next(scope, "account-2:r3", present: present);

        Assert.Equal(account, next.Account);
        Assert.Equal(NextReason.Named, next.Reason);
        Assert.Equal(holds, Holds(next));
    }

    [Fact]
    public void A_scope_whose_one_account_is_cooling_waits_until_it_is_offered_again()
    {
        var next = Next("@account-2", "", "account-2=Cooling");

        Assert.Null(next.Account);
        Assert.Equal(NextReason.Waits, next.Reason);
        Assert.Equal(Until, next.When);
        var held = Assert.Single(next.Others);
        Assert.Equal(new AccountHeld("account-2", NextHold.Cooling, Until), held);
    }

    [Fact]
    public void The_tool_s_own_sign_in_cooling_makes_the_next_start_wait_for_it()
    {
        var next = Next("", "", "own=Cooling");

        Assert.Equal(NextReason.Waits, next.Reason);
        Assert.Equal(new AccountHeld(null, NextHold.Cooling, Until), Assert.Single(next.Others));
    }

    // ——— A list (D130 §16.3): the first ready account of the walk, and the step that put it ahead of the next ready one.

    public static TheoryData<string, string, string, string, NextReason, string?, string> Choices => new()
    {
        // { scope, facts, not ready, takes, reason, weighed against, holds }
        // Where the list begins, and ahead of the next ready account by the step that told them apart, or by the list.
        { "account-1 account-2 account-3", "", "", "account-1", NextReason.List, "account-2", "account-2=ready account-3=ready" },
        { "account-1 account-2", "account-2:S95", "", "account-1", NextReason.Near, "account-2", "account-2=near" },
        // The owner's case (TOOL6e): the default came out of its cool-off, nothing runs, and the others started since.
        { "account-1 account-2 account-3 @account-1", "account-1:s-50h account-2:s-2h account-3:s-1h", "", "account-1", NextReason.LeastRecent, "account-2", "account-2=ready account-3=ready" },
        // Moved off a ready beginning: the step that put it ahead of where the list begins.
        { "account-1 account-2 account-3", "account-1:r1", "", "account-2", NextReason.Fewest, "account-1", "account-3=ready account-1=ready" },
        { "account-1 account-2 account-3", "account-1:r1 account-2:r1", "", "account-3", NextReason.Fewest, "account-1", "account-1=ready account-2=ready" },
        { "account-1 account-2 account-3", "account-3:w+5h", "", "account-3", NextReason.Lapsing, "account-1", "account-1=ready account-2=ready" },
        { "account-1 account-2 account-3", "account-1:W40@+4d account-2:W10@+4d account-3:W20@+4d", "", "account-2", NextReason.Pace, "account-1", "account-3=ready account-1=ready" },
        { "account-1 account-2 account-3", "account-1:s-1h account-2:s-3h account-3:s-2h", "", "account-2", NextReason.LeastRecent, "account-1", "account-3=ready account-1=ready" },
        { "account-1 account-2 account-3", "account-1:s-1h account-2:s-3h", "", "account-3", NextReason.NotStarted, "account-1", "account-2=ready account-1=ready" },
        { "account-1 account-2 account-3", "account-1:S95", "", "account-2", NextReason.Near, "account-1", "account-3=ready account-1=near" },
        // The account where the list begins held: a hold, and the step among the rest is the reason.
        { "account-1 account-2 account-3", "account-2:r1", "account-1=Cooling", "account-3", NextReason.Fewest, "account-2", "account-1=cooling account-2=ready" },
        { "account-1 account-2 account-3", "", "account-1=Cooling account-2=Refused", "account-3", NextReason.OnlyReady, null, "account-1=cooling account-2=refused" },
        { "account-1 account-2 account-3", "", "account-1=SignedOut", "account-2", NextReason.List, "account-3", "account-1=signedOut account-3=ready" },
        // A driven start drops the kept account: a hold.
        { "account-1 account-2 account-3 keep=account-1", "", "", "account-2", NextReason.List, "account-3", "account-3=ready account-1=kept" },
        { "account-1 account-2 keep=account-1", "", "", "account-2", NextReason.OnlyReady, null, "account-1=kept" },
        // Under `order`, the list's order, and a near account still last.
        { "account-1 account-2 account-3 order", "account-1:r3", "", "account-1", NextReason.List, "account-2", "account-2=ready account-3=ready" },
        { "account-1 account-2 account-3 order", "", "account-1=Cooling", "account-2", NextReason.List, "account-3", "account-1=cooling account-3=ready" },
        { "account-1 account-2 account-3 order", "account-1:S95", "", "account-2", NextReason.Near, "account-1", "account-3=ready account-1=near" },
        // A list of one is the one account it names.
        { "account-1", "account-1:r5", "", "account-1", NextReason.Named, null, "" },
    };

    [Theory]
    [MemberData(nameof(Choices))]
    public void The_next_start_takes_the_first_ready_account_for_the_step_that_put_it_ahead(
        string scope, string facts, string unready, string takes, NextReason reason, string? over, string holds)
    {
        var next = Next(scope, facts, unready);

        Assert.Equal(takes, next.Account);
        Assert.Equal(reason, next.Reason);
        Assert.Equal(over, next.Over);
        Assert.Equal(holds, Holds(next));
    }

    [Fact]
    public void A_week_lapsing_names_when_it_resets()
    {
        var next = Next("account-1 account-2 account-3", "account-3:w+5h");

        Assert.Equal(NextReason.Lapsing, next.Reason);
        Assert.Equal(Now.AddHours(5), next.When);
    }

    [Fact]
    public void Every_account_of_the_list_held_makes_the_next_start_wait_for_the_first_reset()
    {
        var next = Next("account-1 account-2 account-3 keep=account-3", "", "account-1=Cooling account-2=Refused");

        Assert.Null(next.Account);
        Assert.Equal(NextReason.Waits, next.Reason);
        Assert.Equal(Until, next.When);
        Assert.Equal("account-1=cooling account-2=refused account-3=kept", Holds(next));
    }

    [Fact]
    public void Held_for_a_person_alone_the_next_start_waits_with_no_time()
    {
        var next = Next("account-1 account-2", "", "account-1=Refused account-2=SignedOut");

        Assert.Equal(NextReason.Waits, next.Reason);
        Assert.Null(next.When);
    }

    [Fact]
    public void Accounts_the_scope_does_not_use_and_accounts_the_machine_does_not_have_are_said_as_such()
    {
        var next = Next("account-1 account-2 account-3", "", present: "account-5 account-4", absent: "account-2");

        Assert.Equal("account-1", next.Account);
        Assert.Equal("account-3=ready account-2=missing account-4=outside account-5=outside", Holds(next));
    }

    // ——— A conversation (§4.6): the kept account last, or first where it is the default.

    [Fact]
    public void A_conversation_takes_the_kept_account_where_it_is_the_default_and_says_it_is_kept()
    {
        var next = Next("account-1 account-2 account-3 @account-2 keep=account-2", "account-2:r3", kind: StartKind.Conversation);

        Assert.Equal("account-2", next.Account);
        Assert.Equal(NextReason.Kept, next.Reason);
    }

    [Fact]
    public void A_conversation_passes_the_kept_account_to_the_last_and_says_so_where_only_it_was_left()
    {
        var next = Next("account-1 account-2 keep=account-1", "account-2:r3", kind: StartKind.Conversation);

        Assert.Equal("account-2", next.Account);
        Assert.Equal(NextReason.Kept, next.Reason);
        Assert.Equal("account-1", next.Over);
    }

    // ——— One judgement: the reason is the step the start's own first line names (`Chose`), where the account it moved
    // off was ready, or its step among the rest where that account was held; a hold is never the reason.

    [Theory]
    [MemberData(nameof(AccountRotationWalkTests.Choices), MemberType = typeof(AccountRotationWalkTests))]
    public void The_reason_is_the_step_the_start_s_first_line_names(
        string scope, string facts, StartKind kind, string unready, string ranOn, WalkStep step, WalkStep? rest)
    {
        var read = Scope(scope);
        var known = Facts(facts);
        var next = Next(scope, facts, unready, kind);

        Assert.Equal(ranOn, next.Account);
        var held = step is WalkStep.Cooling or WalkStep.Refused or WalkStep.SignedOut
            || (step == WalkStep.Kept && !(kind == StartKind.Conversation && read.Use.Keep == ranOn));
        var expected = read.List.Count == 1 ? NextReason.Named
            : !held ? Reason(step, ranOn, known)
            : rest is { } among ? Reason(among, ranOn, known)
            : next.Others.Any(other => other.Hold is NextHold.Ready or NextHold.Near) ? NextReason.List
            : NextReason.OnlyReady;
        Assert.Equal(expected, next.Reason);
    }

    private static NextReason Reason(WalkStep step, string ran, IReadOnlyDictionary<string, AccountFacts> facts) => step switch
    {
        WalkStep.Kept => NextReason.Kept,
        WalkStep.Near => NextReason.Near,
        WalkStep.Fewest => NextReason.Fewest,
        WalkStep.Lapsing => NextReason.Lapsing,
        WalkStep.Pace => NextReason.Pace,
        WalkStep.LeastRecent => facts.GetValueOrDefault(ran) is { LastStarted: not null } or { Chosen: not null }
            ? NextReason.LeastRecent
            : NextReason.NotStarted,
        _ => NextReason.List,
    };

    // ——— Nothing counts accounts (D130 §2 rule 6).

    [Fact]
    public void Six_accounts_read_by_the_same_rules()
    {
        var next = Next(
            "account-1 account-2 account-3 account-4 account-5 account-6",
            "account-1:r1 account-2:r1 account-3:r1 account-4:r1 account-6:r1",
            "account-5=Cooling");

        Assert.Equal("account-1", next.Account);
        Assert.Equal(NextReason.List, next.Reason);
        Assert.Equal("account-2", next.Over);
        Assert.Equal("account-5=cooling account-2=ready account-3=ready account-4=ready account-6=ready", Holds(next));
    }
}
