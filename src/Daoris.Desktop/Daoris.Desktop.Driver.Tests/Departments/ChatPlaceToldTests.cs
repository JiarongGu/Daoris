using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation in a git place reads the place's manifest at its start as a driven turn does, and is told what changed there outside
/// Daoris, with its first message and before the person's words.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ChatPlaceToldTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("chat-place");
    private string Place => Path.Combine(_scratch, "reports");
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private async Task RepositoryAsync()
    {
        Directory.CreateDirectory(Place);
        await GitFixture.RunAsync(Place, "init", "-q", "-b", "main");
        File.WriteAllText(Path.Combine(Place, "notes.md"), "first\n");
        await GitFixture.RunAsync(Place, "add", "-A");
        await GitFixture.RunAsync(Place, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "one");
    }

    [Fact]
    public async Task A_conversation_is_told_the_person_s_uncommitted_paths_and_a_non_git_folder_that_it_is_one()
    {
        await RepositoryAsync();
        Assert.Null(await ChatPlaceTold.ToldAsync(Home, "default", "reports", Place, "c-aaaaaaaa", null, CancellationToken.None));

        File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's line\n");
        var told = await ChatPlaceTold.ToldAsync(Home, "default", "reports", Place, "c-bbbbbbbb", null, CancellationToken.None);

        Assert.Contains("## Your place", told);
        Assert.Contains("`notes.md`", told);
        Assert.Equal(["notes.md"], PlaceTurns.Others(Home, "c-bbbbbbbb"));

        var folder = Path.Combine(_scratch, "plain");
        Directory.CreateDirectory(folder);
        Assert.Contains("It is not a git repository", await ChatPlaceTold.ToldAsync(Home, "default", "plain", folder, "c-cccccccc", null, CancellationToken.None));
    }

    [Fact]
    public void The_first_message_carries_what_the_place_told_ahead_of_the_browser_line_and_once()
    {
        using var runner = new ChatRunner(
            new ChatLedger().Client(), ProviderRegistry.Built(), Home, new SessionProcesses(DaorisHome.Entry(Home, HomeEntry.Sessions)));
        var told = new ChatTold(runner.Context, runner.Context.ChatRuns);
        told.HoldPlace("c1a2b3c4", "## Your place\nthe person changed `notes.md`");

        var first = told.With("c1a2b3c4", new ChatMessage("hello", []) { Briefing = "the browser line" });
        var second = told.With("c1a2b3c4", new ChatMessage("again", []));

        Assert.Equal("the browser line\n\n## Your place\nthe person changed `notes.md`", first.Briefing);
        Assert.Null(second.Briefing);
    }
}
