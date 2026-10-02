using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6b (D130 §16.3, §16.4, §7): the walk toward the goal, as tables per step. Pure: a scope as the file holds it, what
/// Daoris knows of each account without the agent's word (its sessions running, when Daoris last started on it, the
/// starts this look chose, and a weekly reset a limit told), and the order the start tries its accounts in. Then which
/// step chose the account a start ran on: the log's <c>why</c> and the first line's clause.
/// </summary>
/// <remarks>
/// <para>A scope is written as its list, <c>@name</c> for its default, <c>keep=name</c>, and <c>order</c> for D125's
/// walk; it is read through the file's own reader (<see cref="HarnessSettings.ResolveScope"/>), so a default outside its
/// list is read with the list winning, as a start reads it. What Daoris knows is <c>name:flags</c>, each flag one of
/// <c>r&lt;n&gt;</c> sessions running, <c>s&lt;offset&gt;</c> last started that long from now, <c>c&lt;n&gt;</c> chosen
/// by this look as its n-th start, and <c>w&lt;offset&gt;</c> its week resets that long from now.</para>
/// <para>Steps 2 and 5, near and pace, need the agent's word, which no door of Daoris's carries yet (§16.4): TOOL6c adds
/// them. Nothing here counts accounts: rows with one account and with six read by the same rules (§2 rule 6).</para>
/// </remarks>
public sealed class AccountRotationWalkTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static RotationScope Scope(string written)
    {
        var tokens = written.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var list = tokens.Where(token => !token.StartsWith('@') && !token.StartsWith("keep=", StringComparison.Ordinal)
                                         && token is not ("order" or "goal")).ToList();
        var settings = new HarnessSettings().WithRotation("fake", list);
        if (tokens.FirstOrDefault(token => token.StartsWith('@')) is { } named) settings = settings.WithDefault("fake", named[1..]);
        var keep = tokens.FirstOrDefault(token => token.StartsWith("keep=", StringComparison.Ordinal))?["keep=".Length..];
        var use = tokens.Contains("order") ? "order" : null;
        if (keep is not null || use is not null) settings = settings.WithUse("fake", new UseChange(Use: use, Keep: keep));
        return settings.ResolveScope("fake", null);
    }

    private static Dictionary<string, AccountFacts> Facts(string written)
    {
        var facts = new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in written.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, flags) = (entry[..entry.IndexOf(':')], entry[(entry.IndexOf(':') + 1)..].Split(','));
            var fact = new AccountFacts();
            foreach (var flag in flags)
            {
                fact = flag[0] switch
                {
                    'r' => fact with { Running = int.Parse(flag[1..]) },
                    's' => fact with { LastStarted = Now + Offset(flag[1..]) },
                    'c' => fact with { Chosen = long.Parse(flag[1..]) },
                    'w' => fact with { WeekResets = Now + Offset(flag[1..]) },
                    _ => throw new ArgumentException($"no flag `{flag}`"),
                };
            }

            facts[name] = fact;
        }

        return facts;
    }

    /// <summary><c>+20h</c>, <c>-30m</c>, <c>+6d</c>.</summary>
    private static TimeSpan Offset(string written)
    {
        var amount = double.Parse(written[..^1], System.Globalization.CultureInfo.InvariantCulture);
        return written[^1] switch
        {
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            _ => throw new ArgumentException($"no unit in `{written}`"),
        };
    }

    private static string[] Names(RotationScope scope) => [.. scope.List];

    private static string Order(string scope, string facts, StartKind kind = StartKind.Driven, string? absent = null)
    {
        var read = Scope(scope);
        var present = Names(read).Where(name => name != absent).ToList();
        return string.Join(' ', AccountRotation.Order(read, kind, present, Facts(facts), Now));
    }

    // ——— Step 1, keep (§4.6, §16.3): a driven start drops the kept account; a conversation takes it last, or first where
    // it is also the scope's default.

    [Theory]
    [InlineData("account-1 account-2 account-3 keep=account-3", "", "account-1 account-2")]
    [InlineData("account-1 account-2 account-3 keep=account-1", "account-2:r1 account-3:r1", "account-2 account-3")]
    [InlineData("account-1 account-2 account-3 keep=account-2 order", "", "account-1 account-3")]
    public void A_driven_start_drops_the_kept_account(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    [Theory]
    // Last, whatever it would rank by the steps after it.
    [InlineData("account-1 account-2 account-3 keep=account-1", "", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 keep=account-3", "account-1:r2 account-2:r2", "account-1 account-2 account-3")]
    // First where it is also the scope's default: the way to say conversations run here.
    [InlineData("account-1 account-2 account-3 @account-2 keep=account-2", "account-2:r3", "account-2 account-3 account-1")]
    // In its place under `order` (§4.6): reached only when the accounts before it are not ready.
    [InlineData("account-1 account-2 account-3 keep=account-2 order", "", "account-1 account-2 account-3")]
    // No kept account: a conversation walks as a driven start does.
    [InlineData("account-1 account-2 account-3", "account-1:r1", "account-2 account-3 account-1")]
    public void A_conversation_takes_the_kept_account_last_or_first_where_it_is_the_default(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts, StartKind.Conversation));
    }

    [Fact]
    public void A_kept_account_that_would_leave_driven_work_none_is_read_as_none()
    {
        // A door refuses it (§4.6); a file edited by hand that holds it is read with the list winning, as a default is.
        var scope = new HarnessSettings().WithRotation("fake", ["account-1"]).WithUse("fake", new UseChange(Keep: "account-1"))
            .ResolveScope("fake", null);

        Assert.Equal(["account-1"], AccountRotation.Order(scope, StartKind.Driven, ["account-1"], Facts(""), Now));
    }

    // ——— Step 3, fewest running (§4.2, §7): fewest of Daoris's sessions on it now, this look's starts counted.

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-1:r1", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:r2 account-2:r1 account-3:r1", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-2:r1 account-3:r1", "account-1 account-2 account-3")]
    // It outranks a week lapsing: the five-hour window is kept below its limit by spreading.
    [InlineData("account-1 account-2 account-3", "account-3:r1,w+5h", "account-1 account-2 account-3")]
    // Six accounts, one free: nothing counts them.
    [InlineData("account-1 account-2 account-3 account-4 account-5 account-6",
        "account-1:r1 account-2:r1 account-3:r1 account-4:r1 account-5:r1", "account-6 account-1 account-2 account-3 account-4 account-5")]
    public void Fewest_running_first(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // ——— Step 4, its week lapsing (§16.3): a known weekly reset within the next day first, sooner first.

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-3:w+20h", "account-3 account-1 account-2")]
    [InlineData("account-1 account-2 account-3", "account-2:w+20h account-3:w+5h", "account-3 account-2 account-1")]
    [InlineData("account-1 account-2 account-3", "account-3:w+24h", "account-3 account-1 account-2")]
    // Beyond a day it ranks nothing: ranked all week, one account's work would pile onto it while the others' weeks went by.
    [InlineData("account-1 account-2 account-3", "account-3:w+25h", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3", "account-1:w+6d account-2:w+3d", "account-1 account-2 account-3")]
    // It outranks least recently started.
    [InlineData("account-1 account-2 account-3", "account-3:s-1m,w+10h", "account-3 account-1 account-2")]
    public void A_week_lapsing_within_the_day_first(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // ——— Step 6, least recently started (§16.2): never started first, then longest ago; a start this look chose is the
    // most recent of all, in the order it was chosen.

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-1:s-1h account-2:s-3h", "account-3 account-2 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:c1", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:c1 account-2:c2", "account-3 account-1 account-2")]
    [InlineData("account-1 account-2 account-3", "account-2:s-1m account-3:c1", "account-1 account-2 account-3")]
    public void Least_recently_started_first(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // ——— Step 7, list order (§16.3): begun at the scope's default, wrapping; the person's preference has the last word.

    [Theory]
    [InlineData("account-1 account-2 account-3", "", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3 @account-2", "", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 @account-9", "", "account-1 account-2 account-3")]
    [InlineData("account-1", "account-1:r5", "account-1")]
    public void List_order_begun_at_the_default_has_the_last_word(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    [Fact]
    public void An_account_the_machine_does_not_have_is_never_walked_to_but_the_scope_s_own_beginning_is_tried()
    {
        Assert.Equal("account-1 account-3", Order("account-1 account-2 account-3", "", absent: "account-2"));
        // Where it begins is tried as D125 tried a default, so a start pointed at it is refused by the sign-in's own sentence.
        Assert.Equal("account-1 account-2", Order("account-1 account-2", "", absent: "account-1"));
    }

    // ——— `use: order` (D125's walk, exactly): the list begun at its default, whatever Daoris knows.

    [Theory]
    [InlineData("account-1 account-2 account-3 order", "account-1:r3 account-3:w+5h", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3 @account-2 order", "account-2:r1,s-1m", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 @account-3 order", "", "account-3 account-1 account-2")]
    public void Order_keeps_D125_s_walk(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // ——— §7: while every account is ready, no account runs more than ⌈K ÷ N⌉ at once.

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void While_every_account_is_ready_none_runs_more_than_its_share_of_the_cap(int accounts)
    {
        var names = Enumerable.Range(1, accounts).Select(n => $"account-{n}").ToList();
        var scope = new HarnessSettings().WithRotation("fake", names).ResolveScope("fake", null);

        for (var cap = 1; cap <= 9; cap++)
        {
            var facts = new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);
            for (var start = 1; start <= cap; start++)
            {
                var chosen = AccountRotation.Order(scope, StartKind.Driven, names, facts, Now)[0];
                var held = facts.GetValueOrDefault(chosen) ?? new AccountFacts();
                facts[chosen] = held with { Running = held.Running + 1, Chosen = start };
            }

            var share = (cap + accounts - 1) / accounts;
            Assert.All(names, name => Assert.InRange(facts.GetValueOrDefault(name)?.Running ?? 0, cap / accounts, share));
        }
    }

    // ——— Which step chose the account (§13 as §16 amends it, §16.4): against where the scope begins when the start ran
    // elsewhere, else against the account it would have run on next.

    public static TheoryData<string, string, StartKind, string, string, WalkStep, WalkStep?> Choices => new()
    {
        // { scope, facts, kind, not ready (name=readiness), ran on, step, of the rest }
        { "account-1 account-2 account-3", "", StartKind.Driven, "", "account-1", WalkStep.List, null },
        { "account-1 account-2 account-3 @account-2", "", StartKind.Driven, "", "account-2", WalkStep.List, null },
        { "account-1 account-2 account-3", "account-1:r1", StartKind.Driven, "", "account-2", WalkStep.Fewest, null },
        { "account-1 account-2 account-3", "account-1:r1 account-2:r1", StartKind.Driven, "", "account-3", WalkStep.Fewest, null },
        { "account-1 account-2 account-3", "account-2:r1", StartKind.Driven, "", "account-1", WalkStep.List, null },
        { "account-1 account-2 account-3", "account-2:r1 account-3:r1", StartKind.Driven, "", "account-1", WalkStep.Fewest, null },
        { "account-1 account-2 account-3", "account-3:w+5h", StartKind.Driven, "", "account-3", WalkStep.Lapsing, null },
        { "account-1 account-2 account-3", "account-1:w+5h", StartKind.Driven, "", "account-1", WalkStep.Lapsing, null },
        { "account-1 account-2 account-3", "account-1:s-1h", StartKind.Driven, "", "account-2", WalkStep.LeastRecent, null },
        { "account-1 account-2 account-3", "account-1:c1", StartKind.Driven, "", "account-2", WalkStep.LeastRecent, null },
        { "account-1 account-2 account-3", "", StartKind.Driven, "account-1=Cooling", "account-2", WalkStep.Cooling, null },
        { "account-1 account-2 account-3", "account-2:r1", StartKind.Driven, "account-1=Cooling", "account-3", WalkStep.Cooling, WalkStep.Fewest },
        { "account-1 account-2 account-3", "", StartKind.Driven, "account-1=Cooling account-2=Cooling", "account-3", WalkStep.Cooling, null },
        { "account-1 account-2 account-3", "", StartKind.Driven, "account-1=Refused", "account-2", WalkStep.Refused, null },
        { "account-1 account-2 account-3", "", StartKind.Driven, "account-1=SignedOut", "account-2", WalkStep.SignedOut, null },
        { "account-1 account-2 account-3 keep=account-1", "", StartKind.Driven, "", "account-2", WalkStep.Kept, null },
        { "account-1 account-2 account-3 keep=account-1", "account-2:r1", StartKind.Driven, "", "account-3", WalkStep.Kept, WalkStep.Fewest },
        { "account-1 account-2 account-3 keep=account-1", "", StartKind.Conversation, "", "account-2", WalkStep.Kept, null },
        { "account-1 account-2 account-3 @account-2 keep=account-2", "", StartKind.Conversation, "", "account-2", WalkStep.Kept, null },
        { "account-1 account-2 account-3 order", "account-2:r1", StartKind.Driven, "account-1=Cooling", "account-2", WalkStep.Cooling, null },
        { "account-1 account-2 account-3 order", "account-1:r1", StartKind.Driven, "", "account-1", WalkStep.List, null },
        { "account-1", "", StartKind.Driven, "", "account-1", WalkStep.List, null },
    };

    [Theory]
    [MemberData(nameof(Choices))]
    public void The_step_that_chose_the_account(
        string scope, string facts, StartKind kind, string unready, string ranOn, WalkStep step, WalkStep? rest)
    {
        var read = Scope(scope);
        var known = Facts(facts);
        var marks = unready.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(mark => mark[..mark.IndexOf('=')], mark => Enum.Parse<AccountReadiness>(mark[(mark.IndexOf('=') + 1)..]));
        var tried = AccountRotation.Order(read, kind, Names(read), known, Now)
            .Select(name => new AccountState(name, marks.GetValueOrDefault(name, AccountReadiness.Ready)))
            .ToList();
        var at = tried.FindIndex(state => state.IsReady);

        Assert.Equal(ranOn, tried[at].Account);
        Assert.Equal(new WalkChoice(step, rest), AccountRotation.Chose(read, kind, tried, at, known, Now));
    }
}
