using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSCTX1f (D189's SESSCTX1f note; the session-context design §3.5): a session's event file is the contract between this writer and
/// the service's <c>LineEvents</c>, which the connector's <c>line_read</c> pages a line's history with and which shares no code with it,
/// so each side holds the shape. Here, the file as <see cref="SessionEvents.Append"/> writes it; the service's suite
/// (<c>LineEventsShapeTests</c>) reads the same text, line for line.
/// </summary>
public sealed class LineEventFilesTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-line-events-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DateTimeOffset At(int second) => new(2026, 10, 20, 10, 0, second, TimeSpan.Zero);

    /// <summary>
    /// 🔴 The fourteen events the service's table reads: a brief, the person's words with a file, the agent's chunks across a usage
    /// reading, a thought, a tool call and its update, a plan, words shown while they waited and where they were taken, a turn's end,
    /// Daoris's note and a receipt, written as the service reads them.
    /// </summary>
    [Fact]
    public void The_event_file_is_written_as_the_services_line_reader_reads_it()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        SessionEvent[] said =
        [
            new() { Kind = SessionEventKind.User, Text = "The brief of the line.", Origin = "target", Continues = "a1", At = At(0) },
            new() { Kind = SessionEventKind.User, Id = "w1", Text = "Add a dark mode.\n深色", Origin = "person", Files = ["shot.png"], At = At(1) },
            new() { Kind = SessionEventKind.Message, Text = "I will ", At = At(2) },
            new() { Kind = SessionEventKind.Usage, Used = 100, Size = 1000, At = At(3) },
            new() { Kind = SessionEventKind.Message, Text = "read the theme first.", At = At(4) },
            new() { Kind = SessionEventKind.Thought, Text = "Where is the theme?", At = At(5) },
            new()
            {
                Kind = SessionEventKind.Tool, Id = "t1", Title = "Read theme.ts", ToolKind = "read", Status = "pending",
                Locations = ["src/theme.ts"], Input = "{\"path\":\"theme.ts\"}", At = At(6),
            },
            new() { Kind = SessionEventKind.Tool, Id = "t1", Status = "completed", Output = "export const theme = {};", At = At(7) },
            new()
            {
                Kind = SessionEventKind.Plan, Entries = [new("Read the theme", "completed"), new("Add the toggle", "in_progress")], At = At(8),
            },
            new() { Kind = SessionEventKind.User, Id = "w2", Text = "Use the system setting.", Origin = "person", Reaches = "turn-end", At = At(9) },
            new() { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = At(10) },
            new() { Kind = SessionEventKind.User, Id = "w2", Text = "Use the system setting.", Origin = "person", At = At(11) },
            new() { Kind = SessionEventKind.Note, Text = "The account is near its limit.", At = At(12) },
            new() { Kind = SessionEventKind.Receipt, Id = "w2", Receipt = Receipts.Answered, At = At(13) },
        ];

        foreach (var e in said) events.Append("b2", e);

        Assert.Equal(string.Join("\n", ServiceTable) + "\n", File.ReadAllText(events.PathOf("b2")));
    }

    /// <summary>The service's table, <c>LineEventsShapeTests.DriverFile</c>, line for line.</summary>
    private static readonly string[] ServiceTable =
    [
        """{"seq":1,"at":"2026-10-20T10:00:00+00:00","kind":"user","text":"The brief of the line.","origin":"target","continues":"a1"}""",
        """{"seq":2,"at":"2026-10-20T10:00:01+00:00","kind":"user","id":"w1","text":"Add a dark mode.\n深色","origin":"person","files":["shot.png"]}""",
        """{"seq":3,"at":"2026-10-20T10:00:02+00:00","kind":"message","text":"I will "}""",
        """{"seq":4,"at":"2026-10-20T10:00:03+00:00","kind":"usage","used":100,"size":1000}""",
        """{"seq":5,"at":"2026-10-20T10:00:04+00:00","kind":"message","text":"read the theme first."}""",
        """{"seq":6,"at":"2026-10-20T10:00:05+00:00","kind":"thought","text":"Where is the theme?"}""",
        """{"seq":7,"at":"2026-10-20T10:00:06+00:00","kind":"tool","id":"t1","title":"Read theme.ts","toolKind":"read","status":"pending","locations":["src/theme.ts"],"input":"{\"path\":\"theme.ts\"}"}""",
        """{"seq":8,"at":"2026-10-20T10:00:07+00:00","kind":"tool","id":"t1","status":"completed","output":"export const theme = {};"}""",
        """{"seq":9,"at":"2026-10-20T10:00:08+00:00","kind":"plan","entries":[{"content":"Read the theme","status":"completed"},{"content":"Add the toggle","status":"in_progress"}]}""",
        """{"seq":10,"at":"2026-10-20T10:00:09+00:00","kind":"user","id":"w2","text":"Use the system setting.","origin":"person","reaches":"turn-end"}""",
        """{"seq":11,"at":"2026-10-20T10:00:10+00:00","kind":"turn","stopReason":"end_turn"}""",
        """{"seq":12,"at":"2026-10-20T10:00:11+00:00","kind":"user","id":"w2","text":"Use the system setting.","origin":"person"}""",
        """{"seq":13,"at":"2026-10-20T10:00:12+00:00","kind":"note","text":"The account is near its limit."}""",
        """{"seq":14,"at":"2026-10-20T10:00:13+00:00","kind":"receipt","id":"w2","receipt":"answered"}""",
    ];

    /// <summary>
    /// The rows the service's <c>LineEvents.IsLocalId</c> holds (its suite's <c>LineEventsShapeTests</c>): an id of this machine's names a
    /// file under the home; a remote's <c>machine:id</c>, a teammate's <c>origin/id</c> and anything that is a path never do.
    /// </summary>
    [Theory]
    [InlineData("a1b2c3d4", true)]
    [InlineData("c0nv0001", true)]
    [InlineData("x.y-z_1", true)]
    [InlineData("", false)]
    [InlineData("m1:a1b2c3d4", false)]
    [InlineData("origin/a1b2c3d4", false)]
    [InlineData("..", false)]
    [InlineData("a1/../x", false)]
    [InlineData("a\\b", false)]
    [InlineData("a.", false)]
    [InlineData("CON", false)]
    [InlineData("con.events", false)]
    [InlineData("-a", false)]
    [InlineData("a b", false)]
    [InlineData("a\n", false)]
    public void A_local_id_is_the_services(string id, bool local) => Assert.Equal(local, SessionEventText.IsLocalId(id));
}
