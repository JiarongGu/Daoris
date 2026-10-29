using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (D163 point 3): the person's yes and no at a terminal to an agent's ask to hand its session over, waiting under the
/// `handOver` rule's `ask`: <c>daoris-driver sessions hand-over &lt;id&gt; --approve|--decline</c>, the page's judge
/// (<see cref="HandOverAskWord"/>). A yes is the state the look takes; a no is the look's own refusal `declined` with its note, once.
/// </summary>
public sealed class SessionsHandOverWordTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-20T10:00:00Z");

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-sessions-handword-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionsHandOverWordTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionsAsk Read(params string[] words)
    {
        var ask = SessionsCommand.Read(words, out var problem);
        Assert.Null(problem);
        return ask!;
    }

    private SessionsWorld World() =>
        new(new OpinionStandIn().Session("s1", "working", tree: "trees/default/reports/s-1").Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null);

    /// <summary>The agent's ask on file, as <c>handover_propose</c> writes it, and the state the look left it in.</summary>
    private HandOverAskFiles Held(string? state, string? code = null)
    {
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + HandOverAskFiles.AskSuffix),
            $$"""{"id":"a1b2c3d4","session":"s1","note":"Left: ParserTests.","askedBy":"agent","at":"{{At:O}}"}""");
        var files = new HandOverAskFiles(_home);
        if (state is not null) files.WriteState("s1", new HandOverAskState("a1b2c3d4", state, code, "codex-acp", At));
        return files;
    }

    [Theory]
    [InlineData("--approve", "hand-over-approve")]
    [InlineData("--decline", "hand-over-decline")]
    public void The_verb_reads_a_yes_or_a_no_to_a_sessions_ask(string flag, string verb)
    {
        var ask = Read("hand-over", "s1a2b3c4", flag);
        Assert.Equal((verb, "s1a2b3c4"), (ask.Verb, ask.Ids[0]));
        Assert.Contains("sessions hand-over <id> --approve|--decline", SessionsCommand.Usage);
    }

    [Fact]
    public void A_yes_and_a_no_together_or_with_an_agent_is_a_usage_problem()
    {
        Assert.Null(SessionsCommand.Read(["hand-over", "s1", "--approve", "--decline"], out var problem));
        Assert.Contains("`--approve` or `--decline`", problem);
        Assert.Null(SessionsCommand.Read(["hand-over", "s1", "--approve", "--agent", "dsh"], out _));
    }

    /// <summary>A yes to an ask waiting on the person is the `approved` state, naming the agent the look named; said again, the same yes.</summary>
    [Fact]
    public async Task A_yes_is_written_for_the_drivers_look_with_the_agent_it_goes_to()
    {
        var files = Held(HandOverAskStates.Waiting);
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("hand-over", "s1", "--approve"), World(), output));

        Assert.Equal((HandOverAskStates.Approved, "codex-acp"), (files.ReadState("s1")!.State, files.ReadState("s1")!.Agent));
        Assert.Contains("codex-acp", output.ToString());
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("hand-over", "s1", "--approve"), World(), output));
    }

    /// <summary>A no is the look's own refusal `declined`, with the coded note on the session's record, once.</summary>
    [Fact]
    public async Task A_no_refuses_the_ask_declined_with_its_note_once()
    {
        var files = Held(HandOverAskStates.Waiting);
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("hand-over", "s1", "--decline"), World(), output));
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("hand-over", "s1", "--decline"), World(), output));

        Assert.Equal((HandOverAskStates.Refused, HandOverAskWhy.Declined), (files.ReadState("s1")!.State, files.ReadState("s1")!.Code));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var note = Assert.Single(events.After("s1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["handover.refused"], NoteAssert.Codes(note.Parts!));
    }

    /// <summary>Each answer the judge gives as data is a refusal at the terminal, with its sentence, and the state is left as it was.</summary>
    [Theory]
    [InlineData("--approve", "none", null, "holds no ask")]
    [InlineData("--approve", HandOverAskStates.Taken, null, "past your word")]
    [InlineData("--decline", null, null, "not read by the driver yet")]
    [InlineData("--decline", HandOverAskStates.Approved, null, "has your yes")]
    [InlineData("--decline", HandOverAskStates.Refused, HandOverAskWhy.Off, "past your word")]
    public async Task A_word_the_judge_refuses_is_said_and_changes_nothing(string flag, string? state, string? code, string says)
    {
        var files = state == "none" ? new HandOverAskFiles(_home) : Held(state, code);
        var before = files.ReadState("s1");
        var output = new StringWriter();

        Assert.Equal(1, await SessionsCommand.RunAsync(Read("hand-over", "s1", flag), World(), output));

        Assert.Contains(says, output.ToString());
        Assert.Equal(before, files.ReadState("s1"));
    }
}
