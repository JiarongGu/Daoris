using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// COMMS1a (the comms design §3.3): who said a kept word, read once from its marks by <see cref="Senders.Of"/>. It is held to
/// <c>fixtures/senders.json</c> beside this suite, which the driver's <c>SendersTests</c> and the page's <c>sender.test.ts</c> read as
/// well, row for row; a reader's old answer that differs is named and decided there.
/// </summary>
public sealed class SendersTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-02T10:00:00Z");

    public static TheoryData<string> Rows => [.. Table.Read().Rows.Where(row => row.Word is not null).Select(row => row.Name)];

    /// <summary>🔴 Every row of the table that has a kept word: its marks answer the sender the table says.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void A_words_marks_answer_the_sender_the_table_says(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);

        Assert.Equal(row.Sender, Senders.Of(Word(row.Word!)));
    }

    /// <summary>The word's <c>Persons</c> is the person being its sender, as it answered before the rule on every row it could read.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void Persons_is_the_person_being_the_sender(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);
        var word = Word(row.Word!);

        Assert.Equal(row.Sender == Senders.Person, word.Persons);
        Assert.Equal(row.Today.Persons, word.Persons);
    }

    /// <summary>The table's closed set of senders is the rule's, member for member.</summary>
    [Fact]
    public void The_senders_are_the_tables_closed_set()
    {
        var table = Table.Read();
        Assert.Equal(table.Senders.Where(each => each != "composed").Order(StringComparer.Ordinal), Senders.Codes.Order(StringComparer.Ordinal));
        Assert.All(table.Rows, row => Assert.Contains(row.Sender, table.Senders));
    }

    /// <summary>A reader's old answer differs from the rule's exactly where the row lists it, each naming a decision the table holds.</summary>
    [Fact]
    public void Every_disagreement_is_listed_with_its_decision()
    {
        var table = Table.Read();
        foreach (var row in table.Rows)
        {
            var differs = new List<string>();
            if (row.Today.Persons is { } persons && persons != (row.Sender == Senders.Person)) differs.Add("persons");
            if (row.Today.Drawn != row.Sender) differs.Add("drawn");
            Assert.True(differs.Order(StringComparer.Ordinal).SequenceEqual(row.Disagrees.Keys.Order(StringComparer.Ordinal)), row.Name);
            Assert.All(row.Disagrees.Values, decision => Assert.Contains(decision, table.Decisions));
        }
    }

    private static SaidWord Word(WordMarks marks) =>
        new("w1", "said", Now, [], By: marks.By, Helper: marks.Helper, Channel: marks.Channel, From: marks.From, Key: marks.Key);

    private sealed record WordMarks(string? From = null, string? By = null, string? Helper = null, string? Key = null, string? Channel = null);

    private sealed record Today(bool? Persons, string Drawn);

    private sealed record Row(string Name, WordMarks? Word, string Sender, Today Today, Dictionary<string, string> Disagrees);

    private sealed record Table(IReadOnlyList<string> Senders, IReadOnlyList<string> Decisions, IReadOnlyList<Row> Rows)
    {
        public static Table Read()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
            var root = directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
            using var document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "senders.json")));
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            return new Table(
                document.RootElement.GetProperty("senders").Deserialize<List<string>>(options)!,
                [.. document.RootElement.GetProperty("decisions").EnumerateObject().Select(each => each.Name)],
                document.RootElement.GetProperty("rows").Deserialize<List<Row>>(options)!);
        }
    }
}
