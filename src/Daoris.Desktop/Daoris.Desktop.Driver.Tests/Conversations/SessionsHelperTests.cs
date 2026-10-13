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
