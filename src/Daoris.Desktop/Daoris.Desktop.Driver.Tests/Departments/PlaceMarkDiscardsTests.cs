using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A person's act in a repository's own checkout that takes a parked line's uncommitted paths out of it releases them from the line's mark; a path still changed stays marked.</summary>
public sealed class PlaceMarkDiscardsTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("place-mark");
    private readonly PlaceMemory _memory;

    public PlaceMarkDiscardsTests() => _memory = new PlaceMemory(Path.Combine(_scratch, "reports"));

    private string Place => _memory.Place;

    public void Dispose()
    {
        _memory.Dispose();
        ConformanceScratch.Remove(_scratch);
    }

    private Task RepositoryAsync() => _memory.MakeAsync(("a.txt", "a\n"), ("notes/b.md", "b\n"));

    [Fact]
    public async Task A_discarded_path_leaves_the_parked_line_s_mark_and_a_path_still_changed_stays()
    {
        await RepositoryAsync();
        _memory.Put("a.txt", "the session's line\n");
        _memory.Put("notes/b.md", "the session's line\n");
        _memory.Put("new.txt", "a file the session made\n");
        SessionView[] active =
        [
            new("s-parked", "reports", "awaiting-person") { Tree = Place, HoldsPlace = ["a.txt", "notes/b.md", "new.txt"] },
            new("s-running", "reports", "working") { Tree = Place, HoldsPlace = ["a.txt"] },
            new("s-elsewhere", "other", "awaiting-person") { Tree = Place, HoldsPlace = ["a.txt"] },
        ];

        Assert.True((await _memory.Repository.RestoreAsync(Place, ["a.txt"], CancellationToken.None)).Ok);
        _memory.Delete("new.txt");
        var discarded = await PlaceMarkDiscards.DiscardedAsync(active, "reports", Place, CancellationToken.None);

        var only = Assert.Single(discarded);
        Assert.Equal("s-parked", only.Session);
        Assert.Equal(["a.txt", "new.txt"], only.Paths);
    }

    [Fact]
    public async Task Nothing_is_released_where_every_marked_path_is_still_changed()
    {
        await RepositoryAsync();
        _memory.Put("a.txt", "the session's line\n");
        SessionView[] active = [new("s-parked", "reports", "awaiting-person") { Tree = Place, HoldsPlace = ["a.txt"] }];

        Assert.Empty(await PlaceMarkDiscards.DiscardedAsync(active, "reports", Place, CancellationToken.None));
    }
}
