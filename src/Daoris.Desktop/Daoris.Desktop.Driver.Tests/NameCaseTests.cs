using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A name compared without case (CASEFOLD1, CASEFOLD1d; D125's notes): every driver twin of a CLI file compares a name by
/// .NET's <c>OrdinalIgnoreCase</c>, each code point to its one capital and never a wider one, and every CLI twin compares
/// through <c>casefold.ts</c>, whose table, the CLI's <c>test/fixtures/name-case.json</c>, is .NET's answers, measured. The
/// driver's half reads the same table, row for row: whether each pair is one name, and the sign the pair orders by. A row
/// this runtime answers otherwise is a driver that differs from the CLI's measured fold.
/// </summary>
public sealed class NameCaseTests
{
    /// <summary>The table, as the CLI's suite reads it: each row why, two names, whether they are one, and the sign they order by.</summary>
    private static readonly IReadOnlyList<(string Why, string A, string B, bool Same, int Order)> Rows = ReadRows();

    [Fact]
    public void Two_names_are_one_as_the_table_says()
    {
        foreach (var (why, a, b, same, _) in Rows)
        {
            Assert.True(string.Equals(a, b, StringComparison.OrdinalIgnoreCase) == same, why);
            Assert.True(string.Equals(b, a, StringComparison.OrdinalIgnoreCase) == same, $"{why}, the other way");
            // Every map a twin keys by name is built on the comparer, which finds a key by its hash first.
            if (same) Assert.True(StringComparer.OrdinalIgnoreCase.GetHashCode(a) == StringComparer.OrdinalIgnoreCase.GetHashCode(b), $"{why}, hashed");
        }

        Assert.Contains(Rows, row => row.Same);
        Assert.Contains(Rows, row => !row.Same);
    }

    [Fact]
    public void Two_names_order_as_the_table_says()
    {
        foreach (var (why, a, b, _, order) in Rows)
        {
            Assert.True(Math.Sign(string.Compare(a, b, StringComparison.OrdinalIgnoreCase)) == order, why);
            Assert.True(Math.Sign(string.Compare(b, a, StringComparison.OrdinalIgnoreCase)) == -order, $"{why}, the other way");
        }
    }

    private static IReadOnlyList<(string, string, string, bool, int)> ReadRows()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "name-case.json")));
        return
        [
            .. table.RootElement.EnumerateArray()
                .Select(row => (row[0].GetString()!, row[1].GetString()!, row[2].GetString()!, row[3].GetBoolean(), row[4].GetInt32())),
        ];
    }
}
