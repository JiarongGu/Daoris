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
            "root": "C:/somewhere/private/Shared", "joined": true, "sharesKnowledge": true },
          { "repository": "Quiet", "adopted": true, "registered": true, "summary": "Joined only.",
            "owns": [], "accepts": [], "packs": [], "entries": 0,
            "root": "C:/somewhere/private/Quiet", "joined": true, "sharesKnowledge": false },
          { "repository": "Homebody", "adopted": true, "registered": true, "summary": "Stays local.",
            "owns": [], "accepts": [], "packs": [], "entries": 0,
            "root": "C:/somewhere/private/Homebody", "joined": false, "sharesKnowledge": false }
        ]
        """;

    [Fact]
    public void Only_joined_repositories_are_spoken_of()
    {
        var joined = RemoteSyncPayloads.Joined(RegistryJson);

        Assert.Equal(["Shared", "Quiet"], joined.Select(r => r.Repository));
        Assert.True(joined[0].SharesKnowledge);
        Assert.False(joined[1].SharesKnowledge);
    }

    /// <summary>
    /// The registry answer CARRIES roots (a loopback caller sees them); the registration payload must
    /// not — the strip is at parse, so no later step could forward a machine path (D47 §4).
    /// </summary>
    [Fact]
    public void A_registration_payload_carries_the_declaration_and_never_the_root()
    {
        Assert.Contains("root", RegistryJson); // the guard guards something

        var payload = RemoteSyncPayloads.Registration(RemoteSyncPayloads.Joined(RegistryJson)[0]);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("root", out _));
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

        var foreign = RemoteSyncPayloads.ForeignRegistrations(remoteRegistry, localNames);

        var (name, json) = Assert.Single(foreign);
        Assert.Equal("Teammate", name);
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement;
        Assert.False(payload.TryGetProperty("root", out _));
        Assert.True(payload.GetProperty("join").GetBoolean());
        Assert.False(payload.GetProperty("shareKnowledge").GetBoolean());
        Assert.Equal("Theirs.", payload.GetProperty("domain").GetProperty("summary").GetString());
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
/// The machine's remote is one file under the profile, environment overriding (D47 §9) — and absence
/// is the default, silently (D21): a machine with no remote must never have to opt out of one.
/// </summary>
public sealed class RemoteTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "daoris-remote-" + Guid.NewGuid().ToString("N")[..8]);

    private string ConfigPath => Path.Combine(_dir, "remote.json");

    public RemoteTargetTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static Func<string, string?> Env(string? url = null, string? key = null) =>
        name => name == RemoteTarget.UrlVariable ? url : name == RemoteTarget.KeyVariable ? key : null;

    [Fact]
    public void Absence_is_silent()
    {
        Assert.Null(RemoteTarget.Load(Env(), ConfigPath));
    }

    [Fact]
    public void The_file_names_the_remote_and_the_environment_overrides_it()
    {
        File.WriteAllText(ConfigPath, """{ "url": "https://daoris.example.com/", "key": "dk_filekey" }""");

        var fromFile = RemoteTarget.Load(Env(), ConfigPath);
        Assert.Equal("https://daoris.example.com", fromFile!.Url);
        Assert.Equal("dk_filekey", fromFile.Key);

        var overridden = RemoteTarget.Load(Env("https://other.example.com", "dk_envkey"), ConfigPath);
        Assert.Equal("https://other.example.com", overridden!.Url);
        Assert.Equal("dk_envkey", overridden.Key);
    }

    [Fact]
    public void A_half_declared_remote_is_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "url": "https://daoris.example.com" }""");
        Assert.Null(RemoteTarget.Load(Env(), ConfigPath));

        Assert.Null(RemoteTarget.Load(Env(url: "https://daoris.example.com"), Path.Combine(_dir, "absent.json")));
    }

    [Fact]
    public void A_malformed_file_is_no_remote_rather_than_a_crash()
    {
        File.WriteAllText(ConfigPath, "{ not json");
        Assert.Null(RemoteTarget.Load(Env(), ConfigPath));
    }
}
