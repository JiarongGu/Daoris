using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// COAGENT1b (the co-agent design §7.1): a piece's box is the contract between this reader and the driver's <c>PartnerFiles</c>, which
/// share no code, so each side holds the shape. Here, the ask and the state as the driver writes them (the driver's suite,
/// <c>PartnerFilesTests</c>, writes the same text), and the answer <c>partner_answer</c> keeps, which the driver reads.
/// </summary>
public sealed class PartnerBoxShapeTests : IDisposable
{
    /// <summary>The driver's ask, as <c>PartnerFilesTests.TheAsk</c> holds it.</summary>
    internal const string TheAsk =
        "{\n  \"id\": \"p1a2b3c4\",\n  \"asker\": \"lead0001\",\n  \"piece\": \"Make the window seven days.\",\n  \"paths\": [\n    \"src\"\n  ],\n"
        + "  \"agent\": \"codex-acp\",\n  \"base\": \"abc123\",\n  \"uncommitted\": [],\n  \"askedBy\": \"person\",\n  \"at\": \"2026-10-22T12:00:00.0000000+00:00\"\n}\n";

    private static readonly DateTimeOffset At = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "partner-box-" + Guid.NewGuid().ToString("N")[..8]);

    public PartnerBoxShapeTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Partners));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Put(string id, string kind, string text) => File.WriteAllText(PartnerBox.PathOf(_home, id, kind), text);

    private static Session Record(string id, string? serves = null, string? piece = null) =>
        new(id, null, "reports", "codex-acp", SessionState.Working, null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)
        {
            Serves = serves, Piece = piece,
        };

    [Fact]
    public void The_drivers_ask_reads_here_and_an_id_that_could_leave_the_folder_is_never_read()
    {
        Put("p1a2b3c4", "ask", TheAsk);
        var box = new PartnerBox(_home);

        var ask = box.ReadAsk("p1a2b3c4")!;
        Assert.Equal(("p1a2b3c4", "lead0001", "Make the window seven days.", "abc123"), (ask.Id, ask.Asker, ask.Piece, ask.Base));
        Assert.Null(box.ReadAsk("../p1a2b3c4"));
        Assert.Null(box.ReadAsk("nope"));
    }

    /// <summary>The driver's fifteen states, in its order; the state file read with its own marks ignored, an unknown state none.</summary>
    [Fact]
    public void The_drivers_state_reads_with_its_marks_ignored_and_an_unknown_state_is_none()
    {
        Assert.Equal(15, PartnerBox.States.Count);
        Put("p1a2b3c4", "state",
            "{\n  \"id\": \"p1a2b3c4\",\n  \"state\": \"started\",\n  \"code\": null,\n  \"copy\": \"trees/default/reports/p-p1a2b3c4\",\n"
            + "  \"partner\": \"part0001\",\n  \"agent\": \"codex-acp\",\n  \"job\": \"Local\\\\daoris-piece-p1a2b3c4\",\n  \"at\": \"2026-10-22T12:00:00.0000000+00:00\"\n}\n");
        Put("q1a2b3c4", "state", "{ \"id\": \"q1a2b3c4\", \"state\": \"sleeping\" }");
        var box = new PartnerBox(_home);

        var state = box.ReadState("p1a2b3c4")!;
        Assert.Equal(("started", null, "part0001"), (state.State, state.Code, state.Partner));
        Assert.Null(box.ReadState("q1a2b3c4"));
    }

    /// <summary>
    /// 🔴 `partner_answer` keeps the answer once, for the piece whose state names this record its partner while it is started; a
    /// helper, a record of another piece, a piece not started and a second answer are refused, and the text is the driver's to read.
    /// </summary>
    [Fact]
    public void An_answer_is_kept_once_for_the_started_piece_that_names_its_partner()
    {
        Put("p1a2b3c4", "ask", TheAsk);
        Put("p1a2b3c4", "state", "{ \"id\": \"p1a2b3c4\", \"state\": \"started\", \"partner\": \"part0001\" }");
        var box = new PartnerBox(_home);

        Assert.False(box.Answer(Record("help0001", serves: "lead0001"), "help0001", "Done.", null, At).Kept);
        Assert.False(box.Answer(Record("part0002", serves: "lead0001", piece: "p1a2b3c4"), "part0002", "Done.", null, At).Kept);
        Assert.False(box.Answer(Record("part0001", serves: "lead0001", piece: "p1a2b3c4"), "part0001", "  ", null, At).Kept);

        var (kept, said) = box.Answer(Record("part0001", serves: "lead0001", piece: "p1a2b3c4"), "part0001", "Seven days now.", "No test for the edge.", At);
        Assert.True(kept, said);
        Assert.Equal(
            "{\n  \"id\": \"p1a2b3c4\",\n  \"partner\": \"part0001\",\n  \"summary\": \"Seven days now.\",\n  \"left\": \"No test for the edge.\",\n"
            + "  \"at\": \"2026-10-22T12:00:00.0000000\\u002B00:00\"\n}\n",
            File.ReadAllText(PartnerBox.PathOf(_home, "p1a2b3c4", "answer")));
        Assert.False(box.Answer(Record("part0001", serves: "lead0001", piece: "p1a2b3c4"), "part0001", "Again.", null, At).Kept);

        Put("q1a2b3c4", "state", "{ \"id\": \"q1a2b3c4\", \"state\": \"ending\", \"partner\": \"part0003\" }");
        Assert.False(box.Answer(Record("part0003", serves: "lead0001", piece: "q1a2b3c4"), "part0003", "Late.", null, At).Kept);
    }
}
