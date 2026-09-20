using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pure half of the sync (D47 §4/§9): these payloads ARE the disclosure boundary in practice, so
/// the tests hold the strip where it lives — a root or a transcript in a door's answer must not exist
/// in anything built for the wire, and only joined repositories may be spoken of at all.
/// </summary>
public sealed class RemoteSyncTests
{
    private const string RegistryJson = """
        [
          { "repository": "Shared", "adopted": true, "registered": true, "summary": "Shares.",
            "owns": ["an area"], "accepts": ["a quest"], "packs": ["desktop-app"], "entries": 3,
            "root": "C:/somewhere/private/Shared", "joined": true, "sharesKnowledge": true,
            "workspace": "default" },
          { "repository": "Quiet", "adopted": true, "registered": true, "summary": "Joined only.",
            "owns": [], "accepts": [], "packs": [], "entries": 0,
            "root": "C:/somewhere/private/Quiet", "joined": true, "sharesKnowledge": false,
            "workspace": "default" },
          { "repository": "Homebody", "adopted": true, "registered": true, "summary": "Stays local.",
            "owns": [], "accepts": [], "packs": [], "entries": 0,
            "root": "C:/somewhere/private/Homebody", "joined": false, "sharesKnowledge": false,
            "workspace": "default" },
          { "repository": "Teammate", "adopted": true, "registered": true, "summary": "Mirrored down.",
            "owns": [], "accepts": [], "packs": [], "entries": 2, "joined": true, "sharesKnowledge": true,
            "workspace": "default" },
          { "repository": "Elsewhere", "adopted": true, "registered": true, "summary": "Another circle.",
            "owns": [], "accepts": [], "packs": [], "entries": 5,
            "root": "C:/somewhere/private/Elsewhere", "joined": true, "sharesKnowledge": true,
            "workspace": "tools" }
        ]
        """;

    [Fact]
    public void Only_joined_repositories_are_spoken_of()
    {
        var joined = RemoteSyncPayloads.Joined(RegistryJson, RemoteTarget.DefaultWorkspace);

        Assert.Equal(["Shared", "Quiet"], joined.Select(r => r.Repository));
        Assert.True(joined[0].SharesKnowledge);
        Assert.False(joined[1].SharesKnowledge);
    }

    /// <summary>
    /// A joined repository in ANOTHER circle is a declaration addressed to a different deployment
    /// (D48 §5) — it is fully joined, fully sharing, and this remote must never hear of it. That is
    /// the boundary in its most dangerous form: everything about the row says "feed me" except the one
    /// field that decides where.
    /// </summary>
    [Fact]
    public void A_joined_repository_in_another_circle_is_not_this_remote_s_business()
    {
        Assert.DoesNotContain(
            "Elsewhere",
            RemoteSyncPayloads.Joined(RegistryJson, RemoteTarget.DefaultWorkspace).Select(r => r.Repository));

        var tools = RemoteSyncPayloads.Joined(RegistryJson, "tools");
        Assert.Equal(["Elsewhere"], tools.Select(r => r.Repository));
    }

    /// <summary>A row from a host that never heard of workspaces is in the circle silence means.</summary>
    [Fact]
    public void A_row_naming_no_workspace_is_in_the_default_circle()
    {
        const string unstated = """
            [{ "repository": "Old", "adopted": true, "registered": true, "owns": [], "accepts": [],
               "packs": [], "entries": 0, "root": "C:/somewhere/Old", "joined": true, "sharesKnowledge": false }]
            """;

        Assert.Single(RemoteSyncPayloads.Joined(unstated, RemoteTarget.DefaultWorkspace));
        Assert.Empty(RemoteSyncPayloads.Joined(unstated, "tools"));
    }

    /// <summary>
    /// A mirrored-down teammate row is joined and rootless — this machine holds no checkout of it, so
    /// nothing about it may feed UP from here. Without this, the next tick after a mirror would feed
    /// the teammate's repository from a machine that cannot see it — and an empty entries feed is a
    /// replacement, so their shared knowledge on the remote would be wiped by a machine that never had
    /// it. The root is the checkout, and the checkout is the authority (D47 §5).
    /// </summary>
    [Fact]
    public void A_mirrored_down_row_never_feeds_back_up()
    {
        Assert.DoesNotContain(
            "Teammate",
            RemoteSyncPayloads.Joined(RegistryJson, RemoteTarget.DefaultWorkspace).Select(r => r.Repository));
    }

    /// <summary>
    /// The registry answer CARRIES roots (a loopback caller sees them); the registration payload must
    /// not — the strip is at parse, so no later step could forward a machine path (D47 §4).
    /// </summary>
    [Fact]
    public void A_registration_payload_carries_the_declaration_and_never_the_root()
    {
        Assert.Contains("root", RegistryJson); // the guard guards something

        var payload = RemoteSyncPayloads.Registration(
            RemoteSyncPayloads.Joined(RegistryJson, RemoteTarget.DefaultWorkspace)[0]);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("root", out _));
        // Nor a workspace (D48, WSP1's rule, unchanged by the map): a feed that could name its own
        // circle could write itself into one the receiving deployment never joined. WHERE it lands is
        // the receiver's wiring — for a shared host, the one circle it serves.
        Assert.False(root.TryGetProperty("workspace", out _));
        Assert.Equal("Shared", root.GetProperty("repository").GetString());
        Assert.True(root.GetProperty("join").GetBoolean());
        Assert.True(root.GetProperty("shareKnowledge").GetBoolean());
        Assert.Equal("Shares.", root.GetProperty("domain").GetProperty("summary").GetString());
        Assert.DoesNotContain("private", payload);
    }

    [Fact]
    public void Session_records_feed_for_joined_repositories_without_their_transcripts()
    {
        const string sessionsJson = """
            [
              { "id": "ab12cd34", "quest": "abc123", "repository": "Shared", "adapter": "stub",
                "state": "completed", "note": "the quest reached done.", "evidence": "deadbee add note",
                "transcript": "C:/somewhere/private/sessions/ab12cd34.log",
                "created": "2026-09-20T10:00:00+00:00", "updated": "2026-09-20T10:01:00+00:00" },
              { "id": "ef56ab78", "quest": "def456", "repository": "Homebody", "adapter": "stub",
                "state": "working", "created": "2026-09-20T10:00:00+00:00", "updated": "2026-09-20T10:00:00+00:00" },
              { "id": "elsewhere/99aa88bb", "quest": "aaa111", "repository": "Shared", "adapter": "stub",
                "state": "completed", "created": "2026-09-20T09:00:00+00:00", "updated": "2026-09-20T09:01:00+00:00" }
            ]
            """;
        var joined = new HashSet<string>(["Shared", "Quiet"], StringComparer.OrdinalIgnoreCase);

        var feed = RemoteSyncPayloads.Sessions(sessionsJson, joined);

        Assert.NotNull(feed);
        Assert.Equal(1, feed!.Value.Count);
        Assert.DoesNotContain("transcript", feed.Value.Json);
        Assert.DoesNotContain("private", feed.Value.Json);

        using var document = JsonDocument.Parse(feed.Value.Json);
        var record = Assert.Single(document.RootElement.GetProperty("records").EnumerateArray().ToList());
        Assert.Equal("ab12cd34", record.GetProperty("id").GetString());
        Assert.Equal("the quest reached done.", record.GetProperty("note").GetString());
    }

    /// <summary>A record already carrying an origin is somebody else's, mirrored here — never re-fed.</summary>
    [Fact]
    public void Nothing_to_feed_is_null_not_an_empty_envelope()
    {
        const string onlyMirrored = """
            [{ "id": "elsewhere/99aa88bb", "quest": "a", "repository": "Shared", "adapter": "stub",
               "state": "completed", "created": "2026-09-20T09:00:00+00:00", "updated": "2026-09-20T09:00:00+00:00" }]
            """;

        Assert.Null(RemoteSyncPayloads.Sessions(onlyMirrored, new HashSet<string>(["Shared"])));
        Assert.Null(RemoteSyncPayloads.Quests("[]", new HashSet<string>(["Shared"])));
    }

    [Fact]
    public void The_quest_mirror_takes_what_touches_this_family_and_nothing_else()
    {
        const string remoteQuests = """
            [
              { "id": "abc123", "from": "Elsewhere", "to": "Shared", "title": "For us", "body": "b",
                "status": "Open", "filed": "2026-09-20T10:00:00+00:00", "updated": "2026-09-20T10:00:00+00:00" },
              { "id": "def456", "from": "Quiet", "to": "Elsewhere", "title": "From us", "body": "b",
                "status": "Done", "note": "landed", "filed": "2026-09-20T09:00:00+00:00", "updated": "2026-09-20T11:00:00+00:00" },
              { "id": "ffff00", "from": "Elsewhere", "to": "AlsoElsewhere", "title": "Not ours", "body": "b",
                "status": "Open", "filed": "2026-09-20T10:00:00+00:00", "updated": "2026-09-20T10:00:00+00:00" }
            ]
            """;
        var joined = new HashSet<string>(["Shared", "Quiet"], StringComparer.OrdinalIgnoreCase);

        var mirror = RemoteSyncPayloads.Quests(remoteQuests, joined);

        Assert.NotNull(mirror);
        Assert.Equal(2, mirror!.Value.Count);
        using var document = JsonDocument.Parse(mirror.Value.Json);
        var ids = document.RootElement.GetProperty("quests").EnumerateArray()
            .Select(q => q.GetProperty("id").GetString()).ToList();
        Assert.Equal(["abc123", "def456"], ids);
    }

    /// <summary>
    /// The remote's registry mirrors down as FOREIGN rows only (D47 §5): a teammate's repository
    /// becomes addressable here, while anything this machine holds keeps its own registration — the
    /// machine with the checkout is the authority, and its root must survive the sync untouched.
    /// </summary>
    [Fact]
    public void The_family_mirror_takes_foreign_rows_only_and_never_writes_a_root()
    {
        const string remoteRegistry = """
            [
              { "repository": "Shared", "adopted": true, "registered": true, "summary": "Mine.",
                "owns": [], "accepts": [], "packs": [], "entries": 1, "joined": true, "sharesKnowledge": true },
              { "repository": "Teammate", "adopted": true, "registered": true, "summary": "Theirs.",
                "owns": ["their area"], "accepts": ["a quest"], "packs": [], "entries": 2,
                "joined": true, "sharesKnowledge": false }
            ]
            """;
        var localNames = new HashSet<string>(["Shared", "Quiet", "Homebody"], StringComparer.OrdinalIgnoreCase);

        var foreign = RemoteSyncPayloads.ForeignRegistrations(remoteRegistry, localNames, "aurora");

        var (name, json) = Assert.Single(foreign);
        Assert.Equal("Teammate", name);
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement;
        Assert.False(payload.TryGetProperty("root", out _));
        Assert.True(payload.GetProperty("join").GetBoolean());
        Assert.False(payload.GetProperty("shareKnowledge").GetBoolean());
        Assert.Equal("Theirs.", payload.GetProperty("domain").GetProperty("summary").GetString());
        // A row that came from aurora's deployment belongs to aurora on this machine — THIS machine's
        // wiring deciding, not the feed naming itself (D48 §5).
        Assert.Equal("aurora", payload.GetProperty("workspace").GetString());
    }

    /// <summary>
    /// The names guard spans the WHOLE machine, not the synced circle. A repository this machine holds
    /// in another workspace must keep its own registration — root included — or the mirror would
    /// overwrite a local checkout's path with a teammate's stripped copy and re-point its circle at the
    /// same time. The scoped half of the sync is what feeds; the unscoped half is what protects.
    /// </summary>
    [Fact]
    public void A_name_this_machine_holds_in_another_circle_is_still_not_foreign()
    {
        const string remoteRegistry = """
            [{ "repository": "Elsewhere", "adopted": true, "registered": true, "summary": "Theirs.",
               "owns": [], "accepts": [], "packs": [], "entries": 2, "joined": true, "sharesKnowledge": true }]
            """;

        var localNames = RemoteSyncPayloads.Names(RegistryJson);

        Assert.Empty(RemoteSyncPayloads.ForeignRegistrations(remoteRegistry, localNames, "default"));
    }

    [Fact]
    public void Entries_feed_with_their_anchors_and_an_empty_list_still_feeds()
    {
        const string entriesJson = """
            [
              { "id": "Shared:docs/DECISIONS.md#D1", "repository": "Shared", "kind": "Decision",
                "provenance": "Local", "title": "D1", "path": "docs/DECISIONS.md", "body": "why", "anchor": "D1" }
            ]
            """;

        var (json, count) = RemoteSyncPayloads.Entries("Shared", entriesJson);
        Assert.Equal(1, count);
        using var document = JsonDocument.Parse(json);
        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray().ToList());
        Assert.Equal("docs/DECISIONS.md", entry.GetProperty("relativePath").GetString());
        Assert.Equal("D1", entry.GetProperty("anchor").GetString());

        var (emptyJson, emptyCount) = RemoteSyncPayloads.Entries("Shared", "[]");
        Assert.Equal(0, emptyCount);
        Assert.Contains("\"entries\"", emptyJson);
    }
}

/// <summary>
/// The orchestration half, over a stub transport: the ORDER is a contract (registrations go first, so
/// the remote knows who is joined before their records arrive — D47 §9), and a wall is reported and
/// never thrown (records sync eventually; a dead tick would take the driver's whole look with it).
/// </summary>
public sealed class RemoteSyncRunTests
{
    private const string Local = "http://localhost:5177";
    private const string Remote = "https://remote.example.com";

    /// <summary>Canned answers by (method, path prefix); records every call in order.</summary>
    private sealed class StubTransport : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage>? Answer { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(Answer!(request));
        }
    }

    private static HttpResponseMessage Json(string payload) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage AnswerHealthy(HttpRequestMessage request)
    {
        var url = request.RequestUri!.ToString();
        return url switch
        {
            _ when url.StartsWith($"{Local}/api/registry") && request.Method == HttpMethod.Get => Json("""
                [{ "repository": "Shared", "adopted": true, "registered": true, "owns": [], "accepts": [],
                   "packs": [], "entries": 1, "root": "C:/somewhere/Shared", "joined": true, "sharesKnowledge": true }]
                """),
            _ when url.StartsWith($"{Local}/api/sessions") => Json("""
                [{ "id": "ab12cd34", "quest": "abc123", "repository": "Shared", "adapter": "stub",
                   "state": "completed", "created": "2026-09-20T10:00:00+00:00", "updated": "2026-09-20T10:01:00+00:00" }]
                """),
            _ when url.StartsWith($"{Local}/api/entries") => Json("[]"),
            _ when url.StartsWith($"{Remote}/api/registry") && request.Method == HttpMethod.Get => Json("[]"),
            _ when url.StartsWith($"{Remote}/api/quests") => Json("[]"),
            _ => Json("{}"),
        };
    }

    [Fact]
    public async Task Registrations_go_up_before_records_and_content_and_the_mirror_comes_last()
    {
        using var transport = new StubTransport { Answer = AnswerHealthy };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        var order = transport.Calls;
        int At(string fragment) => order.FindIndex(call => call.Contains(fragment));
        Assert.True(At($"POST {Remote}/api/registry") >= 0, string.Join("\n", order));
        Assert.True(At($"POST {Remote}/api/registry") < At($"POST {Remote}/api/feed/sessions"));
        Assert.True(At($"POST {Remote}/api/feed/sessions") < At($"POST {Remote}/api/feed/entries"));
        Assert.True(At($"POST {Remote}/api/feed/entries") < At($"GET {Remote}/api/registry"));
        Assert.True(At($"GET {Remote}/api/registry") < At($"GET {Remote}/api/quests"));
    }

    [Fact]
    public async Task A_wall_is_reported_and_named_never_thrown()
    {
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith(Remote)
                ? new(System.Net.HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(
                        """{ "error": "key `dk_abcd1234` expired 2026-09-01 — ask an operator to mint a fresh one" }""",
                        System.Text.Encoding.UTF8, "application/json"),
                }
                : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.NotNull(report.Problem);
        Assert.Contains("expired", report.Problem);
    }

    [Fact]
    public async Task A_family_with_nothing_joined_syncs_nothing()
    {
        using var transport = new StubTransport
        {
            Answer = request => Json("""
                [{ "repository": "Homebody", "adopted": true, "registered": true, "owns": [], "accepts": [],
                   "packs": [], "entries": 0, "root": "C:/somewhere/Homebody", "joined": false, "sharesKnowledge": false }]
                """),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        Assert.Single(transport.Calls); // the local registry read, and nothing toward the remote
    }
}

/// <summary>
/// One machine, many circles: a sync per workspace with a remote (D48 §5). Each feeds only its own
/// rows, and one circle's wall does not take another's pass with it.
/// </summary>
public sealed class RemoteSyncSetTests
{
    private const string Local = "http://localhost:5177";
    private const string Aurora = "https://aurora.example.com";
    private const string Tools = "https://tools.example.com";

    private const string TwoCircles = """
        [
          { "repository": "Atelier", "adopted": true, "registered": true, "owns": [], "accepts": [],
            "packs": [], "entries": 1, "root": "C:/somewhere/Atelier", "joined": true,
            "sharesKnowledge": false, "workspace": "aurora" },
          { "repository": "Foundry", "adopted": true, "registered": true, "owns": [], "accepts": [],
            "packs": [], "entries": 1, "root": "C:/somewhere/Foundry", "joined": true,
            "sharesKnowledge": false, "workspace": "tools" }
        ]
        """;

    /// <summary>Records the BODY as well as the call: which rows went where is the whole question.</summary>
    private sealed class RecordingTransport : HttpMessageHandler
    {
        public List<(string Call, string Body)> Calls { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage>? Answer { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Calls.Add(($"{request.Method} {request.RequestUri}", body));
            return Answer!(request);
        }
    }

    private static HttpResponseMessage Json(string payload) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var url = request.RequestUri!.ToString();
        return url switch
        {
            _ when url.StartsWith($"{Local}/api/registry") && request.Method == HttpMethod.Get => Json(TwoCircles),
            _ when url.StartsWith($"{Local}/api/sessions") => Json("[]"),
            _ when url.StartsWith($"{Local}/api/entries") => Json("[]"),
            _ when url.Contains("/api/registry") && request.Method == HttpMethod.Get => Json("[]"),
            _ when url.Contains("/api/quests") => Json("[]"),
            _ => Json("{}"),
        };
    }

    private static RemoteSyncSet Set(HttpMessageHandler transport) => new(
    [
        new RemoteSync(Local, null, "aurora", new RemoteTarget(Aurora, "dk_aurora"), transport),
        new RemoteSync(Local, null, "tools", new RemoteTarget(Tools, "dk_tools"), transport),
    ]);

    [Fact]
    public async Task Each_circle_feeds_its_own_remote_and_no_other()
    {
        using var transport = new RecordingTransport { Answer = Answer };
        using var set = Set(transport);

        var report = await set.RunOnceAsync();

        Assert.Null(report.Problem);
        var toAurora = transport.Calls.Where(c => c.Call == $"POST {Aurora}/api/registry").ToList();
        var toTools = transport.Calls.Where(c => c.Call == $"POST {Tools}/api/registry").ToList();
        Assert.Contains("Atelier", Assert.Single(toAurora).Body);
        Assert.DoesNotContain("Foundry", Assert.Single(toAurora).Body);
        Assert.Contains("Foundry", Assert.Single(toTools).Body);
        Assert.DoesNotContain("Atelier", Assert.Single(toTools).Body);
    }

    /// <summary>
    /// A wall in one circle is reported with that circle's name and leaves the other's pass alone. On
    /// a machine holding two deployments' keys, "the sync is failing" is not a usable sentence.
    /// </summary>
    [Fact]
    public async Task One_circles_wall_names_itself_and_does_not_stop_the_other()
    {
        using var transport = new RecordingTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith(Tools)
                ? new(System.Net.HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(
                        """{ "error": "key `dk_abcd1234` expired 2026-09-01 — ask an operator to mint a fresh one" }""",
                        System.Text.Encoding.UTF8, "application/json"),
                }
                : Answer(request),
        };
        using var set = Set(transport);

        var report = await set.RunOnceAsync();

        Assert.NotNull(report.Problem);
        Assert.Contains("tools:", report.Problem);
        Assert.Contains("expired", report.Problem);
        Assert.DoesNotContain("aurora:", report.Problem);
        Assert.Contains(transport.Calls, c => c.Call == $"POST {Aurora}/api/registry");
    }
}

/// <summary>
/// The machine's remotes are a MAP — workspace → { url, key } — in one file under the profile, with
/// the environment overriding (D48 §5); absence is the default, silently (D21), so a machine with no
/// remote never has to opt out of one.
/// </summary>
/// <remarks>
/// This table is the twin of the service's `RemoteConfigTests`. The two loaders are deliberate
/// duplicates — this assembly links against no service code — so the tables move together, and a rule
/// that appears in one only is a rule the other will contradict.
/// </remarks>
public sealed class RemoteTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "daoris-remote-" + Guid.NewGuid().ToString("N")[..8]);

    private string ConfigPath => Path.Combine(_dir, "remotes.json");

    public RemoteTargetTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static Func<string, string?> Env(string? url = null, string? key = null, string? workspace = null) =>
        name => name == RemoteTarget.UrlVariable ? url
            : name == RemoteTarget.KeyVariable ? key
            : name == RemoteTarget.WorkspaceVariable ? workspace
            : null;

    [Fact]
    public void Absence_is_silent()
    {
        Assert.Empty(RemoteTarget.Load(Env(), ConfigPath));
    }

    [Fact]
    public void The_file_names_a_remote_per_workspace()
    {
        File.WriteAllText(ConfigPath, """
            {
              "aurora": { "url": "https://aurora.example.com/", "key": "dk_aurorakey" },
              "tools":  { "url": "https://tools.example.com",   "key": "dk_toolskey" }
            }
            """);

        var remotes = RemoteTarget.Load(Env(), ConfigPath);

        Assert.Equal(2, remotes.Count);
        Assert.Equal("https://aurora.example.com", remotes["aurora"].Url);
        Assert.Equal("dk_aurorakey", remotes["aurora"].Key);
        Assert.Equal("https://tools.example.com", remotes["tools"].Url);
        Assert.True(remotes.ContainsKey("AURORA"));
    }

    /// <summary>
    /// The environment is the answer for the WHOLE MACHINE, not one entry of it. A merge would let a
    /// developer's real map leak into a process that thought it had named its only remote — which is
    /// exactly what the family rehearsal's hermetic guard relies on not happening.
    /// </summary>
    [Fact]
    public void The_environment_pair_replaces_the_file_whole_and_names_its_workspace()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey" } }""");

        var overridden = RemoteTarget.Load(Env("https://env.example.com/", "dk_envkey", "tools"), ConfigPath);

        Assert.Equal(["tools"], overridden.Keys);
        Assert.Equal("https://env.example.com", overridden["tools"].Url);
        Assert.Equal("dk_envkey", overridden["tools"].Key);
    }

    [Fact]
    public void An_unnamed_environment_pair_serves_the_default_workspace()
    {
        var remotes = RemoteTarget.Load(Env("https://env.example.com", "dk_envkey"), ConfigPath);

        Assert.Equal([RemoteTarget.DefaultWorkspace], remotes.Keys);
    }

    [Fact]
    public void A_half_declared_remote_is_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey" } }""");
        Assert.Empty(RemoteTarget.Load(Env(url: "https://env.example.com"), ConfigPath));
        Assert.Empty(RemoteTarget.Load(Env(key: "dk_envkey"), ConfigPath));

        Assert.Empty(RemoteTarget.Load(Env(url: "https://env.example.com"), Path.Combine(_dir, "absent.json")));
    }

    [Fact]
    public void An_entry_missing_half_its_pair_is_skipped_and_the_rest_still_load()
    {
        File.WriteAllText(ConfigPath, """
            {
              "aurora": { "url": "https://aurora.example.com", "key": "dk_aurorakey" },
              "tools":  { "url": "https://tools.example.com" },
              "empty":  {}
            }
            """);

        Assert.Equal(["aurora"], RemoteTarget.Load(Env(), ConfigPath).Keys);
    }

    [Fact]
    public void A_malformed_file_is_no_remote_rather_than_a_crash()
    {
        File.WriteAllText(ConfigPath, "{ not json");
        Assert.Empty(RemoteTarget.Load(Env(), ConfigPath));
    }

    /// <summary>The pre-workspace flat shape names no remote — rebuilt, never migrated (D48 §5).</summary>
    [Fact]
    public void The_pre_workspace_flat_shape_names_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "url": "https://daoris.example.com", "key": "dk_filekey" }""");

        Assert.Empty(RemoteTarget.Load(Env(), ConfigPath));
    }

    /// <summary>
    /// What the shell's settings surface writes, read back by the loaders — the file IS the contract
    /// between them (D50), so a write the readers cannot read is the one failure that would look fine
    /// from both sides of the screen.
    /// </summary>
    [Fact]
    public void What_the_surface_writes_is_what_every_loader_reads()
    {
        RemoteTarget.Save(ConfigPath, new Dictionary<string, RemoteTarget>
        {
            ["tools"] = new("https://tools.example.com", "dk_toolskey0000"),
            ["aurora"] = new("https://aurora.example.com", "dk_aurorakey000"),
        });

        var reread = RemoteTarget.Load(Env(), ConfigPath);

        Assert.Equal(2, reread.Count);
        Assert.Equal("https://aurora.example.com", reread["aurora"].Url);
        Assert.Equal("dk_toolskey0000", reread["tools"].Key);
        // Sorted and newline-terminated: this file is edited by hand as often as by a surface, and a
        // rewrite that reshuffled it would make every edit look like a bigger change than it was.
        var text = File.ReadAllText(ConfigPath);
        Assert.True(text.IndexOf("aurora", StringComparison.Ordinal) < text.IndexOf("tools", StringComparison.Ordinal));
        Assert.EndsWith("\n", text);
    }

    /// <summary>
    /// An EDITOR reads the file even when the environment outranks it — otherwise a machine with the
    /// env pair set could never wire a second workspace, and the surface would silently write over
    /// whatever it had just failed to see.
    /// </summary>
    [Fact]
    public void The_editor_s_read_ignores_the_environment()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey00000" } }""");
        Environment.SetEnvironmentVariable(RemoteTarget.UrlVariable, "https://env.example.com");
        try
        {
            Assert.Equal(["aurora"], RemoteTarget.LoadFile(ConfigPath).Keys);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RemoteTarget.UrlVariable, null);
        }
    }

    /// <summary>
    /// A redaction that leaks a short key is worse than printing it, because it reads as safe. The
    /// prefix shown is the deployment's own audit handle — non-secret by design (D47 §7).
    /// </summary>
    [Fact]
    public void A_redacted_key_shows_the_audit_prefix_and_nothing_else()
    {
        Assert.Equal("dk_abcd1234…", RemoteTarget.Redact("dk_abcd1234wxyzsecret"));
        Assert.Equal("…", RemoteTarget.Redact("dk_short"));
        Assert.Equal("…", RemoteTarget.Redact(""));
    }
}
