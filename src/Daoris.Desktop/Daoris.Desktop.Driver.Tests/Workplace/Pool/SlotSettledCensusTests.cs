using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every row a settling claim leaves is built by <c>SlotStanding</c> from what the slot's disk shows, so no act names a line its disk does
/// not hold: a <c>SlotSettled</c> built anywhere else under the pool's folder fails, as does a claim ended with no row where the list does
/// not say why the row it leaves still holds.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class SlotSettledCensusTests
{
    private const string Standing = "Workplace/Pool/SlotStanding.cs";

    [Fact]
    public void Every_settled_row_of_the_pool_is_built_by_slot_standing()
    {
        var pool = Pool();
        Assert.Contains(pool, each => each.File == Standing);

        var elsewhere = pool.Where(each => each.File != Standing && Built().IsMatch(PoolCensusScan.Code(each.Source))).Select(each => each.File).ToList();

        Assert.True(elsewhere.Count == 0, "a slot's settled row is built outside SlotStanding, which alone reads it from the disk:\n" + string.Join('\n', elsewhere));
    }

    [Fact]
    public void Every_claim_ended_with_the_slots_row_left_as_it_was_is_listed_with_why_the_row_still_holds()
    {
        var found = Pool().Select(each => (each.File, Count: LeftAlone().Count(PoolCensusScan.Code(each.Source))))
            .Where(each => each.Count > 0).ToDictionary(each => each.File, each => each.Count, StringComparer.Ordinal);

        var differ = found.Where(each => !LeftAsItWas.TryGetValue(each.Key, out var listed) || listed.Count != each.Value).Select(each => $"{each.Key}: {each.Value}")
            .Concat(LeftAsItWas.Keys.Where(file => !found.ContainsKey(file)).Select(file => $"{file}: 0")).ToList();

        Assert.True(differ.Count == 0, "a claim is ended with no row where the list does not say why its row still holds; settle it by SlotStanding, or list it:\n" + string.Join('\n', differ));
    }

    /// <summary>The ends of a claim that leave the slot's row untouched, by file, and why the row still says what the disk holds at each.</summary>
    private static readonly Dictionary<string, (int Count, string Why)> LeftAsItWas = new(StringComparer.Ordinal)
    {
        ["Workplace/Pool/TreePoolTakes.cs"] = (4, "no job, a lock refused and a judgement refused, each before the switch; a make whose open never landed, whose folder goes"),
        ["Workplace/Pool/PoolTakeovers.cs"] = (2, "an end, which switches nothing; a make whose open never landed, whose folder goes"),
        ["Workplace/Pool/TreePoolRetires.cs"] = (6, "a retirement refused before its rename, a share's link that would not go among them, which leaves the folder where it was"),
        ["Workplace/Pool/TreePoolFinishes.cs"] = (3, "no job and a lock refused before anything moved; a release once what moved is put back"),
        ["Workplace/Pool/PoolFinishRecoveries.cs"] = (2, "a finish stopped before it moved anything or with what moved put back; a retirement stopped before its rename"),
        ["Workplace/Pool/TreePoolEnds.cs"] = (1, "an end with no job, before anything is read"),
        ["Workplace/Pool/TreePoolMakes.cs"] = (4, "a make, whose slot has no row until its open lands"),
    };

    private static List<(string File, string Source)> Pool()
    {
        var driver = SourceFiles.Project("Daoris.Desktop.Driver");
        return [.. SourceFiles.All("Daoris.Desktop.Driver", "*.cs")
            .Select(file => (File: Path.GetRelativePath(driver, file).Replace('\\', '/'), Source: File.ReadAllText(file)))
            .Where(each => each.File.StartsWith("Workplace/Pool/", StringComparison.Ordinal))];
    }

    [GeneratedRegex(@"\bnew\s+SlotSettled\s*\(")]
    private static partial Regex Built();

    [GeneratedRegex(@"\.Complete\(\s*\w+\s*,\s*null\s*\)")]
    private static partial Regex LeftAlone();
}
