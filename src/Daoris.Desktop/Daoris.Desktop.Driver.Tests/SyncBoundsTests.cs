using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR7's bounds, without git: a look fetches a few repositories at a time rather than one after another, and the page
/// waits as long as the host may work on each long route, from its own copy of the host's bounds.
/// </summary>
/// <remarks>
/// <b>A twin</b> (<c>.claude/knowledge/twins.md</c>): the page's <c>hostBounds</c> in <c>bridge/call.ts</c> spells the
/// driver's <see cref="SyncBounds"/> and <see cref="LandingPlugins.DefaultPatience"/> again, and each row here holds one
/// number against the other. The page gave up after its bridge's default 30 seconds while the host fetched for
/// minutes, and answered nobody: a bound changed on one side only is that defect again.
/// </remarks>
public sealed partial class SyncBoundsTests
{
    /// <summary>Each of the page's numbers, and the host's own, in the page's unit (minutes, or a count).</summary>
    public static TheoryData<string, double> Twins => new()
    {
        { "fetchMinutes", SyncBounds.Fetch.TotalMinutes },
        { "fetchesAtOnce", SyncBounds.FetchesAtOnce },
        { "replayMinutes", SyncBounds.Replay.TotalMinutes },
        { "pluginMinutes", LandingPlugins.DefaultPatience.TotalMinutes },
    };

    [Theory]
    [MemberData(nameof(Twins))]
    public void The_page_waits_as_long_as_the_host_may_work(string name, double host)
    {
        var page = PageBounds();

        Assert.True(page.ContainsKey(name), $"bridge/call.ts's hostBounds has no `{name}`.");
        Assert.Equal(host, page[name]);
    }

    /// <summary>The page's table holds these rows and no other, so a bound added there is added here.</summary>
    [Fact]
    public void The_page_spells_every_bound_and_only_these()
    {
        Assert.Equal(Twins.Select(row => (string)row[0]).Order(), PageBounds().Keys.Order());
    }

    /// <summary>
    /// The fetches run a few at a time (WSR7): as many as the bound at once, never more, and the answers in the order
    /// asked. The first four wait until four are running together, so a bound that let fewer run would not get past.
    /// </summary>
    [Fact]
    public async Task Work_runs_as_many_at_once_as_the_bound_and_never_more_and_answers_in_order()
    {
        var running = 0;
        var most = 0;
        var together = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = Enumerable.Range(0, 10).ToList();

        var answers = await SessionTrees.AtMostAsync(items, SyncBounds.FetchesAtOnce, async (item, ct) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            if (now >= SyncBounds.FetchesAtOnce) together.TrySetResult();
            await Task.WhenAny(together.Task, Task.Delay(TimeSpan.FromSeconds(10), ct));
            await Task.Delay(5, ct);
            Interlocked.Decrement(ref running);
            return item * 2;
        }, CancellationToken.None);

        Assert.True(together.Task.IsCompleted, "the bound's number of fetches never ran together.");
        Assert.Equal(SyncBounds.FetchesAtOnce, most);
        Assert.Equal(items.Select(item => item * 2), answers);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }

    /// <summary>The page's <c>hostBounds</c>, read as text the way a reviewer would: each name and its number.</summary>
    private static Dictionary<string, double> PageBounds()
    {
        var call = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Web", "src", "bridge", "call.ts"));
        var table = HostBounds().Match(call);
        Assert.True(table.Success, "bridge/call.ts holds no `export const hostBounds = { … }`.");
        return BoundRow().Matches(table.Groups[1].Value)
            .ToDictionary(row => row.Groups[1].Value, row => double.Parse(row.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"export const hostBounds = \{([^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex HostBounds();

    [GeneratedRegex(@"^\s*(\w+):\s*(\d+(?:\.\d+)?),", RegexOptions.Multiline)]
    private static partial Regex BoundRow();
}
