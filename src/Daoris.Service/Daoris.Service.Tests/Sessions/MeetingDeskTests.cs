using System.Text.Json.Nodes;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// ONEWAY1t (D177 points 1, 2, 4 and 7): a meeting is a kind of brainstorm, asked of the one desk. It writes the request, the round's
/// send and the stop under <c>brainstorms/meetings/&lt;id&gt;/</c> as the driver's <c>MeetingFiles</c> reads them, and keeps the same id.
/// </summary>
public sealed class MeetingDeskTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "meeting-desk-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset At = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    public MeetingDeskTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Folder(string id) => DaorisHome.Entry(_home, HomeEntry.Brainstorms, "meetings", id);

    private void Put(string id, string file, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Folder(id), file))!);
        File.WriteAllText(Path.Combine(Folder(id), file), text);
    }

    private static JsonNode Table()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "brainstorm-files.json")))!;
    }

    /// <summary>🔴 The twin: the meetings' rows of the one table read here as they read in the driver's <c>MeetingFiles</c>.</summary>
    [Fact]
    public void Every_meeting_row_of_the_files_table_reads_as_the_driver_reads_it()
    {
        var desk = new MeetingDesk(_home);
        var rows = Table()["meetings"]!.AsArray();

        Put("ab12cd37", "request.json", rows[0]!["file"]!.GetValue<string>());
        var read = desk.Find("ab12cd37")!;
        var reads = rows[0]!["reads"]!;
        Assert.Equal((reads["question"]!.GetValue<string>(), reads["repository"]!.GetValue<string>()), (read.Question, read.Repository));
        Assert.Equal(reads["agents"]!.AsArray().Select(agent => agent!.GetValue<string>()), read.Agents);

        foreach (var row in new[] { rows[1], rows[2] })
        {
            Put("ab12cd37", "state.json", row!["state"]!.GetValue<string>());
            var stood = desk.Find("ab12cd37")!;
            Assert.Equal(
                (row["reads"]!["state"]!.GetValue<string>(), row["reads"]!["round"]!.GetValue<int>(), row["reads"]!["packet"]!.GetValue<string>()),
                (stood.State, stood.Round!.Value, stood.Packet!));
        }

        var send = rows[3]!;
        Put("ab12cd37", "state.json", """{"state":"frozen","round":2,"packet":"sha256:5f1c"}""");
        Put("ab12cd37", Path.Combine("sent", $"{send["round"]!.GetValue<int>()}.json"), send["sent"]!.GetValue<string>());
        Assert.Equal(send["reads"]!["sent"]!.GetValue<string>(), desk.Find("ab12cd37")!.Sent);
    }

    [Fact]
    public void A_meeting_is_asked_as_a_request_under_the_meetings_folder_in_the_tables_shape()
    {
        var desk = new MeetingDesk(_home);

        var asked = desk.Ask(" Which window? ", " reports ", ["stub", " acp-stub "], At);

        Assert.Equal(BrainstormRefusal.None, asked.Refusal);
        var id = asked.Brainstorm!.Id;
        Assert.Matches("^[0-9a-f]{8}$", id);
        Assert.Equal("meeting", asked.Brainstorm.Kind);
        Assert.Contains($"Meeting `{id}` asked of `stub`, `acp-stub`, in that order", asked.Message);
        Assert.Contains("its knowledge of `reports`", asked.Message);
        var written = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder(id), "request.json")))!.AsObject();
        var shape = JsonNode.Parse(Table()["meetings"]![0]!["file"]!.GetValue<string>())!.AsObject();
        Assert.Equal(shape.Select(pair => pair.Key), written.Select(pair => pair.Key));
        Assert.Equal(1, written["meeting"]!.GetValue<int>());
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Brainstorms, id)));
        Assert.Empty(new BrainstormDesk(_home).List());
    }

    [Fact]
    public void A_meeting_names_two_to_four_different_agents_a_question_in_bounds_and_no_sources()
    {
        var desk = new MeetingDesk(_home);

        var one = desk.Ask("Which?", null, ["stub"], At);
        Assert.Equal(BrainstormRefusal.BadShape, one.Refusal);
        Assert.Contains("names 2 to 4 different agents, each once; 1 were named", one.Message);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask("Which?", null, ["stub", "STUB"], At).Refusal);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask("Which?", null, ["a", "b", "c", "d", "e"], At).Refusal);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask("Which?", null, null, At).Refusal);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask("Which?", null, ["a b", "c"], At).Refusal);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask(" ", null, ["a", "b"], At).Refusal);
        Assert.Contains("1 to 4000 characters; this one is 4001", desk.Ask(new string('x', 4001), null, ["a", "b"], At).Message);
        Assert.Equal(BrainstormRefusal.BadShape, desk.Ask("Which?", null, ["a", "b"], At, "../x").Refusal);
        Assert.Equal(BrainstormRefusal.NoHome, new MeetingDesk(null).Ask("Which?", null, ["a", "b"], At).Refusal);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Brainstorms)));
    }

    [Fact]
    public void A_meetings_own_id_makes_the_ask_idempotent_and_other_words_under_it_are_refused()
    {
        var desk = new MeetingDesk(_home);
        Assert.Equal(BrainstormRefusal.None, desk.Ask("Which?", null, ["a", "b"], At, "weekly").Refusal);
        var written = File.ReadAllText(Path.Combine(Folder("weekly"), "request.json"));

        var again = desk.Ask("Which?", null, ["a", "b"], At.AddHours(1), "weekly");
        Assert.Equal(BrainstormRefusal.None, again.Refusal);
        Assert.Contains("is already asked: the same meeting, never a second spend", again.Message);
        var other = desk.Ask("Which?", null, ["b", "a"], At, "weekly");
        Assert.Equal(BrainstormRefusal.State, other.Refusal);
        Assert.Contains("was asked with other words; an id names one meeting", other.Message);
        Assert.Equal(written, File.ReadAllText(Path.Combine(Folder("weekly"), "request.json")));
    }

    /// <summary>The first round goes by itself; a later one only named; never while a round runs, and never after a stop or the end.</summary>
    [Fact]
    public void A_round_is_sent_once_by_its_number_and_only_when_the_look_froze_its_packet()
    {
        var desk = new MeetingDesk(_home);
        var id = desk.Ask("Which?", null, ["a", "b"], At).Brainstorm!.Id;

        Assert.Equal(BrainstormRefusal.NotFound, desk.Send("ffffffff", null, At).Refusal);
        var early = desk.Send(id, null, At);
        Assert.Equal(BrainstormRefusal.State, early.Refusal);
        Assert.Contains("has no packet yet", early.Message);

        Put(id, "state.json", """{ "id": "x", "state": "frozen", "round": 1, "packet": "sha256:aa", "code": null }""");
        var first = desk.Send(id, null, At);
        Assert.Equal(BrainstormRefusal.None, first.Refusal);
        Assert.Contains("round 1 sent, packet `sha256:aa`", first.Message);
        var sent = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder(id), "sent", "1.json")))!.AsObject();
        var shape = JsonNode.Parse(Table()["meetings"]![3]!["sent"]!.GetValue<string>())!.AsObject();
        Assert.Equal(shape.Select(pair => pair.Key), sent.Select(pair => pair.Key));
        Assert.Contains("already sent: the same round, never a second spend", desk.Send(id, 1, At).Message);

        Put(id, "state.json", """{ "state": "running", "round": 1, "packet": "sha256:aa" }""");
        Assert.Contains("round 1 is already sent, and running", desk.Send(id, null, At).Message);
        var busy = desk.Send(id, 2, At);
        Assert.Equal(BrainstormRefusal.State, busy.Refusal);
        Assert.Contains("round 1 is running; round 2 can be sent once it ends", busy.Message);

        Put(id, "state.json", """{ "state": "frozen", "round": 2, "packet": "sha256:bb" }""");
        var unnamed = desk.Send(id, null, At);
        Assert.Equal(BrainstormRefusal.State, unnamed.Refusal);
        Assert.Contains("waits for round 2", unnamed.Message);
        Assert.Contains("not round 3", desk.Send(id, 3, At).Message);
        Assert.Equal(BrainstormRefusal.None, desk.Send(id, 2, At).Refusal);
        Assert.True(File.Exists(Path.Combine(Folder(id), "sent", "2.json")));
    }

    [Fact]
    public void A_stopped_or_ended_meeting_sends_nothing_and_stop_writes_the_file_unless_it_ended()
    {
        var desk = new MeetingDesk(_home);
        var id = desk.Ask("Which?", null, ["a", "b"], At).Brainstorm!.Id;
        Put(id, "state.json", """{ "state": "frozen", "round": 1, "packet": "sha256:aa" }""");

        Assert.Equal(BrainstormRefusal.NotFound, desk.Stop("ffffffff", At).Refusal);
        var stopped = desk.Stop(id, At);
        Assert.Equal(BrainstormRefusal.None, stopped.Refusal);
        Assert.Contains($"Meeting `{id}`: stop asked", stopped.Message);
        Assert.True(File.Exists(Path.Combine(Folder(id), "stop.json")));
        var refused = desk.Send(id, null, At);
        Assert.Equal(BrainstormRefusal.State, refused.Refusal);
        Assert.Contains("was stopped: nothing more goes to any agent", refused.Message);

        File.Delete(Path.Combine(Folder(id), "stop.json"));
        Put(id, "state.json", """{ "state": "ended", "round": 2, "packet": "sha256:aa", "code": "rounds-ended" }""");
        Assert.Contains("ended (rounds-ended): nothing more goes", desk.Send(id, null, At).Message);
        Assert.Contains("already ended (rounds-ended)", desk.Stop(id, At).Message);
        Assert.False(File.Exists(Path.Combine(Folder(id), "stop.json")));
    }

    [Fact]
    public void A_meeting_is_cleared_with_its_rooms_never_while_a_round_runs_and_no_brainstorm_is_touched()
    {
        var desk = new MeetingDesk(_home);
        var kept = new BrainstormDesk(_home).Ask("Why?", null, null, At).Brainstorm!.Id;
        var id = desk.Ask("Which?", null, ["a", "b"], At).Brainstorm!.Id;
        var room = DaorisHome.Entry(_home, HomeEntry.BrainstormRooms, "meetings", id, "r1-a1");
        Directory.CreateDirectory(room);
        Put(id, "state.json", """{ "state": "running", "round": 1, "packet": "sha256:aa" }""");

        var running = desk.Clear(id);
        Assert.Equal(BrainstormRefusal.State, running.Refusal);
        Assert.Contains("is running; it is cleared once its round ends", running.Message);

        Put(id, "state.json", """{ "state": "ended", "round": 1, "packet": "sha256:aa" }""");
        var cleared = desk.Clear(id);
        Assert.Equal(BrainstormRefusal.None, cleared.Refusal);
        Assert.Contains("its records, its packets and its transcript are gone", cleared.Message);
        Assert.False(Directory.Exists(Folder(id)));
        Assert.False(Directory.Exists(room));
        Assert.Equal(BrainstormRefusal.NotFound, desk.Clear(id).Refusal);
        Assert.NotNull(new BrainstormDesk(_home).Find(kept));
    }
}
