using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 row d-ii part 3 (D212's note of the part; the foundation design §3.4 rule 3, §19.12): the keyed open moving the old part's words,
/// end to end, the service's own host started in a process of its own over a scratch home (<see cref="RealServiceHost"/>), so the open's
/// exclusive step, the receipt in <c>session_opens</c> and the trigger that settles each moved word on the record it left are the real ones,
/// and the open goes through the driver's own door (<see cref="SessionOpenDoors"/>), which reads <c>opens: 2</c> and sends <c>moves</c>. The
/// person's word the course read and named, and the person's word kept after the read, move to B under their keys; the retry of one named to
/// A is answered settled, naming B; the open's answer lost and asked again after every moved word was taken on B answers the same B and the
/// same transfer, and moves nothing more.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class OpenMovesServiceProcessTests : IAsyncLifetime
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "open-moves", Guid.NewGuid().ToString("N")[..8]);

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

    private async Task ServiceAsync()
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
    }

    /// <summary>An ended conversation, as the driver opens and ends one.</summary>
    private async Task<string> EndedAsync()
    {
        var (id, message) = await _client!.OpenChatAsync("engine", "stub");
        Assert.True(id is not null, message);
        foreach (var state in new[] { "starting", "working", "completed" }) await _client.AdvanceAsync(id!, state);
        return id!;
    }

    /// <summary>The words waiting on a record, as the service lists them: id and key.</summary>
    private async Task<List<(string Id, string? Key)>> WaitingAsync(string session)
    {
        using var records = JsonDocument.Parse(await _client!.SessionRecordsJsonAsync(default));
        var record = records.RootElement.EnumerateArray().Single(each => each.GetProperty("id").GetString() == session);
        if (!record.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array) return [];
        return [.. said.EnumerateArray().Select(word => (
            word.GetProperty("id").GetString()!,
            word.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String ? key.GetString() : null))];
    }

    [Fact]
    public async Task A_keyed_open_moves_the_named_and_the_late_words_and_its_lost_answer_asked_again_answers_the_same_transfer()
    {
        await ServiceAsync();
        var a = await EndedAsync();
        var named = await _client!.SayAsync(a, "the person's words", key: "press/named");
        Assert.NotNull(named.Word);
        var read = (await WaitingAsync(a)).Select(word => word.Id).ToList();
        var late = await _client.SayAsync(a, "a late word", key: "press/late");
        Assert.NotNull(late.Word);
        const string Body = """{"repository":"engine","adapter":"stub","from":"__a__","fromWhy":"limit","fromBy":"daoris"}""";
        var body = Body.Replace("__a__", a, StringComparison.Ordinal);
        var key = $"limit-move/{a}";

        var opened = await _client.Opens.OpenAsync("/api/sessions/chat", body, key, a, moves: new MoveWords(a, [named.Word.Id!], read));

        Assert.True(opened.Moves, opened.Message);
        var b = opened.SessionId!;
        Assert.Equal(
            [new MovedWord(named.Word.Id!, named.Word.Id!, "press/named", $"{key}/{named.Word.Id}", 0),
             new MovedWord(late.Word.Id!, late.Word.Id!, "press/late", $"{key}/{late.Word.Id}", 1)],
            opened.Moved);
        Assert.Equal([(named.Word.Id!, "press/named"), (late.Word.Id!, "press/late")], await WaitingAsync(b));
        Assert.Empty(await WaitingAsync(a));

        var retry = await _http!.PostAsJsonAsync($"/api/sessions/{a}/say", new { text = "a late word", key = "press/late", settled = true });
        using (var answered = JsonDocument.Parse(await retry.Content.ReadAsStringAsync()))
        {
            Assert.Equal(200, (int)retry.StatusCode);
            Assert.Equal(b, answered.RootElement.GetProperty("settled").GetString());
        }

        Assert.True((await _client.TakenAsync(b, [named.Word.Id!, late.Word.Id!])).Ok);
        var again = await _client.Opens.OpenAsync("/api/sessions/chat", body, key, a, retry: true, moves: new MoveWords(a, [named.Word.Id!], read));

        Assert.Equal(b, again.SessionId);
        Assert.Equal(opened.Moved, again.Moved);
        Assert.Empty(await WaitingAsync(b));
        Assert.Empty(await WaitingAsync(a));
    }
}
