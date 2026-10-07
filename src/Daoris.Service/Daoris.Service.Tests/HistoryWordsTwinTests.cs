using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// HIST1m (D153's HIST1m note; the history-clearing design §6.3; <c>.claude/knowledge/twins.md</c>): every word the history
/// doors spell is held to one table, <c>fixtures/history-words.json</c> beside this suite, which the driver's
/// <c>HistoryWordsTwinTests</c> holds its constants to as well. Here each group's names are the desk's enum members, in their
/// order, and each is spelled as the table's word, so a word the desk adds, renames or respells fails until the table says it,
/// and the table's change fails the driver until it reads it.
/// </summary>
public sealed class HistoryWordsTwinTests
{
    /// <summary>Each group of the table this suite spells, and its words as the desk spells them, member by member.</summary>
    private static IReadOnlyList<(string Name, string Word)> Spelled(string group) => group switch
    {
        "kinds" => [.. Enum.GetValues<HistoryUnitKind>().Select(kind => (kind.ToString(), HistoryUnitRef.Spell(kind)))],
        "reasons" =>
        [
            .. Enum.GetValues<HistoryRefusal>().Where(refusal => refusal != HistoryRefusal.None)
                .Select(refusal => (refusal.ToString(), HistoryKept.Spell(refusal))),
        ],
        "waits" => [.. Enum.GetValues<HistoryWaiting>().Select(waiting => (waiting.ToString(), HistoryKept.Spell(waiting)))],
        "stands" => [.. Enum.GetValues<HistoryStands>().Select(stands => (stands.ToString(), HistoryKept.Spell(stands)))],
        "by" => [.. Enum.GetValues<HistoryAwaitedBy>().Select(by => (by.ToString(), HistoryKept.Spell(by)))],
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "a group of the table this suite does not spell"),
    };

    private static readonly string[] Spells = ["kinds", "reasons", "waits", "stands", "by"];

    public static TheoryData<string> Groups => [.. Spells];

    [Theory]
    [MemberData(nameof(Groups))]
    public void Each_word_the_desk_spells_is_the_tables_row_for_row(string group) =>
        Assert.Equal(Table(group), Spelled(group));

    /// <summary>
    /// The table holds no group nobody reads: each is one this suite spells, or <c>machine</c>, the driver's own words for this
    /// machine's half, which the desk never spells, by member or by word.
    /// </summary>
    [Fact]
    public void Every_group_is_read_and_the_desk_spells_none_of_the_machines_words()
    {
        using var table = Read();
        var groups = table.RootElement.EnumerateObject().Select(group => group.Name).Where(name => !name.StartsWith('_'));
        Assert.Equal(Spells.Append("machine").Order(), groups.Order());

        var machine = Table("machine");
        Assert.NotEmpty(machine);
        Assert.DoesNotContain(machine, row => Enum.TryParse<HistoryRefusal>(row.Name, out _));
        Assert.DoesNotContain(machine, row => Spelled("reasons").Any(reason => reason.Word == row.Word));
    }

    private static List<(string Name, string Word)> Table(string group)
    {
        using var table = Read();
        return [.. table.RootElement.GetProperty(group).EnumerateObject().Select(row => (row.Name, row.Value.GetString()!))];
    }

    private static JsonDocument Read() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "history-words.json")));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }
}
