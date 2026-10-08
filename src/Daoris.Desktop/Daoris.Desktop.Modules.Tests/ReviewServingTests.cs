using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// REVIEWENV1d (D154 point 5; the review environment design §2.3 steps 1–4): the shell's own serving of a set-up step's build to
/// its tab, over its own CDP connection, against a Chromium's endpoint standing in and a real loopback server standing for the
/// person's own. Served at the rule's address and the build's base only, and on that tab only; every other path passes through to
/// the person's server; the person's server is never reached for the served paths; it stays served after the session's own
/// connection ends; and it is let go on the verdict, the tab left open. <c>ReviewShowingTests</c> in the driver holds when.
/// </summary>
public sealed class ReviewServingTests : IDisposable
{
    private readonly StandInChromium _chromium = new();
    private readonly PersonServer _person = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-review-serving-" + Guid.NewGuid().ToString("N")[..8], "dist", "app");
    private readonly ReviewTabs _tabs;

    public ReviewServingTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "assets"));
        File.WriteAllText(Path.Combine(_folder, "index.html"), "<!doctype html><base href=\"/v3/\"><title>BRANCH</title>");
        File.WriteAllText(Path.Combine(_folder, "main.js"), "document.body.dataset.app = 'branch';");
        File.WriteAllText(Path.Combine(_folder, "assets", "logo.svg"), "<svg/>");
        _tabs = new ReviewTabs(new Browser(_chromium.Port));
    }

    public void Dispose()
    {
        _tabs.Dispose();
        _chromium.Dispose();
        _person.Dispose();
        try { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(_folder))!, recursive: true); } catch (IOException) { }
    }

    private ReviewServe Serve => new("q2", "Review #q2", _folder, _person.Address, "/v3/", $"{_person.Address}/v3/reports/42") { SetUp = "desk/41" };

    [Fact]
    public async Task The_step_s_tab_is_opened_titled_for_its_quest_in_front_and_opened_once()
    {
        await _tabs.OpenAsync("q2", "Review #q2");
        await _tabs.OpenAsync("q2", "Review #q2");

        var tab = Assert.Single(_chromium.Pages);
        Assert.StartsWith("data:text/html", tab.Url, StringComparison.Ordinal);
        Assert.Contains("<title>Review #q2</title>", Uri.UnescapeDataString(tab.Url), StringComparison.Ordinal);
        Assert.Equal(2, tab.Activated);
        Assert.Empty(_tabs.Serving);
    }

    [Fact]
    public async Task Served_at_the_rule_s_address_and_base_only_from_the_folder_and_the_person_s_server_is_never_reached()
    {
        await _tabs.OpenAsync("q2", "Review #q2");
        await _tabs.ServeAsync(Serve);
        var tab = Assert.Single(_chromium.Pages);

        var page = await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/reports/42");
        var script = await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/main.js", "Script");
        var logo = await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/assets/logo.svg", "Image");

        Assert.Equal([[$"{_person.Address}/v3/*"]], _chromium.PatternsOn(tab.Id));
        Assert.Equal((200, "fulfilled", "text/html"), (page.Status, page.By, page.Type?.Split(';')[0]));
        Assert.Contains("<title>BRANCH</title>", page.Body, StringComparison.Ordinal);
        Assert.Equal(("document.body.dataset.app = 'branch';", "fulfilled"), (script.Body, script.By));
        Assert.StartsWith("text/javascript", script.Type, StringComparison.Ordinal);
        Assert.Equal(("<svg/>", "image/svg+xml"), (logo.Body, logo.Type));
        Assert.Empty(_person.Hits);
        Assert.Equal(Serve, Assert.Single(_tabs.Serving));
    }

    [Fact]
    public async Task Other_paths_pass_through_to_the_person_s_server()
    {
        await _tabs.ServeAsync(Serve);
        var tab = Assert.Single(_chromium.Pages);

        // Outside the base: the app's own calls, which the person's server answers as it does for them.
        var data = await _chromium.LoadAsync(tab.Id, $"{_person.Address}/api/reports", "XHR");
        // Under the base, naming no file of the build and no page: on to the person's server, as every other request goes.
        var call = await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/api/reports", "Fetch");

        Assert.Equal(("PERSON /api/reports", "network"), (data.Body, data.By));
        Assert.Equal(("PERSON /v3/api/reports", "network"), (call.Body, call.By));
        Assert.Equal(["/api/reports", "/v3/api/reports"], _person.Hits);
    }

    [Fact]
    public async Task Only_the_step_s_tab_is_served()
    {
        await _tabs.ServeAsync(Serve);
        var other = _chromium.Open("about:blank");

        var theirs = await _chromium.LoadAsync(other, $"{_person.Address}/v3/reports/42");

        Assert.Equal(("PERSON /v3/reports/42", "network"), (theirs.Body, theirs.By));
        Assert.Empty(_chromium.PatternsOn(other));
    }

    [Fact]
    public async Task Serving_takes_the_tab_to_where_the_set_up_left_it_and_brings_it_forward()
    {
        await _tabs.ServeAsync(Serve);

        var tab = Assert.Single(_chromium.Pages);
        Assert.Equal([$"{_person.Address}/v3/reports/42"], tab.Navigations);
        Assert.True(tab.Activated > 0);
    }

    [Fact]
    public async Task It_stays_served_after_the_session_s_own_connection_ends()
    {
        await _tabs.ServeAsync(Serve);
        var tab = Assert.Single(_chromium.Pages);

        // The session's browser server on the same tab, its own route over the same paths: it answers while it lives.
        await using (var session = await Session.AttachAsync(_chromium.Port, tab.Id, $"{_person.Address}/v3/*", fulfil: "SESSION"))
        {
            Assert.Equal("SESSION", (await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/reports/42")).Body);
        }

        // Its own let go of each request, then it ended: Daoris's answer stood either way.
        await using (var session = await Session.AttachAsync(_chromium.Port, tab.Id, $"{_person.Address}/v3/*", fulfil: null))
        {
            Assert.Contains("BRANCH", (await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/reports/42")).Body, StringComparison.Ordinal);
        }

        var reloaded = await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/reports/42");

        Assert.Equal("fulfilled", reloaded.By);
        Assert.Contains("BRANCH", reloaded.Body, StringComparison.Ordinal);
        Assert.Empty(_person.Hits);
        Assert.Single(_tabs.Serving);
    }

    [Fact]
    public async Task Let_go_on_the_verdict_the_tab_stays_and_the_person_s_server_answers_it_again()
    {
        await _tabs.ServeAsync(Serve);
        var tab = Assert.Single(_chromium.Pages);

        _tabs.Stop("q2");
        await Until(() => _chromium.PatternsOn(tab.Id).Count == 0);
        var after = await _chromium.LoadAsync(tab.Id, $"{_person.Address}/v3/reports/42");

        Assert.Empty(_tabs.Serving);
        Assert.Single(_chromium.Pages);
        Assert.Equal(("PERSON /v3/reports/42", "network"), (after.Body, after.By));
    }

    [Fact]
    public async Task A_tab_the_person_closes_is_served_no_more()
    {
        await _tabs.ServeAsync(Serve);
        var tab = Assert.Single(_chromium.Pages);

        await _chromium.CloseAsync(tab.Id);

        await Until(() => _tabs.Serving.Count == 0);
        // Shown again, it opens a tab of its own rather than reach for the one that went.
        await _tabs.ServeAsync(Serve);
        Assert.NotEqual(tab.Id, Assert.Single(_chromium.Pages).Id);
    }

    [Fact]
    public async Task Serving_again_replaces_what_the_tab_was_served()
    {
        await _tabs.ServeAsync(Serve);
        var tab = Assert.Single(_chromium.Pages);
        var newer = Path.Combine(Path.GetDirectoryName(_folder)!, "next");
        Directory.CreateDirectory(newer);
        File.WriteAllText(Path.Combine(newer, "index.html"), "<title>NEWER</title>");

        await _tabs.ServeAsync(Serve with { Folder = newer, Base = "/", SetUp = "desk/42" });
        await Until(() => _chromium.PatternsOn(tab.Id).Count == 1);

        Assert.Equal([[$"{_person.Address}/*"]], _chromium.PatternsOn(tab.Id));
        Assert.Contains("NEWER", (await _chromium.LoadAsync(tab.Id, $"{_person.Address}/reports")).Body, StringComparison.Ordinal);
        Assert.Equal("desk/42", Assert.Single(_tabs.Serving).SetUp);
    }

    [Fact]
    public async Task No_browser_to_bring_up_is_said_and_nothing_is_served()
    {
        using var none = new ReviewTabs(new Browser(port: null));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => none.ServeAsync(Serve));

        Assert.Contains("browser", refused.Message, StringComparison.Ordinal);
        Assert.Empty(none.Serving);
    }

    [Theory]
    [InlineData("/v3/reports/42", "Document", "index")]
    [InlineData("/v3/", "Document", "index")]
    [InlineData("/v3/main.js", "Script", "file")]
    [InlineData("/v3/main.js", "Document", "file")]
    [InlineData("/v3/assets/logo.svg", "Image", "file")]
    [InlineData("/v3/missing.js", "Script", "pass")]
    [InlineData("/v3/assets/", "Fetch", "pass")]
    [InlineData("/api/data", "XHR", "pass")]
    // Dot segments are resolved before anything is read, as the browser resolves them: this one is outside the base.
    [InlineData("/v3/%2e%2e/secret.txt", "Script", "pass")]
    [InlineData("/v3/assets%2f..%2f..%2fsecret.txt", "Script", "refused")]
    [InlineData("/v3/a%5c..%5c..%5csecret.txt", "Script", "refused")]
    [InlineData("/v3/C:%2fWindows%2fwin.ini", "Script", "refused")]
    public void A_request_is_answered_from_the_build_handed_on_or_refused(string path, string type, string expected)
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_folder)!, "secret.txt"), "not served");

        var answer = ReviewRequests.Answer(Serve, $"{_person.Address}{path}", type, "GET");

        Assert.Equal(expected, answer.Kind switch
        {
            ReviewAnswerKind.File => "file",
            ReviewAnswerKind.Index => "index",
            ReviewAnswerKind.Pass => "pass",
            _ => "refused",
        });
        if (answer.Path is { } file) Assert.StartsWith(_folder + Path.DirectorySeparatorChar, file, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_request_that_is_not_a_read_or_at_another_origin_is_handed_on()
    {
        Assert.Equal(ReviewAnswerKind.Pass, ReviewRequests.Answer(Serve, $"{_person.Address}/v3/main.js", "Fetch", "POST").Kind);
        Assert.Equal(ReviewAnswerKind.Pass, ReviewRequests.Answer(Serve, "https://data.example/v3/main.js", "Script", "GET").Kind);
    }

    [Theory]
    [InlineData("main.js", "text/javascript; charset=utf-8")]
    [InlineData("index.html", "text/html; charset=utf-8")]
    [InlineData("styles.CSS", "text/css; charset=utf-8")]
    [InlineData("data.json", "application/json; charset=utf-8")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("module.wasm", "application/wasm")]
    [InlineData("blob.bin", "application/octet-stream")]
    public void A_file_is_answered_with_its_type(string name, string type) => Assert.Equal(type, ReviewRequests.TypeOf(name));

    /// <summary>Wait until <paramref name="condition"/> holds, the socket's own pace, or fail after ten seconds.</summary>
    private static async Task Until(Func<bool> condition)
    {
        for (var waited = 0; !condition(); waited += 25)
        {
            if (waited > 10_000) throw new TimeoutException("the condition never held");
            await Task.Delay(25);
        }
    }

    /// <summary>Daoris's browser standing in, its endpoint the stand-in Chromium's, or none.</summary>
    private sealed class Browser(int? port) : IInAppBrowser
    {
        public Task<string?> EnsureAsync(CancellationToken ct = default) =>
            Task.FromResult(port is { } at ? InAppBrowser.Endpoint(at) : null);

        public void Show()
        {
        }

        public void Open(string address)
        {
        }
    }

    /// <summary>A session's browser server on the same tab, over a connection of its own: its own route, fulfilling or letting go.</summary>
    private sealed class Session : IAsyncDisposable
    {
        private readonly CdpChannel _channel;

        private Session(CdpChannel channel) => _channel = channel;

        public static async Task<Session> AttachAsync(int port, string target, string pattern, string? fulfil)
        {
            var channel = await CdpChannel.ConnectAsync(port, CancellationToken.None);
            var attached = await channel.CallAsync("Target.attachToTarget", new { targetId = target, flatten = true }, null, CancellationToken.None);
            var session = attached.GetProperty("sessionId").GetString()!;
            channel.Heard += (method, parameters, from) =>
            {
                if (method != "Fetch.requestPaused" || from != session) return;
                var requestId = parameters.GetProperty("requestId").GetString();
                _ = fulfil is null
                    ? channel.CallAsync("Fetch.continueRequest", new { requestId }, session, CancellationToken.None)
                    : channel.CallAsync("Fetch.fulfillRequest", new
                    {
                        requestId, responseCode = 200,
                        responseHeaders = new[] { new { name = "content-type", value = "text/html" } },
                        body = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fulfil)),
                    }, session, CancellationToken.None);
            };
            await channel.CallAsync("Fetch.enable", new { patterns = new[] { new { urlPattern = pattern, requestStage = "Request" } } }, session, CancellationToken.None);
            return new Session(channel);
        }

        public ValueTask DisposeAsync() => _channel.DisposeAsync();
    }
}
