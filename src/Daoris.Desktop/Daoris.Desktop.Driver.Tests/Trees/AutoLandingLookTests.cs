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
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s8", "q8", "engine", "aurora", Path.Combine(_home, "trees", "aurora", "engine", "s-8"), At));
        due.Due(new AutoLanding("s9", "q9", "tools", "aurora", Path.Combine(_home, "trees", "aurora", "tools", "s-9"), At));
        var config = DriverConfig.Empty
            .WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}"))
            .WithLanding("tools", new LandingRule(LandingForm.Branch, "feature/{quest}", AutoAccept: true));

        using var service = _ledger.Client();
        var driver = new Daoris.Driver.Driver(service, config, AdapterSet.Built(), _home,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")));
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
}
