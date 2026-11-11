using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A range's packet as it is sent: its acts with their packet ids, call ids, outcomes and locations; the person's words fenced as theirs,
/// with three earlier ones as context; Ask Daoris's words to the person; the known reads counted and never quoted; the reasoning left out;
/// every cut and missing piece said as a limit; secrets, addresses, checkouts and the home folder redacted; and the whole within its bound,
/// the oldest calls' inputs and outputs cut first.
/// </summary>
public sealed class AskDaorisPacketTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private const string Fence = "f00d42";

    private sealed class Log
    {
        private readonly List<SessionEvent> _events = [];

        public IReadOnlyList<SessionEvent> Events => _events;

        public Log Add(SessionEvent e)
        {
            _events.Add(e with { Seq = _events.Count + 1, At = Start.AddSeconds(_events.Count + 1) });
            return this;
        }

        public Log User(string id, string words) => Add(new SessionEvent { Kind = SessionEventKind.User, Id = id, Text = words, Origin = "person" });

        public Log Says(string words) => Add(new SessionEvent { Kind = SessionEventKind.Message, Text = words });

        public Log Thinks(string words) => Add(new SessionEvent { Kind = SessionEventKind.Thought, Text = words });

        public Log Tool(string id, string kind, string? input = null, string? output = null, IReadOnlyList<string>? locations = null) =>
            Add(new SessionEvent { Kind = SessionEventKind.Tool, Id = id, ToolKind = kind, Title = kind, ToolName = kind == "execute" ? "Bash" : null, Status = "completed", Input = input, Output = output, Locations = locations });

        public Log Turn() => Add(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
    }

    private static AskDaorisReport Report(Log log, int skip = 0)
    {
        var turns = AskDaorisSelection.Turns(new TurnEvidence(log.Events, [], [])).Skip(skip).Where(turn => turn.Selected).ToList();
        return new AskDaorisReport("r-1", "s1", AskDaorisStates.Queued, Start, turns, [.. turns.SelectMany(turn => turn.Limits).Distinct()]);
    }

    private static AskDaorisPacketText Packet(Log log, PacketRedaction? redaction = null, int bound = AskDaorisPacket.Bound, int skip = 0) =>
        AskDaorisPacket.Compose(new PacketRange(Report(log, skip), Mode: "full access"), log.Events, Fence, redaction ?? PacketRedaction.None, bound);

    [Fact]
    public void Its_acts_carry_their_packet_ids_call_ids_outcomes_and_locations_and_its_reads_are_counted_never_quoted()
    {
        var log = new Log().User("w-a", "Push the fix to the engine.")
            .Tool("toolu_read", "read", input: "{\"file\":\"SECRET-READ-CONTENT\"}", output: "the read file's words")
            .Tool("toolu_push", "execute", input: "{\"command\":\"git push origin fix\"}", output: "To origin\n * [new branch] fix", locations: ["engine"])
            .Says("Pushed.").Turn();

        var packet = Packet(log);

        Assert.Contains("[c1]", packet.Text);
        Assert.Contains("toolu_push", packet.Text);
        Assert.Contains("git push origin fix", packet.Text);
        Assert.Contains("completed", packet.Text);
        Assert.Contains("engine", packet.Text);
        Assert.Contains("read 1", packet.Text);
        Assert.DoesNotContain("SECRET-READ-CONTENT", packet.Text);
        Assert.DoesNotContain("the read file's words", packet.Text);
        Assert.Equal(["c1"], packet.Calls);
        Assert.Contains("full access", packet.Text);
    }

    [Fact]
    public void The_person_s_words_are_fenced_as_theirs_with_three_earlier_ones_as_context_and_the_whole_under_its_nonce()
    {
        var log = new Log();
        for (var each = 1; each <= 4; each++) log.User($"old-{each}", $"earlier words {each}").Says("ok").Turn();
        log.User("w-now", "Delete the stale branch.").Tool("toolu_del", "delete").Says("Deleted.").Turn();

        var packet = Packet(log, skip: 4);

        Assert.StartsWith($"<<<packet {Fence}", packet.Text, StringComparison.Ordinal);
        Assert.EndsWith($"packet {Fence}>>>", packet.Text.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains($"<<<person {Fence}\nDelete the stale branch.\nperson {Fence}>>>", packet.Text);
        Assert.DoesNotContain("earlier words 1", packet.Text);
        Assert.Contains("earlier words 2", packet.Text);
        Assert.Contains("earlier words 4", packet.Text);
        Assert.Equal(["w1", "w2", "w3", "w4"], packet.Words.Order(StringComparer.Ordinal));
        Assert.Contains("Deleted.", packet.Text);
    }

    [Fact]
    public void The_reasoning_is_never_sent()
    {
        var log = new Log().User("w-a", "Edit the file.").Thinks("PRIVATE-REASONING").Tool("toolu_e", "edit").Says("Edited.").Turn();

        Assert.DoesNotContain("PRIVATE-REASONING", Packet(log).Text);
    }

    [Fact]
    public void Ask_Daoris_s_last_message_keeps_four_thousand_characters_and_each_earlier_one_a_thousand()
    {
        var log = new Log().User("w-a", "Clean up.").Says(new string('e', 3_000)).Tool("toolu_e", "edit").Says(new string('l', 5_000)).Turn();

        var text = Packet(log).Text;

        Assert.Contains(new string('e', 1_000) + " [cut:", text);
        Assert.DoesNotContain(new string('e', 1_001), text);
        Assert.Contains(new string('l', 4_000) + " [cut:", text);
        Assert.DoesNotContain(new string('l', 4_001), text);
    }

    [Fact]
    public void A_cut_the_record_made_is_marked_and_said_as_a_limit_so_the_reading_is_partial()
    {
        var clipped = new string('x', SessionEventText.RawLimit) + TextBounds.Ellipsis + " (9000 chars)";
        var log = new Log().User("w-a", "Run it.").Tool("toolu_x", "execute", output: clipped).Turn();

        var packet = Packet(log);

        Assert.Contains(packet.Limits, limit => limit.Code == ReadLimit.Cut);
        Assert.Contains("the record cut its input or output", packet.Text);
        Assert.Contains("partial", packet.Text);
    }

    [Fact]
    public void Secrets_addresses_checkouts_and_the_home_folder_are_redacted()
    {
        var input = "{\"command\":\"curl https://user:pw@example.com/a?token=abc#frag -H key-1234567890 && type C:\\\\work\\\\engine\\\\src\\\\a.cs D:\\\\person\\\\notes.txt\"}";
        var log = new Log().User("w-a", "Fetch it.").Tool("toolu_c", "execute", input: input).Turn();
        var redaction = new PacketRedaction(["key-1234567890"], new Dictionary<string, string> { ["engine"] = @"C:\work\engine" }, @"D:\person");

        var text = Packet(log, redaction).Text;

        Assert.DoesNotContain("key-1234567890", text);
        Assert.Contains(WiringRedact.Cut, text);
        Assert.DoesNotContain("token=abc", text);
        Assert.DoesNotContain("pw@", text);
        Assert.DoesNotContain(@"C:\\work", text);
        Assert.Contains(@"[engine]\\src\\a.cs", text);
        Assert.Contains(@"~\\notes.txt", text);
    }

    [Fact]
    public void Over_its_bound_the_oldest_calls_lose_their_input_and_output_first_each_keeping_its_identity_line_and_each_cut_a_limit()
    {
        var log = new Log().User("w-a", "Do the three.");
        for (var each = 1; each <= 3; each++) log.Tool($"toolu_{each}", "edit", input: new string((char)('a' + each), 1_500));
        log.Turn();

        var packet = Packet(log, bound: 4_500);

        Assert.True(packet.Text.Length <= 4_500, $"{packet.Text.Length} characters");
        Assert.Contains("toolu_1", packet.Text);
        Assert.Contains("toolu_3", packet.Text);
        Assert.DoesNotContain(new string('b', 100), packet.Text);
        Assert.Contains(new string('d', 1_500), packet.Text);
        Assert.Contains(new ReadLimitAt(ReadLimit.PacketCut, "c1"), packet.Limits);
        Assert.DoesNotContain(new ReadLimitAt(ReadLimit.PacketCut, "c3"), packet.Limits);
    }
}
