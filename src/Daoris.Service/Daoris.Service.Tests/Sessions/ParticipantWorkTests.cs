using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1c (D212; the foundation design §2.2; <c>.claude/knowledge/twins.md</c>): the participant's work, read once from a record's
/// marks by one rule, and answered for every record of this machine on loopback. It is held to <c>fixtures/participant-work.json</c>
/// beside this suite, which the driver's <c>ParticipantWorkTests</c> and the page's <c>identity.test.ts</c> read as well, row for row:
/// what the rule answers, and where a reader of the same marks answered otherwise before it, each such row named and decided.
/// </summary>
public sealed class ParticipantWorkTests : McpToolsFixture
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-25T10:00:00Z");

    public static TheoryData<string> Rows => [.. Table.Read().Rows.Select(row => row.Name)];

    /// <summary>The closed set of works is the table's, member for member.</summary>
    [Fact]
    public void The_works_are_the_tables_closed_set()
    {
        Assert.Equal(Table.Read().Works.Order(StringComparer.Ordinal), ParticipantWork.Codes.Order(StringComparer.Ordinal));
    }

    /// <summary>🔴 Every row of the table: the marks a record carries answer the work the table says, a mirror's answering none.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void A_records_marks_answer_the_work_the_table_says(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);

        Assert.Equal(row.Work, ParticipantWork.Of(Record(row.Marks)));
    }

    /// <summary>
    /// Census A, the connector's half: where <see cref="ConnectorRoles"/>' reading of <c>serves</c> and <c>piece</c> (a record read from the ledger
    /// as the connector reads it) says a partner, the work says it as well, on every row; a difference would be a listed decision.
    /// </summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public async Task The_connector_reads_a_partner_where_the_work_does(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        var kept = await sessions.CreateAsync(
            null, row.Marks.Repository ?? "Owner", "stub", Now,
            kind: row.Marks.Kind == "chat" ? SessionKind.Chat : SessionKind.Driven,
            ask: row.Marks.Ask, opinion: row.Marks.Opinion, serves: row.Marks.Serves, piece: row.Marks.Piece);

        var role = await new SessionRoles(ledger, new IntakeScope(null, kept.Id)).ReadAsync(default);

        Assert.Equal(row.Today.ConnectorPartner, role.Partner);
        Assert.Equal(row.ByWork.ConnectorPartner, ParticipantWork.Of(Record(row.Marks)) == ParticipantWork.Partner);
    }

    /// <summary>
    /// The table names every difference between a reader's old answer and the work's, and decides it: a reader's two answers differ
    /// exactly where the row lists it, and each listed one names a decision the table holds.
    /// </summary>
    [Fact]
    public void Every_disagreement_is_listed_with_its_decision()
    {
        var table = Table.Read();
        foreach (var row in table.Rows)
        {
            var differs = table.Readers.Where(reader => !Equals(row.Today.Of(reader), row.ByWork.Of(reader))).Order(StringComparer.Ordinal);
            Assert.True(differs.SequenceEqual(row.Disagrees.Keys.Order(StringComparer.Ordinal)), row.Name);
            Assert.All(row.Disagrees.Values, decision => Assert.Contains(decision, table.Decisions));
        }

        Assert.Equal(["partner-drawn"], table.Rows.SelectMany(row => row.Disagrees.Where(each => each.Key == "drawn")).Select(each => each.Value).Distinct());
    }

    private static Session Record(Marks marks) =>
        new("id", null, marks.Repository ?? "Owner", "stub", SessionState.Working, null, null, null, Now, Now,
            Kind: marks.Kind == "chat" ? SessionKind.Chat : SessionKind.Driven)
        {
            Origin = marks.Mirror ? "alice-laptop" : null,
            Ask = marks.Ask, Opinion = marks.Opinion, Serves = marks.Serves, Piece = marks.Piece,
        };

    private sealed record Marks(bool Mirror, string? Kind, string? Repository, string? Ask, string? Serves, string? Piece, string? Opinion);

    private sealed record Answers(bool HoldsSlot, string Drawn, bool ConnectorPartner, bool Help)
    {
        public object Of(string reader) => reader switch
        {
            "holdsSlot" => HoldsSlot, "drawn" => Drawn, "connectorPartner" => ConnectorPartner, "help" => Help,
            _ => throw new ArgumentOutOfRangeException(nameof(reader), reader, "a reader the table does not name"),
        };
    }

    private sealed record Row(string Name, Marks Marks, string? Work, Answers Today, Answers ByWork, Dictionary<string, string> Disagrees);

    private sealed record Table(IReadOnlyList<string> Works, IReadOnlyList<string> Decisions, IReadOnlyList<Row> Rows)
    {
        public IReadOnlyList<string> Readers { get; } = ["holdsSlot", "drawn", "connectorPartner", "help"];

        public static Table Read()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
            var root = directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
            using var document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work.json")));
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            return new Table(
                [.. document.RootElement.GetProperty("works").EnumerateObject().Select(each => each.Name)],
                [.. document.RootElement.GetProperty("decisions").EnumerateObject().Select(each => each.Name)],
                document.RootElement.GetProperty("rows").Deserialize<List<Row>>(options)!);
        }
    }
}
