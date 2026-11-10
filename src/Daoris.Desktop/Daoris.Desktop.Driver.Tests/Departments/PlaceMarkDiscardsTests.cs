using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A person's act in a repository's own checkout that takes a parked line's uncommitted paths out of it (a stash, a commit, a discard)
/// releases those paths from the line's mark; a path still changed stays marked, and a line in another place or still running is not read.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PlaceMarkDiscardsTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("place-mark");
    private string Place => Path.Combine(_scratch, "reports");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private async Task RepositoryAsync()
    {
        Directory.CreateDirectory(Path.Combine(Place, "notes"));
        File.WriteAllText(Path.Combine(Place, "a.txt"), "a\n");
        File.WriteAllText(Path.Combine(Place, "notes", "b.md"), "b\n");
        await GitFixture.RunAsync(Place, "init", "-q", "-b", "main");
        await GitFixture.RunAsync(Place, "add", "-A");
        await GitFixture.RunAsync(Place, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "one");
    }

    [Fact]
    public async Task A_discarded_path_leaves_the_parked_line_s_mark_and_a_path_still_changed_stays()
    {
        await RepositoryAsync();
        File.WriteAllText(Path.Combine(Place, "a.txt"), "the session's line\n");
        File.WriteAllText(Path.Combine(Place, "notes", "b.md"), "the session's line\n");
        File.WriteAllText(Path.Combine(Place, "new.txt"), "a file the session made\n");
        SessionView[] active =
        [
            new("s-parked", "reports", "awaiting-person") { Tree = Place, HoldsPlace = ["a.txt", "notes/b.md", "new.txt"] },
            new("s-running", "reports", "working") { Tree = Place, HoldsPlace = ["a.txt"] },
            new("s-elsewhere", "other", "awaiting-person") { Tree = Place, HoldsPlace = ["a.txt"] },
        ];

        await GitFixture.RunAsync(Place, "checkout", "--", "a.txt");
        File.Delete(Path.Combine(Place, "new.txt"));
        var discarded = await PlaceMarkDiscards.DiscardedAsync(active, "reports", Place, CancellationToken.None);

        var only = Assert.Single(discarded);
        Assert.Equal("s-parked", only.Session);
        Assert.Equal(["a.txt", "new.txt"], only.Paths);
    }

    [Fact]
    public async Task Nothing_is_released_where_every_marked_path_is_still_changed()
    {
        await RepositoryAsync();
        File.WriteAllText(Path.Combine(Place, "a.txt"), "the session's line\n");
        SessionView[] active = [new("s-parked", "reports", "awaiting-person") { Tree = Place, HoldsPlace = ["a.txt"] }];

        Assert.Empty(await PlaceMarkDiscards.DiscardedAsync(active, "reports", Place, CancellationToken.None));
    }
}
