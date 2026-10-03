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
    internal static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    internal static RotationScope Scope(string written)
    {
        var tokens = written.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var list = tokens.Where(token => !token.StartsWith('@') && !token.Contains('=') && token is not ("order" or "goal")).ToList();
        var settings = new HarnessSettings().WithRotation("fake", list);
        if (tokens.FirstOrDefault(token => token.StartsWith('@')) is { } named) settings = settings.WithDefault("fake", named[1..]);
        string? Setting(string name) => tokens.FirstOrDefault(token => token.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..];
        var keep = Setting("keep");
        var use = tokens.Contains("order") ? "order" : null;
        var early = Setting("early") is { } switched ? switched == "on" : (bool?)null;
        var near = Setting("near") is { } percent ? int.Parse(percent) : (int?)null;
        if (keep is not null || use is not null || early is not null || near is not null)
        {
            settings = settings.WithUse("fake", new UseChange(Use: use, Keep: keep, Early: early, Near: near));
        }

        return settings.ResolveScope("fake", null);
    }

    internal static Dictionary<string, AccountFacts> Facts(string written)
    {
        var facts = new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in written.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, flags) = (entry[..entry.IndexOf(':')], entry[(entry.IndexOf(':') + 1)..].Split(','));
            var fact = new AccountFacts();
            var windows = new List<WindowSaid>();
            foreach (var flag in flags)
            {
                fact = flag[0] switch
                {
                    'r' => fact with { Running = int.Parse(flag[1..]) },
                    's' => fact with { LastStarted = Now + Offset(flag[1..]) },
                    'c' => fact with { Chosen = long.Parse(flag[1..]) },
                    'w' => fact with { WeekResets = Now + Offset(flag[1..]) },
                    'S' or 'W' or 'N' or 'X' or 'C' => fact,
                    _ => throw new ArgumentException($"no flag `{flag}`"),
                };

                // What its agent said (TOOL6c): the session window's use, the week's use and reset, a word, credits.
                switch (flag[0])
                {
                    case 'S':
                        windows.Add(Window("session", Percent(flag[1..]), Now.AddHours(3)));
                        break;
                    case 'W':
                        var (used, reset) = (flag[1..flag.IndexOf('@')], flag[(flag.IndexOf('@') + 1)..]);
                        windows.Add(Window("weekly", Percent(used), Now + Offset(reset)));
                        break;
                    case 'N':
                        Stand("session", "near");
                        break;
                    case 'X':
                        Stand("weekly", "refused");
                        break;
                    case 'C':
                        var first = windows.Count > 0 ? windows[0] : Window("session", null, Now.AddHours(3));
                        windows.Remove(first);
                        windows.Insert(0, first with { Credits = true });
                        break;
                }
            }

            facts[name] = windows.Count == 0 ? fact : fact with { Said = new AccountSaid(windows) };

            void Stand(string window, string standing)
            {
                var at = windows.FindIndex(each => each.Window == window);
                if (at < 0) windows.Add(Window(window, null, Now.AddHours(window == "session" ? 3 : 48)) with { Standing = standing });
                else windows[at] = windows[at] with { Standing = standing };
            }
        }

        return facts;
    }

    private static double Percent(string written) => double.Parse(written, System.Globalization.CultureInfo.InvariantCulture) / 100;

    private static WindowSaid Window(string window, double? used, DateTimeOffset reset) =>
        new(window, used, reset, Standing: null, Credits: false, Seen: Now.AddMinutes(-10), Session: "s0");

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

    // ——— Step 2, near last (TOOL6c; §6, §16.3 as the evidence corrects them): an account its agent said is near any
    // window's limit — by its word, by drawing on usage credits, or by a window's use at or over the scope's *near* — goes
    // to the end: passed while another account is ready, run when none is. `S<n>` is the session window's use in percent,
    // `W<n>@<offset>` the week's and its reset, `N` its warning word, `X` its word that a limit was reached, `C` credits.

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-1:S95", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:S88", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3", "account-1:S90", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 near=85", "account-1:S88", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:S30,W95@+3d", "account-2 account-3 account-1")]
    // By its word, whatever the number; by credits; by a limit its word says was reached.
    [InlineData("account-1 account-2 account-3", "account-1:S10,N", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:S10,C", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-2:W40@+2d,X", "account-1 account-3 account-2")]
    // It outranks fewest running: a near account is passed whatever runs on the others.
    [InlineData("account-1 account-2 account-3", "account-1:S95 account-2:r2 account-3:r1", "account-3 account-2 account-1")]
    // Every account near: the steps after it order them, and a start still runs. A pass, never a wait.
    [InlineData("account-1 account-2", "account-1:S95 account-2:S96", "account-1 account-2")]
    [InlineData("account-1 account-2", "account-1:S95,r1 account-2:S96", "account-2 account-1")]
    // The switch off passes nothing; under `order` a near account goes last too, unless the switch is off.
    [InlineData("account-1 account-2 account-3 early=off", "account-1:S95", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3 order", "account-1:S95", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 order early=off", "account-1:S95", "account-1 account-2 account-3")]
    public void An_account_said_to_be_near_goes_last(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    [Fact]
    public void A_conversation_under_order_keeps_its_kept_account_in_place_and_still_passes_a_near_one()
    {
        Assert.Equal(
            "account-2 account-3 account-1",
            Order("account-1 account-2 account-3 keep=account-2 order", "account-1:S95", StartKind.Conversation));
    }

    // ——— Step 5, furthest behind its week's pace (TOOL6c; §16.3): the share of its week gone less the share it said is
    // used, the furthest behind first; an account ahead of pace after every account that is not; one that said nothing
    // between the two. With four days to its reset, three sevenths of a week are gone.

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-1:W40@+4d account-2:W10@+4d account-3:W20@+4d", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:W80@+4d account-3:W10@+4d", "account-3 account-2 account-1")]
    // Unknown sits between clear and near: said clear and behind, said nothing, said near.
    [InlineData("account-1 account-2 account-3", "account-1:S10,N account-3:W10@+4d", "account-3 account-2 account-1")]
    // Below a week lapsing within the day; above least recently started.
    [InlineData("account-1 account-2 account-3", "account-1:W89@+20h,w+20h account-2:W10@+4d", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2", "account-1:W10@+4d,s-1m account-2:s-5h", "account-1 account-2")]
    // Below fewest running.
    [InlineData("account-1 account-2", "account-1:W5@+4d,r1 account-2:W60@+4d", "account-2 account-1")]
    // Under `order` pace ranks nothing.
    [InlineData("account-1 account-2 order", "account-1:W85@+4d account-2:W10@+4d", "account-1 account-2")]
    public void Furthest_behind_its_week_s_pace_first(string scope, string facts, string order)
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

    // ——— What each account said, as a start's first line says it (TOOL6c, §16.4): one sentence in the list's order, each
    // account with its age and what it said; one that said nothing is unknown, said as such; none said is no sentence.

    [Fact]
    public void What_each_account_said_is_one_sentence_in_the_list_s_order_and_an_account_that_said_nothing_is_unknown()
    {
        var facts = Facts("account-1:S95,W30@+4d account-3:N,C");
        facts["account-4"] = new AccountFacts(Said: new AccountSaid([new WindowSaid("session", null, Now.AddHours(3), "clear", false, Now.AddHours(-3), "s0")]));

        Assert.Equal(
            "What each account said: `account-1` 10 min ago, 95% of its session limit and 30% of its weekly limit used, near at 90%; "
            + "`account-2` nothing yet; `account-3` 10 min ago, near its session limit, by its own word, drawing on usage credits; "
            + "`account-4` 3 h ago, clear, by its own word.",
            RotationWords.Said(["account-1", "account-2", "account-3", "account-4"], facts, RotationUse.Default, Now));
        Assert.Null(RotationWords.Said(["account-1", "account-2"], Facts("account-1:r2"), RotationUse.Default, Now));
    }

    // ——— Steps 2 and 5 name the account they are about (TOOL6c): near names the account passed, and pace the one it was
    // weighed against, so the first line can say what that account said.

    public static TheoryData<string, string, string, string, WalkStep, string?, WalkStep?, string?> SaidChoices => new()
    {
        // { scope, facts, not ready, ran on, step, about, of the rest, about }
        { "account-1 account-2 account-3", "account-1:S95", "", "account-2", WalkStep.Near, "account-1", null, null },
        { "account-1 account-2", "account-2:S95", "", "account-1", WalkStep.Near, "account-2", null, null },
        { "account-1 account-2 account-3", "account-2:S95", "", "account-1", WalkStep.List, null, null, null },
        { "account-1 account-2 account-3", "account-1:W40@+4d account-2:W10@+4d", "", "account-2", WalkStep.Pace, "account-1", null, null },
        { "account-1 account-2 account-3", "account-2:S95", "account-1=Cooling", "account-3", WalkStep.Cooling, null, WalkStep.Near, "account-2" },
        { "account-1 account-2 account-3 order", "account-1:S95", "", "account-2", WalkStep.Near, "account-1", null, null },
        { "account-1 account-2 account-3 early=off", "account-1:S95", "", "account-1", WalkStep.List, null, null, null },
    };

    [Theory]
    [MemberData(nameof(SaidChoices))]
    public void Near_and_pace_name_the_account_they_weigh(
        string scope, string facts, string unready, string ranOn, WalkStep step, string? over, WalkStep? rest, string? restOver)
    {
        var read = Scope(scope);
        var known = Facts(facts);
        var marks = unready.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(mark => mark[..mark.IndexOf('=')], mark => Enum.Parse<AccountReadiness>(mark[(mark.IndexOf('=') + 1)..]));
        var tried = AccountRotation.Order(read, StartKind.Driven, Names(read), known, Now)
            .Select(name => new AccountState(name, marks.GetValueOrDefault(name, AccountReadiness.Ready)))
            .ToList();
        var at = tried.FindIndex(state => state.IsReady);

        var chose = AccountRotation.Chose(read, StartKind.Driven, tried, at, known, Now);

        Assert.Equal(ranOn, tried[at].Account);
        Assert.Equal((step, over, rest, restOver), (chose.Step, chose.Over, chose.Rest, chose.RestOver));
    }
}
