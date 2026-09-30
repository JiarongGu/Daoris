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

    /// <summary>
    /// D91: what a joined repository says it uses travels with its declaration — and a declaration of
    /// none goes on the wire exactly as it did before the field existed, so no deployment reads a
    /// changed declaration where nothing changed.
    /// </summary>
    [Fact]
    public void A_registration_payload_carries_what_it_says_it_uses_and_nothing_when_it_says_none()
    {
        var said = RemoteSyncPayloads.Registration(new RemoteSyncPayloads.JoinedRepository(
            "game", "the game", [], [], [], false, "/somewhere", Uses: ["engine"]));
        using (var document = JsonDocument.Parse(said))
        {
            Assert.Equal("engine", document.RootElement.GetProperty("domain").GetProperty("uses")[0].GetString());
        }

        var silent = RemoteSyncPayloads.Registration(new RemoteSyncPayloads.JoinedRepository(
            "game", "the game", [], [], [], false, "/somewhere"));
        Assert.DoesNotContain("uses", silent);
    }

    /// <summary>
    /// A quest pass, as a person hears it (D69): each move of this machine's that lost, each quest the
    /// remote would not keep — in its own words — and a circle still moving after every round.
    /// </summary>
    [Fact]
    public void A_quest_pass_becomes_the_sentences_a_person_reads()
    {
        const string pass = """
            { "workspace": "default", "machine": "m1", "wired": true, "pushed": 2,
              "conflicts": [{ "quest": "q1", "attempted": "Taken" }],
              "refused": [{ "quest": "q3", "reason": "`Stranger` is not registered at this deployment." }],
              "behind": ["q2"], "problem": null }
            """;

        var read = RemoteSyncPayloads.Pass(pass);

        Assert.True(read.Wired);
        Assert.Null(read.Problem);
        Assert.Equal(3, read.Notes.Count);
        Assert.Contains(read.Notes, n => n.Contains("#q1") && n.Contains("`Taken`") && n.Contains("kept on the quest as a conflict"));
        Assert.Contains(read.Notes, n => n.Contains("#q3") && n.Contains("not registered at this deployment"));
        Assert.Contains(read.Notes, n => n.Contains("1 quest(s) moved at the remote"));
    }

    /// <summary>A host answering something that is not a pass is a wall the sync names, never an empty pass.</summary>
    [Fact]
    public void An_answer_that_is_not_a_pass_is_a_wall()
    {
        Assert.Contains("not one", Assert.Throws<DriverException>(() => RemoteSyncPayloads.Pass("[]")).Message);
        Assert.Equal("lost", RemoteSyncPayloads.Claim("""{ "quest": "q1", "claim": "lost" }"""));
        Assert.Equal("none", RemoteSyncPayloads.Claim("[]"));
    }

    /// <summary>
    /// The remote's registry comes down as the TEAM's rows only (D47 §5): a teammate's repository
    /// becomes addressable here, while anything this machine holds keeps its own registration — the
    /// machine with the checkout is the authority, and its root must survive the sync untouched.
    /// </summary>
    [Fact]
    public void The_family_mirror_takes_the_team_s_rows_only_and_never_writes_a_root()
    {
        const string remoteRegistry = """
            [
              { "repository": "Shared", "adopted": true, "registered": true, "summary": "Mine.",
                "owns": [], "accepts": [], "packs": [], "entries": 1, "joined": true, "sharesKnowledge": true },
              { "repository": "Newcomer", "adopted": true, "registered": true, "summary": "Theirs.",
                "owns": ["their area"], "accepts": ["a quest"], "packs": [], "entries": 2,
                "joined": true, "sharesKnowledge": false }
            ]
            """;

        var mirror = RemoteSyncPayloads.Mirror(remoteRegistry, RegistryJson, "default", retiredHere: []);

        var (name, json) = Assert.Single(mirror.Write);
        Assert.Equal("Newcomer", name);
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement;
        Assert.False(payload.TryGetProperty("root", out _));
        Assert.True(payload.GetProperty("join").GetBoolean());
        Assert.False(payload.GetProperty("shareKnowledge").GetBoolean());
        Assert.Equal("Theirs.", payload.GetProperty("domain").GetProperty("summary").GetString());
        // A row that came from this circle's deployment belongs to this circle on this machine — THIS
        // machine's wiring deciding, not the feed naming itself (D48 §5).
        Assert.Equal("default", payload.GetProperty("workspace").GetString());
    }

    /// <summary>
    /// The guard spans the WHOLE machine, not the synced circle. A repository this machine holds with a
    /// root in another workspace keeps its own registration, root included, or the mirror would overwrite
    /// a local checkout's path with a teammate's stripped copy and re-point its circle at the same time.
    /// And a copy another circle's sync keeps is that sync's: two remotes naming one repository must not
    /// take turns re-filing it.
    /// </summary>
    [Fact]
    public void A_name_this_machine_holds_elsewhere_is_never_written_or_retired_by_this_circle()
    {
        const string local = """
            [
              { "repository": "Elsewhere", "owns": [], "accepts": [], "packs": [], "root": "C:/somewhere/Elsewhere",
                "joined": true, "sharesKnowledge": true, "workspace": "tools" },
              { "repository": "Toolmate", "owns": [], "accepts": [], "packs": [], "joined": true,
                "sharesKnowledge": false, "workspace": "tools" }
            ]
            """;
        const string remoteRegistry = """
            [
              { "repository": "Elsewhere", "summary": "Theirs.", "owns": [], "accepts": [], "packs": [], "joined": true, "sharesKnowledge": true },
              { "repository": "Toolmate", "summary": "Re-filed?", "owns": [], "accepts": [], "packs": [], "joined": true, "sharesKnowledge": false }
            ]
            """;

        var mirror = RemoteSyncPayloads.Mirror(remoteRegistry, local, "default", retiredHere: []);

        Assert.Empty(mirror.Write);
        Assert.Empty(mirror.Retire);
    }

    /// <summary>
    /// SYNC0b: a teammate's row came down once and was never touched again. It is written when its
    /// declaration changed and left alone when it did not, and a copy of this circle's that the circle
    /// no longer lists is retired here — the deployment said it is gone, and it is not this machine's.
    /// </summary>
    [Fact]
    public void A_teammate_s_row_is_updated_when_it_changed_and_retired_when_the_circle_dropped_it()
    {
        const string local = """
            [
              { "repository": "Changed", "summary": "Before.", "owns": ["a"], "accepts": [], "packs": [],
                "joined": true, "sharesKnowledge": false, "workspace": "default" },
              { "repository": "Steady", "summary": "Same.", "owns": ["a"], "accepts": [], "packs": [],
                "joined": true, "sharesKnowledge": false, "workspace": "default" },
              { "repository": "Dropped", "summary": "Gone.", "owns": [], "accepts": [], "packs": [],
                "joined": true, "sharesKnowledge": false }
            ]
            """;
        const string remoteRegistry = """
            [
              { "repository": "Changed", "summary": "After.", "owns": ["a"], "accepts": [], "packs": [], "joined": true, "sharesKnowledge": false },
              { "repository": "Steady", "summary": "Same.", "owns": ["a"], "accepts": [], "packs": [], "joined": true, "sharesKnowledge": false }
            ]
            """;

        var mirror = RemoteSyncPayloads.Mirror(remoteRegistry, local, "default", retiredHere: []);

        var (name, json) = Assert.Single(mirror.Write);
        Assert.Equal("Changed", name);
        Assert.Contains("After.", json);
        Assert.Equal(["Dropped"], mirror.Retire);
    }

    /// <summary>
    /// A repository this pass just retired at the circle is still in the list it read a moment before.
    /// Writing it back down would undo the retire here in the same breath.
    /// </summary>
    [Fact]
    public void A_repository_retired_in_this_pass_is_not_brought_back_down()
    {
        const string remoteRegistry = """
            [{ "repository": "Retired", "summary": "Was mine.", "owns": [], "accepts": [], "packs": [], "joined": true, "sharesKnowledge": false }]
            """;

        var mirror = RemoteSyncPayloads.Mirror(remoteRegistry, "[]", "default", retiredHere: ["Retired"]);

        Assert.Empty(mirror.Write);
    }

    /// <summary>The retires a host says this machine owes a circle; an answer that is not one owes none.</summary>
    [Fact]
    public void The_retires_owed_are_read_by_name()
    {
        Assert.Equal(
            ["Gone", "Moved"],
            RemoteSyncPayloads.Retired("""{ "workspace": "default", "repositories": ["Gone", "Moved"] }"""));
        Assert.Empty(RemoteSyncPayloads.Retired("[]"));
    }

    /// <summary>
    /// A registration names the commit its manifest stands on and the held one it descends from (SYNC5b),
    /// exactly as a feed does; one that can name none carries no commit at all, never half of one.
    /// </summary>
    [Fact]
    public void A_registration_names_the_commit_its_manifest_stands_on()
    {
        var repo = RemoteSyncPayloads.Joined(RegistryJson, RemoteTarget.DefaultWorkspace)[0];

        using var named = JsonDocument.Parse(RemoteSyncPayloads.Registration(repo, "main", Head, onBase: "parentparent"));
        using var unnamed = JsonDocument.Parse(RemoteSyncPayloads.Registration(repo, "main"));

        Assert.Equal(Head.Commit, named.RootElement.GetProperty("commit").GetString());
        Assert.Equal("main", named.RootElement.GetProperty("branch").GetString());
        Assert.Equal("parentparent", named.RootElement.GetProperty("base").GetString());
        Assert.True(named.RootElement.TryGetProperty("committedAt", out _));
        Assert.False(named.RootElement.TryGetProperty("root", out _));
        Assert.False(unnamed.RootElement.TryGetProperty("commit", out _));
        Assert.False(unnamed.RootElement.TryGetProperty("base", out _));
    }

    // ——— Where a circle stands (SYNC6a), as `daoris-driver sync status` prints it.

    /// <summary>
    /// Ahead, behind and conflicts in one line, the quests named beneath it, and a wall said as the last
    /// TRY — beside the last time the circle reached its remote, which a wall does not move.
    /// </summary>
    [Fact]
    public void A_circle_s_standing_reads_as_one_line_and_the_quests_beneath_it()
    {
        var standing = RemoteSyncPayloads.Standing("""
            { "workspace": "aurora", "wired": true, "ahead": 2, "behind": ["q2"], "conflicts": ["q1", "q3"],
              "synced": "2026-09-24T10:03:00+00:00", "tried": "2026-09-24T10:05:00+00:00",
              "problem": "the remote could not be reached (connection refused)" }
            """);

        var lines = standing.Describe();

        Assert.Equal("aurora  2 ahead · 1 behind · 2 in conflict · synced 2026-09-24 10:03Z", lines[0]);
        Assert.Contains(lines, line => line.Contains("in conflict: #q1, #q3"));
        Assert.Contains(lines, line => line.Contains("behind: #q2"));
        // Only WHEN leads the wall: the host's sentence already says what went wrong.
        Assert.Contains("  the last try, 10:05Z: the remote could not be reached (connection refused)", lines);
    }

    /// <summary>A circle with no remote here has nothing to be ahead of, and one never synced says so rather than showing a time.</summary>
    [Fact]
    public void An_unwired_circle_and_one_never_synced_each_say_what_they_are()
    {
        var unwired = RemoteSyncPayloads.Standing("""{ "workspace": "tools", "wired": false }""").Describe();
        var fresh = RemoteSyncPayloads.Standing("""
            { "workspace": "aurora", "wired": true, "ahead": 1, "behind": [], "conflicts": [], "synced": null, "tried": null, "problem": null }
            """).Describe();

        Assert.Equal(["tools  no remote on this machine's host — nothing of this workspace leaves it"], unwired);
        Assert.Equal(["aurora  1 ahead · 0 behind · 0 in conflict · not synced yet"], fresh);
    }

    /// <summary>What stands on one held commit is said once, naming everything that does.</summary>
    [Fact]
    public void Everything_waiting_on_one_commit_is_named_in_one_sentence()
    {
        Assert.Equal("registration", RemoteSyncPayloads.Whats(["registration"]));
        Assert.Equal("knowledge and code map", RemoteSyncPayloads.Whats(["knowledge", "code map"]));
        Assert.Equal("registration, knowledge and code map", RemoteSyncPayloads.Whats(["registration", "knowledge", "code map"]));
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

        var (json, count) = RemoteSyncPayloads.Entries("Shared", entriesJson, Head);
        Assert.Equal(1, count);
        using var document = JsonDocument.Parse(json);
        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray().ToList());
        Assert.Equal("docs/DECISIONS.md", entry.GetProperty("relativePath").GetString());
        Assert.Equal("D1", entry.GetProperty("anchor").GetString());

        var (emptyJson, emptyCount) = RemoteSyncPayloads.Entries("Shared", "[]", Head);
        Assert.Equal(0, emptyCount);
        Assert.Contains("\"entries\"", emptyJson);
    }

    /// <summary>Where this checkout stands, as git answered it.</summary>
    private static readonly TreeProvenance Head = new(
        "aaaa1111bbbb2222", DateTimeOffset.Parse("2026-09-20T09:00:00Z"), "main");

    /// <summary>
    /// The feed carries the point in the history it speaks for (D48 §6) — without it the receiving
    /// deployment cannot order a wholesale replacement against what it already holds, which is how two
    /// machines feeding one repository become a flapping generator.
    /// </summary>
    [Fact]
    public void An_entries_feed_names_the_commit_it_speaks_for()
    {
        var (json, _) = RemoteSyncPayloads.Entries("Shared", "[]", Head);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("aaaa1111bbbb2222", root.GetProperty("commit").GetString());
        Assert.Equal("main", root.GetProperty("branch").GetString());
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-20T09:00:00Z"),
            DateTimeOffset.Parse(root.GetProperty("committedAt").GetString()!));
    }

    /// <summary>
    /// A tree git could not answer for names no commit, and the fields are OMITTED rather than sent
    /// empty: the door refuses a feed it cannot compare, and a blank commit would be a claim about a
    /// point in the history that does not exist.
    /// </summary>
    [Fact]
    public void A_tree_git_cannot_answer_for_claims_no_commit_at_all()
    {
        var (json, _) = RemoteSyncPayloads.Entries("Shared", "[]", provenance: null);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("commit", out _));
        Assert.False(document.RootElement.TryGetProperty("branch", out _));
    }

    /// <summary>
    /// The canonical line rides the registration, because the deployment cannot ask git (D48 §6) — and
    /// omission preserves what was declared, so a checkout that cannot answer never erases it.
    /// </summary>
    [Fact]
    public void A_registration_carries_the_canonical_line_when_the_checkout_knows_it()
    {
        var repo = RemoteSyncPayloads.Joined(RegistryJson, RemoteTarget.DefaultWorkspace)[0];

        using var declared = JsonDocument.Parse(RemoteSyncPayloads.Registration(repo, "main"));
        Assert.Equal("main", declared.RootElement.GetProperty("defaultBranch").GetString());

        using var unknown = JsonDocument.Parse(RemoteSyncPayloads.Registration(repo, null));
        Assert.False(unknown.RootElement.TryGetProperty("defaultBranch", out _));
    }

    /// <summary>
    /// The checkout path is kept for the driver's own use — it is how git is asked anything — and
    /// still reaches NO payload. The guarantee lives where it can actually be broken: what goes on
    /// the wire.
    /// </summary>
    [Fact]
    public void The_root_is_known_to_the_driver_and_absent_from_every_payload()
    {
        var repo = RemoteSyncPayloads.Joined(RegistryJson, RemoteTarget.DefaultWorkspace)[0];
        Assert.Equal("C:/somewhere/private/Shared", repo.Root);

        foreach (var payload in new[]
        {
            RemoteSyncPayloads.Registration(repo, "main"),
            RemoteSyncPayloads.Entries(repo.Repository, "[]", Head).Json,
            RemoteSyncPayloads.CodeMap(repo.Repository, LocalMap, Head, onBase: null).Json!,
        })
        {
            Assert.DoesNotContain("private", payload);
            Assert.DoesNotContain("root", payload);
        }
    }

    // ——— Ancestry (SYNC5a): git on this machine says how its commit stands to the one held, and the
    // feed carries the held commit it checked against, so the deployment can take it as a fast-forward.

    [Fact]
    public void An_entries_feed_names_the_held_commit_it_was_checked_against()
    {
        using var based = JsonDocument.Parse(RemoteSyncPayloads.Entries("Shared", "[]", Head, onBase: "parentparent").Json);
        using var unbased = JsonDocument.Parse(RemoteSyncPayloads.Entries("Shared", "[]", Head).Json);

        Assert.Equal("parentparent", based.RootElement.GetProperty("base").GetString());
        Assert.False(unbased.RootElement.TryGetProperty("base", out _));
    }

    /// <summary>What this machine's host answered for its code map (MAP3a's door).</summary>
    private const string LocalMap = """
        { "repository": "Shared", "file": "docs/code-map.json", "problem": null,
          "modules": [{ "id": "core", "path": "src/Core", "summary": "the heart" }, { "id": "web", "path": "src/Web", "summary": "the face" }],
          "dependencies": [{ "from": "web", "to": "core", "kind": "project" }] }
        """;

    /// <summary>The map travels as the file it was read from, in the file's own shape, so the deployment judges it as the reader does.</summary>
    [Fact]
    public void A_code_map_feeds_as_the_file_it_was_read_from()
    {
        var feed = RemoteSyncPayloads.CodeMap("Shared", LocalMap, Head, onBase: "parentparent");

        Assert.Null(feed.Problem);
        using var document = JsonDocument.Parse(feed.Json!);
        var root = document.RootElement;
        Assert.Equal("Shared", root.GetProperty("repository").GetString());
        Assert.Equal("docs/code-map.json", root.GetProperty("file").GetString());
        Assert.Equal("aaaa1111bbbb2222", root.GetProperty("commit").GetString());
        Assert.Equal("parentparent", root.GetProperty("base").GetString());

        using var map = JsonDocument.Parse(root.GetProperty("map").GetString()!);
        Assert.Equal(1, map.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(["core", "web"], map.RootElement.GetProperty("modules").EnumerateArray().Select(m => m.GetProperty("id").GetString()));
        Assert.Equal("core", map.RootElement.GetProperty("dependencies")[0].GetProperty("to").GetString());
    }

    /// <summary>
    /// A checkout that keeps no map says so — that is how a deletion travels. One whose map is broken
    /// feeds nothing and says why: the deployment would refuse it whole, and only this side can fix it.
    /// </summary>
    [Fact]
    public void No_map_feeds_as_none_and_a_broken_map_does_not_feed()
    {
        var none = RemoteSyncPayloads.CodeMap(
            "Shared", """{ "repository": "Shared", "file": null, "problem": null, "modules": [], "dependencies": [] }""",
            Head, onBase: null);
        var broken = RemoteSyncPayloads.CodeMap(
            "Shared", """{ "repository": "Shared", "file": "code-map.json", "problem": "`code-map.json` is not JSON", "modules": [], "dependencies": [] }""",
            Head, onBase: null);

        using var document = JsonDocument.Parse(none.Json!);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("map").ValueKind);
        Assert.Null(broken.Json);
        Assert.Contains("not JSON", broken.Problem);
    }

    [Fact]
    public void What_the_deployment_holds_is_read_per_feed()
    {
        Assert.Equal(
            new RemoteSyncPayloads.HeldCommits("aaaa", null, "bbbb"),
            RemoteSyncPayloads.Held("""{ "repository": "Shared", "knowledge": "aaaa", "codeMap": null, "registration": "bbbb" }"""));
        Assert.Equal(RemoteSyncPayloads.HeldCommits.None, RemoteSyncPayloads.Held("{}"));
    }

    /// <summary>Nothing held, or the very commit held: nothing for git to answer, and the feed goes.</summary>
    [Fact]
    public void Nothing_held_feeds_unbased_and_the_same_commit_feeds_on_itself()
    {
        var fresh = RemoteSyncPayloads.Order("Shared", "knowledge", Head, held: null, relation: null);
        var same = RemoteSyncPayloads.Order("Shared", "knowledge", Head, held: Head.Commit, relation: null);

        Assert.True(fresh.Feed);
        Assert.Null(fresh.Base);
        Assert.True(same.Feed);
        Assert.Equal(Head.Commit, same.Base);
    }

    [Fact]
    public void A_descendant_feeds_on_the_held_commit_and_a_diverged_one_lets_commit_time_decide()
    {
        var descends = RemoteSyncPayloads.Order("Shared", "knowledge", Head, "parentparent", TreeRelation.Descends);
        var diverged = RemoteSyncPayloads.Order("Shared", "knowledge", Head, "cousincousin", TreeRelation.Diverged);

        Assert.Equal((true, "parentparent", (string?)null), (descends.Feed, descends.Base, descends.Note));
        Assert.Equal((true, (string?)null, (string?)null), (diverged.Feed, diverged.Base, diverged.Note));
    }

    /// <summary>
    /// Behind, or not fetched: this machine feeds nothing, and says which — the first is caught up by
    /// a pull, the second by a fetch, and neither is a failure.
    /// </summary>
    [Fact]
    public void A_checkout_behind_or_unaware_of_the_held_commit_feeds_nothing_and_says_why()
    {
        var behind = RemoteSyncPayloads.Order("Shared", "knowledge", Head, "futurefuture", TreeRelation.Behind);
        var unknown = RemoteSyncPayloads.Order("Shared", "code map", Head, "strangerstranger", TreeRelation.Unknown);

        Assert.False(behind.Feed);
        Assert.Contains("futurefu", behind.Note);
        Assert.Contains("ahead of this checkout", behind.Note);
        Assert.False(unknown.Feed);
        Assert.Contains("code map", unknown.Note);
        Assert.Contains("fetch", unknown.Note);
    }
}

/// <summary>
/// The orchestration half, over a stub transport: the ORDER is a contract (registrations go first, so
/// the remote knows who is joined before their records arrive — D47 §9), and a wall is reported and
/// never thrown (records sync eventually; a dead tick would take the driver's whole look with it).
/// </summary>
public sealed class RemoteSyncRunTests : IDisposable
{
    private const string Local = "http://localhost:5177";
    private const string Remote = "https://remote.example.com";

    /// <summary>
    /// A REAL checkout, because knowledge feeds only from a named commit (D48 §6): a fixture root git
    /// cannot answer for feeds nothing at all, and the order this class exists to assert would then be
    /// an order over a step that never happened. The first version of this test kept its invented path
    /// and quietly stopped covering the entries feed the moment provenance landed.
    /// </summary>
    private readonly GitTree _tree = new("remotesync-run");

    public void Dispose() => _tree.Dispose();

    /// <summary>Canned answers by (method, path prefix); records every call in order.</summary>
    private sealed class StubTransport : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];

        /// <summary>What each POST carried, by URL — the last one wins.</summary>
        public Dictionary<string, string> Bodies { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage>? Answer { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add($"{request.Method} {request.RequestUri}");
            if (request.Content is not null) Bodies[request.RequestUri!.ToString()] = await request.Content.ReadAsStringAsync(ct);
            return Answer!(request);
        }
    }

    private static HttpResponseMessage Json(string payload) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
    };

    private HttpResponseMessage AnswerHealthy(HttpRequestMessage request)
    {
        var url = request.RequestUri!.ToString();
        return url switch
        {
            _ when url.StartsWith($"{Local}/api/registry/retired") && request.Method == HttpMethod.Get =>
                Json("""{ "workspace": "default", "repositories": [] }"""),
            _ when url.StartsWith($"{Local}/api/registry") && request.Method == HttpMethod.Get => Json($$"""
                [{ "repository": "Shared", "adopted": true, "registered": true, "owns": [], "accepts": [],
                   "packs": [], "entries": 1, "root": {{System.Text.Json.JsonSerializer.Serialize(_tree.Root)}},
                   "joined": true, "sharesKnowledge": true }]
                """),
            _ when url.StartsWith($"{Local}/api/sessions") => Json("""
                [{ "id": "ab12cd34", "quest": "abc123", "repository": "Shared", "adapter": "stub",
                   "state": "completed", "created": "2026-09-20T10:00:00+00:00", "updated": "2026-09-20T10:01:00+00:00" }]
                """),
            _ when url.StartsWith($"{Local}/api/entries") => Json("[]"),
            _ when url.StartsWith($"{Remote}/api/registry") && request.Method == HttpMethod.Get => Json("[]"),
            // The host's quest pass: wired, and quiet.
            _ when url.StartsWith($"{Local}/api/sync") =>
                Json("""{ "wired": true, "conflicts": [], "refused": [], "behind": [], "problem": null }"""),
            _ => Json("{}"),
        };
    }

    [Fact]
    public async Task Registrations_go_up_before_records_and_content_and_the_quests_come_last()
    {
        using var transport = new StubTransport { Answer = AnswerHealthy };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        Assert.Empty(report.Notes);
        var order = transport.Calls;
        int At(string fragment) => order.FindIndex(call => call.Contains(fragment));
        Assert.True(At($"POST {Remote}/api/registry") >= 0, string.Join("\n", order));
        Assert.True(At($"POST {Remote}/api/registry") < At($"POST {Remote}/api/feed/entries"));
        // The code map rides beside the knowledge (MAP3b): a checkout with none still says so.
        Assert.True(At($"POST {Remote}/api/feed/entries") < At($"POST {Remote}/api/feed/code-map"));
        // Session records ride the host's pass (SYNC4): the driver never feeds them itself.
        Assert.DoesNotContain(order, call => call.Contains("/api/feed/sessions"));
        Assert.True(At($"POST {Remote}/api/feed/entries") < At($"GET {Remote}/api/registry"));
        Assert.True(At($"GET {Remote}/api/registry") < At($"POST {Local}/api/sync?workspace=default"));
    }

    /// <summary>
    /// 🔴 The quest pass is the HOST's (D69): the driver asks for it and reads what it says, and never
    /// speaks to the remote's quest doors itself — one implementation of the sync, shared with the take
    /// that claims by push, rather than two that drift.
    /// </summary>
    [Fact]
    public async Task The_driver_asks_its_host_for_the_quest_pass_and_never_touches_the_remotes_quests()
    {
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith($"{Local}/api/sync")
                ? Json("""
                    { "wired": true, "conflicts": [{ "quest": "q9", "attempted": "Taken" }],
                      "refused": [], "behind": [], "problem": null }
                    """)
                : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Contains($"POST {Local}/api/sync?workspace=default", transport.Calls);
        Assert.DoesNotContain(transport.Calls, call => call.Contains($"{Remote}/api/quests"));
        Assert.Contains(report.Notes, note => note.Contains("#q9") && note.Contains("conflict"));
    }

    /// <summary>A pass that hit a wall is the sync's problem, named — and a host with no remote for the circle is said.</summary>
    [Fact]
    public async Task A_pass_that_hit_a_wall_is_named_and_an_unwired_host_is_said()
    {
        var answer = """{ "wired": true, "problem": "the remote could not be reached (connection refused)" }""";
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith($"{Local}/api/sync")
                ? Json(answer)
                : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var walled = await sync.RunOnceAsync();
        answer = """{ "wired": false }""";
        var unwired = await sync.RunOnceAsync();

        Assert.Contains("quests: the remote could not be reached", walled.Problem);
        Assert.Null(unwired.Problem);
        Assert.Contains(unwired.Notes, note => note.Contains("has no remote for `default`"));
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

    /// <summary>
    /// A feed speaks for a commit, so a checkout with work in flight feeds neither knowledge nor map —
    /// its index would describe the working tree, not the commit it names.
    /// </summary>
    [Fact]
    public async Task A_checkout_with_uncommitted_work_feeds_nothing_and_says_so()
    {
        File.WriteAllText(Path.Combine(_tree.Root, "draft.md"), "not yet committed\n");
        using var transport = new StubTransport { Answer = AnswerHealthy };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        Assert.DoesNotContain(transport.Calls, call => call.Contains("/api/feed/entries") || call.Contains("/api/feed/code-map"));
        Assert.Contains(report.Notes, note => note.Contains("`Shared`") && note.Contains("uncommitted"));
    }

    /// <summary>
    /// The declaration is the manifest (SYNC5b): while the manifest itself is committed, the registration
    /// speaks for the commit, whatever else is in flight; once the manifest is modified, it names none.
    /// </summary>
    [Fact]
    public async Task A_committed_manifest_registers_at_its_commit_and_a_modified_one_names_none()
    {
        _tree.Commit("daoris.json");
        File.WriteAllText(Path.Combine(_tree.Root, "draft.md"), "work in flight\n");
        using var transport = new StubTransport { Answer = AnswerHealthy };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        await sync.RunOnceAsync();
        using (var committed = JsonDocument.Parse(transport.Bodies[$"{Remote}/api/registry"]))
        {
            Assert.Equal(_tree.Output("rev-parse HEAD"), committed.RootElement.GetProperty("commit").GetString());
        }

        File.WriteAllText(Path.Combine(_tree.Root, "daoris.json"), "{ \"edited\": true }\n");
        await sync.RunOnceAsync();
        using var modified = JsonDocument.Parse(transport.Bodies[$"{Remote}/api/registry"]);
        Assert.False(modified.RootElement.TryGetProperty("commit", out _));
    }

    /// <summary>
    /// A checkout behind the declaration held sends none, and one sentence names everything that waits on
    /// that commit — its registration, its knowledge and its map.
    /// </summary>
    [Fact]
    public async Task A_checkout_behind_the_held_declaration_sends_none_and_says_so_once()
    {
        var parent = _tree.Output("rev-parse HEAD");
        _tree.Commit("second.md");
        var ahead = _tree.Output("rev-parse HEAD");
        _tree.Git($"reset -q --hard {parent}");
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith($"{Remote}/api/feed/held")
                ? Json($$"""{ "repository": "Shared", "knowledge": "{{ahead}}", "codeMap": "{{ahead}}", "registration": "{{ahead}}" }""")
                : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.DoesNotContain($"POST {Remote}/api/registry", transport.Calls);
        var note = Assert.Single(report.Notes);
        Assert.Contains("registration, knowledge and code map", note);
        Assert.Contains("ahead of this checkout", note);
    }

    /// <summary>A declaration the remote judged and did not take is news, in the remote's words — never a wall.</summary>
    [Fact]
    public async Task A_registration_the_remote_did_not_take_is_a_note()
    {
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString() == $"{Remote}/api/registry" && request.Method == HttpMethod.Post
                ? new(System.Net.HttpStatusCode.Conflict)
                {
                    Content = new StringContent(
                        """{ "error": "`Shared`'s registration came from `feature`, and its canonical line is `main`.", "information": true }""",
                        System.Text.Encoding.UTF8, "application/json"),
                }
                : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        Assert.Contains(report.Notes, n => n.Contains("canonical line is `main`"));
    }

    // ——— The travelling retire and the team's rows (SYNC5b).

    private Func<HttpRequestMessage, HttpResponseMessage> Answering(
        string localRegistry, string retired, string remoteRegistry) => request =>
    {
        var url = request.RequestUri!.ToString();
        return url switch
        {
            _ when url.StartsWith($"{Local}/api/registry/retired") && request.Method == HttpMethod.Get =>
                Json($$"""{ "workspace": "default", "repositories": {{retired}} }"""),
            _ when url.StartsWith($"{Local}/api/registry") && request.Method == HttpMethod.Get => Json(localRegistry),
            _ when url.StartsWith($"{Remote}/api/registry") && request.Method == HttpMethod.Get => Json(remoteRegistry),
            _ => AnswerHealthy(request),
        };
    };

    private const string Row = """ "owns": [], "accepts": [], "packs": [], "sharesKnowledge": false """;

    /// <summary>
    /// A retire made here is carried to the circle, then cleared — and the circle's list, read a moment
    /// before, does not bring it straight back down.
    /// </summary>
    [Fact]
    public async Task A_retire_made_here_is_carried_to_the_circle_then_cleared()
    {
        using var transport = new StubTransport
        {
            Answer = Answering(
                "[]", """["Gone"]""",
                $$"""[{ "repository": "Gone", "summary": "Was mine.", "joined": true, {{Row}} }]"""),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        var told = transport.Calls.IndexOf($"DELETE {Remote}/api/registry/Gone");
        var cleared = transport.Calls.IndexOf($"DELETE {Local}/api/registry/retired/Gone?workspace=default");
        Assert.True(told >= 0 && cleared > told, string.Join("\n", transport.Calls));
        Assert.DoesNotContain($"POST {Local}/api/registry", transport.Calls);
    }

    /// <summary>
    /// A circle that never listed the repository is not told of its retire: there is nothing to take
    /// back, and telling it would name a repository to a deployment that never heard of it.
    /// </summary>
    [Fact]
    public async Task A_retire_the_circle_never_heard_of_is_cleared_without_telling_it()
    {
        using var transport = new StubTransport { Answer = Answering("[]", """["Unheard"]""", "[]") };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        await sync.RunOnceAsync();

        Assert.DoesNotContain(transport.Calls, call => call.StartsWith($"DELETE {Remote}"));
        Assert.Contains($"DELETE {Local}/api/registry/retired/Unheard?workspace=default", transport.Calls);
    }

    /// <summary>A retire owed for a repository joined here again is void: cleared, and the circle keeps it.</summary>
    [Fact]
    public async Task A_retire_owed_for_a_repository_joined_here_again_is_void()
    {
        using var transport = new StubTransport
        {
            Answer = Answering(
                $$"""[{ "repository": "Shared", "root": {{JsonSerializer.Serialize(_tree.Root)}}, "joined": true, {{Row}} }]""",
                """["Shared"]""",
                $$"""[{ "repository": "Shared", "joined": true, {{Row}} }]"""),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        await sync.RunOnceAsync();

        Assert.DoesNotContain($"DELETE {Remote}/api/registry/Shared", transport.Calls);
        Assert.Contains($"DELETE {Local}/api/registry/retired/Shared?workspace=default", transport.Calls);
    }

    /// <summary>
    /// SYNC0b end to end: a teammate's copy is rewritten when the circle's declaration changed, and a
    /// copy the circle no longer lists is retired here through the host's own door.
    /// </summary>
    [Fact]
    public async Task A_teammate_s_copy_follows_the_circle_both_ways()
    {
        using var transport = new StubTransport
        {
            Answer = Answering(
                $$"""
                [{ "repository": "Teammate", "summary": "Before.", "joined": true, {{Row}} },
                 { "repository": "Dropped", "summary": "Gone.", "joined": true, {{Row}} }]
                """,
                "[]",
                $$"""[{ "repository": "Teammate", "summary": "After.", "joined": true, {{Row}} }]"""),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        Assert.Contains("After.", transport.Bodies[$"{Local}/api/registry"]);
        Assert.Contains($"DELETE {Local}/api/registry/Dropped", transport.Calls);
        Assert.DoesNotContain(transport.Calls, call => call.StartsWith($"POST {Remote}/api/registry"));
    }

    /// <summary>
    /// The deployment holds an earlier commit of this checkout's own line: both feeds name it as their
    /// base, which is what lets the deployment take them as a fast-forward — and the map goes too.
    /// </summary>
    [Fact]
    public async Task A_checkout_ahead_of_what_is_held_feeds_both_on_the_held_commit()
    {
        var parent = _tree.Output("rev-parse HEAD");
        _tree.Commit("second.md");
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith($"{Remote}/api/feed/held")
                ? Json($$"""{ "repository": "Shared", "knowledge": "{{parent}}", "codeMap": "{{parent}}" }""")
                : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Empty(report.Notes);
        foreach (var door in new[] { "entries", "code-map" })
        {
            using var body = JsonDocument.Parse(transport.Bodies[$"{Remote}/api/feed/{door}"]);
            Assert.Equal(parent, body.RootElement.GetProperty("base").GetString());
            Assert.Equal(_tree.Output("rev-parse HEAD"), body.RootElement.GetProperty("commit").GetString());
        }
    }

    /// <summary>The deployment holds a commit this checkout has not reached: nothing goes, and the note says to pull.</summary>
    [Fact]
    public async Task A_checkout_behind_what_is_held_feeds_nothing()
    {
        var parent = _tree.Output("rev-parse HEAD");
        _tree.Commit("second.md");
        var ahead = _tree.Output("rev-parse HEAD");
        _tree.Git($"reset -q --hard {parent}");
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith($"{Remote}/api/feed/held")
                ? Json($$"""{ "repository": "Shared", "knowledge": "{{ahead}}", "codeMap": "{{ahead}}" }""")
                : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.DoesNotContain(transport.Calls, call => call.Contains("/api/feed/entries") || call.Contains("/api/feed/code-map"));
        // One note, not one per feed: both stand on the same held commit.
        Assert.Single(report.Notes, note => note.Contains("ahead of this checkout"));
    }

    /// <summary>
    /// A WIRED circle gets its pass whether or not anything here joins it (sync design §6): nothing
    /// leaves that no manifest declared, and the team's rows and quests still come down — a machine that
    /// joined nothing still addresses its teammates, and *Sync now* there must do what it says.
    /// </summary>
    [Fact]
    public async Task A_wired_circle_with_nothing_joined_feeds_nothing_and_still_hears_the_team()
    {
        using var transport = new StubTransport
        {
            Answer = request =>
            {
                var url = request.RequestUri!.ToString();
                return url switch
                {
                    _ when url.StartsWith($"{Local}/api/registry/retired") => Json("""{ "workspace": "default", "repositories": [] }"""),
                    _ when url.StartsWith($"{Local}/api/sync") =>
                        Json("""{ "wired": true, "conflicts": [], "refused": [], "behind": [], "problem": null }"""),
                    _ when url.StartsWith($"{Remote}/api/registry") && request.Method == HttpMethod.Get => Json("""
                        [{ "repository": "Teammate", "owns": [], "accepts": [], "packs": [], "joined": true, "sharesKnowledge": false }]
                        """),
                    _ => Json("""
                        [{ "repository": "Homebody", "adopted": true, "registered": true, "owns": [], "accepts": [],
                           "packs": [], "entries": 0, "root": "C:/somewhere/Homebody", "joined": false, "sharesKnowledge": false }]
                        """),
                };
            },
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        // Nothing of Homebody goes up: it declared no join.
        Assert.DoesNotContain(transport.Calls, call => call.StartsWith($"POST {Remote}"));
        Assert.DoesNotContain(transport.Calls, call => call.Contains("Homebody"));
        // The team's row comes down, and the host runs its pass.
        Assert.Contains($"POST {Local}/api/registry", transport.Calls);
        Assert.Contains($"POST {Local}/api/sync?workspace=default", transport.Calls);
    }

    /// <summary>
    /// 🔴 A wall in the feed does not stop the host's pass (SYNC6b). The host's pass is where a try is
    /// RECORDED, so when the feed's first call hit a remote that was down and returned early, the
    /// standing kept saying "synced" while *Sync now* had just failed (seen on the real window).
    /// The wall is still the pass's problem, named once.
    /// </summary>
    [Fact]
    public async Task A_wall_in_the_feed_still_asks_the_host_for_its_pass()
    {
        using var transport = new StubTransport
        {
            Answer = request => request.RequestUri!.ToString().StartsWith(Remote)
                ? throw new HttpRequestException("No connection could be made (remote.example.com)")
                : request.RequestUri.ToString().StartsWith($"{Local}/api/sync")
                    ? Json("""{ "wired": true, "problem": "the remote could not be reached (connection refused)" }""")
                    : AnswerHealthy(request),
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Contains($"POST {Local}/api/sync?workspace=default", transport.Calls);
        Assert.Equal("No connection could be made (remote.example.com)", report.Problem);
    }

    /// <summary>
    /// The last joined checkout retired: nothing is joined here any more, and the circle is still owed
    /// the retire. It is carried.
    /// </summary>
    [Fact]
    public async Task A_machine_with_nothing_joined_still_carries_its_last_retire()
    {
        using var transport = new StubTransport
        {
            Answer = request =>
            {
                var url = request.RequestUri!.ToString();
                return url switch
                {
                    _ when url.StartsWith($"{Local}/api/registry/retired") =>
                        Json("""{ "workspace": "default", "repositories": ["Last"] }"""),
                    _ when url.StartsWith($"{Remote}/api/registry") && request.Method == HttpMethod.Get => Json("""
                        [{ "repository": "Last", "owns": [], "accepts": [], "packs": [], "joined": true, "sharesKnowledge": false }]
                        """),
                    _ when url.StartsWith($"{Local}/api/sync") =>
                        Json("""{ "wired": true, "conflicts": [], "refused": [], "behind": [], "problem": null }"""),
                    _ when url.StartsWith($"{Local}/api/registry") && request.Method == HttpMethod.Get => Json("""
                        [{ "repository": "Homebody", "owns": [], "accepts": [], "packs": [], "root": "C:/somewhere/Homebody",
                           "joined": false, "sharesKnowledge": false }]
                        """),
                    _ => Json("{}"),
                };
            },
        };
        using var sync = new RemoteSync(
            Local, null, RemoteTarget.DefaultWorkspace, new RemoteTarget(Remote, "dk_test"), transport);

        var report = await sync.RunOnceAsync();

        Assert.Null(report.Problem);
        Assert.Contains($"DELETE {Remote}/api/registry/Last", transport.Calls);
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
            _ => Json("{}"),
        };
    }

    private static RemoteSyncSet Set(HttpMessageHandler transport) => new(
    [
        new RemoteSync(Local, null, "aurora", new RemoteTarget(Aurora, "dk_aurora"), transport),
        new RemoteSync(Local, null, "tools", new RemoteTarget(Tools, "dk_tools"), transport),
    ]);

    /// <summary>
    /// `daoris-driver sync --workspace` and *Sync now* run ONE circle's pass (SYNC6a): the other circle's
    /// remote hears nothing, and a circle this machine has no remote for is not a pass at all.
    /// </summary>
    [Fact]
    public async Task One_circle_syncs_on_its_own_when_it_is_named()
    {
        using var transport = new RecordingTransport { Answer = Answer };
        using var set = Set(transport);

        var report = await set.RunOnceAsync("TOOLS");

        Assert.Null(report.Problem);
        Assert.Contains(transport.Calls, c => c.Call.StartsWith($"POST {Tools}/"));
        Assert.DoesNotContain(transport.Calls, c => c.Call.Contains(Aurora));
        var unwired = await Assert.ThrowsAsync<DriverException>(() => set.RunOnceAsync("studio"));
        Assert.Contains("no remote for `studio`", unwired.Message);
    }

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

    // ——— SYNC0d: the map is re-read on every pass, so wiring a remote needs no restart.

    /// <summary>Which key each call to a host carried — the header is what a rotated key changes.</summary>
    private sealed class KeyedTransport : HttpMessageHandler
    {
        public List<(string Call, string? Key)> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add(($"{request.Method} {request.RequestUri}", request.Headers.Authorization?.Parameter));
            return Task.FromResult(Answer(request));
        }
    }

    /// <summary>
    /// 🔴 The set was built once when the loop started, so a remote wired afterwards synced nothing
    /// until a restart — though the remotes editor says the loop re-reads the map on its next pass.
    /// </summary>
    [Fact]
    public async Task A_remote_wired_after_the_loop_started_syncs_on_the_next_pass()
    {
        var map = new Dictionary<string, RemoteTarget>(StringComparer.OrdinalIgnoreCase);
        using var transport = new KeyedTransport();
        using var set = RemoteSyncSet.Watching(Local, null, () => map, transport);

        await set.RunOnceAsync();
        Assert.DoesNotContain(transport.Calls, c => c.Call.StartsWith($"POST {Aurora}"));
        Assert.Empty(set.Workspaces);

        map["aurora"] = new RemoteTarget(Aurora, "dk_aurora");
        await set.RunOnceAsync();

        Assert.Contains(transport.Calls, c => c.Call == $"POST {Aurora}/api/registry");
        Assert.Equal(["aurora"], set.Workspaces);
    }

    [Fact]
    public async Task A_changed_key_is_used_on_the_next_pass_and_a_removed_remote_stops()
    {
        var map = new Dictionary<string, RemoteTarget>(StringComparer.OrdinalIgnoreCase)
        {
            ["aurora"] = new RemoteTarget(Aurora, "dk_old"),
        };
        using var transport = new KeyedTransport();
        using var set = RemoteSyncSet.Watching(Local, null, () => map, transport);

        await set.RunOnceAsync();
        map["aurora"] = new RemoteTarget(Aurora, "dk_new");
        transport.Calls.Clear();
        await set.RunOnceAsync();

        Assert.All(transport.Calls.Where(c => c.Call.StartsWith($"POST {Aurora}")), c => Assert.Equal("dk_new", c.Key));

        map.Clear();
        transport.Calls.Clear();
        await set.RunOnceAsync();

        Assert.DoesNotContain(transport.Calls, c => c.Call.Contains(Aurora));
        Assert.Empty(set.Workspaces);
    }
}
