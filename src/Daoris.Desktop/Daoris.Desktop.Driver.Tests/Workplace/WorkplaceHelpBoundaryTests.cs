using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class WorkplaceHelpBoundaryTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-help", Guid.NewGuid().ToString("N"));
    public void Dispose() => GitSeed.Remove(_home);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("default")]
    [InlineData(" Default ")]
    public void Default_scope_keeps_the_resolved_default_room(string? workspace)
    {
        var expected = DaorisHome.Entry(_home, HomeEntry.HelpRoom);
        Assert.Null(HelpRoom.ScopeOf(workspace));
        Assert.Null(KnowledgeRoomPaths.ScopeOf(workspace));
        Assert.Equal(expected, HelpRoom.PathOf(_home));
        Assert.Equal(expected, HelpRoom.PathOf(_home, workspace));
        Assert.Equal(expected, KnowledgeRoomPaths.PathOf(_home, workspace));
        Assert.Equal("help", HelpRoom.Folder);
    }

    [Theory]
    [InlineData(" ALPHA ", "help-alpha")]
    [InlineData("a/b", "help-a_2fb")]
    [InlineData("a_b", "help-a_5fb")]
    [InlineData("é", "help-_c3_89")]
    [InlineData("e\u0301", "help-e_cc_81")]
    [InlineData("😀", "help-_f0_9f_98_80")]
    [InlineData("..", "help-_2e_2e")]
    [InlineData("Ü", "help-_c3_9c")]
    public void Named_folders_keep_case_invariant_UTF8_rune_spelling_without_new_normalization(string workspace, string expected)
    {
        Assert.Equal(expected, HelpRoom.FolderOf(workspace));
        Assert.Equal(expected, KnowledgeRoomPaths.FolderOf(workspace));
        var parent = Path.GetDirectoryName(HelpRoom.PathOf(_home))!;
        Assert.Equal(Path.Combine(parent, expected), HelpRoom.PathOf(_home, workspace));
        Assert.Equal(workspace.Trim(), HelpRoom.ScopeOf(workspace));
    }

    [Fact]
    public void The_48_spelled_character_boundary_keeps_32_characters_and_16_digest_hex_digits_only_above_it()
    {
        Assert.Equal("help-" + new string('x', 48), HelpRoom.FolderOf(new string('x', 48)));
        Assert.Equal("help-" + new string('x', 32) + "-10ae6e781a44a4d0", HelpRoom.FolderOf(new string('x', 49)));
        Assert.Equal(HelpRoom.FolderOf(new string('x', 49)), HelpRoom.FolderOf(" " + new string('X', 49) + " "));
        Assert.Equal("help-" + string.Concat(Enumerable.Repeat("_21", 16)), HelpRoom.FolderOf(new string('!', 16)));
        Assert.Equal("help-" + string.Concat(Enumerable.Repeat("_21", 10)) + "_2-8c8dbc4bf02965b8", HelpRoom.FolderOf(new string('!', 17)));
    }

    [Theory]
    [InlineData("new")]
    [InlineData("old")]
    [InlineData("conflict")]
    public void Named_rooms_stay_beside_the_actual_default_placement_and_conflicts_propagate(string placement)
    {
        var places = DaorisHome.Places(_home, HomeEntry.HelpRoom);
        var next = places[0];
        var old = places[1];
        foreach (var room in placement == "conflict" ? new[] { next, old } : [placement == "old" ? old : next])
        {
            Directory.CreateDirectory(room);
            File.WriteAllText(Path.Combine(room, "CLAUDE.md"), "kept");
        }
        if (placement == "conflict")
        {
            Assert.Throws<HomeEntryTwiceException>(() => HelpRoom.PathOf(_home));
            Assert.Throws<HomeEntryTwiceException>(() => HelpRoom.PathOf(_home, "alpha"));
            Assert.Throws<HomeEntryTwiceException>(() => KnowledgeRoomPaths.PathOf(_home, "alpha"));
            Assert.Throws<HomeEntryTwiceException>(() => ToolHomeLeftovers.Origins(_home,
                "[{\"id\":\"s1\",\"state\":\"completed\",\"profile\":\"work\",\"repository\":\"daoris:help\",\"workspace\":\"alpha\"}]"));
            return;
        }
        var expected = placement == "old" ? old : next;
        Assert.Equal(expected, HelpRoom.PathOf(_home));
        Assert.Equal(Path.Combine(Path.GetDirectoryName(expected)!, "help-alpha"), HelpRoom.PathOf(_home, "alpha"));
        Assert.Equal(HelpRoom.PathOf(_home, "alpha"), KnowledgeRoomPaths.PathOf(_home, "alpha"));
    }

    [Fact]
    public void Invalid_null_home_still_throws_instead_of_receiving_a_fallback()
    {
        Assert.Throws<ArgumentNullException>(() => HelpRoom.PathOf(null!));
        Assert.Throws<ArgumentNullException>(() => HelpRoom.PathOf(null!, "alpha"));
        Assert.Throws<ArgumentNullException>(() => KnowledgeRoomPaths.PathOf(null!, "alpha"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(".help.json")]
    [InlineData(".wired.json")]
    [InlineData(".published.json")]
    public void Literal_historical_files_keep_help_inventory_order_bytes_and_refusal_continuation(string? refused)
    {
        var sessions = DaorisHome.Entry(_home, HomeEntry.Sessions);
        Directory.CreateDirectory(sessions);
        var suffixes = new[] { ".help.json", ".wired.json", ".published.json" };
        var sizes = new[] { 3, 5, 7 };
        for (var index = 0; index < suffixes.Length; index++) File.WriteAllText(Path.Combine(sessions, "s1" + suffixes[index]), new string('x', sizes[index]));
        var other = Path.Combine(sessions, "s2.help.json");
        File.WriteAllText(other, "another session");
        var conversation = Path.Combine(sessions, "s1.events.jsonl");
        File.WriteAllText(conversation, "abc");
        var calls = new List<string>();
        var inventory = new SessionHomeFiles(_home, (path, folder) =>
        {
            Assert.False(folder);
            calls.Add(path);
            if (refused is not null && path == Path.Combine(sessions, "s1" + refused))
                throw refused == ".wired.json" ? new UnauthorizedAccessException("held") : new IOException("held");
            File.Delete(path);
        });
        Assert.Equal((3L, 15L), (inventory.Size("s1").Conversations, inventory.Size("s1").Other));
        var result = inventory.Remove(["s1"], null);
        Assert.Equal(new[] { conversation }.Concat(suffixes.Select(suffix => Path.Combine(sessions, "s1" + suffix))), calls);
        Assert.Equal(["conversation", "help"], result.Went["s1"]);
        Assert.Equal(refused is null ? Array.Empty<string>() : ["help"], result.Stayed["s1"]);
        Assert.Equal(refused is null ? Array.Empty<string>() : [Path.Combine(sessions, "s1" + refused)], result.Failed);
        Assert.Equal(15 - (refused is null ? 0 : sizes[Array.IndexOf(suffixes, refused)]), result.Bytes["s1"].Other);
        Assert.Equal(3, result.Bytes["s1"].Conversations);
        Assert.Equal("another session", File.ReadAllText(other));
        foreach (var suffix in suffixes) Assert.Equal(suffix == refused, File.Exists(Path.Combine(sessions, "s1" + suffix)));
    }

    [Theory]
    [InlineData("daoris:help", "a1", "help")]
    [InlineData("DAORIS:HELP", "a1", "intake")]
    [InlineData("engine", "a1", "intake")]
    [InlineData("engine", null, "chat")]
    public async Task Logged_delete_kind_keeps_exact_help_identity_and_precedence_over_intake(string repository, string? ask, string expected)
    {
        using var ledger = new Ledger(repository, ask);
        using var service = new ServiceClient("http://ledger.test", null, new HttpClient(ledger, disposeHandler: false));
        using (var log = new MachineLog(_home, "desktop"))
            Assert.Equal(DeleteVerdict.Deleted, (await new SessionDeletion(_home).DeleteAsync(service, "s1", "screen", log)).Verdict);
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        var lines = Directory.GetFiles(folder).SelectMany(File.ReadAllLines).Select(line => JsonDocument.Parse(line)).ToList();
        try
        {
            var row = Assert.Single(lines, line => line.RootElement.GetProperty("event").GetString() == "session.deleted");
            Assert.Equal(expected, row.RootElement.GetProperty("data").GetProperty("kind").GetString());
        }
        finally { foreach (var line in lines) line.Dispose(); }
    }

    private sealed class Ledger(string repository, string? ask) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            JsonNode body = request.RequestUri!.AbsolutePath == "/api/sessions"
                ? new JsonArray(new JsonObject { ["id"] = "s1", ["repository"] = repository, ["ask"] = ask, ["kind"] = "chat", ["state"] = "completed",
                    ["created"] = "2026-10-27T00:00:00Z", ["updated"] = "2026-10-27T00:00:00Z", ["deletable"] = true })
                : new JsonObject { ["id"] = "s1", ["deletable"] = true, ["message"] = "deleted" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public void Fixed_origins_and_wiring_keep_exact_order_provenance_and_terminal_output()
    {
        var scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-help-parity");
        try
        {
            var home = Path.Combine(scratch, "home");
            var tool = Path.Combine(scratch, "person");
            var tree = DaorisHome.Entry(home, HomeEntry.Trees, "alpha", "engine", "s1");
            var records = JsonSerializer.Serialize(new object[]
            {
                new { id = "s1", state = "working", tree, profile = "work", repository = "daoris:help", workspace = "alpha", ask = "a1" },
                new { id = "s2", state = "completed", tree, profile = "work", repository = "DAORIS:HELP", workspace = "beta", ask = "a2" },
                new { id = "s3", state = "completed", tree, profile = "work", repository = "daoris:help", workspace = " Default ", ask = (string?)null },
            });
            var origins = ToolHomeLeftovers.Origins(home, records)!;
            Assert.Equal(["tree", "help", "intake", "tree", "intake", "tree", "help"], origins.Select(each => each.Source));
            Assert.Equal(["s1", "s1", "s1", "s2", "s2", "s3", "s3"], origins.Select(each => each.By));
            Assert.Equal(["session", "session", "session", null, null, null, null], origins.Select(each => each.Keeps));
            Assert.Equal(HelpRoom.PathOf(home, "alpha"), origins[1].Folder);
            Assert.Equal(HelpRoom.PathOf(home), origins[^1].Folder);
            foreach (var origin in origins)
            {
                var folder = Path.Combine(tool, "projects", ToolHomeLeftovers.Flatten(origin.Folder));
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "kept.jsonl"), "unchanged\n");
            }
            var world = new WiringWorld
            {
                Home = home, BaseDirectory = scratch, Process = _ => null, Account = _ => null, Machine = _ => null,
                Lives = (_, _) => false, AskStatus = _ => throw new InvalidOperationException("no network"),
                ToolHome = tool, ToolOrigins = origins, Sizes = new WiringSizes(),
            };
            var answer = SystemWiring.Read(world);
            Assert.NotEmpty(answer["toolHome"]!["folders"]!.AsArray());
            Assert.Equal(0, WiringCommand.Run(["--json"], world, out var json));
            Assert.Equal(SystemWiring.Json(answer), json);
            Assert.Equal(0, WiringCommand.Run([], world, out var text));
            Assert.Equal(WiringCommand.Text(answer), text);
            var evidence = Path.Combine(GitSeed.RepoRoot(), "local", "scratch", "workplace-help");
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "origins-current.json"), JsonSerializer.Serialize(origins), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidence, "parity-current.json"), json, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidence, "parity-current.txt"), text, new UTF8Encoding(false));
        }
        finally { GitSeed.Remove(scratch); }
    }
}
