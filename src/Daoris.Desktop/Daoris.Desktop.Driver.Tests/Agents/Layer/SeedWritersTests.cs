using System.Text.Json.Nodes;
using Daoris.Driver;
using Lyntai.Agents;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each provider's seed writer holds Lyntai's contract (<see cref="SeedWriterConformance"/>) and writes its agent's own format: Claude Code's
/// conversation lines in the tree's project folder, Codex's rollout under its day; dsh states none, so its moves take the brief. The layer's
/// seeding writes a session's events and Daoris's record as one conversation under the account's home.
/// </summary>
public sealed class SeedWritersTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 1, 2, 3, TimeSpan.Zero);

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-seed-" + Guid.NewGuid().ToString("N")[..8]);

    private string Tree => Path.Combine(_root, "tree");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void Claude_Code_s_writer_keeps_the_contract_on_both_doors(string door) =>
        Assert.Empty(SeedWriterConformance.Check(new ClaudeSeedWriter(Tree, At), door));

    [Fact]
    public void Codex_s_writer_keeps_the_contract() => Assert.Empty(SeedWriterConformance.Check(new CodexSeedWriter(Tree, At), "codex-acp"));

    [Fact]
    public void dsh_states_no_seed_so_its_moves_take_the_brief()
    {
        Assert.Empty(SeedWriterConformance.Check(DshSeedWriter.Writer, "dsh"));
        Assert.Equal(AgentSupport.Unknown, DshSeedWriter.Writer.SupportFor("dsh"));
        Assert.Null(ConversationSeedings.Through(ProviderRegistry.Built(), new DoorId("dsh")));
    }

    /// <summary>🔴 Claude Code reads a conversation by its folder and its chain: each line a user or assistant one, chained by parent.</summary>
    [Fact]
    public void Claude_Code_s_conversation_is_its_own_lines_in_the_tree_s_project_folder()
    {
        var seeded = new ClaudeSeedWriter(Path.GetFullPath(Tree), At).Write(SeedWriterConformance.Sample, "claude-code", "11111111-2222-3333-4444-555555555555");

        var file = Assert.Single(seeded.Files);
        Assert.Equal($"projects/{ClaudeConversationHome.Home.Projects!.Flatten(Path.GetFullPath(Tree))}/11111111-2222-3333-4444-555555555555.jsonl", file.Name);
        var lines = file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();
        Assert.All(lines, l => Assert.Equal("11111111-2222-3333-4444-555555555555", (string?)l["sessionId"]));
        Assert.All(lines, l => Assert.Equal(Path.GetFullPath(Tree), (string?)l["cwd"]));
        Assert.Null(lines[0]["parentUuid"]);
        for (var i = 1; i < lines.Count; i++) Assert.Equal((string?)lines[i - 1]["uuid"], (string?)lines[i]["parentUuid"]);
        Assert.Equal(["user", "assistant", "user", "user", "assistant", "user"], lines.Select(l => (string?)l["type"]));

        var calls = lines[1]["message"]!["content"]!.AsArray();
        Assert.Equal("tests/Fixture.cs", (string?)calls[1]!["input"]!["path"]);
        Assert.True((bool)lines[3]["message"]!["content"]![0]!["is_error"]!);
        Assert.Equal("<synthetic>", (string?)lines[1]["message"]!["model"]);
    }

    /// <summary>🔴 Codex finds a thread by its id in the name of a rollout under its day; its first line is the thread's meta.</summary>
    [Fact]
    public void Codex_s_thread_is_a_rollout_under_its_day_opening_on_its_meta()
    {
        var seeded = new CodexSeedWriter(Path.GetFullPath(Tree), At).Write(SeedWriterConformance.Sample, "codex-acp", "0199aaaa-bbbb-7ccc-8ddd-eeeeeeeeeeee");

        var file = Assert.Single(seeded.Files);
        Assert.Equal("sessions/2026/11/15/rollout-2026-11-15T01-02-03-0199aaaa-bbbb-7ccc-8ddd-eeeeeeeeeeee.jsonl", file.Name);
        var lines = file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();
        Assert.Equal("session_meta", (string?)lines[0]["type"]);
        Assert.Equal("0199aaaa-bbbb-7ccc-8ddd-eeeeeeeeeeee", (string?)lines[0]["payload"]!["id"]);
        var call = lines.Single(l => (string?)l["payload"]!["type"] == "function_call" && (string?)l["payload"]!["call_id"] == "call-1");
        Assert.Equal(((SeedToolCall)SeedWriterConformance.Sample.Messages[1].Parts[1]).Arguments, (string?)call["payload"]!["arguments"]);
        Assert.Equal(["user", "assistant", "assistant", "user"], lines.Where(l => (string?)l["payload"]!["type"] == "message").Select(l => (string?)l["payload"]!["role"]));
    }

    /// <summary>
    /// 🔴 A session's events become one conversation in the account's home: the person's words, the agent's call and its result, then the
    /// session's context entries as the host's record, an agent's as its claim.
    /// </summary>
    [Fact]
    public void The_layer_s_seeding_writes_a_session_s_events_and_record_as_the_agent_s_conversation()
    {
        var home = Path.Combine(_root, "home");
        SessionEvent[] events =
        [
            new() { Kind = SessionEventKind.User, Text = "Fix the login test.", Origin = "person" },
            new() { Kind = SessionEventKind.Message, Text = "Reading " },
            new() { Kind = SessionEventKind.Message, Text = "the fixture." },
            new() { Kind = SessionEventKind.Tool, Id = "t1", ToolName = "Read", Input = "{\"path\":\"a.cs\"}", Status = "pending" },
            new() { Kind = SessionEventKind.Tool, Id = "t1", Output = "line 1", Status = "completed" },
            new() { Kind = SessionEventKind.Note, Text = "a note of Daoris's" },
            new() { Kind = SessionEventKind.Message, Text = "It shares a clock." },
        ];

        ContextEntry[] context =
        [
            new(ContextKind.Work, "Fix the login test."),
            new(ContextKind.Standing, "Where it stands: the clock is shared."),
            new(ContextKind.Found, "a.cs (2 times)") { Subject = "files read" },
            new(ContextKind.Standing, "I was about to move the clock.") { Claim = true },
        ];

        var placed = ClaudeSeeding.Seeding.Place(new SeedAsk("claude-code", home, Tree, "seed-1", events, context, At));

        Assert.Null(placed.Refused);
        var name = Assert.Single(placed.Files);
        var read = new ClaudeSeedWriter(Path.GetFullPath(Tree), At).Read(new SeededConversation("seed-1", [new ContextFile(name, File.ReadAllText(Path.Combine(home, name)))]));
        Assert.Equal(SeedRole.Host, read.Messages[0].Role);
        Assert.Equal(new SeedText("Fix the login test."), read.Messages[0].Parts[0]);
        Assert.Equal([new SeedText("Reading the fixture."), new SeedToolCall("t1", "Read", "{\"path\":\"a.cs\"}")], read.Messages[1].Parts);
        Assert.Equal(new SeedToolResult("t1", "line 1"), read.Messages[2].Parts[0]);
        Assert.Equal(new SeedText("It shares a clock."), read.Messages[3].Parts[0]);
        // The session's context entries are the host's last message, each by its kind, the agent's quoted as its claim.
        var record = ((SeedText)read.Messages[^1].Parts[0]).Text;
        Assert.Equal(SeedRole.Host, read.Messages[^1].Role);
        Assert.Contains("work: Fix the login test.", record);
        Assert.Contains("standing: Where it stands: the clock is shared.", record);
        Assert.Contains("(files read) found: a.cs (2 times)", record);
        Assert.Matches(@"(?m)^> .*I was about to move the clock\.", record);
        Assert.DoesNotContain(read.Messages, m => m.Parts.OfType<SeedText>().Any(t => t.Text.Contains("a note of Daoris's")));

        ClaudeSeeding.Seeding.Remove(home, placed);
        Assert.False(File.Exists(Path.Combine(home, name)));
    }

    [Fact]
    public void Claude_Code_and_Codex_seed_through_their_doors_once_the_layer_is_loaded()
    {
        var registry = ProviderRegistry.Built();
        Assert.NotNull(ConversationSeedings.Through(registry, new DoorId("claude-code")));
        Assert.NotNull(ConversationSeedings.Through(registry, new DoorId("claude-code-acp")));
        Assert.NotNull(ConversationSeedings.Through(registry, new DoorId("codex-acp")));
    }

    [Fact]
    public void A_call_s_input_that_is_no_object_is_held_under_input()
    {
        Assert.Equal("{\"a\":1}", SeedHistory.Arguments("{\"a\":1}"));
        Assert.Equal("{\"input\":\"ls -la \\\"x\"}", SeedHistory.Arguments("ls -la \"x"));
        Assert.Equal("{}", SeedHistory.Arguments(null));
    }
}
