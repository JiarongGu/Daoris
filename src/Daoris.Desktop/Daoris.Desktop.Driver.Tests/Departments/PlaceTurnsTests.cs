using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A turn in a git place reads its manifest at its start and its end: the start against the place's last manifest finds what the
/// person changed meanwhile, which the session is told; the end against the start, with what the line carried in, is what of its own
/// stays uncommitted, the mark it keeps the place with. A commit clears it, and an end is read once.
/// </summary>
[Trait(Category.Parallel, Category.Git)]
public sealed class PlaceTurnsTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("place-turns");
    private string Place => Path.Combine(_scratch, "reports");
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private async Task<string> RepositoryAsync()
    {
        Directory.CreateDirectory(Place);
        await GitFixture.RunAsync(Place, "init", "-q", "-b", "main");
        File.WriteAllText(Path.Combine(Place, "notes.md"), "first\n");
        await GitFixture.RunAsync(Place, "add", "-A");
        await GitFixture.RunAsync(Place, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "one");
        return Place;
    }

    private Task<PlaceTold?> StartAsync(string record, string? continues = null) =>
        PlaceTurns.StartAsync(Home, "default", "reports", Place, record, continues, CancellationToken.None);

    private Task<IReadOnlyList<string>?> EndAsync(string record) => PlaceTurns.EndAsync(Home, record, CancellationToken.None);

    [Fact]
    public async Task The_start_finds_the_person_s_edit_between_turns_and_the_paths_that_are_not_the_session_s_own()
    {
        await RepositoryAsync();

        Assert.Equal(new PlaceTold([], []), await StartAsync("s-aaaaaaaa"));
        File.WriteAllText(Path.Combine(Place, "draft.md"), "the session's\n");
        Assert.Equal(["draft.md"], await EndAsync("s-aaaaaaaa"));

        File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's line\n");
        var told = await StartAsync("s-bbbbbbbb");

        Assert.Equal(["notes.md"], told!.Outside);
        Assert.Equal(["draft.md", "notes.md"], told.Others);
        Assert.Equal(["draft.md", "notes.md"], PlaceTurns.Others(Home, "s-bbbbbbbb"));
        Assert.Empty(PlaceTurns.Others(Home, "s-aaaaaaaa"));
    }

    [Fact]
    public async Task A_line_keeps_its_own_paths_across_its_turns_until_a_commit_clears_them()
    {
        await RepositoryAsync();

        await StartAsync("s-aaaaaaaa");
        File.WriteAllText(Path.Combine(Place, "draft.md"), "the session's\n");
        Assert.Equal(["draft.md"], await EndAsync("s-aaaaaaaa"));

        // Its next turn carries its own path in, so it is not told it is someone else's, and keeps it where the turn left it.
        Assert.Equal(new PlaceTold([], []), await StartAsync("s-aaaaaaaa"));
        Assert.Equal(["draft.md"], await EndAsync("s-aaaaaaaa"));

        // A part carrying the line on carries the path in from the record it continues.
        Assert.Equal(new PlaceTold([], []), await StartAsync("s-cccccccc", continues: "s-aaaaaaaa"));
        await GitFixture.RunAsync(Place, "add", "draft.md");
        await GitFixture.RunAsync(Place, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "two");
        Assert.Empty((await EndAsync("s-cccccccc"))!);
    }

    [Fact]
    public async Task An_end_is_read_once_and_a_record_with_no_turn_in_place_marks_nothing()
    {
        await RepositoryAsync();

        Assert.Null(await EndAsync("s-dddddddd"));

        await StartAsync("s-aaaaaaaa");
        File.WriteAllText(Path.Combine(Place, "draft.md"), "the session's\n");
        Assert.Equal(["draft.md"], await EndAsync("s-aaaaaaaa"));

        // The person's end of the park after the turn returned reads the turn's end, never the person's later edits as its own.
        File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's line\n");
        Assert.Equal(["draft.md"], await EndAsync("s-aaaaaaaa"));
    }

    [Fact]
    public async Task A_folder_that_is_not_a_git_place_reads_no_manifest()
    {
        Directory.CreateDirectory(Place);

        Assert.Null(await StartAsync("s-aaaaaaaa"));
        Assert.Null(await EndAsync("s-aaaaaaaa"));
    }

    [Fact]
    public async Task The_denies_are_handed_where_the_start_found_the_person_s_paths_and_only_there()
    {
        await RepositoryAsync();
        var composed = new RuleLists([], [], ["Bash(git push:*)"]);

        await StartAsync("s-aaaaaaaa");
        Assert.Equal(composed.Deny, PlaceDenies.Compose(composed, PlaceTurns.Others(Home, "s-aaaaaaaa")).Deny);

        File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's line\n");
        await StartAsync("s-bbbbbbbb");
        var handed = PlaceDenies.Compose(composed, PlaceTurns.Others(Home, "s-bbbbbbbb")).Deny;

        Assert.Contains("Bash(git push:*)", handed);
        foreach (var rule in (string[])["Bash(git add -A)", "Bash(git add --all:*)", "Bash(git add .)", "Bash(git commit -a:*)",
                     "Bash(git commit -am:*)", "Bash(git stash)", "Bash(git stash:*)", "Bash(git -* add -A)", "Bash(git -* stash *)"])
        {
            Assert.Contains(rule, handed);
        }
    }

    [Fact]
    public void The_session_is_told_at_most_thirty_paths_then_a_count()
    {
        var outside = Enumerable.Range(0, 32).Select(i => $"docs/{i:D2}.md").ToList();

        var told = PlacePrompt.Told(new PlaceTold(outside, ["notes.md"]));

        Assert.Contains("these changed outside Daoris", told);
        Assert.Contains("`docs/29.md`", told);
        Assert.DoesNotContain("`docs/30.md`", told);
        Assert.Contains("and 2 more", told);
        Assert.Contains("read a file again before you change it, and keep what the person changed", told);
        Assert.Contains("`notes.md`", told);
        Assert.Contains("leave them out of your commits; commit your own by path", told);
        Assert.Equal("", PlacePrompt.Told(null));
        Assert.Equal("", PlacePrompt.Told(new PlaceTold([], [])));
        Assert.Equal(2, PlacePrompt.Left(new PlaceTold(outside, ["notes.md"])));
    }
}
