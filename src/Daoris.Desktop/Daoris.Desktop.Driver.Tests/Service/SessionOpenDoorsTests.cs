using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

public sealed class SessionOpenDoorsTests
{
    private const string Body = """{"repository":"Owner","adapter":"stub","from":"left"}""";

    private sealed class Host : IServiceDoors
    {
        public string BaseUrl => "http://stand-in";
        public List<(string Path, string? Body)> Heard { get; } = [];
        public string? Run { get; set; } = "first";
        public int Mark { get; set; } = 1;
        public bool Lose { get; set; }
        public bool Echo { get; set; } = true;
        public bool BrokenAnswer { get; set; }
        public string? AnswerRun { get; set; } = "first";
        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();
        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Heard.Add((path, json));
            if (path == "/api/protocol") return Task.FromResult(Run is null
                ? new ServiceReply(404, "") : new ServiceReply(200, JsonSerializer.Serialize(new { run = Run, marks = new { opens = Mark } })) { Run = Run });
            if (verb == ServiceVerb.Get) return Task.FromResult(new ServiceReply(200, """[{"id":"candidate","from":"left"},{"id":"other","from":"elsewhere"}]""") { Run = AnswerRun });
            if (Lose) throw new HttpRequestException("answer lost");
            if (BrokenAnswer) return Task.FromResult(new ServiceReply(200, "{"));
            using var document = JsonDocument.Parse(json!);
            var key = Echo && document.RootElement.TryGetProperty("opens", out var opens) ? opens.GetString() : null;
            return Task.FromResult(new ServiceReply(200, JsonSerializer.Serialize(new
            {
                session = new { id = "opened" }, message = "opened", opens = key, transfer = Array.Empty<object>(),
            })) { Run = AnswerRun });
        }
        public void Dispose() { }
    }

    [Fact]
    public async Task A_marked_host_gets_the_key_and_its_answer_keeps_echo_and_transfer()
    {
        using var host = new Host();
        using var client = new ServiceClient(host);
        var answer = await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left");
        Assert.Equal("/api/protocol", host.Heard[0].Path);
        Assert.Contains("\"opens\":\"key\"", host.Heard[1].Body);
        Assert.Equal("opened", answer.SessionId);
        Assert.Equal("key", answer.Opens);
        Assert.Empty(answer.Transfer!);
        Assert.False(answer.Held);
        await client.Opens.OpenAsync("/api/sessions/chat", Body, "second", "left");
        Assert.Equal(1, host.Heard.Count(call => call.Path == "/api/protocol"));
    }

    [Fact]
    public async Task An_older_host_gets_no_key_and_a_first_answer_without_echo_is_todays_open()
    {
        using var host = new Host { Run = null, AnswerRun = null };
        using var client = new ServiceClient(host);
        var answer = await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left");
        Assert.DoesNotContain("opens", host.Heard[1].Body);
        Assert.Equal("opened", answer.SessionId);
        Assert.Null(answer.Opens);
        Assert.False(answer.Held);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("replacement")]
    public async Task A_changed_or_missing_run_is_read_again_before_the_next_keyed_step(string? run)
    {
        using var host = new Host { AnswerRun = run };
        using var client = new ServiceClient(host);
        await client.Opens.OpenAsync("/api/sessions/chat", Body, "first", "left");
        host.Run = run;
        await client.Opens.OpenAsync("/api/sessions/chat", Body, "second", "left");
        Assert.Equal("/api/protocol", host.Heard[2].Path);
        Assert.Equal("/api/sessions/chat", host.Heard[3].Path);
        if (run is null) Assert.DoesNotContain("opens", host.Heard[3].Body);
    }

    [Fact]
    public async Task A_lost_answer_reloads_marks_and_a_retried_answer_without_echo_is_held_with_candidates()
    {
        using var host = new Host { Lose = true };
        using var client = new ServiceClient(host);
        Assert.True((await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left")).Held);
        host.Lose = false;
        // Replacement after GET /api/protocol but before POST: the response has neither run nor echo.
        host.AnswerRun = null;
        host.Echo = false;
        var retryAt = host.Heard.Count;
        var retry = await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left");
        Assert.Equal("/api/protocol", host.Heard[retryAt].Path);
        Assert.True(retry.Held);
        Assert.Null(retry.SessionId);
        Assert.Equal(["candidate"], retry.Candidates);
    }

    [Fact]
    public async Task A_lost_answer_followed_by_an_unmarked_host_is_held_without_another_open()
    {
        using var host = new Host { Lose = true };
        using var client = new ServiceClient(host);
        await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left");
        host.Run = null;
        host.AnswerRun = null;
        host.Lose = false;
        var retryAt = host.Heard.Count;
        var answer = await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left");
        Assert.Equal("/api/protocol", host.Heard[retryAt].Path);
        Assert.Single(host.Heard.Where(call => call.Body is not null));
        Assert.True(answer.Held);
        Assert.Equal(["candidate"], answer.Candidates);
    }

    [Fact]
    public async Task An_unreadable_committed_answer_is_held_and_renegotiated_before_retry()
    {
        using var host = new Host { BrokenAnswer = true };
        using var client = new ServiceClient(host);
        Assert.True((await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left")).Held);
        var retryAt = host.Heard.Count;
        host.BrokenAnswer = false;
        var answer = await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left");
        Assert.Equal("/api/protocol", host.Heard[retryAt].Path);
        Assert.Equal("opened", answer.SessionId);
    }

    [Fact]
    public async Task A_first_marked_answer_without_echo_is_read_as_an_older_hosts_open()
    {
        using var host = new Host { Echo = false, AnswerRun = null };
        using var client = new ServiceClient(host);
        var answer = await client.Opens.OpenAsync("/api/sessions/chat", Body, "key", "left");
        Assert.Equal("opened", answer.SessionId);
        Assert.False(answer.Held);
    }
}
