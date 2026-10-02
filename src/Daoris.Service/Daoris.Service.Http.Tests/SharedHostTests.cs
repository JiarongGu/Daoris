using System.Security.Cryptography;
using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>A shared host: the team's deployment, fed and never scanned (D47 §4).</summary>
public sealed class SharedHost() : DaorisHost(ServiceMode.Shared)
{
    /// <summary>A key for one person on one machine, live for a month (D47 §7).</summary>
    public async Task<MintedKey> MintAsync(string name = "someone@a-machine", TimeSpan? lifetime = null, DateTimeOffset? minted = null) =>
        await Composed.Keys.MintAsync(name, lifetime ?? TimeSpan.FromDays(30), minted ?? DateTimeOffset.UtcNow);
}

/// <summary>
/// The shared host's doors (HTTP1, D47 §7): every route under the gate and none outside it; a valid
/// minted key answered, anything else refused with a sentence that names an expired or revoked key by
/// its prefix and never repeats what was presented; no page; no machine path in any answer, to any
/// caller; and no delete door, because a person deletes on their own machine (D95).
/// </summary>
public sealed class SharedHostTests(SharedHost host) : IClassFixture<SharedHost>
{
    private const string NoKey =
        "this deployment answers only minted keys — ask an operator for one, sent as a bearer token";

    /// <summary>A pattern made into a path a request can take: every parameter filled with <paramref name="value"/>.</summary>
    private static string Concrete(string pattern, string value = "probe") =>
        System.Text.RegularExpressions.Regex.Replace(pattern, @"\{[^}]+\}", value);

    /// <summary>What a request to <paramref name="method"/> carries: a body where a body is read.</summary>
    private static string? BodyFor(string method) => method is "POST" or "PUT" ? "{}" : null;

    private async Task RegisterAsync(string repository, string key, string? root = null) =>
        Assert.Equal(200, (await host.PostAsync("/api/registry", new
        {
            repository, packs = Array.Empty<string>(), root, join = true, shareKnowledge = true,
            domain = new { summary = $"{repository}'s own area.", owns = new[] { "its own tree" }, accepts = new[] { "a quest" } },
        }, key: key)).Status);

    /// <summary>
    /// The enumeration is the host's own route table, so a door added tomorrow is under test the day it
    /// lands. It must reach the shared-only doors, or it is proving the gate over fewer routes than exist.
    /// </summary>
    [Fact]
    public void Every_route_a_shared_host_maps_is_under_the_gate()
    {
        var routes = host.Routes();

        Assert.Contains(("POST", "/api/feed/sessions"), routes);
        Assert.Contains(("POST", "/api/quests/operations"), routes);
        Assert.Contains(("GET", "/api/registry"), routes);
        Assert.All(routes, route => Assert.StartsWith("/api/", route.Pattern));
    }

    /// <summary>🔴 Reads included (D47 §7): a remote serving the family's knowledge to an unkeyed GET is the leak with no key leaked.</summary>
    [Fact]
    public async Task Every_api_route_refuses_a_caller_without_a_key()
    {
        var routes = host.Routes();
        Assert.True(routes.Count >= 20, $"only {routes.Count} routes were enumerated");

        foreach (var (method, pattern) in routes)
        {
            var answer = await host.SendAsync(method, Concrete(pattern), DaorisHost.Loopback, json: BodyFor(method));

            Assert.True(answer.Status == 401, $"{method} {pattern} answered {answer.Status} without a key");
            Assert.Equal(NoKey, answer.Error);
        }

        // The gate is the prefix, not the route table: a path nobody mapped is refused before it is looked up.
        Assert.Equal(401, (await host.GetAsync("/api/nothing-here")).Status);
    }

    [Theory]
    [InlineData("Basic c29tZW9uZTpzZWNyZXQ=")]
    [InlineData("Bearer ")]
    [InlineData("bearer dk_0123456789abcdef")]
    public async Task A_header_that_is_not_a_bearer_key_is_refused(string header)
    {
        var context = await host.Server.SendAsync(http =>
        {
            http.Request.Method = "GET";
            http.Request.Path = "/api/status";
            http.Connection.RemoteIpAddress = DaorisHost.Loopback;
            http.Request.Headers.Authorization = header;
        });

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task Every_api_route_answers_a_valid_minted_key()
    {
        var key = (await host.MintAsync()).Key;

        foreach (var (method, pattern) in host.Routes())
        {
            var answer = await host.SendAsync(method, Concrete(pattern), DaorisHost.Loopback, key, BodyFor(method));

            Assert.True(answer.Status != 401, $"{method} {pattern} refused a valid key: {answer.Body}");
        }

        Assert.Equal(200, (await host.GetAsync("/api/status", key: key)).Status);
    }

    /// <summary>
    /// A key nobody minted is refused without a word about what was presented — and so is a near miss on
    /// a real key's prefix, which must look exactly like a stranger's, or guessing would teach something.
    /// </summary>
    [Fact]
    public async Task An_unknown_key_or_a_near_miss_is_refused_without_echoing_it()
    {
        var real = (await host.MintAsync("near-miss@a-machine")).Key;
        var nearMiss = real[..^1] + (real[^1] == '0' ? '1' : '0');
        var stranger = "dk_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

        foreach (var presented in new[] { stranger, nearMiss, "not-a-key-at-all" })
        {
            var refused = await host.GetAsync("/api/registry", key: presented);

            Assert.Equal(401, refused.Status);
            Assert.Equal(NoKey, refused.Error);
            Assert.DoesNotContain(presented, refused.Body);
            Assert.DoesNotContain(presented[3..11], refused.Body);
        }
    }

    /// <summary>An expired key is named back to its holder by its prefix — the audit handle — and never shown (§5b).</summary>
    [Fact]
    public async Task An_expired_key_is_named_by_its_prefix_and_never_echoed()
    {
        var expired = await host.MintAsync("expired@a-machine", TimeSpan.FromDays(1), DateTimeOffset.UtcNow.AddDays(-2));

        var refused = await host.GetAsync("/api/quests", key: expired.Key);

        Assert.Equal(401, refused.Status);
        Assert.StartsWith($"key `{expired.Record.Prefix}` expired {expired.Record.Expires:yyyy-MM-dd}", refused.Error);
        Assert.DoesNotContain(expired.Key, refused.Body);
        Assert.DoesNotContain(expired.Key[11..], refused.Body);
    }

    [Fact]
    public async Task A_revoked_key_is_named_by_its_prefix_and_never_echoed()
    {
        var revoked = await host.MintAsync("revoked@a-machine");
        Assert.Equal(200, (await host.GetAsync("/api/status", key: revoked.Key)).Status);
        Assert.True(await host.Composed.Keys.RevokeAsync(revoked.Record.Prefix, DateTimeOffset.UtcNow));

        var refused = await host.GetAsync("/api/status", key: revoked.Key);

        Assert.Equal(401, refused.Status);
        Assert.StartsWith($"key `{revoked.Record.Prefix}` was revoked", refused.Error);
        Assert.DoesNotContain(revoked.Key, refused.Body);
        Assert.DoesNotContain(revoked.Key[11..], refused.Body);
    }

    /// <summary>A refusal leaves no key in the machine's log either — the failure path is where one would leak (§5b).</summary>
    [Fact]
    public async Task No_presented_key_reaches_the_machine_log()
    {
        var expired = await host.MintAsync("logged@a-machine", TimeSpan.FromDays(1), DateTimeOffset.UtcNow.AddDays(-2));
        var stranger = "dk_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        await host.GetAsync("/api/registry", key: expired.Key);
        await host.GetAsync("/api/registry", key: stranger);

        var log = string.Join('\n', host.LogLines());

        Assert.Contains("app.started", log);
        Assert.DoesNotContain(expired.Key[3..], log);
        Assert.DoesNotContain(stranger[3..], log);
    }

    /// <summary>
    /// The remote is an API until person-auth exists (D47 §7): the page a local host serves from the very
    /// same web root is not served here, keyed or not.
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    public async Task No_page_is_served(string path)
    {
        var key = (await host.MintAsync()).Key;

        foreach (var answer in new[] { await host.GetAsync(path), await host.GetAsync(path, key: key) })
        {
            Assert.Equal(404, answer.Status);
            Assert.DoesNotContain(DaorisHost.PageMarker, answer.Body);
        }
    }

    /// <summary>
    /// 🔴 No machine path in any answer, to any caller — the loopback included, because in shared mode the
    /// answer is no for everyone (D47 §4). A registration that arrived carrying a root is stored without
    /// it, and every read this host maps is searched for any path under the scratch this machine made.
    /// </summary>
    [Fact]
    public async Task No_machine_path_appears_in_any_answer()
    {
        var key = (await host.MintAsync("paths@a-machine")).Key;
        var sentRoot = Path.Combine(host.Scratch, "checkouts", "PathKeeper");
        await RegisterAsync("PathKeeper", key, root: sentRoot);
        await RegisterAsync("PathAsker", key, root: Path.Combine(host.Scratch, "checkouts", "PathAsker"));
        var content = "a screenshot's bytes"u8.ToArray();
        var published = await host.PostAsync("/api/quests", new
        {
            from = "PathAsker", to = "PathKeeper", title = "Paths stay home", body = "Named files only.",
            attachments = new[] { new { name = "shot.png", sha256 = Convert.ToHexStringLower(SHA256.HashData(content)), bytes = content.Length } },
        }, key: key);
        Assert.Equal(200, published.Status);
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;

        var registry = await host.GetAsync("/api/registry", key: key);
        var row = registry.Json.EnumerateArray().Single(r => r.GetProperty("repository").GetString() == "PathKeeper");
        Assert.False(row.TryGetProperty("root", out _));
        var attachment = published.Json.GetProperty("quest").GetProperty("attachments")[0];
        Assert.False(attachment.TryGetProperty("path", out _));

        var forbidden = new[] { host.Scratch, JsonEncodedText.Encode(host.Scratch).ToString(), Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) };
        var query = $"?q=paths&repository=PathKeeper&id={quest}&includeClosed=true";
        var reads = host.Routes().Where(route => route.Method == "GET").ToList();
        Assert.Contains(reads, route => route.Pattern == "/api/registry");
        foreach (var (_, pattern) in reads)
        {
            foreach (var from in new[] { DaorisHost.Loopback, DaorisHost.OffMachine })
            {
                var answer = await host.GetAsync(Concrete(pattern, pattern.Contains("{repository}") ? "PathKeeper" : quest) + query, from, key);

                foreach (var path in forbidden)
                {
                    Assert.False(
                        answer.Body.Contains(path, StringComparison.OrdinalIgnoreCase),
                        $"GET {pattern} from {from} answered a machine path: {answer.Body}");
                }
            }
        }
    }

    /// <summary>
    /// A shared deployment has no door that removes a record (D95): a person deletes on their own machine,
    /// and the tombstone travels. Keyed, the verb finds no route — and the quest is still there after it.
    /// </summary>
    [Fact]
    public async Task The_delete_doors_do_not_exist_on_a_shared_host()
    {
        var key = (await host.MintAsync("deleter@a-machine")).Key;
        await RegisterAsync("DeleteKeeper", key);
        await RegisterAsync("DeleteAsker", key);
        var published = await host.PostAsync("/api/quests", new
        {
            from = "DeleteAsker", to = "DeleteKeeper", title = "Published at the remote", body = "Nobody took it.",
        }, key: key);
        Assert.Equal(200, published.Status);
        var quest = published.Json.GetProperty("quest");
        var id = quest.GetProperty("id").GetString()!;
        Assert.False(quest.GetProperty("deletable").GetBoolean());

        var routes = host.Routes();
        Assert.DoesNotContain(("DELETE", "/api/quests/{id}"), routes);
        Assert.DoesNotContain(("DELETE", "/api/asks/{id}"), routes);
        foreach (var path in new[] { $"/api/quests/{id}", $"/api/asks/{id}" })
        {
            var refused = await host.DeleteAsync(path, key: key);
            Assert.True(refused.Status is 404 or 405, $"DELETE {path} answered {refused.Status}");
        }

        var listed = await host.GetAsync("/api/quests", key: key);
        Assert.Contains(listed.Json.EnumerateArray(), row => row.GetProperty("id").GetString() == id);
    }

    /// <summary>
    /// DRIFT1d (D133 §4): the person's yes to a departure is said on their own machine, and travels from there as an
    /// operation, as a delete does — a shared host has no accept door.
    /// </summary>
    [Fact]
    public void The_accept_door_does_not_exist_on_a_shared_host()
    {
        Assert.DoesNotContain(("POST", "/api/quests/{id}/accept"), host.Routes());
    }
}
