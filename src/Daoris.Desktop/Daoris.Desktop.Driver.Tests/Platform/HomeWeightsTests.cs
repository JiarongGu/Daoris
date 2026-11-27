using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the home weighs: each place over its entry's budget, the largest files, and the groups of identical files held more than once,
/// from the one walk of <see cref="HomeStrays"/>; a file under two names of one record is one copy.
/// </summary>
public sealed class HomeWeightsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-weights-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Put(string relative, byte[] bytes)
    {
        var path = Path.Combine(_home, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Bytes(int count, byte fill) => Enumerable.Repeat(fill, count).ToArray();

    private HomeWeightReading Read(long threshold = 100, Func<string, long>? budgets = null) =>
        HomeWeights.Read(_home, WiringBudget.Default, budgets ?? (_ => long.MaxValue), threshold, top: 10);

    [Fact]
    public void A_place_over_its_entry_s_budget_is_named_with_both_and_one_under_is_not()
    {
        Put("work/trees/games/Daoris/s-aaaa1111/a.bin", Bytes(900, 1));
        Put("work/trees/games/Daoris/s-bbbb2222/a.bin", Bytes(300, 2));

        var reading = Read(budgets: entry => entry == "trees" ? 500 : long.MaxValue);

        var over = Assert.Single(reading.Over);
        Assert.Equal(("work/trees/games/Daoris/s-aaaa1111", "trees", 900L, 500L), (over.Path, over.Entry, over.Bytes, over.Budget));
    }

    [Fact]
    public void Every_entry_of_the_table_has_a_budget_and_a_default_is_never_unbounded()
    {
        foreach (var entry in HomeEntry.Table)
        {
            Assert.InRange(HomeBudgets.Of(entry.Name), 1, long.MaxValue - 1);
        }
    }

    [Fact]
    public void The_largest_files_are_listed_biggest_first_and_a_file_under_the_threshold_is_not()
    {
        Put("data/sessions/big.bin", Bytes(5000, 1));
        Put("data/sessions/mid.bin", Bytes(2000, 2));
        Put("data/sessions/small.bin", Bytes(50, 3));

        var reading = Read();

        Assert.Equal(["data/sessions/big.bin", "data/sessions/mid.bin"], reading.Largest.Select(each => each.Path));
        Assert.Equal([5000L, 2000L], reading.Largest.Select(each => each.Bytes));
    }

    [Fact]
    public void Two_identical_files_are_one_group_with_what_the_second_copy_costs_and_a_different_file_of_the_size_is_not()
    {
        Put("work/trees/games/Daoris/s-aaaa1111/lib.dll", Bytes(1000, 7));
        Put("work/trees/games/Daoris/s-bbbb2222/lib.dll", Bytes(1000, 7));
        Put("work/trees/games/Daoris/s-cccc3333/other.dll", Bytes(1000, 8));

        var group = Assert.Single(Read().Duplicates);

        Assert.Equal(1000L, group.Bytes);
        Assert.Equal(1000L, group.Wasted);
        Assert.Equal(
            ["work/trees/games/Daoris/s-aaaa1111/lib.dll", "work/trees/games/Daoris/s-bbbb2222/lib.dll"], group.Paths.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_file_linked_under_two_names_is_one_copy_and_no_duplicate()
    {
        var one = Put("work/trees/games/Daoris/s-aaaa1111/lib.dll", Bytes(1000, 7));
        var beside = Put("work/trees/games/Daoris/s-bbbb2222/keep.txt", Bytes(1, 1));
        var two = Path.Combine(Path.GetDirectoryName(beside)!, "lib.dll");
        Assert.True(FileLinks.Make(two, one), "the platform makes a hard link");

        Assert.Empty(Read().Duplicates);
    }

    [Fact]
    public void A_home_whose_walk_stops_early_says_so()
    {
        Put("data/sessions/a.bin", Bytes(500, 1));
        Put("data/sessions/b.bin", Bytes(500, 1));
        Put("data/sessions/c.bin", Bytes(500, 1));

        var reading = HomeWeights.Read(_home, new WiringBudget(1, TimeSpan.FromSeconds(10)), _ => long.MaxValue, 100, top: 10);

        Assert.NotNull(reading.Partial);
    }

    [Fact]
    public void Disk_space_says_what_the_home_weighs_and_nothing_while_the_first_count_runs()
    {
        var reading = new HomeWeightReading(
            [new HomeOver("work/trees/games/Daoris/s-aaaa1111", "trees", 900, 500)],
            [new HomeBigFile("data/knowledge.db", 5000)],
            [new HomeDuplicates(1000, ["a/lib.dll", "b/lib.dll"])],
            Partial: null);
        var now = new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero);

        var weight = SystemSpace.Read(new SpaceWorld(_home) { Sizes = new SpaceSizes(), Now = () => now, Weights = (_, _) => reading })["weight"]!;
        var counting = SystemSpace.Read(new SpaceWorld(_home) { Sizes = new SpaceSizes(), Now = () => now, Weights = (_, _) => null })["weight"];

        Assert.Equal(500, weight["over"]![0]!["budget"]!.GetValue<long>());
        Assert.Equal(5000, weight["largest"]![0]!["bytes"]!.GetValue<long>());
        Assert.Equal(1000, weight["duplicates"]![0]!["wasted"]!.GetValue<long>());
        Assert.False(weight["partial"]!.GetValue<bool>());
        Assert.Null(counting);
    }
}
