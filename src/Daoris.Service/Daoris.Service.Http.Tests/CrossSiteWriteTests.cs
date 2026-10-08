using System.Text;
using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// One request as a browser shapes it, from this machine: the headers a page's request carries, the host it named, and a
/// body of a simple request's kind.
/// </summary>
internal static class PageRequests
{
    /// <summary>A page on another site, as its browser names it.</summary>
    public const string Elsewhere = "https://evil.example";

    /// <summary>The headers a page on another site sends with every write.</summary>
    public static readonly IReadOnlyDictionary<string, string> CrossSite = new Dictionary<string, string>
    {
        ["Origin"] = Elsewhere, ["Sec-Fetch-Site"] = "cross-site",
    };

    /// <summary>A pattern made into a path a request can take: every parameter filled with <paramref name="value"/>.</summary>
    public static string Concrete(string pattern, string value = "probe") =>
        System.Text.RegularExpressions.Regex.Replace(pattern, @"\{[^}]+\}", value);

    /// <summary>Every write route the host mapped: what a browser may send without reading the answer.</summary>
    public static IReadOnlyList<(string Method, string Pattern)> Writes(DaorisHost host) =>
        host.Routes().Where(route => route.Method is "POST" or "PUT" or "PATCH" or "DELETE").ToList();

    public static async Task<Answer> SendAsync(
        DaorisHost host, string method, string path, IReadOnlyDictionary<string, string>? headers = null,
        string? body = null, string contentType = "text/plain", string? hostHeader = null)
    {
        var context = await host.Server.SendAsync(http =>
        {
            http.Request.Method = method;
            http.Request.Path = path;
            http.Connection.RemoteIpAddress = DaorisHost.Loopback;
            if (hostHeader is not null) http.Request.Host = new HostString(hostHeader);
            foreach (var (name, value) in headers ?? new Dictionary<string, string>()) http.Request.Headers[name] = value;
            if (body is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                http.Request.Body = new MemoryStream(bytes);
                http.Request.ContentType = contentType;
                http.Request.ContentLength = bytes.Length;
                // A context built by hand says it can carry no body, and a route would bind none.
                http.Features.Set<IHttpRequestBodyDetectionFeature>(new HasBody());
            }
        });

        using var reader = new StreamReader(context.Response.Body);
        return new Answer(context.Response.StatusCode, await reader.ReadToEndAsync(), context.Response.ContentType);
    }

    private sealed class HasBody : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}

/// <summary>
/// ORIGIN1: no website can press a door on the local host. CORS keeps a page from READING an answer, not from sending
/// the request: a <c>POST</c> with no body, or a text or form body, is a simple request a browser sends without asking
/// first, so before this a page on any site, open in any browser on this machine, could accept a departure at
/// <c>/accept</c>. A write whose <c>Origin</c> is not one this host allows, or that names no origin and that its browser
/// says came from another site, is refused 403 with the house's sentence and a code; the shell's page, the host's own
/// page, and a client that sends neither header (the CLI, the driver) are answered as before.
/// </summary>
public sealed class CrossSiteWriteTests(LocalHost host) : IClassFixture<LocalHost>
{
    private static void AssertRefused(Answer answer, string what)
    {
        Assert.True(answer.Status == 403, $"{what} answered {answer.Status}: {answer.Body}");
        Assert.Equal(BrowserOrigins.Sentence, answer.Error);
        Assert.Equal(BrowserOrigins.Code, answer.Json.GetProperty("code").GetString());
    }

    /// <summary>A quest done with a departure, held for the person's yes at <c>/accept</c> (D133 §4).</summary>
    private async Task<string> HeldForTheYesAsync()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Build the weekly report through the v3 bridge.",
        });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var published = await host.PostAsync($"/api/asks/{ask}/publish", new
        {
            to = "Keeper", title = "Build the weekly report", body = "Reached through the bridge.",
            requirements = new[] { new { quote = "through the v3 bridge", check = "It opens through the bridge's route." } },
        });
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);
        var closed = await host.PostAsync($"/api/quests/{quest}/respond", new
        {
            action = "done", reason = "Built.",
            answers = new object[]
            {
                new { requirement = 1, departed = "The weekly totals needed a route of their own.", quote = "the v3 bridge" },
            },
        });
        Assert.Equal(200, closed.Status);
        Assert.True(closed.Json.GetProperty("quest").GetProperty("held").GetBoolean());
        return quest;
    }

    private async Task<JsonElement> QuestAsync(string quest) =>
        (await host.GetAsync("/api/quests")).Json.EnumerateArray().Single(row => row.GetProperty("id").GetString() == quest);

    /// <summary>
    /// 🔴 The case: a page elsewhere posts to <c>/accept</c> with no body, a text body or a form body, each a simple
    /// request its browser sends without a preflight. The yes is not given; the shell's page gives it after.
    /// </summary>
    [Fact]
    public async Task A_page_on_another_site_cannot_give_the_yes_to_a_departure()
    {
        var quest = await HeldForTheYesAsync();
        var accept = $"/api/quests/{quest}/accept";

        AssertRefused(await PageRequests.SendAsync(host, "POST", accept, PageRequests.CrossSite), "a POST with no body");
        AssertRefused(
            await PageRequests.SendAsync(host, "POST", accept, PageRequests.CrossSite, body: "yes"), "a POST with a text body");
        AssertRefused(
            await PageRequests.SendAsync(
                host, "POST", accept, PageRequests.CrossSite, body: "a=b", contentType: "application/x-www-form-urlencoded"),
            "a POST with a form body");
        Assert.True((await QuestAsync(quest)).GetProperty("held").GetBoolean());

        // The shell's page lives on its engine's app origin and calls the loopback (D92): another site, by its browser's
        // reckoning, and allowed by its name.
        var pressed = await PageRequests.SendAsync(host, "POST", accept, new Dictionary<string, string>
        {
            ["Origin"] = DesktopPage.Origin, ["Sec-Fetch-Site"] = "cross-site",
        });
        Assert.Equal(200, pressed.Status);
        Assert.False(pressed.Json.GetProperty("quest").GetProperty("held").GetBoolean());
    }

    /// <summary>
    /// Every write route, by the host's own route table, so a door added tomorrow is under the gate the day it lands:
    /// refused to a page elsewhere whatever the route would have made of the request.
    /// </summary>
    [Fact]
    public async Task Every_write_route_refuses_a_page_on_another_site()
    {
        var writes = PageRequests.Writes(host);
        Assert.Contains(("POST", "/api/quests/{id}/accept"), writes);
        Assert.Contains(("POST", "/api/quests/{id}/done"), writes);
        Assert.Contains(("DELETE", "/api/quests/{id}"), writes);
        Assert.True(writes.Count >= 30, $"only {writes.Count} write routes were enumerated");

        var pages = new (string What, Dictionary<string, string> Headers, string? Host)[]
        {
            ("a page elsewhere", new() { ["Origin"] = PageRequests.Elsewhere, ["Sec-Fetch-Site"] = "cross-site" }, null),
            ("a page elsewhere, by its origin alone", new() { ["Origin"] = PageRequests.Elsewhere }, null),
            ("a page elsewhere whose browser names no origin", new() { ["Sec-Fetch-Site"] = "cross-site" }, null),
            ("a page with an opaque origin", new() { ["Origin"] = "null", ["Sec-Fetch-Site"] = "cross-site" }, null),
            // Another port on the loopback is the same site to a browser: any local server's page.
            ("a page on another loopback port", new() { ["Origin"] = "http://localhost:3000", ["Sec-Fetch-Site"] = "same-site" }, "localhost:5177"),
            ("a page on another loopback port that names no origin", new() { ["Sec-Fetch-Site"] = "same-site" }, "localhost:5177"),
            // A name rebound to the loopback: the page is its own origin, under a name that is not this machine's.
            ("a page under a rebound name", new() { ["Origin"] = "http://rebound.example:5177", ["Sec-Fetch-Site"] = "same-origin" }, "rebound.example:5177"),
        };

        var executed = new List<string>();
        foreach (var (method, pattern) in writes)
        {
            foreach (var (what, headers, named) in pages)
            {
                var answer = await PageRequests.SendAsync(host, method, PageRequests.Concrete(pattern), headers, hostHeader: named);
                if (answer.Status != 403) executed.Add($"{method} {pattern} from {what}: {answer.Status}");
                else AssertRefused(answer, $"{method} {pattern} from {what}");
            }
        }

        Assert.True(executed.Count == 0, "reached the route:\n" + string.Join("\n", executed));

        // The gate is the method, not the route table: a path nobody mapped is refused before it is looked up.
        AssertRefused(await PageRequests.SendAsync(host, "POST", "/api/nothing-here", PageRequests.CrossSite), "an unmapped path");
    }

    /// <summary>
    /// The callers this host has: the shell's page, the host's own page (a browser on its address, and the platform in
    /// the web gate), and a client that sends neither header. None of them is refused on any write route.
    /// </summary>
    [Fact]
    public async Task The_shells_page_the_hosts_own_page_and_a_client_with_no_origin_are_answered()
    {
        var callers = new (string What, Dictionary<string, string> Headers, string? Host)[]
        {
            ("the shell's page", new() { ["Origin"] = DesktopPage.Origin, ["Sec-Fetch-Site"] = "cross-site" }, "127.0.0.1:5177"),
            ("the host's own page", new() { ["Origin"] = "http://localhost:5177", ["Sec-Fetch-Site"] = "same-origin" }, "localhost:5177"),
            ("the host's own page by its address", new() { ["Origin"] = "http://127.0.0.1:5196", ["Sec-Fetch-Site"] = "same-origin" }, "127.0.0.1:5196"),
            ("the host's own page over IPv6", new() { ["Origin"] = "http://[::1]:5177", ["Sec-Fetch-Site"] = "same-origin" }, "[::1]:5177"),
            ("a client with no origin", new(), null),
            ("a browser's own navigation", new() { ["Sec-Fetch-Site"] = "none" }, null),
            ("a same-origin request that names no origin", new() { ["Sec-Fetch-Site"] = "same-origin" }, null),
        };

        foreach (var (method, pattern) in PageRequests.Writes(host))
        {
            foreach (var (what, headers, named) in callers)
            {
                var answer = await PageRequests.SendAsync(
                    host, method, PageRequests.Concrete(pattern), headers,
                    body: method == "DELETE" ? null : "{}", contentType: "application/json", hostHeader: named);

                Assert.True(answer.Status != 403, $"{method} {pattern} refused {what}: {answer.Body}");
            }
        }
    }

    /// <summary>
    /// A read is CORS's, as before: a page elsewhere is answered and its browser keeps the answer from it, and a
    /// preflight is CORS's to answer, which allows the shell's page and nobody else.
    /// </summary>
    [Fact]
    public async Task A_read_and_a_preflight_are_left_to_CORS()
    {
        var read = await PageRequests.SendAsync(host, "GET", "/api/status", PageRequests.CrossSite);
        Assert.Equal(200, read.Status);

        var elsewhere = await host.Server.SendAsync(http =>
        {
            http.Request.Method = "OPTIONS";
            http.Request.Path = "/api/quests/probe/accept";
            http.Connection.RemoteIpAddress = DaorisHost.Loopback;
            http.Request.Headers.Origin = PageRequests.Elsewhere;
            http.Request.Headers.AccessControlRequestMethod = "POST";
        });
        Assert.NotEqual(403, elsewhere.Response.StatusCode);
        Assert.False(elsewhere.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));

        var shell = await host.Server.SendAsync(http =>
        {
            http.Request.Method = "OPTIONS";
            http.Request.Path = "/api/quests/probe/accept";
            http.Connection.RemoteIpAddress = DaorisHost.Loopback;
            http.Request.Headers.Origin = DesktopPage.Origin;
            http.Request.Headers.AccessControlRequestMethod = "POST";
        });
        Assert.Equal(DesktopPage.Origin, shell.Response.Headers.AccessControlAllowOrigin.ToString());
    }
}

/// <summary>ORIGIN1's other hosts: the development UI's origin when one is named, and a shared host.</summary>
public sealed class CrossSiteWriteHostTests
{
    private static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;

    /// <summary>
    /// The refusal is one warning in the machine log, by the route's pattern and which header refused it: never the
    /// page's address, never the body, and no failed request beside it.
    /// </summary>
    [Fact]
    public async Task A_refusal_is_one_warning_by_its_route_and_never_the_page_or_the_body()
    {
        using var host = new LocalHost();
        const string Page = "https://words-only-the-origin-holds.example";
        const string Words = "words-only-the-body-holds";

        var refused = await PageRequests.SendAsync(
            host, "POST", "/api/quests/a-quest-id-in-the-path/accept",
            new Dictionary<string, string> { ["Origin"] = Page, ["Sec-Fetch-Site"] = "cross-site" }, body: Words);
        Assert.Equal(403, refused.Status);

        var lines = host.LogLines();
        var logged = Parse(Assert.Single(lines, line => Parse(line).GetProperty("event").GetString() == BrowserOrigins.Event));
        Assert.Equal("warn", logged.GetProperty("level").GetString());
        var data = logged.GetProperty("data");
        Assert.Equal("POST", data.GetProperty("method").GetString());
        Assert.Equal("/api/quests/{id}/accept", data.GetProperty("route").GetString());
        Assert.Equal("origin", data.GetProperty("by").GetString());
        Assert.Equal("cross-site", data.GetProperty("site").GetString());
        Assert.DoesNotContain(lines, line => Parse(line).GetProperty("event").GetString() == "request.failed");
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain("words-only-the-origin-holds", line);
            Assert.DoesNotContain(Words, line);
            Assert.DoesNotContain("a-quest-id-in-the-path", line);
        });

        // A browser that names no origin is refused by what it says of the site, and the line says so.
        Assert.Equal(403, (await PageRequests.SendAsync(
            host, "DELETE", "/api/quests/another", new Dictionary<string, string> { ["Sec-Fetch-Site"] = "same-site" })).Status);
        var bySite = Parse(host.LogLines().Last(line => Parse(line).GetProperty("event").GetString() == BrowserOrigins.Event))
            .GetProperty("data");
        Assert.Equal("site", bySite.GetProperty("by").GetString());
        Assert.Equal("same-site", bySite.GetProperty("site").GetString());
    }

    /// <summary>The development UI's origin, when <c>DAORIS_WEB_ORIGIN</c> names one, is CORS's and the gate's alike.</summary>
    [Fact]
    public async Task The_development_uis_named_origin_is_answered_and_its_neighbour_is_not()
    {
        using var host = new DaorisHost(
            ServiceMode.Local, environment: new Dictionary<string, string?> { ["DAORIS_WEB_ORIGIN"] = "http://localhost:5178" });

        var named = await PageRequests.SendAsync(host, "POST", "/api/quests/probe/accept", new Dictionary<string, string>
        {
            ["Origin"] = "http://localhost:5178", ["Sec-Fetch-Site"] = "same-site",
        }, hostHeader: "localhost:5177");
        Assert.Equal(404, named.Status);

        var neighbour = await PageRequests.SendAsync(host, "POST", "/api/quests/probe/accept", new Dictionary<string, string>
        {
            ["Origin"] = "http://localhost:5179", ["Sec-Fetch-Site"] = "same-site",
        }, hostHeader: "localhost:5177");
        Assert.Equal(403, neighbour.Status);
    }

    /// <summary>
    /// A shared host is on the network, so a browser reaches it too. Its key gate holds today, since a browser never
    /// sends a bearer key on its own; the origin gate stands in front of it all the same, so a browser credential the
    /// remote gains later is no door for a page elsewhere. Its clients are machines, which send no origin.
    /// </summary>
    [Fact]
    public async Task A_shared_host_refuses_a_page_elsewhere_even_with_a_key_and_answers_its_machines()
    {
        using var host = new SharedHost();
        var key = (await host.MintAsync()).Key;
        var keyed = new Dictionary<string, string> { ["Authorization"] = "Bearer " + key };

        var writes = PageRequests.Writes(host);
        Assert.Contains(("POST", "/api/quests/operations"), writes);
        foreach (var (method, pattern) in writes)
        {
            var path = PageRequests.Concrete(pattern);
            var elsewhere = await PageRequests.SendAsync(
                host, method, path, new Dictionary<string, string>(keyed) { ["Origin"] = PageRequests.Elsewhere });
            Assert.True(elsewhere.Status == 403, $"{method} {pattern} answered a page elsewhere {elsewhere.Status}");
            Assert.Equal(BrowserOrigins.Code, elsewhere.Json.GetProperty("code").GetString());

            // The shell's page is a local host's caller only (D92): a shared host allows it nothing.
            var shell = await PageRequests.SendAsync(
                host, method, path, new Dictionary<string, string>(keyed) { ["Origin"] = DesktopPage.Origin });
            Assert.True(shell.Status == 403, $"{method} {pattern} answered the shell's page {shell.Status}");

            var machine = await PageRequests.SendAsync(
                host, method, path, keyed, body: method == "DELETE" ? null : "{}", contentType: "application/json");
            Assert.True(machine.Status is not (401 or 403), $"{method} {pattern} refused a keyed machine: {machine.Body}");
        }

        // A read is CORS's and the key's, as before.
        Assert.Equal(200, (await PageRequests.SendAsync(
            host, "GET", "/api/status", new Dictionary<string, string>(keyed) { ["Origin"] = PageRequests.Elsewhere })).Status);
    }
}
