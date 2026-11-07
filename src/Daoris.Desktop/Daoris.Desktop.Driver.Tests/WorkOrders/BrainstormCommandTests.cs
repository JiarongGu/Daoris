using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris brainstorm</c> at the terminal. An ask, a send, a stop and a clear
/// are presses of the running service's desk, which writes the records and says the sentence the verb prints; <c>show</c> and
/// <c>list</c> read the records here. With no service the verb says so and writes nothing.
/// </summary>
public sealed class BrainstormCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-brainstorm-verb-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly BrainstormDoorStandIn _service = new();

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    public BrainstormCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private BrainstormFiles Files => new(_home);

    private (int Code, string Out) Run(params string[] args)
    {
        var output = new StringWriter();
        var code = BrainstormCommand.Run(args, _home, output, At, _service);
        return (code, output.ToString());
    }

    private string Asked() => Files.WriteRequest("Why does the report drop its last day?", "reports", [], At);

    /// <summary>The look's freeze, as the driver writes it: the packet and the state naming its digest.</summary>
    private string Freeze(string id)
    {
        var packet = BrainstormPackets.Freeze(
            Files.Request(id)!.Question, "reports", "words", "0123456789abcdef",
            [new BrainstormFound("decision", "docs/decisions/D7.md", 3, 9, "The window is exclusive of its end.", "daily report")],
            BrainstormBounds.First);
        Files.WritePacket(id, BrainstormPackets.Bytes(packet));
        Files.WriteState(new BrainstormState(id, BrainstormStates.Frozen, packet.Digest, null, At));
        return packet.Digest;
    }

    [Fact]
    public void An_ask_is_one_press_of_the_desk_with_the_words_as_typed_and_prints_the_desks_sentence()
    {
        _service.Answer = (_, _, _) => new ServiceReply(200, """{ "brainstorm": { "id": "ab12cd34" }, "message": "Brainstorm `ab12cd34` asked. Daoris freezes its packet at its next look." }""");

        var (code, said) = Run("Why does the report drop its last day?", "--repository", "reports", "--agents", "stub, acp-stub", "--id", "daily");

        Assert.Equal(0, code);
        var (verb, path, json) = Assert.Single(_service.Sent);
        Assert.Equal((ServiceVerb.Post, "/api/brainstorms"), (verb, path));
        var body = JsonDocument.Parse(json!).RootElement;
        Assert.Equal("Why does the report drop its last day?", body.GetProperty("question").GetString());
        Assert.Equal("reports", body.GetProperty("repository").GetString());
        Assert.Equal(["stub", "acp-stub"], body.GetProperty("agents").EnumerateArray().Select(agent => agent.GetString()));
        Assert.Equal("daily", body.GetProperty("id").GetString());
        Assert.False(body.TryGetProperty("kind", out _));
        Assert.Equal("Brainstorm `ab12cd34` asked. Daoris freezes its packet at its next look.", said.Trim());
        Assert.Empty(Files.Requests());
    }

    [Fact]
    public void Each_source_is_sent_as_typed_for_the_desk_to_judge()
    {
        Run("Why?", "--repository", "daoris", "--source", "docs/a.md", "--source", "src\\B.cs");

        var body = JsonDocument.Parse(Assert.Single(_service.Sent).Json!).RootElement;
        Assert.Equal(["docs/a.md", "src\\B.cs"], body.GetProperty("sources").EnumerateArray().Select(source => source.GetString()));
    }

    [Fact]
    public void A_refusal_of_the_desk_is_printed_as_its_sentence_and_is_exit_1()
    {
        _service.Answer = (_, _, _) => new ServiceReply(409, """{ "error": "Brainstorm `daily` was asked with other words; an id names one brainstorm. Nothing was asked." }""");

        var (code, said) = Run("Why is the report slow?", "--id", "daily");

        Assert.Equal(1, code);
        Assert.Equal("daoris: Brainstorm `daily` was asked with other words; an id names one brainstorm. Nothing was asked.", said.Trim());
    }

    /// <summary>🔴 One writer: with no service the verb says so, says what to start, and writes no file of its own.</summary>
    [Fact]
    public void With_no_service_the_verb_says_so_and_writes_nothing()
    {
        _service.Down = new DriverException("no DAORIS_SERVICE_URL — the driver watches a service.");

        foreach (var line in new[] { new[] { "Why?" }, ["send", "ab12cd34"], ["stop", "ab12cd34"], ["clear", "ab12cd34"] })
        {
            var (code, said) = Run(line);
            Assert.Equal(1, code);
            Assert.Contains("Daoris is not running", said, StringComparison.Ordinal);
            Assert.Contains("Start Daoris", said, StringComparison.Ordinal);
            Assert.Contains("nothing was written", said, StringComparison.Ordinal);
        }

        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Brainstorms)));
    }

    [Fact]
    public void A_service_that_stopped_answering_is_the_same_refusal()
    {
        _service.Down = new HttpRequestException("connection refused");

        var (code, said) = Run("Why?");

        Assert.Equal(1, code);
        Assert.Contains("connection refused", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("--agents")]
    [InlineData("show", "a", "b")]
    [InlineData("Why?", "--colour", "red")]
    [InlineData("Why?", "--kind", "party")]
    [InlineData("Why?", "--kind")]
    public void A_mistyped_line_is_told_its_usage_and_asks_the_service_nothing(params string[] args)
    {
        var (code, said) = Run(args);

        Assert.Equal(2, code);
        Assert.Contains("daoris brainstorm", said, StringComparison.Ordinal);
        Assert.Empty(_service.Sent);
    }

    [Fact]
    public void Send_stop_and_clear_are_presses_on_the_brainstorm_by_its_id()
    {
        Assert.Equal(0, Run("send", "ab12cd34").Code);
        Assert.Equal(0, Run("stop", "ab12cd34").Code);
        Assert.Equal(0, Run("clear", "ab12cd34").Code);

        Assert.Equal(
            [(ServiceVerb.Post, "/api/brainstorms/ab12cd34/send"), (ServiceVerb.Post, "/api/brainstorms/ab12cd34/stop"), (ServiceVerb.Delete, "/api/brainstorms/ab12cd34")],
            _service.Sent.Select(each => (each.Verb, each.Path)));
    }

    [Fact]
    public void The_kind_meeting_asks_and_presses_a_meeting_of_the_same_desk()
    {
        Run("Which window?", "--kind", "meeting", "--agents", "stub,acp-stub", "--id", "weekly");
        Run("send", "weekly", "--kind", "meeting", "--round", "2");
        Run("stop", "--kind", "meeting", "weekly");
        Run("clear", "weekly", "--kind", "meeting");

        Assert.Equal(
            [
                (ServiceVerb.Post, "/api/brainstorms"), (ServiceVerb.Post, "/api/brainstorms/weekly/send?kind=meeting&round=2"),
                (ServiceVerb.Post, "/api/brainstorms/weekly/stop?kind=meeting"), (ServiceVerb.Delete, "/api/brainstorms/weekly?kind=meeting"),
            ],
            _service.Sent.Select(each => (each.Verb, each.Path)));
        var body = JsonDocument.Parse(_service.Sent[0].Json!).RootElement;
        Assert.Equal("meeting", body.GetProperty("kind").GetString());
        Assert.Equal("Which window?", body.GetProperty("question").GetString());
    }

    [Fact]
    public void Show_prints_the_packet_whole_and_says_where_the_send_is()
    {
        var id = Asked();

        // Before the look froze it, there is nothing to show.
        Assert.Contains("next look", Run("show", id).Out, StringComparison.Ordinal);

        var digest = Freeze(id);
        var (shown, printed) = Run("show", id);
        Assert.Equal(0, shown);
        Assert.Contains(digest, printed, StringComparison.Ordinal);
        Assert.Contains("S1  decision  docs/decisions/D7.md:3-9 at 0123456789ab", printed, StringComparison.Ordinal);
        Assert.Contains("The window is exclusive of its end.", printed, StringComparison.Ordinal);
        Assert.Contains($"brainstorm send {id}", printed, StringComparison.Ordinal);
        Assert.Empty(_service.Sent);
    }

    [Fact]
    public void Show_prints_the_document_once_there_is_one_and_list_names_each()
    {
        var id = Asked();
        var digest = Freeze(id);
        var packet = BrainstormPackets.Read(Files.PacketBytes(id)!)!;
        Files.WriteDocument(id, BrainstormDocuments.Unranked(id, packet, [], [], [], "every answer said it had no proposal"));
        Files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, BrainstormStates.Unranked, At));

        Assert.Contains($"# Brainstorm `{id}`", Run("show", id).Out, StringComparison.Ordinal);
        Assert.Contains($"{id}  ended", Run("list").Out, StringComparison.Ordinal);
        Assert.Equal(1, Run("show", "ffffffff").Code);
    }
}
