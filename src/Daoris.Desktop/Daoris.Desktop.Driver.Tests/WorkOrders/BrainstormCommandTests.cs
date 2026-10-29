using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a (D176 points 1, 2 and 8): <c>daoris-driver brainstorm</c> at the terminal. The verb writes a request the driver's look
/// takes; <c>show</c> prints the packet the person sends, whole, and the document once there is one; <c>send</c> is the person's press
/// and sends the packet they saw; <c>clear</c> removes the records and the packet. Files only: no service, no process.
/// </summary>
public sealed class BrainstormCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-verb-" + Guid.NewGuid().ToString("N")[..8]);

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
        var code = BrainstormCommand.Run(args, _home, output, At);
        return (code, output.ToString());
    }

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
    public void The_verb_writes_a_request_the_look_takes_and_says_so()
    {
        var (code, said) = Run("Why does the report drop its last day?", "--repository", "reports", "--agents", "stub,acp-stub");

        Assert.Equal(0, code);
        var request = Assert.Single(Files.Requests());
        Assert.Equal(("Why does the report drop its last day?", "reports"), (request.Question, request.Repository));
        Assert.Equal(["stub", "acp-stub"], request.Agents);
        Assert.Contains(request.Id, said, StringComparison.Ordinal);
        Assert.Contains("next look", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("--agents")]
    [InlineData("send")]
    [InlineData("show", "a", "b")]
    [InlineData("Why?", "--colour", "red")]
    public void A_mistyped_line_is_told_its_usage(params string[] args)
    {
        var (code, said) = Run(args);

        Assert.Equal(2, code);
        Assert.Contains("daoris-driver brainstorm", said, StringComparison.Ordinal);
        Assert.Empty(Files.Requests());
    }

    [Fact]
    public void A_question_past_its_bound_is_refused()
    {
        var (code, said) = Run(new string('q', 4_001));

        Assert.Equal(1, code);
        Assert.Contains("4000", said, StringComparison.Ordinal);
        Assert.Empty(Files.Requests());
    }

    [Fact]
    public void Show_prints_the_packet_whole_and_send_sends_the_one_the_person_saw()
    {
        Run("Why does the report drop its last day?", "--repository", "reports");
        var id = Files.Requests().Single().Id;

        // Before the look froze it, there is nothing to show and nothing to send.
        Assert.Contains("next look", Run("show", id).Out, StringComparison.Ordinal);
        Assert.Equal(1, Run("send", id).Code);
        Assert.Null(Files.Sent(id));

        var digest = Freeze(id);
        var (shown, printed) = Run("show", id);
        Assert.Equal(0, shown);
        Assert.Contains(digest, printed, StringComparison.Ordinal);
        Assert.Contains("S1  decision  docs/decisions/D7.md:3-9 at 0123456789ab", printed, StringComparison.Ordinal);
        Assert.Contains("The window is exclusive of its end.", printed, StringComparison.Ordinal);
        Assert.Contains($"brainstorm send {id}", printed, StringComparison.Ordinal);

        Assert.Equal(0, Run("send", id).Code);
        Assert.Equal(digest, Files.Sent(id));
        // Sent again, it is the same brainstorm: never a second spend.
        var (again, said) = Run("send", id);
        Assert.Equal(0, again);
        Assert.Contains("already", said, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_prints_the_document_once_there_is_one_and_list_names_each()
    {
        Run("Why does the report drop its last day?");
        var id = Files.Requests().Single().Id;
        var digest = Freeze(id);
        var packet = BrainstormPackets.Read(Files.PacketBytes(id)!)!;
        Files.WriteDocument(id, BrainstormDocuments.Unranked(id, packet, [], [], [], "every answer said it had no proposal"));
        Files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, BrainstormStates.Unranked, At));

        Assert.Contains($"# Brainstorm `{id}`", Run("show", id).Out, StringComparison.Ordinal);
        Assert.Contains($"{id}  ended", Run("list").Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_removes_the_records_and_the_packet_and_never_a_running_one()
    {
        Run("Why does the report drop its last day?");
        var id = Files.Requests().Single().Id;
        var digest = Freeze(id);
        Files.WriteState(new BrainstormState(id, BrainstormStates.Running, digest, null, At));

        Assert.Equal(1, Run("clear", id).Code);
        Assert.True(Directory.Exists(Files.Folder(id)));

        Files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, null, At));
        Assert.Equal(0, Run("clear", id).Code);
        Assert.False(Directory.Exists(Files.Folder(id)));
        Assert.Equal(1, Run("show", id).Code);
    }
}
