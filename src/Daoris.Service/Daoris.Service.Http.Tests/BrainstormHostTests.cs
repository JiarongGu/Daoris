using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// BRAINSTORM1a3 (D176 points 1, 2 and 8): the local host's brainstorm door, which a terminal's <c>daoris brainstorm</c> reaches. It
/// writes the request the driver's look takes under the home, and answers show, send, clear and list from the home's records. It
/// is this machine's alone: answered to a caller on this machine only, as a second opinion's doors are.
/// </summary>
public sealed class BrainstormHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private string Folder(string id) => Daoris.Knowledge.DaorisHome.Entry(host.Home, Daoris.Knowledge.HomeEntry.Brainstorms, id);

    /// <summary>
    /// 🔴 The round trip a terminal makes: a question asked is a request on disk; shown, it waits for the look; sent before a packet
    /// is frozen it is refused; frozen, show answers the packet whole and send names its digest; cleared, it is gone.
    /// </summary>
    [Fact]
    public async Task A_question_is_asked_shown_sent_and_cleared_through_the_door_on_this_machine()
    {
        var asked = await host.PostAsync("/api/brainstorms", new { question = "Why does the report drop its last day?", repository = "reports", agents = new[] { "stub" } });

        Assert.Equal(200, asked.Status);
        var id = asked.Json.GetProperty("brainstorm").GetProperty("id").GetString()!;
        Assert.Contains($"Brainstorm `{id}` asked", asked.Json.GetProperty("message").GetString());
        Assert.True(File.Exists(Path.Combine(Folder(id), "request.json")));

        var listed = (await host.GetAsync("/api/brainstorms")).Json.EnumerateArray().Single(each => each.GetProperty("id").GetString() == id);
        Assert.Equal(("asked", "reports"), (listed.GetProperty("state").GetString(), listed.GetProperty("repository").GetString()));
        Assert.Equal(["stub"], listed.GetProperty("agents").EnumerateArray().Select(agent => agent.GetString()));

        var waiting = await host.GetAsync($"/api/brainstorms/{id}");
        Assert.Equal(200, waiting.Status);
        // Absent for none, as every answer here leaves a null out.
        Assert.False(waiting.Json.TryGetProperty("frozen", out var none) && none.ValueKind != JsonValueKind.Null, waiting.Body);
        Assert.False(waiting.Json.TryGetProperty("document", out var no) && no.ValueKind != JsonValueKind.Null, waiting.Body);

        var early = await host.PostAsync($"/api/brainstorms/{id}/send", new { });
        Assert.Equal(409, early.Status);
        Assert.Contains("has no packet yet", early.Error);

        File.WriteAllText(Path.Combine(Folder(id), "packet.json"),
            """{ "packet": 1, "digest": "sha256:5f1c", "question": "Why does the report drop its last day?", "repository": "reports", "tier": "words", "sources": [], "leftOut": [] }""");
        File.WriteAllText(Path.Combine(Folder(id), "state.json"), """{ "id": "x", "state": "frozen", "packet": "sha256:5f1c", "code": null }""");
        var frozen = (await host.GetAsync($"/api/brainstorms/{id}")).Json;
        Assert.Equal("sha256:5f1c", frozen.GetProperty("frozen").GetProperty("digest").GetString());
        Assert.Equal(("frozen", "sha256:5f1c"), (frozen.GetProperty("brainstorm").GetProperty("state").GetString(), frozen.GetProperty("brainstorm").GetProperty("packet").GetString()));

        var sent = await host.SendAsync("POST", $"/api/brainstorms/{id}/send", DaorisHost.Loopback);
        Assert.Equal(200, sent.Status);
        Assert.Equal("sha256:5f1c", sent.Json.GetProperty("brainstorm").GetProperty("sent").GetString());
        Assert.True(File.Exists(Path.Combine(Folder(id), "sent.json")));

        var cleared = await host.DeleteAsync($"/api/brainstorms/{id}");
        Assert.Equal(200, cleared.Status);
        Assert.Contains("gone from this machine", cleared.Json.GetProperty("message").GetString());
        Assert.False(Directory.Exists(Folder(id)));
        var gone = await host.GetAsync($"/api/brainstorms/{id}");
        Assert.Equal(404, gone.Status);
        Assert.Contains($"no brainstorm `{id}`", gone.Error);
    }

    /// <summary>A question out of its bounds is a bad request with the desk's sentence, and a running brainstorm is not cleared.</summary>
    [Fact]
    public async Task A_question_out_of_bounds_is_400_and_a_running_brainstorm_is_409_to_clear()
    {
        var blank = await host.PostAsync("/api/brainstorms", new { question = "  " });
        Assert.Equal(400, blank.Status);
        Assert.Contains("needs its question", blank.Error);
        Assert.Equal(400, (await host.PostAsync("/api/brainstorms", new { question = "Why?", agents = new[] { "a", "b", "c", "d", "e" } })).Status);

        var id = (await host.PostAsync("/api/brainstorms", new { question = "Why?" })).Json.GetProperty("brainstorm").GetProperty("id").GetString()!;
        File.WriteAllText(Path.Combine(Folder(id), "state.json"), """{ "state": "running", "packet": "sha256:5f1c" }""");
        var running = await host.DeleteAsync($"/api/brainstorms/{id}");
        Assert.Equal(409, running.Status);
        Assert.Contains("is running", running.Error);
        Assert.True(Directory.Exists(Folder(id)));
    }

    /// <summary>🔴 A caller off this machine is answered by no brainstorm door, with the sentence that says why, and nothing is written.</summary>
    [Fact]
    public async Task The_brainstorm_doors_answer_no_caller_off_this_machine()
    {
        var id = (await host.PostAsync("/api/brainstorms", new { question = "Why?" })).Json.GetProperty("brainstorm").GetProperty("id").GetString()!;
        var before = Directory.EnumerateDirectories(Path.GetDirectoryName(Folder(id))!).Count();

        foreach (var (method, path, body) in new (string, string, string?)[]
                 {
                     ("GET", "/api/brainstorms", null), ("POST", "/api/brainstorms", """{ "question": "Why?" }"""), ("GET", $"/api/brainstorms/{id}", null),
                     ("POST", $"/api/brainstorms/{id}/send", null), ("DELETE", $"/api/brainstorms/{id}", null),
                 })
        {
            var refused = await host.SendAsync(method, path, DaorisHost.OffMachine, json: body);
            Assert.True(refused.Status == 404, $"{method} {path} answered {refused.Status}");
            Assert.Contains("kept on this machine", refused.Error);
        }

        Assert.Equal(before, Directory.EnumerateDirectories(Path.GetDirectoryName(Folder(id))!).Count());
        Assert.True(Directory.Exists(Folder(id)));
    }
}
