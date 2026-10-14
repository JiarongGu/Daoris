using System.Net;
using System.Security.Cryptography;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's one send (DriverHttp.SendAsync) and the one download wrapper and streaming hash
/// (Downloads) the tool installer and the Claude release download share (DEDUP8). The words they throw
/// are pinned: callers and rehearsals read them.
/// </summary>
public sealed class DownloadsAndSendTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-downloads-" + Guid.NewGuid().ToString("N")[..8]);

    public DownloadsAndSendTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task A_non_success_send_throws_the_url_the_code_and_the_services_sentence()
    {
        using var http = DriverHttp.Client(null, new Answer(HttpStatusCode.Conflict, "{\"error\":\"too late\"}"));

        var error = await Assert.ThrowsAsync<DriverException>(
            () => DriverHttp.PostAsync(http, "http://host/x", "{}", CancellationToken.None));
        Assert.Equal("http://host/x answered 409: too late", error.Message);

        error = await Assert.ThrowsAsync<DriverException>(
            () => DriverHttp.DeleteAsync(http, "http://host/y", CancellationToken.None));
        Assert.Equal("http://host/y answered 409: too late", error.Message);

        error = await Assert.ThrowsAsync<DriverException>(
            () => DriverHttp.GetAsync(http, "http://host/z", CancellationToken.None));
        Assert.Equal("http://host/z answered 409: too late", error.Message);
    }

    [Fact]
    public async Task A_success_send_answers_the_payload()
    {
        using var http = DriverHttp.Client(null, new Answer(HttpStatusCode.OK, "{\"ok\":true}"));

        Assert.Equal("{\"ok\":true}", await DriverHttp.GetAsync(http, "http://host/x", CancellationToken.None));
        Assert.Equal("{\"ok\":true}", await DriverHttp.PostAsync(http, "http://host/x", "{}", CancellationToken.None));
        Assert.Equal("{\"ok\":true}", await DriverHttp.DeleteAsync(http, "http://host/x", CancellationToken.None));
    }

    [Fact]
    public async Task A_deliberate_refusal_is_news_and_anything_else_throws()
    {
        using var informed = DriverHttp.Client(null, new Answer(HttpStatusCode.Conflict, "{\"error\":\"stale\",\"information\":true}"));
        Assert.Equal("stale", await DriverHttp.PostInformableAsync(informed, "http://host/x", "{}", CancellationToken.None));

        using var walled = DriverHttp.Client(null, new Answer(HttpStatusCode.BadGateway, "{\"error\":\"down\"}"));
        var error = await Assert.ThrowsAsync<DriverException>(
            () => DriverHttp.PostInformableAsync(walled, "http://host/x", "{}", CancellationToken.None));
        Assert.Equal("http://host/x answered 502: down", error.Message);

        using var took = DriverHttp.Client(null, new Answer(HttpStatusCode.OK, "{}"));
        Assert.Null(await DriverHttp.PostInformableAsync(took, "http://host/x", "{}", CancellationToken.None));
    }

    [Fact]
    public async Task A_download_answers_null_for_a_404_and_the_callers_exception_otherwise()
    {
        using var missing = Downloads.Client(new Answer(HttpStatusCode.NotFound, ""));
        Assert.Null(await Downloads.GetAsync(missing, "https://lists.example/a", CancellationToken.None, Words));

        using var broken = Downloads.Client(new Answer(HttpStatusCode.InternalServerError, ""));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Downloads.GetAsync(broken, "https://lists.example/a", CancellationToken.None, Words));
        Assert.Equal("https://lists.example/a answered 500 — nothing was installed.", error.Message);
    }

    [Fact]
    public async Task An_unreachable_host_is_worded_with_its_name_and_the_transports_reason()
    {
        using var down = Downloads.Client(new Unreachable());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Downloads.GetAsync(down, "https://lists.example/a", CancellationToken.None, Words));
        Assert.Equal("could not reach lists.example — no route. Nothing was installed.", error.Message);
    }

    [Fact]
    public async Task A_download_is_streamed_to_disk_and_hashed_on_the_way()
    {
        var body = new byte[200_000];
        new Random(7).NextBytes(body);
        using var http = Downloads.Client(new Answer(HttpStatusCode.OK, body));
        var to = Path.Combine(_folder, "file.bin");

        var saved = await Downloads.SaveAsync(http, "https://lists.example/a", to, CancellationToken.None, Words);

        Assert.NotNull(saved);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(body)), saved.Value.Sha256);
        Assert.Equal(body.Length, saved.Value.Size);
        Assert.Equal(body, await File.ReadAllBytesAsync(to));
    }

    [Fact]
    public async Task A_client_carries_the_daoris_user_agent_and_no_timeout()
    {
        var seen = new Answer(HttpStatusCode.OK, "x");
        using var http = Downloads.Client(seen);

        await Downloads.GetAsync(http, "https://lists.example/a", CancellationToken.None, Words);

        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
        Assert.Equal("daoris", seen.UserAgent);
    }

    private static Exception Words(string said) => new InvalidOperationException(said);

    private sealed class Answer(HttpStatusCode status, byte[] body) : HttpMessageHandler
    {
        public Answer(HttpStatusCode status, string body) : this(status, System.Text.Encoding.UTF8.GetBytes(body)) { }

        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("no route");
    }
}
