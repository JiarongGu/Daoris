using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1a (D195's KEEPSIZE1 note; the keep-size design §1.1 and §6): the folders set aside at every place of the trees entry. Before
/// it, the purge, the slim, the press's list, the floor, the look and the card read <c>.tidied</c> under the trees entry's answered place
/// alone, so the folders set aside before the home's layout moved, at the old place, were never purged, listed or counted as set aside.
/// A home holding a set-aside folder at each place, as the look leaves them: files only, so no git and no process is needed.
/// </summary>
public sealed class SetAsidePlacesTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "aside-places", Guid.NewGuid().ToString("N")[..8]);

    public SetAsidePlacesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The trees entry's new place, where a folder moves today.</summary>
    private string NewPlace => DaorisHome.Places(_home, HomeEntry.Trees)[0];

    /// <summary>The trees entry's old place, where the folders set aside before the layout moved still lie.</summary>
    private string OldPlace => DaorisHome.Places(_home, HomeEntry.Trees)[1];

    /// <summary>
    /// A folder as the look sets it aside under <paramref name="place"/>'s <c>.tidied</c>, moved at <paramref name="moved"/>: one file of
    /// <paramref name="bytes"/>, every entry dated an hour before the move, and no manifest, as a folder set aside before KEEP1a has none.
    /// </summary>
    private static string Aside(string place, string name, DateTimeOffset moved, int bytes)
    {
        var folder = Path.Combine(place, TreeSetAside.SetAsideFolder, "games", "Daoris",
            $"{name}-{moved.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture)}");
        var file = Path.Combine(folder, "README.md");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(file, new byte[bytes]);
        var before = moved.UtcDateTime.AddHours(-1);
        File.SetCreationTimeUtc(file, before);
        File.SetLastWriteTimeUtc(file, before);
        return folder;
    }

    /// <summary>A world over this home with its own sizes and a volume that answers.</summary>
    private SpaceWorld World() => new(_home)
    {
        Sizes = new SpaceSizes(),
        Now = () => Now,
        Volume = _ => (143L << 30, 1024L << 30),
        Uses = () => DownloadUses.Read(_home, new HarnessSettings(), null),
    };

    private static JsonObject KindOf(JsonObject space, string id) =>
        space["kinds"]!.AsArray().Select(node => node!.AsObject()).Single(kind => (string?)kind["id"] == id);

    /// <summary>§6: a folder at each place past its days, both purged by the Keeping pass's removal, each place's record left clean.</summary>
    [Fact]
    public void A_folder_at_each_place_is_purged_at_its_days()
    {
        Assert.Equal(2, DaorisHome.Places(_home, HomeEntry.Trees).Count);
        var fresh = Aside(NewPlace, "s-new", Now.AddDays(-15), 100);
        var old = Aside(OldPlace, "s-old", Now.AddDays(-20), 200);
        var trees = new SessionTrees(_home);

        var run = trees.KeepSetAside(KeepPolicy.Default, Now, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(
            [(Path.GetFullPath(fresh), true, 100L), (Path.GetFullPath(old), true, 200L)],
            run.Purged.Select(each => (Path.GetFullPath(each.Folder), each.Purged, each.Bytes)).OrderBy(each => each.Item3));
        Assert.False(Directory.Exists(fresh));
        Assert.False(Directory.Exists(old));
        Assert.Empty(trees.SetAsideFailed());
    }

    /// <summary>§6: a folder at the old place within its days stays; one written into after its move is kept and said, at its days too.</summary>
    [Fact]
    public void A_folder_at_the_old_place_within_its_days_or_written_into_stays()
    {
        // A folder at the new place, so the trees entry answers it, as on the install since its layout moved.
        Aside(NewPlace, "s-new", Now.AddDays(-1), 10);
        var waiting = Aside(OldPlace, "s-wait", Now.AddDays(-3), 10);
        var written = Aside(OldPlace, "s-late", Now.AddDays(-20), 10);
        File.WriteAllText(Path.Combine(written, "late.txt"), "late\n");
        var trees = new SessionTrees(_home);

        var run = trees.KeepSetAside(KeepPolicy.Default, Now, TimeSpan.FromMinutes(1), CancellationToken.None);

        var kept = Assert.Single(run.Purged);
        Assert.Equal((Path.GetFullPath(written), false, SetAsideKept.Written), (Path.GetFullPath(kept.Folder), kept.Purged, kept.Code));
        Assert.Contains("late.txt", kept.Kept, StringComparison.Ordinal);
        Assert.True(Directory.Exists(waiting));
        Assert.True(Directory.Exists(written));
    }

    /// <summary>§6: the press's list, the trees' list and the work inventory name the folders at both places.</summary>
    [Fact]
    public async Task The_folders_at_both_places_are_listed()
    {
        Aside(NewPlace, "s-new", Now.AddDays(-2), 10);
        Aside(OldPlace, "s-old", Now.AddDays(-20), 10);
        var trees = new SessionTrees(_home);

        var listed = trees.ListSetAside(KeepPolicy.Default, Now);
        var all = await trees.ListAsync(CancellationToken.None);

        Assert.Equal(
            [("games/Daoris/s-new", false), ("games/Daoris/s-old", true)],
            listed.Select(each => (each.Id[..each.Id.LastIndexOf('-')], each.Past)).OrderBy(each => each.Item1, StringComparer.Ordinal));
        Assert.Equal(
            ["s-new", "s-old"],
            all.Where(each => each.Branch == SessionTree.SetAside)
                .Select(each => Path.GetFileName(each.Path)[..Path.GetFileName(each.Path).LastIndexOf('-')]).Order(StringComparer.Ordinal));
    }

    /// <summary>§6: the card counts both places' folders under set-aside, none under the rest, and its next reads both.</summary>
    [Fact]
    public void The_card_counts_both_places_under_set_aside_and_none_under_the_rest()
    {
        Aside(NewPlace, "s-new", Now.AddDays(-15), 1000);
        Aside(OldPlace, "s-old", Now.AddDays(-15).AddHours(-1), 2000);
        var world = World();
        foreach (var kind in SpaceKinds.Table) world.Sizes.MeasureNow(_home, kind.Id, WiringBudget.Default, CancellationToken.None);

        var space = SystemSpace.Read(world);

        Assert.Equal(3000, (long)KindOf(space, "set-aside")["bytes"]!);
        Assert.Equal(0, (long)KindOf(space, "trees")["bytes"]!);
        Assert.Equal(0, (long)KindOf(space, "rest")["bytes"]!);
        var next = KindOf(space, "set-aside")["next"]!;
        Assert.Equal(2, (int)next["count"]!);
        Assert.Equal(3000, (long)next["bytes"]!);
    }
}
