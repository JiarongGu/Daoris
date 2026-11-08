using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A pooled slot holding shares over real git and real links: a take and a retirement never reach the shared place, a top holding a
/// share is partitioned at it and never entered, a share whose entry no longer reads as its link is the line's own, and a link that
/// leads elsewhere is refused as foreign.
/// </summary>
public sealed class TreePoolSharesGitTests : IDisposable
{
    private const string Rules = """
        { "form": 2, "rules": [
          { "kind": "local", "paths": ["/local/rules/"], "reach": "write" },
          { "kind": "local", "paths": ["/settings/shared/"], "reach": "write" }
        ] }
        """;

    private PoolGitFixture? _fixture;
    private readonly List<string> _slots = [];

    public void Dispose()
    {
        foreach (var slot in _slots) TreeLinks.Unlinked(slot);
        _fixture?.Dispose();
    }

    private async Task<PoolGitFixture> FixtureAsync()
    {
        var fixture = _fixture = await PoolGitFixture.CreateAsync(Rules);
        await File.AppendAllTextAsync(Path.Combine(fixture.Root, ".gitignore"), "local/\nsettings/\n");
        await GitAsync(fixture.Root, "commit", "--quiet", "-am", "ignore the shared places");
        fixture.Start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();
        Directory.CreateDirectory(Place(fixture, "local/rules"));
        Directory.CreateDirectory(Place(fixture, "settings/shared"));
        await File.WriteAllTextAsync(Place(fixture, "local/rules/house.md"), "the house rule\n");
        await File.WriteAllTextAsync(Place(fixture, "settings/shared/team.json"), "{}\n");
        return fixture;
    }

    private static string Place(PoolGitFixture fixture, string path) => Path.Combine(fixture.Root, path.Replace('/', Path.DirectorySeparatorChar));

    private static string In(KeptTree slot, string path) => Path.Combine(slot.Path, path.Replace('/', Path.DirectorySeparatorChar));

    private async Task<KeptTree> SharedAsync(PoolGitFixture fixture, TreePool pool, int number, string line)
    {
        var slot = await fixture.MadeAsync(pool, number, line);
        _slots.Add(slot.Path);
        await StageAsync(fixture, slot);
        return slot;
    }

    private static async Task StageAsync(PoolGitFixture fixture, KeptTree slot)
    {
        await new TreeFilesStage(fixture.Home, fixture.Root, slot.Path, PoolGitFixture.Repository).RunAsync(CancellationToken.None);
        Assert.True(File.GetAttributes(In(slot, "local/rules")).HasFlag(FileAttributes.ReparsePoint));
    }

    private static async Task EndAsync(PoolGitFixture fixture, TreePool pool, int number)
    {
        var ended = await pool.EndAsync(new PoolEnd(fixture.Site, number, "quest-1", "session-1"));
        Assert.True(ended.Ok, ended.Refused ?? ended.Stuck);
    }

    private static IReadOnlyList<string> Shared(PoolGitFixture fixture, KeptTree slot) =>
        [.. (TreeFilesRecord.Read(fixture.Home, slot.Path)?.Shares ?? []).Select(share => share.Path)];

    [Fact]
    public async Task A_take_and_a_retirement_never_delete_the_shared_places_files()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await SharedAsync(fixture, pool, 1, "s-aaaa0001");
        await File.WriteAllTextAsync(In(slot, "local/rules/learned.md"), "a write through the share\n");
        await EndAsync(fixture, pool, 1);

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.True(File.Exists(Place(fixture, "local/rules/house.md")));
        Assert.True(File.Exists(Place(fixture, "local/rules/learned.md")));
        Assert.True(File.Exists(Place(fixture, "settings/shared/team.json")));
        Assert.Empty(Shared(fixture, slot));

        await StageAsync(fixture, slot);
        Assert.Equal(["local/rules/", "settings/shared/"], Shared(fixture, slot).Order(StringComparer.Ordinal));
        await EndAsync(fixture, pool, 1);
        var retired = await pool.RetireAsync(fixture.Site, 1, "quest-1");

        Assert.True(retired.Ok, retired.Refused ?? retired.Stuck);
        Assert.True(File.Exists(Place(fixture, "local/rules/house.md")));
        Assert.True(File.Exists(Place(fixture, "local/rules/learned.md")));
        Assert.True(File.Exists(Place(fixture, "settings/shared/team.json")));
        var aside = Directory.EnumerateDirectories(Path.Combine(DaorisHome.Entry(fixture.Home, HomeEntry.Trees), ".tidied", PoolGitFixture.Workspace, PoolGitFixture.Repository)).Single();
        Assert.False(Path.Exists(Path.Combine(aside, "local", "rules")));
        Assert.False(Path.Exists(Path.Combine(aside, "settings", "shared")));
    }

    [Fact]
    public async Task A_top_holding_a_share_is_partitioned_so_a_file_the_line_wrote_beside_it_refuses_the_take_and_the_share_does_not()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await SharedAsync(fixture, pool, 1, "s-aaaa0001");
        await File.WriteAllTextAsync(In(slot, "settings/mine.txt"), "the line's own\n");
        await EndAsync(fixture, pool, 1);

        var refused = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.Contains(SlotJudgement.LinesOwn, refused.Refused);
        var status = await FillJudgement.StatusAsync(slot.Path, ignored: true, CancellationToken.None);
        var sorted = await SlotIgnored.ReadAsync(fixture.Site, slot.Path, status, fixture.Start, CancellationToken.None);
        Assert.Equal(["settings/mine.txt"], sorted.Own);
        Assert.Equal(["local/rules/", "settings/shared/"], sorted.Shared.Order(StringComparer.Ordinal));

        File.Delete(In(slot, "settings/mine.txt"));
        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));
        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
    }

    [Fact]
    public async Task A_top_holding_only_a_copy_and_a_share_is_Daoris_s_own_and_the_partition_never_enters_the_link()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await SharedAsync(fixture, pool, 1, "s-aaaa0001");
        await File.WriteAllTextAsync(In(slot, "local/copied.txt"), "a copy\n");
        TreeFilesRecord.Change(fixture.Home, slot.Path, state => state with
        {
            Copies = new Dictionary<string, string> { ["local/copied.txt"] = TreeCopies.HashOf(In(slot, "local/copied.txt"))! },
        });
        await File.WriteAllTextAsync(Place(fixture, "local/rules/inside.md"), "never listed\n");
        var listed = new List<string>();
        TopPartition.Listing.Value = listed.Add;

        var status = await FillJudgement.StatusAsync(slot.Path, ignored: true, CancellationToken.None);
        var sorted = await SlotIgnored.ReadAsync(fixture.Site, slot.Path, status, fixture.Start, CancellationToken.None);

        TopPartition.Listing.Value = null;
        Assert.Empty(sorted.Own);
        Assert.Equal(["local/copied.txt"], sorted.Copies);
        Assert.Contains("local/", listed);
        Assert.DoesNotContain(listed, folder => folder.StartsWith("local/rules", StringComparison.Ordinal) || folder.StartsWith("settings/shared", StringComparison.Ordinal));
        await EndAsync(fixture, pool, 1);
        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));
        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.True(File.Exists(Place(fixture, "local/rules/inside.md")));
    }

    [Fact]
    public async Task A_junction_the_line_replaced_with_a_folder_is_its_own_refuses_the_take_and_goes_aside_at_its_finish()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await SharedAsync(fixture, pool, 1, "s-aaaa0001");
        await File.WriteAllTextAsync(In(slot, "src/Work.cs"), "class Work {}\n");
        await GitAsync(slot.Path, "add", "src/Work.cs");
        await GitAsync(slot.Path, "commit", "--quiet", "-m", "work");
        Directory.Delete(In(slot, "local/rules"), recursive: false);
        Directory.CreateDirectory(In(slot, "local/rules"));
        await File.WriteAllTextAsync(In(slot, "local/rules/mine.md"), "the line's rule\n");
        await EndAsync(fixture, pool, 1);

        var refused = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.Contains(SlotJudgement.LinesOwn, refused.Refused);
        var record = TreeFilesRecord.Read(fixture.Home, slot.Path)!;
        Assert.DoesNotContain("local/rules/", record.Shares.Select(share => share.Path));
        Assert.Contains("local/rules/", record.Lost);

        var finished = await pool.FinishAsync(new PoolFinish(fixture.Site, "quest-1", "s-aaaa0001", "main"));

        Assert.True(finished.Ok, finished.Refused ?? finished.Stuck);
        Assert.False(Path.Exists(In(slot, "local/rules")));
        var aside = Directory.EnumerateFiles(TreePoolFinishes.SetAsideRoot(fixture.Site), "mine.md", SearchOption.AllDirectories).Single();
        Assert.Equal("the line's rule\n", await File.ReadAllTextAsync(aside));
        Assert.True(File.Exists(Place(fixture, "local/rules/house.md")));
        Assert.True(File.Exists(Place(fixture, "settings/shared/team.json")));
    }

    [Fact]
    public async Task A_share_whose_link_was_pointed_elsewhere_is_refused_as_a_foreign_link_and_its_target_is_never_touched()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await SharedAsync(fixture, pool, 1, "s-aaaa0001");
        var elsewhere = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        await File.WriteAllTextAsync(Path.Combine(elsewhere, "theirs.md"), "not the checkout's\n");
        Directory.Delete(In(slot, "local/rules"), recursive: false);
        DependencyLinks.Link(In(slot, "local/rules"), elsewhere);
        await EndAsync(fixture, pool, 1);

        var refused = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));
        var retired = await pool.RetireAsync(fixture.Site, 1, "quest-1");

        Assert.Contains(SlotJudgement.Linked, refused.Refused);
        Assert.Contains(SlotJudgement.Linked, retired.Refused);
        Assert.DoesNotContain("local/rules/", Shared(fixture, slot));
        Assert.True(File.Exists(Path.Combine(elsewhere, "theirs.md")));
        Assert.True(File.Exists(Place(fixture, "local/rules/house.md")));
    }
}
