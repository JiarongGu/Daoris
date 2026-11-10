using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A path a turn in place changed that no tool event of its run names was changed while it ran, by the person or another program: it is
/// said so on the turn's review and told to the line at its next turn. A path its own file tools named is its own.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TurnUnattributedTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("unattributed");
    private string Place => Path.Combine(_scratch, "reports");
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private async Task RepositoryAsync()
    {
        Directory.CreateDirectory(Place);
        File.WriteAllText(Path.Combine(Place, "notes.md"), "first\n");
        File.WriteAllText(Path.Combine(Place, "plan.md"), "first\n");
        await GitFixture.RunAsync(Place, "init", "-q", "-b", "main");
        await GitFixture.RunAsync(Place, "add", "-A");
        await GitFixture.RunAsync(Place, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "one");
    }

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
        File.WriteAllText(Path.Combine(Place, "notes.md"), "the session's line\n");
        File.WriteAllText(Path.Combine(Place, "plan.md"), "the person's line\n");
        await PlaceTurns.EndAsync(Home, "s-aaaaaaaa", CancellationToken.None);
        SessionEvent[] events = [Edited("notes.md", began), Edited("plan.md", began.AddDays(-1))];

        Assert.Equal(["plan.md"], TurnUnattributed.Of(Home, "s-aaaaaaaa", events));

        var told = await PlaceTurns.StartAsync(Home, "default", "reports", Place, "s-bbbbbbbb", "s-aaaaaaaa", CancellationToken.None, events: events);
        Assert.Equal(["plan.md"], told!.Unattributed);
        Assert.Contains("While your last turn ran these changed by none of your tools: `plan.md`", PlacePrompt.Told(told));
    }

    [Fact]
    public void A_turn_with_no_end_read_yet_says_nothing()
    {
        Assert.Empty(TurnUnattributed.Of(Home, "s-nothing", []));
    }
}
