using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// FOUNDATION1 row p (D212; the foundation design §19.2, §19.12) over the wire: <c>GET /api/protocol</c> answers the host's run and its
/// <c>settled</c> mark, and a keyed say or hand that sends <c>settled: true</c> is answered, after the take, with a success naming the record
/// the word left and no <c>said</c>, while a request without it, an older client's, is answered by today's door.
/// </summary>
public sealed class SaidSettledHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<Session> EndedAsync(string repository)
    {
        var sessions = host.Composed.Sessions;
        var made = await sessions.CreateAsync(null, repository, "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        return (await sessions.SetStateAsync(made.Id, SessionState.Completed, null, null, null, DateTimeOffset.UtcNow))!;
    }

    /// <summary>The host's run is drawn at its start and stays through its life, and its marks name the settlement at version 1.</summary>
    [Fact]
    public async Task The_protocol_answers_the_hosts_run_and_its_settled_mark()
    {
        var first = await host.GetAsync("/api/protocol");
        var second = await host.GetAsync("/api/protocol");

        Assert.Equal(200, first.Status);
        var run = first.Json.GetProperty("run").GetString();
        Assert.Matches("^[0-9a-f]{32}$", run!);
        Assert.Equal(run, second.Json.GetProperty("run").GetString());
        Assert.Equal(1, first.Json.GetProperty("marks").GetProperty("settled").GetInt32());
        // Row d-i adds replay alone; d-ii's say door adds `head` 1 (the `Continued` refusal), and its open, which moves the words, `opens` 2.
        // Row k adds `journal` 1: the change stream answers the event journal's family and place to a reader that asks for them.
        Assert.Equal(["settled", "opens", "head", "journal"], first.Json.GetProperty("marks").EnumerateObject().Select(mark => mark.Name).ToArray());
        Assert.Equal(2, first.Json.GetProperty("marks").GetProperty("opens").GetInt32());
        Assert.Equal(1, first.Json.GetProperty("marks").GetProperty("head").GetInt32());
        Assert.Equal(1, first.Json.GetProperty("marks").GetProperty("journal").GetInt32());
    }

    /// <summary>
    /// 🔴 Row d-ii over the wire: a say to a record its line has left is refused <c>continued</c>, beside <c>head</c>, the head's id, at the
    /// status the say door gives its other refusals of a record as it stands (409), nothing kept; the head keeps the same say.
    /// </summary>
    [Fact]
    public async Task A_say_to_a_record_its_line_left_is_refused_continued_with_the_head_at_the_doors_conflict_status()
    {
        var left = await EndedAsync("Heads");
        var head = await host.Composed.Sessions.CreateAsync(
            null, "Heads", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat, from: left.Id, fromWhy: "limit", fromBy: "daoris");

        var refused = await host.PostAsync($"/api/sessions/{left.Id}/say", new { text = "A late word.", key = "wire/head", from = "daoris", settled = true });

        Assert.Equal(409, refused.Status);
        Assert.Equal("continued", refused.Json.GetProperty("refusal").GetString());
        Assert.Equal(head.Id, refused.Json.GetProperty("head").GetString());
        Assert.Contains(head.Id, refused.Json.GetProperty("error").GetString());
        Assert.Empty((await host.Composed.Sessions.FindAsync(left.Id))!.Said);

        // A refusal of another kind names no head.
        var missing = await host.PostAsync("/api/sessions/nosuch01/say", new { text = "A late word." });
        Assert.Equal(404, missing.Status);
        Assert.False(missing.Json.TryGetProperty("head", out _));

        await host.Composed.Sessions.SetStateAsync(head.Id, SessionState.Completed, null, null, null, DateTimeOffset.UtcNow);
        var kept = await host.PostAsync($"/api/sessions/{head.Id}/say", new { text = "A late word.", key = "wire/head", from = "daoris", settled = true });
        Assert.Equal(200, kept.Status);
        Assert.Single((await host.Composed.Sessions.FindAsync(head.Id))!.Said);
    }

    /// <summary>A record left by an ended conversation part and the part it went on in, as an open writes them; returns (left, head).</summary>
    private async Task<(Session Left, Session Head)> WentOnAsync(string repository)
    {
        var left = await EndedAsync(repository);
        var head = await host.Composed.Sessions.CreateAsync(
            null, repository, "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat, from: left.Id, fromWhy: "limit", fromBy: "daoris");
        return (left, head);
    }

    /// <summary>
    /// 🔴 Row d-ii, what the driver's readers act on (the steward's first): a <c>continued</c> refusal is the say route's refusal status, 409,
    /// with <c>error</c>, <c>refusal</c> and a <c>head</c> that is never empty, the id of a record this host holds.
    /// </summary>
    [Fact]
    public async Task A_continued_refusal_is_409_with_its_error_its_refusal_and_a_head_that_is_a_record_here()
    {
        var (left, head) = await WentOnAsync("Heads1");

        var refused = await host.PostAsync($"/api/sessions/{left.Id}/say", new { text = "A late word." });

        Assert.Equal(409, refused.Status);
        Assert.Equal("continued", refused.Json.GetProperty("refusal").GetString());
        Assert.False(string.IsNullOrWhiteSpace(refused.Error));
        var named = refused.Json.GetProperty("head").GetString();
        Assert.False(string.IsNullOrWhiteSpace(named));
        Assert.Equal(head.Id, named);
        Assert.NotNull(await host.Composed.Sessions.FindAsync(named!));
    }

    /// <summary>
    /// 🔴 Row d-ii (the steward's second): the sentence names the head, since an older desktop's terminal, headless door and pump each print
    /// the service's sentence as it comes: it names the record the say named and the head, and is the ledger's own sentence word for word.
    /// </summary>
    [Fact]
    public async Task A_continued_refusals_sentence_names_the_head_as_an_older_desktop_prints_it()
    {
        var (left, head) = await WentOnAsync("Heads2");

        var refused = await host.PostAsync($"/api/sessions/{left.Id}/say", new { text = "A late word." });

        Assert.Contains($"`{head.Id}`", refused.Error, StringComparison.Ordinal);
        Assert.Contains($"`{left.Id}`", refused.Error, StringComparison.Ordinal);
        Assert.Equal((await host.Composed.Ledger.SayAsync(left.Id, "A late word.", null, DateTimeOffset.UtcNow)).Message, refused.Error);
    }

    /// <summary>
    /// 🔴 Row d-ii (the steward's third): every say answer names the host's run in <c>x-daoris-run</c>, the run <c>GET /api/protocol</c>
    /// answers, a <c>continued</c> refusal, a kept word and a settled retry alike, so a client keeping the <c>head</c> mark by run reads the
    /// protocol again only when a run changes.
    /// </summary>
    [Fact]
    public async Task Every_say_answer_names_the_hosts_run_a_continued_refusal_a_kept_word_and_a_settled_retry_alike()
    {
        var run = (await host.GetAsync("/api/protocol")).Json.GetProperty("run").GetString();
        var (left, head) = await WentOnAsync("Heads3");
        await host.Composed.Sessions.SetStateAsync(head.Id, SessionState.Completed, null, null, null, DateTimeOffset.UtcNow);

        var refused = await host.PostAsync($"/api/sessions/{left.Id}/say", new { text = "A late word.", key = "wire/run", from = "daoris", settled = true });
        var kept = await host.PostAsync($"/api/sessions/{head.Id}/say", new { text = "A late word.", key = "wire/run", from = "daoris", settled = true });
        await host.Composed.Sessions.TakeSaidAsync(head.Id, [kept.Json.GetProperty("said").GetProperty("id").GetString()!]);
        var settled = await host.PostAsync($"/api/sessions/{left.Id}/say", new { text = "A late word.", key = "wire/run", from = "daoris", settled = true });

        Assert.Equal((409, "continued"), (refused.Status, refused.Json.GetProperty("refusal").GetString()));
        Assert.Equal(200, kept.Status);
        Assert.Equal(head.Id, settled.Json.GetProperty("settled").GetString());
        Assert.All(new[] { refused, kept, settled }, answer => Assert.Equal(run, answer.Headers["x-daoris-run"]));
    }

    /// <summary>
    /// 🔴 Row d-ii: a line planted as a cycle by an older writer's own statement reaches the say door as <c>unplaced</c>, 409, with its sentence
    /// and no <c>head</c>, never <c>continued</c> with an empty or cyclic head; nothing is kept.
    /// </summary>
    [Fact]
    public async Task A_line_that_loops_is_refused_unplaced_with_its_sentence_and_no_head()
    {
        var (left, head) = await WentOnAsync("Heads4");
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={host.Database};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var plant = connection.CreateCommand();
            plant.CommandText = "UPDATE sessions SET from_session = $head WHERE id = $left";
            plant.Parameters.AddWithValue("$head", head.Id);
            plant.Parameters.AddWithValue("$left", left.Id);
            Assert.Equal(1, await plant.ExecuteNonQueryAsync());
        }

        var refused = await host.PostAsync($"/api/sessions/{left.Id}/say", new { text = "A late word." });

        Assert.Equal(409, refused.Status);
        Assert.Equal("unplaced", refused.Json.GetProperty("refusal").GetString());
        Assert.False(refused.Json.TryGetProperty("head", out _));
        Assert.Contains("loops", refused.Error, StringComparison.Ordinal);
        Assert.Empty((await host.Composed.Sessions.FindAsync(left.Id))!.Said);
        Assert.Empty((await host.Composed.Sessions.FindAsync(head.Id))!.Said);
    }

    /// <summary>
    /// 🔴 Row d-ii, the say door's word table cell for cell: every refusal the ledger knows has its own word on the wire, the table below, so
    /// none falls to the switch's default, which would send a new refusal as <c>running</c>. A refusal added to the ledger fails this until
    /// its word is written here and in <c>Program.SayRefusal</c>.
    /// </summary>
    [Fact]
    public void Every_say_refusal_has_its_own_word_on_the_wire_cell_for_cell()
    {
        var table = new Dictionary<SessionSayRefusal, string>
        {
            [SessionSayRefusal.None] = "none",
            [SessionSayRefusal.Empty] = "no-words",
            [SessionSayRefusal.NotFound] = "not-found",
            [SessionSayRefusal.NotOurs] = "not-ours",
            [SessionSayRefusal.Intake] = "intake",
            [SessionSayRefusal.StoodDown] = "stood-down",
            [SessionSayRefusal.Running] = "running",
            [SessionSayRefusal.Opinion] = "opinion",
            [SessionSayRefusal.Helper] = "helper",
            [SessionSayRefusal.Taken] = "taken",
            [SessionSayRefusal.From] = "from",
            [SessionSayRefusal.Level] = "level",
            [SessionSayRefusal.SupervisorText] = "supervisor-text",
            [SessionSayRefusal.KeyMismatch] = "key-mismatch",
            [SessionSayRefusal.DecisionTaken] = "decision-taken",
            [SessionSayRefusal.NotParked] = "not-parked",
            [SessionSayRefusal.NoQuest] = "no-quest",
            [SessionSayRefusal.QuestionChanged] = "question-changed",
            [SessionSayRefusal.NotOfferedChoice] = "not-offered-choice",
            [SessionSayRefusal.OwnerQuestion] = "owner-question",
            [SessionSayRefusal.GoAheadOpen] = "go-ahead-open",
            [SessionSayRefusal.GoAheadUnread] = "go-ahead-unread",
            [SessionSayRefusal.OwnerAnswered] = "owner-answered",
            [SessionSayRefusal.NoHome] = "no-home",
            [SessionSayRefusal.Continued] = "continued",
            [SessionSayRefusal.Unplaced] = "unplaced",
        };

        Assert.Equal(Enum.GetValues<SessionSayRefusal>().Order(), table.Keys.Order());
        foreach (var (refusal, word) in table) Assert.Equal((refusal, word), (refusal, Program.SayRefusal(refusal)));
        Assert.Equal(table.Count, table.Values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A keyed say after the take, asking <c>settled</c>, is answered <c>200</c> with <c>settled</c> naming the record and no <c>said</c> and no
    /// session; a say without it keeps a second word under a new id, as today.
    /// </summary>
    [Fact]
    public async Task A_keyed_say_after_the_take_asks_for_settled_and_an_older_client_is_answered_by_todays_door()
    {
        var record = await EndedAsync("Settle");
        var first = await host.PostAsync($"/api/sessions/{record.Id}/say", new { text = "Press it.", key = "wire/1", from = "daoris", settled = true });
        Assert.Equal(200, first.Status);
        var word = first.Json.GetProperty("said").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{record.Id}/taken", new { said = new[] { word } })).Status);

        var retry = await host.PostAsync($"/api/sessions/{record.Id}/say", new { text = "Press it.", key = "wire/1", from = "daoris", settled = true });

        Assert.Equal(200, retry.Status);
        Assert.Equal(record.Id, retry.Json.GetProperty("settled").GetString());
        Assert.False(retry.Json.TryGetProperty("said", out _));
        Assert.False(retry.Json.TryGetProperty("session", out _));
        Assert.Contains(record.Id, retry.Json.GetProperty("message").GetString());
        Assert.Empty((await host.Composed.Sessions.FindAsync(record.Id))!.Said);

        var older = await host.PostAsync($"/api/sessions/{record.Id}/say", new { text = "Press it.", key = "wire/1", from = "daoris" });
        Assert.Equal(200, older.Status);
        Assert.False(older.Json.TryGetProperty("settled", out _));
        Assert.NotEqual(word, older.Json.GetProperty("said").GetProperty("id").GetString());
    }

    /// <summary>A helper's hand and a partner's, retried after the take, are answered <c>settled</c> to a request that asks, with no word or record.</summary>
    [Fact]
    public async Task A_helpers_and_a_partners_hand_after_the_take_are_answered_settled_to_a_request_that_asks()
    {
        var sessions = host.Composed.Sessions;
        var asker = await EndedAsync("Hands");
        var helper = await sessions.CreateAsync(null, "Hands", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: asker.Id);
        var handed = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five.", settled = true });
        Assert.Equal(200, handed.Status);
        await sessions.TakeSaidAsync(asker.Id, [handed.Json.GetProperty("said").GetProperty("id").GetString()!]);

        var retry = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five.", settled = true });
        Assert.Equal(200, retry.Status);
        Assert.Equal(asker.Id, retry.Json.GetProperty("settled").GetString());
        Assert.False(retry.Json.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.Object);
        Assert.Empty((await sessions.FindAsync(asker.Id))!.Said);

        var lead = await EndedAsync("Hands");
        var partner = await sessions.CreateAsync(null, "Hands", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id, piece: "piece9");
        var piece = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "The piece.", settled = true });
        Assert.Equal(200, piece.Status);
        await sessions.TakeSaidAsync(lead.Id, [piece.Json.GetProperty("said").GetProperty("id").GetString()!]);

        var again = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "The piece.", settled = true });
        Assert.Equal(lead.Id, again.Json.GetProperty("settled").GetString());
        Assert.Empty((await sessions.FindAsync(lead.Id))!.Said);

        var older = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "The piece." });
        Assert.False(older.Json.TryGetProperty("settled", out var settled) && settled.ValueKind == JsonValueKind.String);
        Assert.Equal(JsonValueKind.Object, older.Json.GetProperty("said").ValueKind);
    }
}
