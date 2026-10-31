using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a rule 20: <c>daoris-driver sessions at-work</c>. With no Daoris window running the verb composes the same answer by
/// the same function, issuing no token, so an open child reads <i>not known whether it still runs</i> and never <i>working</i>.
/// </summary>
public sealed class AtWorkVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-atwork-verb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly StandInLedger _ledger = new();

    public AtWorkVerbTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionsAsk Read(params string[] words)
    {
        var ask = SessionsCommand.Read(words, out var problem);
        Assert.Null(problem);
        return ask!;
    }

    private SessionsWorld World() => new(_ledger.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null);

    private void OpenChild()
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.Driven("s1", "q1", tree);
        ChildLineage.Start(_home, "s1", "a000000000000001", "4242 638000000000000000", DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", new JsonObject { ["by"] = "stream", ["event"] = "started", ["agent"] = "a1", ["proc"] = "a000000000000001" }.ToJsonString());
    }

    [Fact]
    public void The_verb_reads_one_session_and_json_or_is_a_usage_problem()
    {
        var plain = Read("at-work", "s1a2b3c4");
        Assert.Equal(("at-work", "s1a2b3c4", false), (plain.Verb, plain.Ids[0], plain.Json));
        Assert.True(Read("at-work", "s1", "--json").Json);

        foreach (var words in new[] { new[] { "at-work" }, ["at-work", "s1", "--x"], ["at-work", "s1", "s2"] })
        {
            Assert.Null(SessionsCommand.Read(words, out var problem));
            Assert.Contains("at-work", problem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task With_no_window_an_open_child_is_not_known_whether_it_still_runs_and_never_working()
    {
        OpenChild();
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1"), World(), output));

        var text = output.ToString();
        Assert.Contains("a1 · not known whether it still runs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("working with", text, StringComparison.Ordinal);
        Assert.DoesNotContain("a1 · working", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_prints_the_answer_whole_with_the_child_s_state_uncertain()
    {
        OpenChild();
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1", "--json"), World(), output));

        var answer = JsonDocument.Parse(output.ToString()).RootElement.GetProperty("answer");
        Assert.Equal("s1", answer.GetProperty("attended").GetString());
        Assert.Equal("uncertain", answer.GetProperty("subagents").GetProperty("children")[0].GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_session_the_records_do_not_hold_is_said_so_and_exits_two()
    {
        var output = new StringWriter();
        var error = Console.Error;
        var said = new StringWriter();
        Console.SetError(said);
        try
        {
            Assert.Equal(2, await SessionsCommand.RunAsync(Read("at-work", "s9"), World(), output));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Equal("", output.ToString());
        Assert.Contains("no session s9 is held here", said.ToString(), StringComparison.Ordinal);
    }
}
