using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 row d-ii part 2 (D212's note of the part; the foundation design §3.4 rules 2 and 5, §19.13): the line's head at the say door end
/// to end, the service's own host started in a process of its own over a scratch home (<see cref="RealServiceHost"/>), so the door's step, its
/// walk over <c>sessions_from</c> and <c>sessions_quest</c> and the trigger that writes the settlement are the real ones. The says go over the
/// wire as the client sends them (<c>settled: true</c> with a key), read as the wire answers them, so the cases hold the service's half whatever
/// the driver's readers of <c>continued</c> make of it (part 1's). Codex's case: a word kept with its answer lost, taken on its record, the
/// record continued by a successor, and the retry named to either part answered settled, one word; a re-say at the head after
/// <c>continued</c> kept once. And a late word to an ended driven part, as a closed quest's part goes on as a conversation, refused
/// <c>continued</c> naming that conversation, which keeps it.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class HeadServiceProcessTests : IAsyncLifetime
{
    private const string Key = "press/7a2e";

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "head-service", Guid.NewGuid().ToString("N")[..8]);

    private RealServiceHost? _host;
    private ServiceClient? _client;
    private HttpClient? _http;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _http?.Dispose();
        _client?.Dispose();
        _host?.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
        return Task.CompletedTask;
    }

    private async Task<ServiceClient> ServiceAsync()
    {
        _host = await RealServiceHost.StartAsync(Path.Combine(_scratch, "service"));
        _client = new ServiceClient(new ServiceHttp(_host.Url, key: null));
        _http = new HttpClient { BaseAddress = new Uri(_host.Url) };
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
            "engine", "stub", from: from is null ? null : new ContinuedFrom(from, "limit", "daoris"));
        Assert.True(id is not null, message);
        foreach (var state in new[] { "starting", "working", "completed" }) await _client.AdvanceAsync(id!, state);
        return id!;
    }

    /// <summary>A keyed say of Daoris's as the newer client sends it, asking for a settled answer; the status and the answer's body.</summary>
    private async Task<(int Status, JsonElement Body)> SayAsync(string session, string key, string text = "the producer's words")
    {
        var answer = await _http!.PostAsJsonAsync($"/api/sessions/{session}/say", new { text, key, from = DaorisWords.From, settled = true });
        using var body = JsonDocument.Parse(await answer.Content.ReadAsStringAsync());
        return ((int)answer.StatusCode, body.RootElement.Clone());
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

    /// <summary>
    /// Codex's case at the head: K said through the driver's client to A and kept, its answer lost; the word taken on A; A continued by B. K's
    /// retry named to A, the record the line left, is answered settled before it could be refused <c>continued</c>, and the pump's forward to
    /// B, the head, is answered settled too: no word on either part. Another key said to A is refused <c>continued</c>, 409, naming B in
    /// <c>head</c> and in its sentence; said again at B it is kept, and once more answered as kept with that word.
    /// </summary>
    [Fact]
    public async Task Codexs_case_answers_a_retry_named_to_either_part_settled_and_a_re_say_at_the_head_after_continued_is_kept_once()
    {
        await ServiceAsync();
        var a = await EndedAsync();
        var first = await _client!.SayAsync(a, "the producer's words", key: Key, from: DaorisWords.From);
        Assert.NotNull(first.Word);
        Assert.True((await _client.TakenAsync(a, [first.Word.Id!])).Ok);
        var b = await EndedAsync(from: a);

        var toA = await SayAsync(a, Key);
        var toB = await SayAsync(b, Key);

        Assert.Equal((200, a), (toA.Status, toA.Body.GetProperty("settled").GetString()));
        Assert.False(toA.Body.TryGetProperty("said", out _));
        Assert.Equal((200, a), (toB.Status, toB.Body.GetProperty("settled").GetString()));
        Assert.Empty(await WaitingAsync(a));
        Assert.Empty(await WaitingAsync(b));

        const string Other = "press/7a2f";
        var refused = await SayAsync(a, Other);
        Assert.Equal((409, "continued", b), (refused.Status, refused.Body.GetProperty("refusal").GetString(), refused.Body.GetProperty("head").GetString()));
        Assert.Contains(b, refused.Body.GetProperty("error").GetString(), StringComparison.Ordinal);
        var kept = await SayAsync(b, Other);
        Assert.Equal(200, kept.Status);
        var word = kept.Body.GetProperty("said").GetProperty("id").GetString()!;
        var again = await SayAsync(b, Other);
        Assert.Equal((200, word), (again.Status, again.Body.GetProperty("said").GetProperty("id").GetString()));
        Assert.Equal([(word, Other, false)], await WaitingAsync(b));
        Assert.Empty(await WaitingAsync(a));
    }

    /// <summary>
    /// A late word to a quest's ended driven part, which went on as a conversation from it as a closed quest's part goes on (LIMITSWITCH2's
    /// ended move): the say door refuses it <c>continued</c>, naming the conversation, nothing kept on the driven part, and the conversation
    /// keeps it, once. The walk reads no quest's state, only the line, so the quest's close is the driver's to make and not this case's.
    /// </summary>
    [Fact]
    public async Task A_late_word_to_an_ended_driven_part_is_refused_continued_naming_the_conversation_that_went_on_from_it_which_keeps_it()
    {
        await ServiceAsync();
        var published = await _http!.PostAsJsonAsync("/api/quests", new { from = "game", to = "engine", title = "Note the cache", body = "Say that the frame cache exists." });
        published.EnsureSuccessStatusCode();
        using var quest = JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        var questId = quest.RootElement.GetProperty("quest").GetProperty("id").GetString()!;
        var (driven, opened) = await _client!.OpenSessionAsync(questId, "stub");
        Assert.True(driven is not null, opened);
        foreach (var state in new[] { "starting", "working", "completed" }) await _client.AdvanceAsync(driven!, state);
        var conversation = await EndedAsync(from: driven);

        var late = await SayAsync(driven!, "late/1", "a late word");

        Assert.Equal(409, late.Status);
        Assert.Equal("continued", late.Body.GetProperty("refusal").GetString());
        Assert.Equal(conversation, late.Body.GetProperty("head").GetString());
        Assert.Contains(conversation, late.Body.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Empty(await WaitingAsync(driven!));

        var reached = await SayAsync(conversation, "late/1", "a late word");
        Assert.Equal(200, reached.Status);
        var word = reached.Body.GetProperty("said").GetProperty("id").GetString()!;
        Assert.Equal([(word, "late/1", false)], await WaitingAsync(conversation));
    }
}
