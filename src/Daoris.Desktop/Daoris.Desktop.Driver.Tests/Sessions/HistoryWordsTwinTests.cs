using System.Reflection;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HIST1m (D153's HIST1m note; the history-clearing design §6.3; <c>.claude/knowledge/twins.md</c>): every word the service's
/// history doors spell and this driver reads is held to one table, the service suite's <c>fixtures/history-words.json</c>, which
/// the service's <c>HistoryWordsTwinTests</c> holds the desk's enums to as well. Here each class's constants are the table's
/// rows, name for name and word for word, so a word the service adds fails until the table says it, and then fails here until
/// the driver has a constant for it; a constant added here alone fails here.
/// </summary>
public sealed class HistoryWordsTwinTests
{
    [Theory]
    [InlineData("kinds", typeof(HistoryKinds))]
    [InlineData("waits", typeof(HistoryWaits))]
    [InlineData("stands", typeof(HistoryStands))]
    [InlineData("by", typeof(HistoryAwaitedBy))]
    public void Each_word_the_driver_reads_is_the_tables_row_for_row(string group, Type words) =>
        Assert.Equal(Table(group).OrderBy(row => row.Name, StringComparer.Ordinal), Constants(words));

    /// <summary>
    /// The refusal's words are the service's reasons and this machine's own (design §1.2: a tree here, a landing's standing
    /// branch), which the service never spells: together they are every word <see cref="HistoryWords"/> holds.
    /// </summary>
    [Fact]
    public void The_refusals_words_are_the_services_reasons_and_this_machines_own() =>
        Assert.Equal(
            Table("reasons").Concat(Table("machine")).OrderBy(row => row.Name, StringComparer.Ordinal),
            Constants(typeof(HistoryWords)));

    /// <summary>A class's string constants, each its name and its word, by name.</summary>
    private static List<(string Name, string Word)> Constants(Type words) =>
    [
        .. words.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (field.Name, (string)field.GetRawConstantValue()!))
            .OrderBy(row => row.Name, StringComparer.Ordinal),
    ];

    /// <summary>One group of the shared table, each row its name and its word.</summary>
    private static List<(string Name, string Word)> Table(string group)
    {
        using var table = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "history-words.json")));
        return [.. table.RootElement.GetProperty(group).EnumerateObject().Select(row => (row.Name, row.Value.GetString()!))];
    }
}
