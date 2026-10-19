using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// BRAINSTORM1a3 (D176 points 1, 2 and 8): the local host's brainstorm door writes the request the driver's look takes, and answers
/// show, send, clear and list from the home's records. The files are the contract with the driver's <c>BrainstormFiles</c>, which
/// shares no code with this desk: both are held, row for row, to one table, the driver suite's <c>fixtures/brainstorm-files.json</c>.
/// </summary>
public sealed class BrainstormDeskTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "brainstorm-desk-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset At = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    public BrainstormDeskTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Folder(string id) => DaorisHome.Entry(_home, HomeEntry.Brainstorms, id);

    private void Put(string id, string file, string text)
    {
        Directory.CreateDirectory(Folder(id));
        File.WriteAllText(Path.Combine(Folder(id), file), text);
    }

    private static JsonNode Table()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "brainstorm-files.json")))!;
    }

    /// <summary>🔴 The twin: every request, state and send row of the one table reads here as it reads in the driver.</summary>
    [Fact]
    public void Every_row_of_the_files_table_reads_as_the_driver_reads_it()
    {
        var desk = new BrainstormDesk(_home);
        var table = Table();
        var rows = 0;
        foreach (var row in table["requests"]!.AsArray())
        {
            var id = $"rq{rows++:D6}";
            Put(id, "request.json", row!["file"]!.GetValue<string>());
            var read = desk.Find(id);
            var name = row["name"]!.GetValue<string>();
            if (row["reads"] is not JsonObject reads)
            {
                Assert.True(read is null, name);
                continue;
            }

            Assert.True(read is not null, name);
            Assert.Equal(reads["question"]!.GetValue<string>(), read.Question);
            Assert.Equal(reads["repository"]?.GetValue<string>(), read.Repository);
            Assert.Equal(reads["agents"]!.AsArray().Select(agent => agent!.GetValue<string>()), read.Agents);
            Assert.Equal(DateTimeOffset.Parse(reads["at"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture), read.At);
            Assert.Equal(("asked", null, null), (read.State, read.Packet, read.Sent));
        }

        foreach (var row in table["states"]!.AsArray())
        {
            var id = $"st{rows++:D6}";
            Put(id, "request.json", """{ "question": "Why?" }""");
            Put(id, "state.json", row!["file"]!.GetValue<string>());
            var read = desk.Find(id)!;
            if (row["reads"] is not JsonObject reads)
            {
                Assert.Equal(("asked", null, null), (read.State, read.Packet, read.Code));
                continue;
            }

            Assert.Equal(
                (reads["state"]!.GetValue<string>(), reads["packet"]?.GetValue<string>(), reads["code"]?.GetValue<string>()),
                (read.State, read.Packet, read.Code));
        }

        foreach (var row in table["sent"]!.AsArray())
        {
            var id = $"se{rows++:D6}";
            Put(id, "request.json", """{ "question": "Why?" }""");
            Put(id, "sent.json", row!["file"]!.GetValue<string>());
            Assert.Equal(row["reads"]?.GetValue<string>(), desk.Find(id)!.Sent);
        }
    }

    /// <summary>
    /// The request the door writes is the table's first row's shape, key for key in its order, and is read back as asked: the words
    /// trimmed, a blank repository none, the agents' blanks dropped.
    /// </summary>
    [Fact]
    public void A_question_asked_is_written_once_as_the_driver_reads_a_request()
    {
        var desk = new BrainstormDesk(_home);

        var asked = desk.Ask("  Why does the daily report drop its last day?  ", " reports ", ["stub", " ", "acp-stub"], At);

        Assert.Equal(BrainstormRefusal.None, asked.Refusal);
        var id = asked.Brainstorm!.Id;
        Assert.Matches("^[0-9a-f]{8}$", id);
        Assert.Contains($"Brainstorm `{id}` asked", asked.Message);
        Assert.Contains("its knowledge of `reports`", asked.Message);
        var written = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder(id), "request.json")))!.AsObject();
        var shape = JsonNode.Parse(Table()["requests"]![0]!["file"]!.GetValue<string>())!.AsObject();
        Assert.Equal(shape.Select(pair => pair.Key), written.Select(pair => pair.Key));
        Assert.Equal(1, written["brainstorm"]!.GetValue<int>());
        Assert.Equal(id, written["id"]!.GetValue<string>());

        var read = desk.Find(id)!;
        Assert.Equal(("Why does the daily report drop its last day?", "reports", "asked"), (read.Question, read.Repository, read.State));
        Assert.Equal(["stub", "acp-stub"], read.Agents);
        Assert.Equal(At, read.At);

        var alone = desk.Ask("What next?", "  ", null, At);
        Assert.Null(alone.Brainstorm!.Repository);
        Assert.Contains("the question alone", alone.Message);
    }

    /// <summary>The request's bounds are the driver verb's: a question of 1 to 4,000 characters, at most four agents; with no home, nothing.</summary>
    [Fact]
    public void A_question_out_of_its_bounds_or_with_no_home_is_refused_and_nothing_is_written()
    {
        var desk = new BrainstormDesk(_home);

        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask("   ", null, null, At).Refusal);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask(null, null, null, At).Refusal);
        var long_ = desk.Ask(new string('x', BrainstormDesk.MostQuestion + 1), null, null, At);
        Assert.Equal(BrainstormRefusal.BadShape, long_.Refusal);
        Assert.Contains("1 to 4000 characters; this one is 4001", long_.Message);
        var many = desk.Ask("Why?", null, ["a", "b", "c", "d", "e"], At);
        Assert.Equal(BrainstormRefusal.BadShape, many.Refusal);
        Assert.Contains("at most 4 agents; 5 were named", many.Message);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask("Why?", null, ["two words"], At).Refusal);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Brainstorms)));

        Assert.Equal(BrainstormRefusal.NoHome, new BrainstormDesk(null).Ask("Why?", null, null, At).Refusal);
        Assert.Equal(BrainstormRefusal.None, desk.Ask(new string('x', BrainstormDesk.MostQuestion), null, ["a", "b", "c", "d"], At).Refusal);
    }

    /// <summary>
    /// 🔴 Send is the person's press (D176 point 2), as the driver's verb makes it: nothing to send before the look froze a packet;
    /// frozen, the send names the frozen digest; sent again it is the same brainstorm, never a second spend.
    /// </summary>
    [Fact]
    public void Send_names_the_frozen_packet_once_and_only_once_the_look_froze_one()
    {
        var desk = new BrainstormDesk(_home);
        var id = desk.Ask("Why?", "reports", null, At).Brainstorm!.Id;

        Assert.Equal(BrainstormRefusal.NotFound, desk.Send("ffffffff", At).Refusal);
        var early = desk.Send(id, At);
        Assert.Equal(BrainstormRefusal.State, early.Refusal);
        Assert.Contains("has no packet yet", early.Message);
        Assert.False(File.Exists(Path.Combine(Folder(id), "sent.json")));

        Put(id, "state.json", """{ "id": "x", "state": "frozen", "packet": "sha256:5f1c", "code": null, "at": "2026-10-19T10:01:00Z" }""");
        var sent = desk.Send(id, At);
        Assert.Equal(BrainstormRefusal.None, sent.Refusal);
        Assert.Contains("sent, packet `sha256:5f1c`", sent.Message);
        Assert.Equal("sha256:5f1c", sent.Brainstorm!.Sent);
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder(id), "sent.json")))!;
        Assert.Equal("sha256:5f1c", file["packet"]!.GetValue<string>());

        var again = desk.Send(id, At);
        Assert.Equal(BrainstormRefusal.None, again.Refusal);
        Assert.Contains("never a second spend", again.Message);

        Put(id, "state.json", """{ "id": "x", "state": "running", "packet": "sha256:5f1c", "code": null }""");
        Assert.Contains("was already sent (running)", desk.Send(id, At).Message);
    }

    /// <summary>Clear removes the records, the packet and the rooms (D176 point 8), never a running brainstorm's.</summary>
    [Fact]
    public void Clear_takes_the_records_and_the_rooms_and_never_a_running_brainstorm()
    {
        var desk = new BrainstormDesk(_home);
        var id = desk.Ask("Why?", null, null, At).Brainstorm!.Id;
        var room = DaorisHome.Entry(_home, HomeEntry.BrainstormRooms, id, "g1");
        Directory.CreateDirectory(room);
        Put(id, "state.json", """{ "state": "running", "packet": "sha256:5f1c" }""");

        var running = desk.Clear(id);
        Assert.Equal(BrainstormRefusal.State, running.Refusal);
        Assert.Contains("is running; it is cleared once it ends", running.Message);
        Assert.True(Directory.Exists(Folder(id)));

        Put(id, "state.json", """{ "state": "ended", "packet": "sha256:5f1c", "code": null }""");
        var cleared = desk.Clear(id);
        Assert.Equal(BrainstormRefusal.None, cleared.Refusal);
        Assert.Contains("its records and its packet are gone", cleared.Message);
        Assert.False(Directory.Exists(Folder(id)));
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.BrainstormRooms, id)));
        Assert.Equal(BrainstormRefusal.NotFound, desk.Clear(id).Refusal);
    }

    /// <summary>Show is the request, the packet as frozen and, once ended, the document's Markdown; list is each, oldest first.</summary>
    [Fact]
    public void Show_answers_the_packet_as_frozen_and_the_document_and_list_answers_each_oldest_first()
    {
        var desk = new BrainstormDesk(_home);
        var later = desk.Ask("Second?", null, null, At.AddMinutes(1)).Brainstorm!.Id;
        var first = desk.Ask("First?", "reports", null, At).Brainstorm!.Id;
        Put("zz000000", "request.json", "{ not json");

        Assert.Equal([first, later], desk.List().Select(each => each.Id));
        var waiting = desk.Show(first)!;
        Assert.Null(waiting.Frozen);
        Assert.Null(waiting.Document);

        Put(first, "packet.json", """{ "packet": 1, "digest": "sha256:5f1c", "question": "First?", "sources": [], "leftOut": [] }""");
        Put(first, "state.json", """{ "state": "ended", "packet": "sha256:5f1c", "code": null }""");
        Put(first, "document.md", "# Brainstorm\n\n1. Inclusive window\n");
        var shown = desk.Show(first)!;
        Assert.Equal("sha256:5f1c", shown.Frozen!.Value.GetProperty("digest").GetString());
        Assert.Equal(JsonValueKind.Array, shown.Frozen.Value.GetProperty("sources").ValueKind);
        Assert.Equal("# Brainstorm\n\n1. Inclusive window\n", shown.Document);
        Assert.Equal("ended", shown.Brainstorm.State);
        Assert.Null(desk.Show("zz000000"));
    }

    /// <summary>BRAINSTORM-RESERVED1: `meetings` holds the meetings' records (AGENTROOM1a), so no door treats it as a brainstorm's id.</summary>
    [Fact]
    public void The_reserved_id_meetings_is_no_brainstorm_and_clear_leaves_every_meeting_whole()
    {
        var desk = new BrainstormDesk(_home);
        Put("meetings/ab12cd34", "request.json", """{ "question": "Why?" }""");
        Put("meetings", "request.json", """{ "question": "Why?" }""");
        var kept = Path.Combine(Folder("meetings"), "ab12cd34", "request.json");

        foreach (var id in new[] { "meetings", "Meetings" })
        {
            Assert.Null(desk.Find(id));
            Assert.Null(desk.Show(id));
            var sent = desk.Send(id, At);
            Assert.Equal(BrainstormRefusal.NotFound, sent.Refusal);
            Assert.Contains("`meetings` holds the meetings' records, not a brainstorm", sent.Message);
            var cleared = desk.Clear(id);
            Assert.Equal(BrainstormRefusal.NotFound, cleared.Refusal);
            Assert.Contains("`meetings` holds the meetings' records, not a brainstorm", cleared.Message);
        }

        Assert.True(File.Exists(kept));
        Assert.DoesNotContain(desk.List(), each => each.Id.Equals("meetings", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>An id is a plain name: one that is not reaches nothing, and nothing outside the brainstorms is read or removed.</summary>
    [Fact]
    public void An_id_that_is_not_a_plain_name_reaches_nothing()
    {
        var desk = new BrainstormDesk(_home);
        var kept = Path.Combine(_home, "kept.txt");
        File.WriteAllText(kept, "mine");
        Put("ab12cd34", "request.json", """{ "question": "Why?" }""");

        foreach (var id in new[] { "..", "../..", "ab12cd34/..", "a b", "", new string('a', 65) })
        {
            Assert.Null(desk.Find(id));
            Assert.Null(desk.Show(id));
            Assert.Equal(BrainstormRefusal.NotFound, desk.Send(id, At).Refusal);
            Assert.Equal(BrainstormRefusal.NotFound, desk.Clear(id).Refusal);
        }

        Assert.True(File.Exists(kept));
        Assert.True(Directory.Exists(Folder("ab12cd34")));
    }
}
