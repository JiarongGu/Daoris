using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The salvage kind: a migration's folder whose newest file is thirty days old goes once a day, the card says when the next goes, and
/// <c>space free</c> lists each folder as a group to take early and removes the one a press names.
/// </summary>
public sealed class SalvageKeepingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "salvage-keeping", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Folder(string migration, int daysOld, int bytes = 4)
    {
        var folder = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Salvage), migration);
        var file = Path.Combine(folder, "data", "state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, new byte[bytes]);
        var made = Now.UtcDateTime.AddDays(-daysOld);
        File.SetCreationTimeUtc(file, made);
        File.SetLastWriteTimeUtc(file, made);
        foreach (var path in new[] { Path.GetDirectoryName(file)!, folder })
        {
            Directory.SetCreationTimeUtc(path, made);
            Directory.SetLastWriteTimeUtc(path, made);
        }

        return folder;
    }

    [Fact]
    public void A_folder_whose_newest_file_is_thirty_days_old_goes_and_a_younger_one_stays()
    {
        var old = Folder("m1", 31, 7);
        var young = Folder("m2", 29);
        var keeping = new SalvageKeeping(() => Now);

        Assert.True(keeping.Due(_home, KeepPolicy.Default));
        var (removed, bytes) = keeping.Keep(_home, KeepPolicy.Default);

        Assert.Equal((1, 7L), (removed, bytes));
        Assert.False(Directory.Exists(old));
        Assert.True(Directory.Exists(young));
        Assert.False(keeping.Due(_home, KeepPolicy.Default));
    }

    [Fact]
    public void A_folder_a_later_step_wrote_into_is_judged_by_its_newest_file()
    {
        var folder = Folder("m1", 40);
        var late = Path.Combine(folder, "data", "later.json");
        File.WriteAllBytes(late, new byte[2]);
        File.SetCreationTimeUtc(late, Now.UtcDateTime.AddDays(-2));
        File.SetLastWriteTimeUtc(late, Now.UtcDateTime.AddDays(-2));

        var keeping = new SalvageKeeping(() => Now);

        Assert.False(keeping.Due(_home, KeepPolicy.Default));
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void The_card_names_the_earliest_day_and_the_kind_sits_in_the_tables()
    {
        Folder("m1", 10, 5);
        Folder("m2", 3);

        var next = SalvageKeeping.Next(_home, KeepPolicy.Default)!;

        Assert.Equal(1, (int)next["count"]!);
        Assert.Equal(5L, (long)next["bytes"]!);
        Assert.Contains(SpaceKinds.Table, kind => kind.Id == "salvage" && kind.Keeps == KeepKinds.Salvage && kind.Rule == "days");
        Assert.Equal([Path.Combine(DaorisHome.Entry(_home, HomeEntry.Salvage))], SpaceKinds.Places(_home, "salvage").Places);
        Assert.Contains(SpaceKinds.Places(_home, "rest").Skip, place => place == DaorisHome.Entry(_home, HomeEntry.Salvage));
    }

    [Fact]
    public async Task Space_free_lists_each_folder_early_and_removes_the_one_pressed()
    {
        var first = Folder("m1", 5);
        var second = Folder("m2", 6);
        var world = new SpaceFreeWorld(_home)
        {
            Sizes = new SpaceSizes(),
            Now = () => Now,
            Judge = () => new DownloadJudge(() => DownloadUses.Read(_home, new HarnessSettings(), null), () => Now),
        };

        var plan = SpaceFree.Plan(world);

        Assert.Contains("salvage", SpacePlan.Groups);
        Assert.Equal(["m1", "m2"], plan.Units.Where(unit => unit.Kind == "salvage").Select(unit => unit.Id).Order(StringComparer.Ordinal));
        var asked = plan.Units.Where(unit => unit.Kind == "salvage" && unit.Id == "m1")
            .Select(unit => new SpaceAsked(unit.Kind, unit.Id, unit.Early, unit.Why, false)).ToList();
        var run = Assert.IsType<SpaceRun>(new SpaceRuns().Start(world, KeepingRun.Press, asked).Run);
        await run.Finished.WaitAsync(Poll.Patience);

        Assert.False(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
        Assert.Equal(1, run.Kinds.Single(kind => kind.Id == "salvage").Removed);
    }
}
