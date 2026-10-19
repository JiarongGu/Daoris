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
/// walk, <c>switch=smart|curve|off</c> and a person's curve a window each, <c>curve=session=85</c> flat or
/// <c>curve=weekly=0:10,100:60</c> as points (D165 point 2), and the old names <c>early=on|off</c> and <c>near=85</c>,
/// read on; it is read through the file's own reader (<see cref="HarnessSettings.ResolveScope"/>), so a default outside its
/// list is read with the list winning, as a start reads it. What Daoris knows is <c>name:flags</c>, each flag one of
/// <c>r&lt;n&gt;</c> sessions running, <c>s&lt;offset&gt;</c> last started that long from now and <c>c&lt;n&gt;</c> chosen
/// by this look as its n-th start; what its agent said is <c>S</c>, <c>W</c>, <c>N</c>, <c>X</c> and <c>C</c> (step 2's
/// table below).</para>
/// <para>Steps 2, 4 and 5, over its curve, what would lapse at its week's reset and most room under its curve, read the
/// agent's word (TOOL6c) against the scope's curve (SWITCH1b-ii, USAGERESET1). Nothing here counts accounts: rows with one
/// account and with six read by the same rules (§2 rule 6).</para>
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
        var @switch = Setting("switch");
        var curve = tokens.Where(token => token.StartsWith("curve=", StringComparison.Ordinal)).Select(token => token["curve=".Length..]).ToList();
        if (keep is not null || use is not null || early is not null || near is not null || @switch is not null || curve.Count > 0)
        {
            settings = settings.WithUse("fake", new UseChange(
                Use: use, Keep: keep, Early: early, Near: near, Switch: @switch, Curve: curve.Count > 0 ? AccountReadingsTests.Curve(curve) : null));
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
                        Stand("session", "warned");
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
    [InlineData("account-1 account-2 account-3", "account-3:r1,W60@+5h", "account-1 account-2 account-3")]
    // Six accounts, one free: nothing counts them.
    [InlineData("account-1 account-2 account-3 account-4 account-5 account-6",
        "account-1:r1 account-2:r1 account-3:r1 account-4:r1 account-5:r1", "account-6 account-1 account-2 account-3 account-4 account-5")]
    public void Fewest_running_first(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // ——— Step 4, what would lapse at its week's reset (§16.3, as USAGERESET1 weighs it): within its last day, the most room
    // under its weekly curve over the hours to its reset first, an hour at the least; a tie goes on to step 5's tightest room.
    // The smart week's tail: with 20 hours left, 97.6; with 8, 99.0; with 2, 99.8.

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-3:W60@+20h", "account-3 account-1 account-2")]
    [InlineData("account-1 account-2 account-3", "account-3:W60@+24h", "account-3 account-1 account-2")]
    // The owner's case: 3 points resetting in 2 hours (1.4 an hour) and 58 in 20 (2.9 an hour). By its time alone the
    // sooner went first and the larger lapsed.
    [InlineData("account-1 account-2 account-3", "account-2:W97@+2h account-3:W40@+20h", "account-3 account-2 account-1")]
    // The same room sooner is the more urgent.
    [InlineData("account-1 account-2 account-3", "account-2:W60@+20h account-3:W60@+5h", "account-3 account-2 account-1")]
    // A tie goes on to the tightest room: account-2's five-hour window leaves it 10 points.
    [InlineData("account-1 account-2 account-3", "account-2:S80,W60@+8h account-3:W60@+8h", "account-3 account-2 account-1")]
    // An explicit zero is a week all unused, the most there is to lapse, where no reading earns nothing.
    [InlineData("account-1 account-2 account-3", "account-2:W50@+8h account-3:W0@+8h", "account-3 account-2 account-1")]
    // Beyond a day it ranks nothing: ranked all week, one account's work would pile onto it while the others' weeks went by.
    // Step 5's room decides, and 53 points four days off come before 47 a day and an hour off.
    [InlineData("account-1 account-2 account-3", "account-2:W10@+4d account-3:W50@+25h", "account-2 account-3 account-1")]
    // It outranks least recently started.
    [InlineData("account-1 account-2 account-3", "account-3:s-1m,W60@+10h", "account-3 account-1 account-2")]
    public void Within_its_last_day_what_would_lapse_first(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // The five-hour window beside a week about to reset: it stays at its curve's 90 and is not ranked by its reset. Under it,
    // the week's lapse puts the account first; over it, the account goes last whatever would lapse.
    [Theory]
    [InlineData("account-1 account-2 account-3", "account-3:S80,W40@+3h", "account-3 account-1 account-2")]
    [InlineData("account-1 account-2 account-3", "account-3:S89,W40@+3h", "account-3 account-1 account-2")]
    [InlineData("account-1 account-2 account-3", "account-3:S91,W40@+3h", "account-1 account-2 account-3")]
    // The five-hour window's own reset, three hours off, earns nothing: 80 points there would be 27 an hour.
    [InlineData("account-1 account-2 account-3", "account-2:S10 account-3:W40@+20h", "account-3 account-2 account-1")]
    public void The_five_hour_window_beside_a_week_about_to_reset(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // The week's tail spends what would lapse, and a week far from its reset is still never run into its limit while another
    // has room: three days off, its curve stands at 77, so 85% is over it and goes last. Twelve hours off it stands at 98.6:
    // 97% is spent on, where the old cap at 95 passed it, and 99% still goes last.
    [Theory]
    [InlineData("account-1 account-2 account-3", "account-1:W85@+3d account-2:W20@+3d", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:W97@+12h account-2:W20@+3d", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3", "account-1:W99@+12h account-2:W20@+3d", "account-2 account-3 account-1")]
    public void The_week_s_tail_spends_what_would_lapse_and_a_week_far_off_is_spared(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    /// <summary>
    /// The walk's ranking is a total preorder over any set of accounts (USAGERESET1): antisymmetric, and transitive in both
    /// its order and its ties, so a sort over it is well defined whatever the list's order. Generated: every step's input,
    /// readings with and without numbers, words, credits, resets past, near and far, and readings older than the newest.
    /// </summary>
    [Theory]
    [InlineData("account-1", 7)]
    [InlineData("account-1 switch=off", 11)]
    [InlineData("account-1 switch=curve curve=weekly=0:10,60:70,100:90 curve=session=70", 13)]
    [InlineData("account-1 order", 17)]
    public void The_ranking_is_a_total_preorder_over_a_generated_set(string written, int seed)
    {
        var random = new Random(seed);
        var use = Scope(written).Use;
        var facts = new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);
        var names = Enumerable.Range(1, 24).Select(n => $"a{n}").ToList();
        foreach (var name in names)
        {
            var windows = new List<WindowSaid>();
            if (random.Next(3) > 0)
            {
                windows.Add(new WindowSaid(
                    "session", random.Next(4) == 0 ? null : random.Next(0, 101) / 100.0, Now.AddMinutes(random.Next(-30, 300)),
                    random.Next(8) == 0 ? AccountReadings.RefusedWord : null, random.Next(10) == 0, Now.AddMinutes(-random.Next(0, 600)), "s0"));
            }

            if (random.Next(4) > 0)
            {
                windows.Add(new WindowSaid(
                    "weekly", random.Next(5) == 0 ? null : random.Next(0, 101) / 100.0, Now.AddHours(random.Next(-2, 7 * 24)),
                    null, false, Now.AddMinutes(-random.Next(0, 600)), "s0"));
            }

            facts[name] = new AccountFacts(
                Running: random.Next(3),
                LastStarted: random.Next(2) == 0 ? null : Now.AddMinutes(-random.Next(1, 5000)),
                Chosen: random.Next(5) == 0 ? random.Next(1, 4) : null,
                Said: windows.Count == 0 ? null : new AccountSaid(windows));
        }

        int Sign(string a, string b) => Math.Sign(AccountRotation.Rank(a, b, facts, Now, use).Order);
        foreach (var a in names)
        {
            foreach (var b in names)
            {
                Assert.Equal(-Sign(b, a), Sign(a, b));
                foreach (var c in names)
                {
                    if (Sign(a, b) <= 0 && Sign(b, c) <= 0) Assert.True(Sign(a, c) <= 0, $"{a} ≤ {b} ≤ {c}, yet {a} > {c}");
                }
            }
        }
    }

    // ——— Step 2, over its curve last (TOOL6c; §6, §16.3 as the evidence corrects them; D165 point 2, the switch design §3):
    // an account near any window's limit — by its word that a limit was reached, by drawing on usage credits, or by a
    // window's use over the scope's curve now (SWITCH1b-ii) — goes to the end: passed while another account is ready, run
    // when none is. `S<n>` is the session window's use in percent, resetting in three hours (two fifths of it gone, under the
    // smart curve's flat 90), `W<n>@<offset>` the week's and its reset (the smart curve its pace plus twenty), `N` its pace
    // warning (`warned`), `X` its word that a limit was reached, `C` credits. The pace warning is never near alone
    // (USAGEREAD3a, the account-switch design §1.3).

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-1:S95", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:S88", "account-1 account-2 account-3")]
    // On its curve is not over it (`UsageCurves.Over`): the smart session's 90 passes nothing, 91 does.
    [InlineData("account-1 account-2 account-3", "account-1:S90", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3", "account-1:S91", "account-2 account-3 account-1")]
    // A person's curve: the session flat at 85 passes 88, and the old `near=85` reads on as that curve (D165's SWITCH1a note).
    [InlineData("account-1 account-2 account-3 switch=curve curve=session=85", "account-1:S88", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 near=85", "account-1:S88", "account-2 account-3 account-1")]
    // The smart curve over the week: four days to its reset, three sevenths gone, the curve stands at 63%.
    [InlineData("account-1 account-2 account-3", "account-1:S30,W70@+4d", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:S30,W60@+4d", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3", "account-1:S30,W95@+3d", "account-2 account-3 account-1")]
    // A person's weekly curve: half its week gone, a straight line from 10% to 60% stands at 35%, where the smart one's 70%
    // would pass nothing.
    [InlineData("account-1 account-2 account-3 switch=curve curve=weekly=0:10,100:60", "account-1:W50@+84h", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:W50@+84h", "account-1 account-2 account-3")]
    // By credits; by a limit its word says was reached, whatever the number.
    [InlineData("account-1 account-2 account-3", "account-1:S10,C", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-2:W40@+2d,X", "account-1 account-3 account-2")]
    // Its pace warning passes nothing alone; the number beside it still does.
    [InlineData("account-1 account-2 account-3", "account-1:S10,N", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3", "account-1:S95,N", "account-2 account-3 account-1")]
    // It outranks fewest running: a near account is passed whatever runs on the others.
    [InlineData("account-1 account-2 account-3", "account-1:S95 account-2:r2 account-3:r1", "account-3 account-2 account-1")]
    // Every account near: the steps after it order them, and a start still runs. A pass, never a wait.
    [InlineData("account-1 account-2", "account-1:S95 account-2:S96", "account-1 account-2")]
    [InlineData("account-1 account-2", "account-1:S95,r1 account-2:S95", "account-2 account-1")]
    // ACCTWALK3, measured against the curve (SWITCH1b-ii): among near accounts the one least over its curve goes first,
    // before fewest running and the rest; equal room, or no number said, leaves the later steps as they were; credits
    // without a number rank after any number below 100%; an account whose limit its word says was reached stays last,
    // whatever room its number leaves.
    [InlineData("account-1 account-2", "account-1:S95,r1 account-2:S96", "account-1 account-2")]
    [InlineData("account-1 account-2", "account-1:S96 account-2:S95", "account-2 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:S99 account-2:S95 account-3:S97", "account-2 account-3 account-1")]
    // Measured on each account's own tightest window: nine points over its session against seven over its week.
    [InlineData("account-1 account-2", "account-1:S99 account-2:S30,W70@+4d", "account-2 account-1")]
    [InlineData("account-1 account-2", "account-1:C account-2:C", "account-1 account-2")]
    [InlineData("account-1 account-2", "account-1:C,r1 account-2:C", "account-2 account-1")]
    [InlineData("account-1 account-2", "account-1:C account-2:S96", "account-2 account-1")]
    [InlineData("account-1 account-2", "account-1:X account-2:C", "account-2 account-1")]
    [InlineData("account-1 account-2", "account-1:X account-2:S99", "account-2 account-1")]
    [InlineData("account-1 account-2", "account-1:W10@+4d,X account-2:S99", "account-2 account-1")]
    [InlineData("account-1 account-2 order", "account-1:S96 account-2:S95", "account-1 account-2")]
    // The install: a conversation opened on the account at 99% of its weekly limit, passing one at 60% of its week that
    // its pace warning had made near. That account is under its curve's 63%, so it goes first, ahead of both near ones.
    [InlineData("gmail account-1 account-3", "gmail:W99@+4d account-1:W60@+4d,N account-3:X", "account-1 gmail account-3")]
    [InlineData("account-1 account-2", "account-1:N account-2:S96", "account-1 account-2")]
    // `off` still passes an account over the smart curve at a start (the switch design §2–§3), never by the person's curve
    // it keeps; under `order` a near account goes last too, whatever the switch.
    [InlineData("account-1 account-2 account-3 switch=off", "account-1:S95", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 early=off", "account-1:S95", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 switch=off curve=session=80", "account-1:S85", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2 account-3 order", "account-1:S95", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3 order switch=off", "account-1:S95", "account-2 account-3 account-1")]
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

    // ——— Step 5, most room under its curve first (D165 point 2, the switch design §3): the points of room its tightest
    // window leaves under the scope's curve now, the most first; an account that said no number between those with room and
    // those over (absent is never zero, D57), as one on its curve. With four days to its reset, three sevenths of a week are
    // gone and the smart curve stands at 63%.

    [Theory]
    [InlineData("account-1 account-2 account-3", "account-1:W40@+4d account-2:W10@+4d account-3:W20@+4d", "account-2 account-3 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:W80@+4d account-3:W10@+4d", "account-3 account-2 account-1")]
    // Unknown sits between: room said, said nothing, over.
    [InlineData("account-1 account-2 account-3", "account-1:S95 account-3:W10@+4d", "account-3 account-2 account-1")]
    [InlineData("account-1 account-2 account-3", "account-1:W50@+4d account-3:W10@+4d", "account-3 account-1 account-2")]
    // The tightest window decides: twenty points under its session's curve, though fifty-three under its week's.
    [InlineData("account-1 account-2", "account-1:S70,W10@+4d account-2:W40@+4d", "account-2 account-1")]
    // A person's curve ranks by its own line: the session flat at 60 leaves account-1 ten points where the smart 90 left forty.
    [InlineData("account-1 account-2", "account-1:S50,W10@+84h account-2:W40@+84h", "account-1 account-2")]
    [InlineData("account-1 account-2 switch=curve curve=session=60", "account-1:S50,W10@+84h account-2:W40@+84h", "account-2 account-1")]
    // Below a week lapsing within the day; above least recently started.
    [InlineData("account-1 account-2 account-3", "account-1:W89@+20h account-2:W10@+4d", "account-1 account-2 account-3")]
    [InlineData("account-1 account-2", "account-1:W10@+4d,s-1m account-2:s-5h", "account-1 account-2")]
    // Below fewest running.
    [InlineData("account-1 account-2", "account-1:W5@+4d,r1 account-2:W60@+4d", "account-2 account-1")]
    // Under `order` room ranks nothing.
    [InlineData("account-1 account-2 order", "account-1:W50@+4d account-2:W10@+4d", "account-1 account-2")]
    public void Most_room_under_its_curve_first(string scope, string facts, string order)
    {
        Assert.Equal(order, Order(scope, facts));
    }

    // ——— Under `switch: off`, step 5 stands as D130 wrote it (the switch design §3): furthest behind its week's pace first,
    // the share of its week gone less the share it said is used; one ahead of pace after every account that is not; one that
    // said nothing between the two.

    [Theory]
    [InlineData("account-1 account-2 account-3 switch=off", "account-1:W40@+4d account-2:W10@+4d account-3:W20@+4d", "account-2 account-3 account-1")]
    // Ahead of its pace, though under its curve: after one that said nothing, where the curve put it before.
    [InlineData("account-1 account-2 account-3 switch=off", "account-1:W50@+4d account-3:W10@+4d", "account-3 account-2 account-1")]
    // The week alone: furthest behind its week, though its session is the tighter.
    [InlineData("account-1 account-2 switch=off", "account-1:S70,W10@+4d account-2:W40@+4d", "account-1 account-2")]
    public void Under_off_furthest_behind_its_week_s_pace_first(string scope, string facts, string order)
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
    [InlineData("account-1 account-2 account-3 order", "account-1:r3 account-3:W60@+5h", "account-1 account-2 account-3")]
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
        { "account-1 account-2 account-3", "account-3:W60@+5h", StartKind.Driven, "", "account-3", WalkStep.Lapsing, null },
        { "account-1 account-2 account-3", "account-1:W60@+5h", StartKind.Driven, "", "account-1", WalkStep.Lapsing, null },
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
            "What each account said: `account-1` 10 min ago, 95% of its session limit and 30% of its weekly limit used, over its session curve's 90%; "
            + "`account-2` nothing yet; `account-3` 10 min ago, ahead of its pace on its session window, by its own word, drawing on usage credits; "
            + "`account-4` 3 h ago, clear, by its own word.",
            RotationWords.Said(["account-1", "account-2", "account-3", "account-4"], facts, RotationUse.Default, Now));
        Assert.Null(RotationWords.Said(["account-1", "account-2"], Facts("account-1:r2"), RotationUse.Default, Now));
    }

    /// <summary>
    /// SWITCH1b-ii: an account over its curve says which window and the curve's value there now, the window its walk passed
    /// it on; one under its curve says only what it used.
    /// </summary>
    [Fact]
    public void An_account_over_its_curve_says_the_window_and_the_curve_s_value_now()
    {
        var facts = Facts("account-1:S30,W70@+4d account-2:S30,W60@+4d");

        Assert.Equal(
            "What each account said: `account-1` 10 min ago, 30% of its session limit and 70% of its weekly limit used, over its weekly curve's 63%; "
            + "`account-2` 10 min ago, 30% of its session limit and 60% of its weekly limit used.",
            RotationWords.Said(["account-1", "account-2"], facts, RotationUse.Default, Now));
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
        // Over its curve (SWITCH1b-ii): the smart one on its week and on its session, a person's, and the smart one under `off`.
        { "account-1 account-2 account-3", "account-1:S30,W70@+4d", "", "account-2", WalkStep.Near, "account-1", null, null },
        { "account-1 account-2 account-3", "account-1:S91", "", "account-2", WalkStep.Near, "account-1", null, null },
        { "account-1 account-2 account-3 switch=curve curve=weekly=0:10,100:60", "account-1:W50@+84h", "", "account-2", WalkStep.Near, "account-1", null, null },
        { "account-1 account-2 account-3 switch=off", "account-1:S95", "", "account-2", WalkStep.Near, "account-1", null, null },
        { "account-1 account-2 account-3 early=off", "account-1:S95", "", "account-2", WalkStep.Near, "account-1", null, null },
        // ACCTWALK3: both near, the one least over its curve ran, naming the account it was weighed against; a limit reached
        // stays last, whatever room its number leaves.
        { "account-1 account-2", "account-1:S99 account-2:S92", "", "account-2", WalkStep.Room, "account-1", null, null },
        { "account-1 account-2", "account-1:W10@+4d,X account-2:S99", "", "account-2", WalkStep.Room, "account-1", null, null },
        // The install (USAGEREAD3a): the account its pace warning had made near is not, so it ran ahead of the near one.
        { "gmail account-1 account-3", "gmail:W99@+4d account-1:W60@+4d,N account-3:X", "", "account-1", WalkStep.Near, "gmail", null, null },
        // Most room under its curve (step 5): where the list begins, ahead of the next by its room; ahead of one that said
        // nothing, which sits between; and under `off`, D130's pace.
        { "account-1 account-2 account-3", "account-1:W10@+4d account-2:W40@+4d", "", "account-1", WalkStep.Pace, "account-2", null, null },
        { "account-1 account-2", "account-2:W10@+4d", "", "account-2", WalkStep.Pace, "account-1", null, null },
        { "account-1 account-2 account-3", "account-1:W50@+4d account-3:W10@+4d", "", "account-3", WalkStep.Pace, "account-1", null, null },
        { "account-1 account-2 switch=off", "account-1:S70,W10@+4d account-2:W40@+4d", "", "account-1", WalkStep.Pace, "account-2", null, null },
        { "account-1 account-2 account-3 switch=off", "account-1:W50@+4d account-3:W10@+4d", "", "account-3", WalkStep.Pace, "account-1", null, null },
        // A tie on what would lapse goes on to the tightest room (USAGERESET1): account-1's five-hour window leaves it 10 points.
        { "account-1 account-2 account-3", "account-1:S80,W60@+8h account-2:W60@+8h", "", "account-2", WalkStep.Pace, "account-1", null, null },
        // Over its curve while the account where the list begins cools: the step among the rest names it.
        { "account-1 account-2 account-3", "account-2:S30,W70@+4d", "account-1=Cooling", "account-3", WalkStep.Cooling, null, WalkStep.Near, "account-2" },
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
