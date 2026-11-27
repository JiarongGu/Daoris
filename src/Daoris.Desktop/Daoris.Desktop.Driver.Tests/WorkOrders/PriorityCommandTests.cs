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
        "second" => new PriorityTarget(PriorityBook.Quest, "second", "games", null) { Run = "2c0c81fd6f19" },
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
    public async Task A_class_set_on_a_chain_s_later_quest_is_kept_with_its_run_and_its_run_reads_it()
    {
        Assert.Equal(0, (await RunAsync(World(), "second", "urgent", "--why", "the fix", "--until", "2h")).Exit);

        Assert.Equal("2c0c81fd6f19", Assert.Single(PriorityBook.Read(_home)).Run);
        Assert.Equal(PriorityClasses.Urgent, PriorityBook.OfRun(PriorityBook.Read(_home), "2c0c81fd6f19", null, Now).Class);
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

    [Fact]
    public async Task A_hold_on_a_chain_s_later_quest_is_kept_with_its_run_said_and_lifted_by_the_same_word()
    {
        var (exit, said) = await RunAsync(World("session s-1"), "second", "hold", "--why", "make room for the fix");

        Assert.Equal(0, exit);
        Assert.Contains("quest #second is held", said);
        Assert.Contains("make room for the fix", said);
        var kept = Assert.Single(HoldBook.Read(_home));
        Assert.Equal((PriorityBook.Quest, "second", "2c0c81fd6f19", true, "session s-1"), (kept.Target, kept.Id, kept.Run, kept.Held, kept.By));
        Assert.True(HoldBook.IsHeld(HoldBook.Read(_home), "2c0c81fd6f19", null));
        Assert.Empty(PriorityBook.Read(_home));

        var (lifted, liftedSaid) = await RunAsync(World(), "second", "lift");
        Assert.Equal(0, lifted);
        Assert.Contains("quest #second is not held", liftedSaid);
        Assert.False(HoldBook.IsHeld(HoldBook.Read(_home), "2c0c81fd6f19", null));
    }

    [Fact]
    public async Task A_chain_s_run_quest_says_the_hold_its_later_quest_holds_it_by_and_the_lift_that_ends_it()
    {
        await RunAsync(World(), "second", "hold", "--why", "make room");

        var held = (await RunAsync(World(), "2c0c81fd6f19")).Said;
        Assert.Contains("held by the person: make room, set on quest #second", held);

        await RunAsync(World(), "2c0c81fd6f19", "lift");
        Assert.Contains("quest #second is not held", (await RunAsync(World(), "second")).Said);
    }

    [Fact]
    public async Task The_id_alone_says_a_hold_beside_the_class_and_a_hold_needs_its_why()
    {
        Assert.Equal(2, (await RunAsync(World(), "e27fc2", "hold")).Exit);
        await RunAsync(World(), "e27fc2", "hold", "--why", "stand aside");

        var (exit, said) = await RunAsync(World(), "e27fc2");

        Assert.Equal(0, exit);
        Assert.Contains("ask #e27fc2 is normal", said);
        Assert.Contains("held by the person: stand aside", said);
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
