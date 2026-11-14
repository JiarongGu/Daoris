using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A path a turn in place changed that no tool event of its run names is one the person or another program changed: said so on the turn's review and told to the line at its next turn.</summary>
public sealed class TurnUnattributedTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("unattributed");
    private readonly PlaceMemory _memory;

    public TurnUnattributedTests() => _memory = new PlaceMemory(Path.Combine(_scratch, "reports"));

    private string Place => _memory.Place;
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose()
    {
        _memory.Dispose();
        ConformanceScratch.Remove(_scratch);
    }

    private Task RepositoryAsync() => _memory.MakeAsync(("notes.md", "first\n"), ("plan.md", "first\n"));

    private SessionEvent Edited(string relative, DateTimeOffset at) => new()
    {
        Kind = SessionEventKind.Tool, At = at, ToolKind = "edit", Locations = [Path.Combine(Place, relative.Replace('/', Path.DirectorySeparatorChar))],
    };

    [Fact]
    public async Task A_path_no_tool_of_the_turn_named_is_unattributed_and_the_next_turn_is_told_it()
    {
        await RepositoryAsync();
        await PlaceTurns.StartAsync(Home, "default", "reports", Place, "s-aaaaaaaa", null, CancellationToken.None);
        var began = DateTimeOffset.UtcNow;
        _memory.Put("notes.md", "the session's line\n");
        _memory.Put("plan.md", "the person's line\n");
        await PlaceTurns.EndAsync(Home, "s-aaaaaaaa", CancellationToken.None);
        SessionEvent[] events = [Edited("notes.md", began), Edited("plan.md", began.AddDays(-1))];

        Assert.Equal(["plan.md"], TurnUnattributed.Of(Home, "s-aaaaaaaa", events));

        var told = await PlaceTurns.StartAsync(Home, "default", "reports", Place, "s-bbbbbbbb", "s-aaaaaaaa", CancellationToken.None, events: events);
        Assert.Equal(["plan.md"], told!.Unattributed);
        Assert.Contains("While your last turn ran these changed by none of your tools: `plan.md`", PlacePrompt.Told(told));
    }

    [Fact]
    public async Task A_path_a_tool_only_read_is_still_unattributed()
    {
        await RepositoryAsync();
        await PlaceTurns.StartAsync(Home, "default", "reports", Place, "s-aaaaaaaa", null, CancellationToken.None);
        var began = DateTimeOffset.UtcNow;
        _memory.Put("plan.md", "the person's line");
        await PlaceTurns.EndAsync(Home, "s-aaaaaaaa", CancellationToken.None);
        var read = new SessionEvent
        {
            Kind = SessionEventKind.Tool, At = began, ToolKind = "read", Locations = [Path.Combine(Place, "plan.md")],
        };

        Assert.Equal(["plan.md"], TurnUnattributed.Of(Home, "s-aaaaaaaa", [read]));
    }

    [Fact]
    public async Task A_path_a_tool_named_in_another_case_is_its_own_where_the_disk_ignores_case()
    {
        if (!OperatingSystem.IsWindows()) return;
        await RepositoryAsync();
        await PlaceTurns.StartAsync(Home, "default", "reports", Place, "s-aaaaaaaa", null, CancellationToken.None);
        var began = DateTimeOffset.UtcNow;
        _memory.Put("notes.md", "the session's line");
        await PlaceTurns.EndAsync(Home, "s-aaaaaaaa", CancellationToken.None);

        Assert.Empty(TurnUnattributed.Of(Home, "s-aaaaaaaa", [Edited("NOTES.md", began)]));
    }

    [Fact]
    public void A_turn_with_no_end_read_yet_says_nothing()
    {
        Assert.Empty(TurnUnattributed.Of(Home, "s-nothing", []));
    }
}
