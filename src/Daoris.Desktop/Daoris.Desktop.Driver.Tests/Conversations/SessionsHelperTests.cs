using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT2a-ii (D163 point 2): the person's own door to a helper, <c>daoris-driver sessions helper &lt;session&gt; "&lt;question&gt;"
/// [--agent &lt;name&gt;]</c>. It writes the ask a connector's <c>helper_ask</c> writes, as the person (<c>askedBy: person</c>), for the
/// driver's look to take; it judges only what the file's reader cannot, since the person is the one asking: the session is one of
/// this machine's that may ask, and the question is a question.
/// </summary>
public sealed class SessionsHelperTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-sessions-helper-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionsHelperTests() => Directory.CreateDirectory(_home);

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

    [Fact]
    public void The_verb_reads_a_session_a_question_and_an_optional_agent()
    {
        var plain = Read("helper", "s1a2b3c4", "Why does it drop the last day?");
        Assert.Equal(("helper", "s1a2b3c4", "Why does it drop the last day?", null), (plain.Verb, plain.Ids[0], plain.Text, plain.Agent));

        var named = Read("helper", "s1a2b3c4", "Why?", "--agent", "codex-acp");
        Assert.Equal(("codex-acp", "Why?"), (named.Agent, named.Text));
    }

    [Theory]
    [InlineData("helper")]
    [InlineData("helper", "s1")]
    [InlineData("helper", "s1", "  ")]
    [InlineData("helper", "s1", "Why?", "--agent")]
    [InlineData("helper", "s1", "Why?", "--agent", "")]
    [InlineData("helper", "s1", "Why?", "--agent", "codex-acp", "more")]
    public void Anything_else_is_a_usage_problem(params string[] words)
    {
        Assert.Null(SessionsCommand.Read(words, out var problem));
        Assert.Contains("`helper` takes one session's id, the question", problem);
    }

    [Fact]
    public void The_usage_spells_the_verb_in_both_places_a_person_reads_it()
    {
        Assert.Contains("sessions helper <id> \"<question>\" [--agent <name>]", SessionsCommand.Usage);
        Assert.Contains("sessions helper <id> \"<question>\" [--agent <name>]", DriverCommand.Usage);
    }

    private (SessionsWorld World, OpinionStandIn Host) World(Action<OpinionStandIn>? set = null)
    {
        var host = new OpinionStandIn().Session("s1", "completed", tree: "trees/default/reports/s-1");
        set?.Invoke(host);
        return (new SessionsWorld(host.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null), host);
    }

    /// <summary>The ask is written as the connector writes it, as the person's, with the question and agent as typed; the driver takes it at its next look.</summary>
    [Fact]
    public async Task The_ask_is_written_as_the_person_asking_for_the_driver_to_take()
    {
        var (world, _) = World();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(Read("helper", "s1", "Why does it drop the last day?", "--agent", "codex-acp"), world, output);

        Assert.Equal(0, exit);
        var ask = Assert.Single(new HelperFiles(_home).Asks());
        Assert.Equal(("s1", "Why does it drop the last day?", "codex-acp", HelperAsk.ByPerson), (ask.Asker, ask.Question, ask.Agent, ask.AskedBy));
        Assert.Contains($"#{ask.Id}", output.ToString());
        Assert.Contains("next look", output.ToString());
    }

    [Theory]
    [InlineData("gone", "no session `s1`")]
    [InlineData("helper", "is itself a helper")]
    [InlineData("reviewer", "reads another session's work")]
    [InlineData("intake", "is the intake")]
    public async Task A_session_that_cannot_ask_is_refused_and_nothing_is_written(string row, string says)
    {
        var (world, _) = World(host =>
        {
            switch (row)
            {
                case "helper": host.Set("s1", "serves", "s0"); break;
                case "reviewer": host.Set("s1", "opinion", "o1"); break;
                case "intake": host.Set("s1", "ask", "a9"); break;
            }
        });
        var output = new StringWriter();
        var ask = row == "gone" ? Read("helper", "s1", "Why?") : Read("helper", "s1", "Why?");
        if (row == "gone") world = world with { Service = new OpinionStandIn().Client() };

        var exit = await SessionsCommand.RunAsync(ask, world, output);

        Assert.Equal(1, exit);
        Assert.Contains(says, output.ToString());
        Assert.Empty(new HelperFiles(_home).Asks());
    }

    // ——— HELPERTERM1 (D163 point 3, D50): the yes and the no at a terminal, the page's judge (`HelperPersonWord`).

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-16T10:00:00Z");

    /// <summary>An agent's ask on file, as <c>helper_ask</c> writes it, and the state the look left it in.</summary>
    private HelperFiles Held(string id, string? state, string? code = null, string by = HelperAsk.ByAgent)
    {
        var files = new HelperFiles(_home);
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(files.PathOf(id, "ask"),
            $$"""{"id":"{{id}}","asker":"s1","question":"Why?","agent":null,"askedBy":"{{by}}","at":"{{At:O}}"}""");
        if (state is not null) files.WriteState(new HelperState(id, state, code, null, At));
        return files;
    }

    [Theory]
    [InlineData("--approve", "helper-approve")]
    [InlineData("--decline", "helper-decline")]
    public void The_verb_reads_a_yes_or_a_no_to_one_ask(string flag, string verb)
    {
        var ask = Read("helper", flag, "a1b2c3d4");
        Assert.Equal((verb, "a1b2c3d4"), (ask.Verb, ask.Ids[0]));
    }

    [Theory]
    [InlineData("helper", "--approve")]
    [InlineData("helper", "--decline")]
    [InlineData("helper", "--approve", "a1", "more")]
    [InlineData("helper", "--approve", "--decline")]
    public void A_yes_or_a_no_without_one_ask_is_a_usage_problem(params string[] words)
    {
        Assert.Null(SessionsCommand.Read(words, out var problem));
        Assert.Contains("`helper --approve` and `helper --decline` take one ask's id", problem);
    }

    [Fact]
    public void The_usage_spells_the_yes_and_the_no_in_both_places()
    {
        Assert.Contains("sessions helper --approve|--decline <ask>", SessionsCommand.Usage);
        Assert.Contains("sessions helper --approve|--decline <ask>", DriverCommand.Usage);
    }

    /// <summary>A yes to an ask waiting on the person's word is written as the page's is, and said again it is the same yes.</summary>
    [Fact]
    public async Task A_yes_to_an_ask_waiting_on_the_person_is_written_for_the_driver_s_look()
    {
        var files = Held("a1", HelperStates.Waiting);
        var (world, _) = World();
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("helper", "--approve", "a1"), world, output));
        Assert.Equal(HelperStates.Approved, files.ReadState("a1")!.State);
        Assert.Contains("has your yes", output.ToString());
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("helper", "--approve", "a1"), world, output));
    }

    /// <summary>A no is the look's own refusal `declined`, with the coded note on the asker's record, once.</summary>
    [Fact]
    public async Task A_no_to_an_ask_waiting_on_the_person_refuses_it_declined_with_its_note_once()
    {
        var files = Held("a1", HelperStates.Waiting);
        var (world, _) = World();
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("helper", "--decline", "a1"), world, output));
        Assert.Equal((HelperStates.Refused, HelperWhy.Declined), (files.ReadState("a1")!.State, files.ReadState("a1")!.Code));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var note = Assert.Single(events.After("s1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["helper.refused"], NoteAssert.Codes(note.Parts!));

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("helper", "--decline", "a1"), world, output));
        Assert.Single(events.After("s1", 0).Events, e => e.Kind == SessionEventKind.Note);
    }

    /// <summary>Each answer the judge gives as data is a refusal at the terminal, with its sentence, and the state is left as it was.</summary>
    [Theory]
    [InlineData("--approve", null, null, HelperAsk.ByAgent, "No ask #a1")]
    [InlineData("--approve", HelperStates.Waiting, null, HelperAsk.ByPerson, "your own ask")]
    [InlineData("--approve", HelperStates.Waiting, "cap", HelperAsk.ByAgent, "waits on cap")]
    [InlineData("--approve", HelperStates.Started, null, HelperAsk.ByAgent, "past its start")]
    [InlineData("--decline", HelperStates.Approved, null, HelperAsk.ByAgent, "has your yes")]
    [InlineData("--decline", null, null, HelperAsk.ByAgent, "No ask #a1")]
    [InlineData("--decline", HelperStates.Waiting, "cap", HelperAsk.ByAgent, "waits on cap")]
    public async Task What_the_judge_refuses_is_a_refusal_at_the_terminal(string flag, string? state, string? code, string by, string says)
    {
        var files = state is null && says.StartsWith("No ask") ? new HelperFiles(_home) : Held("a1", state, code, by);
        var before = files.ReadState("a1");
        var (world, _) = World();
        var output = new StringWriter();

        Assert.Equal(1, await SessionsCommand.RunAsync(Read("helper", flag, "a1"), world, output));
        Assert.Contains(says, output.ToString());
        Assert.Equal(before, files.ReadState("a1"));
    }

    [Fact]
    public async Task A_question_over_the_bound_or_an_agent_that_is_no_one_word_is_refused()
    {
        var (world, _) = World();
        var output = new StringWriter();

        Assert.Equal(1, await SessionsCommand.RunAsync(Read("helper", "s1", new string('x', 4001)), world, output));
        Assert.Contains("at most 4000 characters", output.ToString());
        Assert.Empty(new HelperFiles(_home).Asks());
    }
}
