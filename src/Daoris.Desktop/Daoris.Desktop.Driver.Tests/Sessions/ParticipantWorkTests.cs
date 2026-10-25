using System.Reflection;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1c (D212; the foundation design §2.2; <c>.claude/knowledge/twins.md</c>): the participant's work, read once from the marks a
/// record carries, which each caller hands as a value, <see cref="WorkMarks"/>. The service's twin answers it as <c>work</c> on loopback;
/// both are held to <c>fixtures/participant-work.json</c> among the service's tests, which the page's <c>identity.test.ts</c> reads as
/// well, row for row: what the rule answers, what each reader of the same marks answered before it, and every place they differ, named
/// and decided there. The cap's slot is the work's table (<see cref="ActiveSessions.HoldsSlot(string?, string?, string?, string?, string?)"/>).
/// </summary>
public sealed class ParticipantWorkTests
{
    public static TheoryData<string> Rows => [.. Table.Read().Rows.Select(row => row.Name)];

    /// <summary>The closed set of works is the table's, each with the cap's slot the table says.</summary>
    [Fact]
    public void The_works_are_the_tables_closed_set_each_with_its_slot()
    {
        var table = Table.Read();
        var constants = typeof(ParticipantWork).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!).Order(StringComparer.Ordinal);

        Assert.Equal(table.Works.Keys.Order(StringComparer.Ordinal), constants);
        Assert.Equal(table.Works.Keys.Order(StringComparer.Ordinal), ParticipantWork.Codes.Order(StringComparer.Ordinal));
        Assert.All(table.Works, work => Assert.Equal(work.Value, ParticipantWork.HoldsSlot(work.Key)));
        Assert.False(ParticipantWork.HoldsSlot(null));
        // The Ask Daoris repository's spelling, held beside HelpRoom's, which the work's module may not name.
        Assert.Equal(HelpRoom.Repository, ParticipantWork.HelpRepository);
    }

    /// <summary>🔴 Every row of the table: the marks a record carries answer the work the table says, a mirror's answering none.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void A_records_marks_answer_the_work_the_table_says(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);

        Assert.Equal(row.Work, ParticipantWork.Of(row.Marks.Value));
    }

    /// <summary>
    /// Census A: on every row the readers of the same marks answer as they did, the cap's slot (<see cref="ActiveSessions.HoldsSlot(SessionView)"/>
    /// and its fields' form) and Ask Daoris's repository (<see cref="WordsNever.IsHelp"/>), and the work's table answers as the row's
    /// <c>byWork</c>; where the two differ the row says so and names its decision, the cap's slot being told no mirror by its callers.
    /// </summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void The_readers_of_the_same_marks_answer_as_they_did_and_the_works_table_as_the_row_says(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);
        var marks = row.Marks;
        var view = new SessionView("s1", marks.Repository ?? "", "working")
        {
            Kind = marks.Kind ?? ActiveSessions.DrivenKind, Ask = marks.Ask, Serves = marks.Serves, Opinion = marks.Opinion, Piece = marks.Piece,
        };

        Assert.Equal(row.Today.HoldsSlot, ActiveSessions.HoldsSlot(view));
        Assert.Equal(row.Today.HoldsSlot, ActiveSessions.HoldsSlot(marks.Kind, marks.Repository, marks.Ask, marks.Serves, marks.Opinion));
        Assert.Equal(row.Today.Help, WordsNever.IsHelp(new SessionRecord("s1", marks.Repository ?? "", "working")));

        var work = ParticipantWork.Of(marks.Value);
        Assert.Equal(row.ByWork.HoldsSlot, ParticipantWork.HoldsSlot(work));
        Assert.Equal(row.ByWork.Help, work == ParticipantWork.AskDaoris);
        Assert.Equal(row.ByWork.ConnectorPartner, work == ParticipantWork.Partner);
    }

    /// <summary>
    /// The table names every difference between a reader's old answer and the work's, and decides it: a reader's two answers differ
    /// exactly where the row lists it, and each listed one names a decision the table holds. Only the partner's drawing changes what a
    /// person sees (D203 point 1).
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

    /// <summary>
    /// The cap's slot reads the work's table and moves no answer: the planner's count over a mixed list is today's, a mirror's record
    /// (told no mirror) counted as driven work as it was.
    /// </summary>
    [Fact]
    public void The_cap_counts_the_sessions_the_driver_runs_as_it_did()
    {
        SessionView View(string kind, string repository, string? ask = null, string? serves = null, string? piece = null, string? opinion = null) =>
            new("s", repository, "working") { Kind = kind, Ask = ask, Serves = serves, Piece = piece, Opinion = opinion };

        var sessions = new[]
        {
            View("driven", "engine"), View("chat", "engine"), View("chat", HelpRoom.Repository), View("chat", "ask #7", ask: "7"),
            View("chat", "engine", opinion: "o1"), View("chat", "engine", serves: "lead"), View("chat", "engine", serves: "lead", piece: "p1"),
        };

        Assert.Equal(5, ActiveSessions.Held(sessions));
    }

    /// <summary>
    /// Row 10 of the field's chain (<c>session-record-fields.md</c>): the driver's record of a session reads the <c>work</c> the service
    /// answers, and a host from before it, whose record carries none, reads none, whatever its marks say.
    /// </summary>
    [Fact]
    public void A_record_reads_the_work_a_host_answers_and_none_from_a_host_before_it()
    {
        var records = ServiceClient.ReadRecords("""
            [
              { "id": "p1", "repository": "engine", "kind": "chat", "serves": "lead", "piece": "pc1", "work": "partner" },
              { "id": "h1", "repository": "engine", "kind": "chat", "serves": "lead" },
              { "id": "m1", "repository": "engine", "kind": "driven", "work": null }
            ]
            """);

        Assert.Equal("partner", records["p1"].Work);
        Assert.Null(records["h1"].Work);
        Assert.Null(records["m1"].Work);
    }

    /// <summary>Row 11: the stand-in ledger answers a record's work as the service does, from its marks, so a test's read of it passes on what the real service answers.</summary>
    [Fact]
    public async Task The_stand_in_ledger_answers_a_records_work_from_its_marks()
    {
        var ledger = new StandInLedger();
        ledger.Working("c1", "engine", "t1");
        ledger.Working("h1", "engine", "t2", serves: "lead");
        ledger.Working("p1", "engine", "t3", serves: "lead", piece: "pc1");
        ledger.Working("i1", "ask #7", "t4", ask: "7");
        ledger.Working("a1", HelpRoom.Repository, "t5");
        var client = ledger.Client();

        Assert.Equal(
            new string?[] { "chat", "help", "partner", "intake", "ask-daoris" },
            new string?[]
            {
                (await client.RecordAsync("c1"))!.Work, (await client.RecordAsync("h1"))!.Work, (await client.RecordAsync("p1"))!.Work,
                (await client.RecordAsync("i1"))!.Work, (await client.RecordAsync("a1"))!.Work,
            });

        var chats = new ChatLedger().Register("engine", "root");
        chats.Chat("c2", "working", "stub", "t6");
        Assert.Equal(ParticipantWork.Chat, (await chats.Client().RecordAsync("c2"))!.Work);
    }

    private sealed record Marks(bool Mirror, string? Kind, string? Repository, string? Ask, string? Serves, string? Piece, string? Opinion)
    {
        public WorkMarks Value => new(Mirror, Kind, Repository, Ask, Serves, Piece, Opinion);
    }

    private sealed record Answers(bool HoldsSlot, string Drawn, bool ConnectorPartner, bool Help)
    {
        public object Of(string reader) => reader switch
        {
            "holdsSlot" => HoldsSlot, "drawn" => Drawn, "connectorPartner" => ConnectorPartner, "help" => Help,
            _ => throw new ArgumentOutOfRangeException(nameof(reader), reader, "a reader the table does not name"),
        };
    }

    private sealed record Row(string Name, Marks Marks, string? Work, Answers Today, Answers ByWork, Dictionary<string, string> Disagrees);

    private sealed record Table(IReadOnlyDictionary<string, bool> Works, IReadOnlyList<string> Decisions, IReadOnlyList<Row> Rows)
    {
        public IReadOnlyList<string> Readers { get; } = ["holdsSlot", "drawn", "connectorPartner", "help"];

        public static Table Read()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work.json")));
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            return new Table(
                document.RootElement.GetProperty("works").EnumerateObject()
                    .ToDictionary(each => each.Name, each => each.Value.GetProperty("holdsSlot").GetBoolean()),
                [.. document.RootElement.GetProperty("decisions").EnumerateObject().Select(each => each.Name)],
                document.RootElement.GetProperty("rows").Deserialize<List<Row>>(options)!);
        }
    }
}
