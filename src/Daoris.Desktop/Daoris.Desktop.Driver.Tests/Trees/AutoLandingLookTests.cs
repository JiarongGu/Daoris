using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND2b (D145 point 2): every look works the due list after its endings, and what it decides joins its report. Held here on
/// what a look decides before any git is asked — a rule switched off since, a tree discarded since — over the real client and
/// the real driver with the service standing in, so the wiring stays in the suite's fast half. The landing itself over real
/// git is <see cref="AutoLandingTests"/>.
/// </summary>
public sealed class AutoLandingLookTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-auto-look-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public AutoLandingLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A rule switched off since the session became due closes its entry, and the look says so: the person's press is the way
    /// again. A tree discarded since closes it too. Nothing is landed, and nothing is left running beside the look.
    /// </summary>
    [Fact]
    public async Task A_look_works_the_due_list_and_says_what_it_closed()
    {
        // The home's lease, as the loop that lands holds it (FABRIC-END7's review).
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s8", "q8", "engine", "aurora", DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "engine", "s-8"), At));
        due.Due(new AutoLanding("s9", "q9", "tools", "aurora", DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "tools", "s-9"), At));
        var config = DriverConfig.Empty
            .WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}"))
            .WithLanding("tools", new LandingRule(LandingForm.Branch, "feature/{quest}", AutoAccept: true));

        using var service = _ledger.Client();
        var driver = new Daoris.Driver.Driver(service, config, AdapterSet.Built(), _home,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var look = await driver.TickAsync().WaitAsync(Bound);

        Assert.Contains(look.Events, line => line.StartsWith("landing  session s8 (#q8 → engine): its repository's rule no longer accepts automatically", StringComparison.Ordinal));
        Assert.Contains(look.Events, line => line.StartsWith("landing  session s9 (#q9 → tools): its tree was discarded before it landed", StringComparison.Ordinal));
        Assert.Empty(due.Open());
        Assert.Equal(AutoLandingCode.Off, due.Of("s8")!.Last!.Code);
        Assert.Equal(AutoLandingCode.Gone, due.Of("s9")!.Last!.Code);
        Assert.False(driver.Running.Landing);
        Assert.True(driver.Running.Idle);

        // Closed is closed: the next look says nothing more of them.
        var next = await driver.TickAsync().WaitAsync(Bound);
        Assert.DoesNotContain(next.Events, line => line.StartsWith("landing  ", StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTONOMY1c2 (D184 points 2 and 3): the look judges each entry by the effective rule. Under the repository's setup saying
    /// <c>rule</c>, or full access, a branch rule whose own switch is off does not close it as off: its tree is gone here, so it closes
    /// as that. Under <c>person</c> it closes as off, and a merge rule under <c>rule</c> does too, since a merge lands nothing by itself
    /// (D51 rule 6) and the person's press is the way.
    /// </summary>
    [Theory]
    [InlineData(AccessRules.Rule, LandingForm.Branch, AutoLandingCode.Gone)]
    [InlineData(AccessRules.FullWord, LandingForm.Branch, AutoLandingCode.Gone)]
    [InlineData(AccessRules.Person, LandingForm.Branch, AutoLandingCode.Off)]
    [InlineData(AccessRules.Rule, LandingForm.Merge, AutoLandingCode.Off)]
    public async Task A_look_judges_each_entry_by_the_setups_landing(string landing, string form, string code)
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s8", "q8", "engine", "aurora", DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-8"), At));
        var setting = landing == AccessRules.FullWord
            ? AccessSetting.FullAccess
            : new AccessSetting { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [AccessRules.Landing] = landing } };
        var config = DriverConfig.Empty
            .WithLanding("engine", form == LandingForm.Merge ? LandingRule.Merge : new LandingRule(LandingForm.Branch, "feature/{quest}")) with
        {
            Access = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["engine"] = setting },
        };

        using var service = _ledger.Client();
        var driver = new Daoris.Driver.Driver(service, config, AdapterSet.Built(), _home,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var look = await driver.TickAsync().WaitAsync(Bound);

        Assert.Equal(code, due.Of("s8")!.Last!.Code);
        Assert.Equal(code == AutoLandingCode.Off,
            look.Events.Any(line => line.StartsWith("landing  session s8 (#q8 → engine): its repository's rule no longer accepts automatically", StringComparison.Ordinal)));
        Assert.Empty(due.Open());
    }

    /// <summary>
    /// FABRIC-END7's review, finding 2: only the process holding the home's lease selects or launches an automatic landing. A loop beside
    /// it (<c>drive --share</c>) reads nothing of the due list and says nothing of it; its wakes are spent, since they are not its to land.
    /// </summary>
    [Fact]
    public async Task A_loop_that_does_not_hold_the_home_s_lease_lands_nothing_automatically()
    {
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s8", "q8", "engine", "aurora", DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-8"), At));
        var config = DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}"));

        using var service = _ledger.Client();
        var beside = new Daoris.Driver.Driver(service, config, AdapterSet.Built(), _home,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Woke = [new(ChangeTopics.Ledger, "quests")],
        };
        var look = await beside.TickAsync().WaitAsync(Bound);

        Assert.DoesNotContain(look.Events, line => line.StartsWith("landing  ", StringComparison.Ordinal));
        Assert.Null(Assert.Single(due.Open()).Last);
        Assert.True(beside.WokeSpent);
        Assert.False(beside.Running.Landing);

        // The holder's look reads it, and closes it as the rule now says.
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var holder = new Daoris.Driver.Driver(service, config, AdapterSet.Built(), _home,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var held = await holder.TickAsync().WaitAsync(Bound);
        Assert.Contains(held.Events, line => line.StartsWith("landing  session s8 (#q8 → engine): its repository's rule no longer accepts automatically", StringComparison.Ordinal));
        Assert.Empty(due.Open());
    }

    /// <summary>
    /// FABRIC-END7's review, finding 3: a look whose landing is still running judges nothing, and leaves its wakes unspent, held back for
    /// that landing's end; the entry they concern is not read.
    /// </summary>
    [Fact]
    public async Task A_look_while_a_landing_runs_judges_nothing_and_leaves_its_wakes_unspent()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s8", "q8", "engine", "aurora", DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-8"), At));
        var config = DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}"));

        using var service = _ledger.Client();
        var driver = new Daoris.Driver.Driver(service, config, AdapterSet.Built(), _home,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Woke = [new(ChangeTopics.Ledger, "quests")],
        };
        var landing = new TaskCompletionSource<IReadOnlyList<string>>();
        Assert.True(driver.Running.Beside(_ => landing.Task, CancellationToken.None));

        var look = await driver.TickAsync().WaitAsync(Bound);
        landing.SetResult([]);

        Assert.DoesNotContain(look.Events, line => line.StartsWith("landing  ", StringComparison.Ordinal));
        Assert.Null(Assert.Single(due.Open()).Last);
        Assert.False(driver.WokeSpent);
        Assert.True(driver.LandingBusy);
    }
}
