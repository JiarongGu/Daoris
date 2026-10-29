using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 row p (D212's note of the row; the foundation design §3.4 rule 5, §19.2): the settlement end to end. The driver's own client and
/// dispatchers talk to the service's own host, started in a process of its own over a scratch home (<see cref="RealServiceHost"/>), so the
/// service's say door, its desks and the trigger that writes the settlement are the real ones, and the client reads what they write on the wire.
/// Codex's reused id through both real dispatchers: a word K kept on A and taken, a successor B the test's own statement makes holding another
/// word whose id the test sets to K's, and K's retry kept through a fresh <see cref="ChatTurns"/> and, in another run, a fresh
/// <see cref="DrivenInbox"/> of B answered settled: nothing queued, steered, attempted or taken, B's word waiting unattempted, and K's text never
/// reaching the stand-in door's agent; the same retry from an older client keeping a second word under a new id, B's word untouched; a helper's and
/// a partner's hand retried after its take, nothing kept.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SettledServiceProcessTests : IAsyncLifetime
{
    private const string Key = "press/3f1c";

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "settled-service", Guid.NewGuid().ToString("N")[..8]);

    private RealServiceHost? _host;
    private ServiceClient? _client;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _host?.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
        return Task.CompletedTask;
    }

    private string Database => Path.Combine(_scratch, "service", "index", "knowledge.db");

    private async Task<ServiceClient> ServiceAsync()
    {
        _host = await RealServiceHost.StartAsync(Path.Combine(_scratch, "service"));
        _client = new ServiceClient(new ServiceHttp(_host.Url, key: null));
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        var (ok, message) = await _client.RegisterAsync(new JsonObject
        {
            ["repository"] = "engine",
            ["root"] = root,
            ["packs"] = new JsonArray(),
            ["domain"] = new JsonObject { ["summary"] = "An engine.", ["owns"] = new JsonArray("its own tree"), ["accepts"] = new JsonArray("a quest") },
        });
        Assert.True(ok, message);
        return _client;
    }

    /// <summary>An ended conversation, as the driver opens and ends one, continuing <paramref name="from"/> where it names one.</summary>
    private async Task<string> EndedAsync(string? from = null)
    {
        var (id, message) = await _client!.OpenChatAsync(
            "engine", "stub", from: from is null ? null : new ContinuedFrom(from, "press", "person"));
        Assert.True(id is not null, message);
        foreach (var state in new[] { "starting", "working", "completed" }) await _client.AdvanceAsync(id!, state);
        return id!;
    }

    private void Statement(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqliteConnection($"Data Source={Database};Mode=ReadWrite");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    /// <summary>The words waiting on a record, as the service lists them: id, key and whether a dispatcher marked an attempt.</summary>
    private async Task<List<(string Id, string? Key, bool Attempted)>> WaitingAsync(string session)
    {
        using var records = JsonDocument.Parse(await _client!.SessionRecordsJsonAsync(default));
        var record = records.RootElement.EnumerateArray().Single(each => each.GetProperty("id").GetString() == session);
        if (!record.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array) return [];
        return [.. said.EnumerateArray().Select(word => (
            word.GetProperty("id").GetString()!,
            word.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String ? key.GetString() : null,
            word.TryGetProperty("attempt", out var attempt) && attempt.ValueKind == JsonValueKind.String))];
    }

    /// <summary>K kept on A and taken; B continues A and holds a word whose id the test sets to K's. Returns (A, B, K's id).</summary>
    private async Task<(string A, string B, string Reused)> ReusedIdAsync()
    {
        var a = await EndedAsync();
        var first = await _client!.SayAsync(a, "the first record's words", key: Key, from: DaorisWords.From);
        Assert.NotNull(first.Word);
        Assert.True((await _client.TakenAsync(a, [first.Word.Id!])).Ok);

        var b = await EndedAsync(from: a);
        var own = await _client.SayAsync(b, "the successor's own words", key: "other/key", from: DaorisWords.From);
        // The test's own statement: the successor's word carries the id K's word had.
        Statement("UPDATE sessions SET said = REPLACE(said, $own, $reused) WHERE id = $id", ("$own", own.Word!.Id!), ("$reused", first.Word.Id!), ("$id", b));
        Assert.Equal([(first.Word.Id!, "other/key", false)], await WaitingAsync(b));
        return (a, b, first.Word.Id!);
    }

    [Fact]
    public async Task A_settled_retry_through_a_fresh_chat_turns_of_the_successor_reaches_nothing_and_an_older_clients_keeps_a_second_word()
    {
        await ServiceAsync();
        var (a, b, reused) = await ReusedIdAsync();
        var took = new List<string>();
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true), take: (message, sent) => { took.Add(message.Text); sent(); return Task.CompletedTask; },
            interrupt: () => Task.CompletedTask, changed: _ => { });

        var kept = await turns.KeepAsync(
            new SaidDoor(_client!, b), new ChatMessage("the first record's words, said again", []) { Key = Key, Level = MessageLevel.Urgent }, default);

        Assert.True(kept!.Settled);
        Assert.Null(kept.Word);
        Assert.Empty(turns.State.Queued);
        Assert.Empty(took);
        Assert.Equal([(reused, "other/key", false)], await WaitingAsync(b));

        // An older client sends no `settled`: today's door keeps a second word under a new id, the successor's own untouched.
        using var http = new HttpClient { BaseAddress = new Uri(_host!.Url) };
        var older = await http.PostAsJsonAsync($"/api/sessions/{b}/say", new { text = "the first record's words, said again", key = Key, from = DaorisWords.From });
        older.EnsureSuccessStatusCode();
        using var answer = JsonDocument.Parse(await older.Content.ReadAsStringAsync());
        Assert.False(answer.RootElement.TryGetProperty("settled", out _));
        var second = answer.RootElement.GetProperty("said").GetProperty("id").GetString()!;
        Assert.NotEqual(reused, second);
        Assert.Equal([(reused, "other/key", false), (second, Key, false)], await WaitingAsync(b));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task A_settled_retry_through_a_fresh_driven_inbox_of_the_successor_holds_and_sends_nothing()
    {
        await ServiceAsync();
        var (_, b, reused) = await ReusedIdAsync();
        var sent = new List<string>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: message => { sent.Add(message.Text); return Task.FromResult("end_turn"); });
        inbox.Flow();

        var kept = await inbox.KeepAsync(
            new SaidDoor(_client!, b), new ChatMessage("the first record's words, said again", []) { Key = Key, Level = MessageLevel.Urgent }, default);

        Assert.Equal(InboxKeep.Held, kept.Outcome);
        Assert.True(kept.Settled);
        Assert.Null(kept.Said!.Word);
        Assert.Empty(inbox.State.Queued);
        Assert.False(inbox.Holds);
        Assert.Empty(sent);
        Assert.Equal([(reused, "other/key", false)], await WaitingAsync(b));
    }

    /// <summary>
    /// The pump's schedule (the design's O8-F1): K said through the shell to conversation A and kept, its answer lost and the word held there; a
    /// move that read A's waiting words before the keep took only those and went on in B. The pump forwards K with its key to B, whose record
    /// keeps it as a new word, never settled, since K never left <c>said</c>; B's dispatcher delivers it once, and A's copy, which waits on a
    /// record the line has left, is never handed on. A key that waits on a record the line has left is answered by the door as it stands on the
    /// record named, as today.
    /// </summary>
    [Fact]
    public async Task A_word_waiting_on_a_record_the_line_has_left_is_kept_by_its_successor_and_delivered_once()
    {
        await ServiceAsync();
        var a = await EndedAsync();
        var first = await _client!.SayAsync(a, "the first record's words", key: Key, from: DaorisWords.From);
        Assert.NotNull(first.Word);
        // The move read A's waiting words before the keep: it takes none of K. B continues A.
        var b = await EndedAsync(from: a);
        var delivered = new List<string>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: message => { delivered.Add(message.Text); return Task.FromResult("end_turn"); });
        inbox.Flow();

        var forwarded = await inbox.KeepAsync(
            new SaidDoor(_client, b), new ChatMessage("the first record's words", []) { Key = Key, Level = MessageLevel.Urgent }, default);

        Assert.Equal(InboxKeep.Held, forwarded.Outcome);
        Assert.False(forwarded.Settled);
        Assert.NotNull(forwarded.Said!.Word);
        Assert.NotEqual(first.Word.Id, forwarded.Said.Word.Id);
        Assert.Equal(["the first record's words"], delivered);
        Assert.Equal([(first.Word.Id!, Key, false)], await WaitingAsync(a));
    }

    /// <summary>
    /// A helper's hand and a partner's, each retried after its take, are answered settled with no word: the helper's asker is the newest record
    /// of its line and its word left the predecessor it was kept on; nothing is kept again on either line.
    /// </summary>
    [Fact]
    public async Task A_helpers_and_a_partners_hand_retried_after_the_take_are_answered_settled_by_the_real_desks()
    {
        await ServiceAsync();
        var asker = await EndedAsync();
        var (helper, helperMessage) = await _client!.Helpers.OpenAsync(asker, "stub", Path.Combine(_scratch, "helper-clone"), null, null, null);
        Assert.True(helper is not null, helperMessage);
        var handed = await _client.Helpers.HandAsync(helper!, "the helper's answer");
        Assert.Equal(HelperHanded.Kept, handed.Outcome);
        Assert.True((await _client.TakenAsync(asker, [handed.Word!])).Ok);
        var successor = await EndedAsync(from: asker);

        var retry = await _client.Helpers.HandAsync(helper!, "the helper's answer");

        Assert.Equal(HelperHanded.Settled, retry.Outcome);
        Assert.Null(retry.Word);
        Assert.Empty(await WaitingAsync(asker));
        Assert.Empty(await WaitingAsync(successor));

        var lead = await EndedAsync();
        Statement(
            "INSERT INTO sessions (id, repository, adapter, state, created, updated, kind, serves, piece) VALUES ('pt000001', 'engine', 'stub', 'Completed', $at, $at, 'Chat', $lead, 'piece1')",
            ("$at", DateTimeOffset.UtcNow.ToString("O")), ("$lead", lead));
        var piece = await _client.Partners.HandAsync("pt000001", "the piece");
        Assert.True(piece.Handed);
        Assert.NotNull(piece.Word);
        Assert.True((await _client.TakenAsync(lead, [piece.Word])).Ok);

        var again = await _client.Partners.HandAsync("pt000001", "the piece");

        Assert.True(again.Handed);
        Assert.Equal(lead, again.Settled);
        Assert.Null(again.Word);
        Assert.Empty(await WaitingAsync(lead));
    }
}
