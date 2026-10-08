using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// ORIGIN2: a local host answers only a request that names it by a loopback name. A website whose DNS name is pointed at
/// 127.0.0.1 (DNS rebinding) is its own origin here, so CORS and ORIGIN1's gate both see a same-origin page: its reads
/// would be answered, the index, the quests and the registration roots a loopback caller is given among them. Every
/// request under any other name is refused 403 before anything runs, with the house's sentence and a code; the names
/// every caller uses, <c>localhost</c>, <c>127.x.x.x</c> and <c>[::1]</c>, with any port, are answered as before.
/// </summary>
public sealed class ReboundHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    /// <summary>A website's name, pointed at this machine's loopback by its own DNS.</summary>
    private const string Rebound = "rebound.example:5177";

    /// <summary>What the rebound page is, by its browser's reckoning: its own origin, calling itself.</summary>
    private static readonly IReadOnlyDictionary<string, string> SameOrigin = new Dictionary<string, string>
    {
        ["Origin"] = "http://" + Rebound, ["Sec-Fetch-Site"] = "same-origin",
    };

    private static void AssertRefused(Answer answer, string what)
    {
        Assert.True(answer.Status == 403, $"{what} answered {answer.Status}: {answer.Body}");
        Assert.Equal(BrowserOrigins.HostSentence, answer.Error);
        Assert.Equal(BrowserOrigins.HostCode, answer.Json.GetProperty("code").GetString());
    }

    /// <summary>
    /// 🔴 The case: the rebound page reads what a loopback caller is given (the registry with its roots, the quests, the
    /// index) and writes as its own origin. Each is refused, and the yes it would give is not given.
    /// </summary>
    [Fact]
    public async Task A_name_rebound_to_the_loopback_is_refused_on_a_read_and_a_write()
    {
        // What the rebound page is after: answered by the host's own name, so the refusal below is the name's alone.
        var registry = await PageRequests.SendAsync(host, "GET", "/api/registry");
        Assert.Equal(200, registry.Status);
        var keeper = registry.Json.EnumerateArray().Single(row => row.GetProperty("repository").GetString() == "Keeper");
        Assert.Equal(host.RootOf("Keeper"), keeper.GetProperty("root").GetString());

        AssertRefused(await PageRequests.SendAsync(host, "GET", "/api/registry", SameOrigin, hostHeader: Rebound), "the registry");
        AssertRefused(await PageRequests.SendAsync(host, "GET", "/api/quests", SameOrigin, hostHeader: Rebound), "the quests");
        AssertRefused(await PageRequests.SendAsync(host, "GET", "/api/search?q=keeps", SameOrigin, hostHeader: Rebound), "a search");
        AssertRefused(await PageRequests.SendAsync(host, "GET", "/", hostHeader: Rebound), "the page");
        AssertRefused(
            await PageRequests.SendAsync(host, "POST", "/api/quests/probe/accept", SameOrigin, hostHeader: Rebound),
            "a no-body write as its own origin");
        AssertRefused(
            await PageRequests.SendAsync(host, "POST", "/api/quests/probe/accept", hostHeader: Rebound),
            "a write that names no origin");

        // A preflight is refused by the name too, before CORS is asked.
        var preflight = await host.Server.SendAsync(http =>
        {
            http.Request.Method = "OPTIONS";
            http.Request.Path = "/api/quests/probe/accept";
            http.Request.Host = new Microsoft.AspNetCore.Http.HostString(Rebound);
            http.Connection.RemoteIpAddress = DaorisHost.Loopback;
            http.Request.Headers.Origin = "http://" + Rebound;
            http.Request.Headers.AccessControlRequestMethod = "POST";
        });
        Assert.Equal(403, preflight.Response.StatusCode);
    }

    /// <summary>
    /// Every route the host mapped, reads and writes, by its own route table: a door added tomorrow is under the name's
    /// gate the day it lands, and so is a path nobody mapped.
    /// </summary>
    [Fact]
    public async Task Every_route_refuses_a_name_that_is_not_the_loopbacks()
    {
        var routes = host.Routes();
        Assert.Contains(("GET", "/api/registry"), routes);
        Assert.Contains(("GET", "/api/quests"), routes);
        Assert.Contains(("POST", "/api/quests/{id}/accept"), routes);

        var answered = new List<string>();
        foreach (var named in new[] { Rebound, "rebound.example", "localhost.rebound.example:5177", "192.168.1.20:5177", "0.0.0.0:5177" })
        {
            foreach (var (method, pattern) in routes)
            {
                var answer = await PageRequests.SendAsync(host, method, PageRequests.Concrete(pattern), hostHeader: named);
                if (answer.Status != 403) answered.Add($"{method} {pattern} under {named}: {answer.Status}");
                else AssertRefused(answer, $"{method} {pattern} under {named}");
            }
        }

        Assert.True(answered.Count == 0, "answered:\n" + string.Join("\n", answered));
        AssertRefused(await PageRequests.SendAsync(host, "GET", "/api/nothing-here", hostHeader: Rebound), "an unmapped path");
    }

    /// <summary>
    /// The names every caller uses: the shell at <c>127.0.0.1</c> (and its page, which calls that address from its own
    /// origin, D92), the CLI, the rehearsals, Playwright and the development proxy at <c>localhost</c>, any port, any
    /// case; the rest of 127/8 and IPv6's loopback. A request that names no host is no browser's, and a program's.
    /// </summary>
    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost:5177")]
    [InlineData("LocalHost:5196")]
    [InlineData("127.0.0.1:5177")]
    [InlineData("127.0.0.1")]
    [InlineData("127.8.9.10:5199")]
    [InlineData("[::1]:5177")]
    [InlineData("[::1]")]
    [InlineData("")]
    public async Task A_loopback_name_with_any_port_is_answered(string named)
    {
        var status = await PageRequests.SendAsync(host, "GET", "/api/status", hostHeader: named);
        Assert.True(status.Status == 200, $"{named} answered {status.Status}: {status.Body}");

        var quests = await PageRequests.SendAsync(host, "GET", "/api/quests", hostHeader: named);
        Assert.Equal(200, quests.Status);

        // The shell's page, pressing a door from its own origin at the host's address.
        var pressed = await PageRequests.SendAsync(host, "POST", "/api/quests/probe/accept", new Dictionary<string, string>
        {
            ["Origin"] = DesktopPage.Origin, ["Sec-Fetch-Site"] = "cross-site",
        }, hostHeader: named);
        Assert.Equal(404, pressed.Status);
    }

    /// <summary>The one definition both gates read: a loopback name, the port already taken off.</summary>
    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.255.255.254", true)]
    [InlineData("[::1]", true)]
    [InlineData("::1", true)]
    [InlineData("rebound.example", false)]
    [InlineData("localhost.rebound.example", false)]
    [InlineData("rebound.localhost", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("10.0.0.5", false)]
    [InlineData("[::]", false)]
    [InlineData("", false)]
    public void A_loopback_name_is_localhost_or_a_loopback_address(string name, bool loopback) =>
        Assert.Equal(loopback, BrowserOrigins.IsLoopbackName(name));

    /// <summary>
    /// ORIGIN1's own origin reads the same test, so a rebound page is never its own origin here even where the name's
    /// gate does not stand in front of it.
    /// </summary>
    [Theory]
    [InlineData("localhost:5177", "http://localhost:5177", true)]
    [InlineData("[::1]:5177", "http://[::1]:5177", true)]
    [InlineData(Rebound, "http://" + Rebound, false)]
    public void The_hosts_own_origin_is_only_ever_under_a_loopback_name(string named, string origin, bool own)
    {
        var request = new Microsoft.AspNetCore.Http.DefaultHttpContext().Request;
        request.Scheme = "http";
        request.Host = new Microsoft.AspNetCore.Http.HostString(named);
        Assert.Equal(own, BrowserOrigins.IsOwnOrigin(request, origin));
    }
}

/// <summary>ORIGIN2's log line, and the shared host, which is reached by its real name.</summary>
public sealed class ReboundHostLogTests
{
    private static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;

    /// <summary>One warning per refusal, by the route's pattern: never the name the page used, nor its address.</summary>
    [Fact]
    public async Task A_refusal_is_one_warning_by_its_route_and_never_the_name()
    {
        using var host = new LocalHost();
        const string Name = "name-only-the-host-header-holds.example:5177";

        Assert.Equal(403, (await PageRequests.SendAsync(
            host, "POST", "/api/quests/a-quest-id-in-the-path/accept", PageRequests.CrossSite, hostHeader: Name)).Status);

        var lines = host.LogLines();
        var logged = Parse(Assert.Single(lines, line => Parse(line).GetProperty("event").GetString() == BrowserOrigins.HostEvent));
        Assert.Equal("warn", logged.GetProperty("level").GetString());
        var data = logged.GetProperty("data");
        Assert.Equal("POST", data.GetProperty("method").GetString());
        Assert.Equal("/api/quests/{id}/accept", data.GetProperty("route").GetString());
        // The name refuses it first: ORIGIN1's gate never runs, so the request is one line, not two.
        Assert.DoesNotContain(lines, line => Parse(line).GetProperty("event").GetString() == BrowserOrigins.Event);
        Assert.DoesNotContain(lines, line => Parse(line).GetProperty("event").GetString() == "request.failed");
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain("name-only-the-host-header-holds", line);
            Assert.DoesNotContain("a-quest-id-in-the-path", line);
            Assert.DoesNotContain("127.0.0.1", line);
        });
    }

    /// <summary>
    /// A shared host is reached by whatever name its deployment gives it, which this build cannot know; its key gate is
    /// what keeps a page off it (D47 §7), since a browser never sends a bearer key on its own. So the name is not judged
    /// there.
    /// </summary>
    [Fact]
    public async Task A_shared_host_answers_its_machines_under_its_own_name()
    {
        using var host = new SharedHost();
        var key = (await host.MintAsync()).Key;

        var keyed = await PageRequests.SendAsync(
            host, "GET", "/api/quests", new Dictionary<string, string> { ["Authorization"] = "Bearer " + key },
            hostHeader: "daoris.team.example");
        Assert.Equal(200, keyed.Status);

        var unkeyed = await PageRequests.SendAsync(host, "GET", "/api/quests", hostHeader: "daoris.team.example");
        Assert.Equal(401, unkeyed.Status);
    }
}
