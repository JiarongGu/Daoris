using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1d: <c>daoris ask at-work</c> (the cowork view design §3.5). With no Daoris window running the verb composes the answer by
/// the function the window's read composes by, from the listing's look and the asks the service holds; with one, it is the window's
/// <c>SESSION_AT_WORK</c> keyed <c>{ ask }</c>. It prints the lines of work and the quests, and one line naming what it leaves out.
/// </summary>
public sealed class AskAtWorkVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-ask-atwork-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly StandInLedger _ledger = new();

    public AskAtWorkVerbTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static AskAtWorkAsk Read(params string[] words)
    {
        var ask = AskAtWorkVerb.Read(words, out var problem);
        Assert.Null(problem);
        return ask!;
    }

    private SessionsWorld World() => new(_ledger.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null);

    /// <summary>An ask with one quest to <c>engine</c> served by a session that has started one subagent.</summary>
    private void AskWithOneEntry(string ask = "a1")
    {
        _ledger.Ask(ask);
        _ledger.Publish("q1", "engine", from: $"ask #{ask}");
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.Driven("s1", "q1", tree);
        _ledger.InEntry("s1", $"ask:{ask}/engine");
        ChildLineage.Start(_home, "s1", "a000000000000001", "4242 638000000000000000", DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", new JsonObject { ["by"] = "stream", ["event"] = "started", ["agent"] = "a1", ["proc"] = "a000000000000001" }.ToJsonString());
    }

    [Fact]
    public void The_verb_reads_one_ask_and_json_or_is_a_usage_problem()
    {
        Assert.Equal(("a1", false), (Read("a1").Ask, Read("a1").Json));
        Assert.Equal(("a1", true), (Read("#a1", "--json").Ask, Read("#a1", "--json").Json));
        Assert.True(AskAtWorkVerb.Asks(["at-work", "a1"]));
        Assert.False(AskAtWorkVerb.Asks(["channel", "a1"]));
        foreach (var words in new[] { Array.Empty<string>(), ["--json"], ["a1", "--x"], ["a1", "a2"] })
        {
            Assert.Null(AskAtWorkVerb.Read(words, out var problem));
            Assert.Contains("at-work", problem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task With_no_window_it_prints_the_lines_of_work_the_quests_and_what_it_leaves_out()
    {
        AskWithOneEntry();
        var output = new StringWriter();

        Assert.Equal(0, await AskAtWorkVerb.RunAsync(World(), Read("a1"), output, CancellationToken.None));

        var lines = output.ToString().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("ask #a1: 1 line of work", lines[0]);
        Assert.Contains("engine · working · 1 part, 1 working · 1 subagent, 0 working · opens s1", lines[1], StringComparison.Ordinal);
        Assert.Contains("#q1", string.Join('\n', lines), StringComparison.Ordinal);
        Assert.Contains("taken by s1", string.Join('\n', lines), StringComparison.Ordinal);
        Assert.Contains("the driver's last look's", lines[^1], StringComparison.Ordinal);
        Assert.Contains("`daoris ask channel a1`", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_prints_the_answer_whole_as_the_ask_branch_with_no_attended_part()
    {
        AskWithOneEntry();
        var output = new StringWriter();

        Assert.Equal(0, await AskAtWorkVerb.RunAsync(World(), Read("a1", "--json"), output, CancellationToken.None));

        var root = JsonDocument.Parse(output.ToString()).RootElement;
        Assert.False(root.TryGetProperty("answer", out _));
        var answer = root.GetProperty("askAnswer");
        Assert.Equal("a1", answer.GetProperty("of").GetProperty("ask").GetString());
        var entry = answer.GetProperty("entries")[0];
        Assert.Equal(("engine", "s1", 1), (entry.GetProperty("repository").GetString(), entry.GetProperty("opens").GetString(), entry.GetProperty("parts").GetInt32()));
        Assert.Equal("q1", answer.GetProperty("quests")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task An_ask_the_service_holds_with_nothing_at_work_is_valid_and_empty()
    {
        _ledger.Ask("a2");
        var output = new StringWriter();

        Assert.Equal(0, await AskAtWorkVerb.RunAsync(World(), Read("a2"), output, CancellationToken.None));

        Assert.StartsWith("ask #a2: 0 lines of work", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_ask_is_refused_by_name_and_exits_one()
    {
        _ledger.Ask("a2");
        var error = Console.Error;
        var said = new StringWriter();
        Console.SetError(said);
        try
        {
            var output = new StringWriter();
            Assert.Equal(1, await AskAtWorkVerb.RunAsync(World(), Read("zz"), output, CancellationToken.None));
            Assert.Equal("", output.ToString());
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Contains("no ask #zz is held here", said.ToString(), StringComparison.Ordinal);
    }

    private static int Asked(InstanceStandIn standIn) =>
        standIn.Presses.Count(press => press.GetProperty("type").GetString() == "SESSION_AT_WORK");

    [Fact]
    public async Task A_window_answers_the_ask_keyed_read_and_its_answer_is_printed()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new
        {
            askAnswer = new
            {
                of = new { ask = "a1" },
                entries = new[] { new { entry = "e1", repository = "engine", opens = "s1", shown = "working", parts = 2, working = 1, subagents = 3, subagentsWorking = 2 } },
            },
        };
        var output = new StringWriter();

        Assert.Equal(0, await AskAtWorkVerb.RunAsync(World() with { Instance = home.World("ask") }, Read("a1"), output, CancellationToken.None));

        Assert.Equal("a1", Assert.Single(standIn.Presses).GetProperty("payload").GetProperty("ask").GetString());
        Assert.Contains("engine · working · 2 parts, 1 working · 3 subagents, 2 working · opens s1", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_window_that_does_not_hold_the_ask_refuses_it_at_once_without_asking_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { pending = true, waits = "ask" };
        var error = Console.Error;
        var said = new StringWriter();
        Console.SetError(said);
        try
        {
            Assert.Equal(1, await AskAtWorkVerb.RunAsync(World() with { Instance = home.World("ask") }, Read("a1"), new StringWriter(), CancellationToken.None));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Equal(1, Asked(standIn));
        Assert.Contains("no ask #a1 is held here", home.Error.ToString() + said, StringComparison.Ordinal);
    }
}
