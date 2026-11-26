using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris priority &lt;ask|quest&gt; &lt;class&gt; --why "…" [--until &lt;time&gt;]</c> sets a class as a recorded event, an
/// urgent one with its end (the workspace's life where none is given) and within its workspace's cap; the id alone says the class.
/// </summary>
public sealed class PriorityCommandTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 11, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "priority-command", Guid.NewGuid().ToString("N")[..8]);

    public PriorityCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private PriorityWorld World(string by = "the person") => new(_home, (id, _) => Task.FromResult(id.TrimStart('#') switch
    {
        "2c0c81fd6f19" => new PriorityTarget(PriorityBook.Quest, "2c0c81fd6f19", "games", "e27fc2"),
        "e27fc2" => new PriorityTarget(PriorityBook.Ask, "e27fc2", "games", null),
        _ => (PriorityTarget?)null,
    }), () => Now, by);

    private async Task<(int Exit, string Said)> RunAsync(PriorityWorld world, params string[] args)
    {
        using var said = new StringWriter();
        var exit = await PriorityCommand.RunAsync(args, world, said, CancellationToken.None);
        return (exit, said.ToString());
    }

    [Fact]
    public async Task A_class_set_on_a_quest_is_kept_with_its_why_and_who_and_said()
    {
        var (exit, said) = await RunAsync(World("session s-1"), "#2c0c81fd6f19", "high", "--why", "the release waits on it");

        Assert.Equal(0, exit);
        Assert.Contains("quest #2c0c81fd6f19 is high", said);
        var kept = Assert.Single(PriorityBook.Read(_home));
        Assert.Equal((PriorityBook.Quest, "2c0c81fd6f19", "games", PriorityClasses.High, "the release waits on it", "session s-1", Now),
            (kept.Target, kept.Id, kept.Workspace, kept.Class, kept.Why, kept.By, kept.At));
    }

    [Fact]
    public async Task Urgent_takes_the_end_given_or_the_workspace_s_life()
    {
        Assert.Equal(0, (await RunAsync(World(), "e27fc2", "urgent", "--why", "fix first", "--until", "4h")).Exit);
        Assert.Equal(Now.AddHours(4), PriorityBook.Read(_home)[^1].Until);

        Assert.Equal(0, (await RunAsync(World(), "2c0c81fd6f19", "urgent", "--why", "fix first", "--until", "2026-11-27T08:00:00Z")).Exit);
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 8, 0, 0, TimeSpan.Zero), PriorityBook.Read(_home)[^1].Until);

        Assert.Equal(0, (await RunAsync(World(), "2c0c81fd6f19", "urgent", "--why", "again")).Exit);
        Assert.Equal(Now + PriorityRules.Default.UrgentLife, PriorityBook.Read(_home)[^1].Until);
    }

    [Fact]
    public async Task The_id_alone_says_the_class_and_where_it_comes_from()
    {
        Assert.Contains("quest #2c0c81fd6f19 is normal", (await RunAsync(World(), "2c0c81fd6f19")).Said);

        await RunAsync(World(), "e27fc2", "high", "--why", "the release");
        var (exit, said) = await RunAsync(World(), "2c0c81fd6f19");
        Assert.Equal(0, exit);
        Assert.Contains("quest #2c0c81fd6f19 is high, from its ask #e27fc2, set by the person: the release", said);
    }

    [Fact]
    public async Task A_refusal_and_a_name_of_nothing_exit_one_and_keep_nothing()
    {
        var cap = PriorityRules.Default.UrgentCap;
        for (var at = 0; at < cap; at++)
        {
            PriorityBook.Set(_home, new PriorityEvent(Now, PriorityBook.Quest, $"held{at}", "games", PriorityClasses.Urgent, "w", "the person") { Until = Now.AddHours(1) }, Now, PriorityRules.Default);
        }

        var (refused, said) = await RunAsync(World(), "2c0c81fd6f19", "urgent", "--why", "fix first");
        Assert.Equal(1, refused);
        Assert.Contains("quest #held0", said);

        Assert.Equal(1, (await RunAsync(World(), "ffffffffffff", "high", "--why", "w")).Exit);
        Assert.Equal(cap, PriorityBook.Read(_home).Count);
    }

    [Theory]
    [InlineData]
    [InlineData("2c0c81fd6f19", "critical", "--why", "w")]
    [InlineData("2c0c81fd6f19", "high")]
    [InlineData("2c0c81fd6f19", "high", "--why")]
    [InlineData("2c0c81fd6f19", "high", "--why", "w", "--until", "soon")]
    [InlineData("2c0c81fd6f19", "high", "--why", "w", "--frobnicate")]
    public async Task Words_it_does_not_take_are_the_usage(params string[] args)
    {
        var (exit, said) = await RunAsync(World(), args);

        Assert.Equal(2, exit);
        Assert.Contains(PriorityCommand.Usage, said);
        Assert.Empty(PriorityBook.Read(_home));
    }
}
