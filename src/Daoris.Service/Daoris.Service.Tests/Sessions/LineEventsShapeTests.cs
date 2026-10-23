using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// SESSCTX1f (D189's SESSCTX1f note; the session-context design §3.5): a session's event file is the contract between the driver's
/// <c>SessionEvents</c>, which writes it, and this reader of a line's history, which shares no code with it, so each side holds the
/// shape. Here, the file as the driver writes it, read as entries; the driver's suite (<c>LineEventFilesTests</c>) writes the same
/// text from its side.
/// </summary>
public sealed class LineEventsShapeTests : IDisposable
{
    /// <summary>The file the driver's <c>SessionEvents.Append</c> writes for these fourteen events, line for line.</summary>
    internal static readonly string[] DriverFile =
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

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "line-events-" + Guid.NewGuid().ToString("N")[..8]);

    public LineEventsShapeTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 The driver's file reads as a reader reads a conversation: each word where it was taken, the agent's chunks joined into one
    /// message across a usage reading, a tool call with its updates as one entry at its first number, the plan, the turn's end and
    /// Daoris's note. A thought, a usage reading, a word shown while it waited and a receipt are no entry, and a torn line costs itself.
    /// </summary>
    [Fact]
    public void The_drivers_event_file_reads_as_entries_a_reader_reads()
    {
        File.WriteAllText(LineEvents.PathOf(_home, "b2"), string.Join("\n", DriverFile) + "\n" + """{"seq":15,"kind":""" + "\n");

        var entries = LineEvents.Entries(_home, "b2");

        Assert.Equal([1L, 2, 3, 7, 9, 11, 12, 13], entries.Select(entry => entry.Seq));
        Assert.Equal(["user", "user", "message", "tool", "plan", "turn", "user", "note"], entries.Select(entry => entry.Kind));
        Assert.Equal(("target", "brief", "The brief of the line."), (entries[0].Origin, entries[0].Mark, entries[0].Text));
        Assert.Equal(("person", null, "Add a dark mode.\n深色"), (entries[1].Origin, entries[1].Mark, entries[1].Text));
        Assert.Equal(["shot.png"], entries[1].Files!);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T10:00:01+00:00"), entries[1].At);
        Assert.Equal("I will read the theme first.", entries[2].Text);
        Assert.Equal(
            ("Read theme.ts", "read", "completed", "{\"path\":\"theme.ts\"}", "export const theme = {};"),
            (entries[3].Title, entries[3].ToolKind, entries[3].Status, entries[3].Input, entries[3].Output));
        Assert.Equal(["src/theme.ts"], entries[3].Locations!);
        Assert.Equal([("Read the theme", "completed"), ("Add the toggle", "in_progress")], entries[4].Steps!.Select(step => (step.Content, step.Status)));
        Assert.Equal("end_turn", entries[5].StopReason);
        Assert.Equal(("person", "Use the system setting."), (entries[6].Origin, entries[6].Text));
        Assert.Equal("The account is near its limit.", entries[7].Text);
    }

    /// <summary>
    /// SUPERVISOR1a-i (D208; the supervisor design §6.1): a supervisor's word read from the log after a restart is marked by its event's
    /// <c>from</c>, said as the supervisor's on the owner's behalf, never the person's nor Daoris's, whatever origin the line claims.
    /// </summary>
    [Fact]
    public void A_supervisors_word_reads_as_the_supervisors_never_the_persons_nor_daoriss()
    {
        File.WriteAllText(LineEvents.PathOf(_home, "d4"), string.Join("\n",
        [
            """{"seq":1,"at":"2026-10-24T10:00:00+00:00","kind":"user","id":"w1","text":"The supervisor chose.","origin":"target","from":"supervisor","decision":"d1"}""",
            """{"seq":2,"at":"2026-10-24T10:00:01+00:00","kind":"user","id":"w2","text":"A forged one.","origin":"person","from":"supervisor"}""",
        ]) + "\n");

        var entries = LineEvents.Entries(_home, "d4");

        Assert.Equal(["supervisor", "supervisor"], entries.Select(entry => entry.Mark));
        Assert.All(entries, entry => Assert.Equal("the supervisor, on the owner's behalf", LineEvents.Who(entry)));
    }

    /// <summary>A part whose file is not there keeps no entries, and a file that is no record reads as none, never a throw.</summary>
    [Fact]
    public void No_file_or_a_foreign_one_is_no_entry()
    {
        File.WriteAllText(LineEvents.PathOf(_home, "c3"), "not json\n{\"kind\":\"\"}\n[1,2]\n");

        Assert.Empty(LineEvents.Entries(_home, "a1"));
        Assert.Empty(LineEvents.Entries(_home, "c3"));
    }

    /// <summary>
    /// The driver's <c>SessionEventText.IsLocalId</c> rows (its suite's <c>LineEventFilesTests</c> holds the same): an id of this
    /// machine's names a file under the home; a remote's <c>machine:id</c>, a teammate's <c>origin/id</c> and anything that is a path
    /// never do.
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
    public void A_local_id_is_the_drivers(string id, bool local) => Assert.Equal(local, LineEvents.IsLocalId(id));

    /// <summary>
    /// The line is followed back by each record's <c>from</c>, oldest part first, as the driver's <c>ContextBriefs.LineOf</c> follows
    /// it: a link that comes back on itself, a record not held here, or a teammate's record ends it there.
    /// </summary>
    [Fact]
    public async Task The_line_follows_from_back_and_ends_at_a_repeat_a_record_not_held_or_a_teammates()
    {
        var records = new Dictionary<string, Session>
        {
            ["a1"] = Record("a1", from: null),
            ["b2"] = Record("b2", from: "a1"),
            ["c3"] = Record("c3", from: "b2"),
            ["r1"] = Record("r1", from: "r2"),
            ["r2"] = Record("r2", from: "r1"),
            ["g1"] = Record("g1", from: "gone"),
            ["t2"] = Record("t2", from: "t1"),
            ["t1"] = Record("t1", from: null) with { Origin = "laptop" },
        };
        Task<Session?> Find(string id, CancellationToken _) => Task.FromResult(records.GetValueOrDefault(id));

        Assert.Equal(["a1", "b2", "c3"], (await LineEvents.LineOfAsync(records["c3"], Find)).Select(part => part.Id));
        Assert.Equal(["r1", "r2"], (await LineEvents.LineOfAsync(records["r2"], Find)).Select(part => part.Id));
        Assert.Equal(["g1"], (await LineEvents.LineOfAsync(records["g1"], Find)).Select(part => part.Id));
        Assert.Equal(["t2"], (await LineEvents.LineOfAsync(records["t2"], Find)).Select(part => part.Id));
        Assert.Empty(await LineEvents.LineOfAsync(records["t1"], Find));
    }

    private static Session Record(string id, string? from) =>
        new(id, "q1", "reports", "claude-code", SessionState.Working, null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)
        {
            From = from,
            FromWhy = from is null ? null : "press",
        };
}
