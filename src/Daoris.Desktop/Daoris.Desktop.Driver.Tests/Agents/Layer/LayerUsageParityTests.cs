using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTLYNTAI1c (the agent-lyntai design §6.3, §9.2): the usage port's two implementations, today's reader and the layer's read, over
/// every recorded answer and every fixture of both readers' tests, the usage history's among them, observed whole by the oracle: the spawn,
/// the lines written, the walk's windows, the history's rows with their plan, and whether anything was read; the source and the reader by
/// the relation. Each case states the channels it differs on, and only a named gap differs.
/// </summary>
public sealed class LayerUsageParityTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-usageparity-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly ParityRelation Relation = new(
    [
        new(ClaudeUsage.Source, "layer:" + ClaudeUsage.Source),
        new(CodexUsage.Method, "layer:" + CodexUsage.Method),
        new("1", "1"),
    ]);

    /// <summary>Claude's server is started with <c>-p</c> on the layer, measured to change no answer (the design's §9.2, 1c's third difference).</summary>
    private static readonly string[] ClaudeSpawn = ["Spawn"];

    /// <summary>
    /// A value the layer's reader cannot recognise reads the whole answer unknown (Lyntai's D237, D222), where today's keeps every other
    /// window it said: nothing reaches the walk or the history, and no provenance is kept.
    /// </summary>
    private static readonly string[] Unrecognised = ["Windows", "Records", "End", "provenance", "order"];

    /// <summary>The same gap where today's walk read nothing either: only the history's rows today keeps are lost.</summary>
    private static readonly string[] UnrecognisedRows = ["Records", "provenance", "order"];

    private static readonly HashSet<string> ClaudeRowsLost =
        ["a reset in words", "a use above a hundred", "both windows null", "a negative use", "a use in words"];

    private static readonly HashSet<string> CodexRowsLost = ["a use in words", "a reset in words", "a negative use", "a reset no calendar holds"];

    private sealed record Case(bool Claude, string Body, DateTimeOffset Now, UsageServerMode Mode, string[] Gap);

    private static readonly Dictionary<string, Case> Corpus = Build();

    private static Dictionary<string, Case> Build()
    {
        var corpus = new Dictionary<string, Case>(StringComparer.Ordinal);
        void Claude(string name, string body, string[]? gap = null, UsageServerMode mode = UsageServerMode.Answers) =>
            corpus.Add("claude: " + name, new(true, body, ClaudeUsageTests.Now, mode, [.. ClaudeSpawn, .. gap ?? []]));
        void Codex(string name, string body, string[]? gap = null, UsageServerMode mode = UsageServerMode.Answers) =>
            corpus.Add("codex: " + name, new(false, body, CodexUsageTests.Now, mode, gap ?? []));

        string W(double percent, string? resets) => ClaudeUsageTests.Window(percent, resets);
        string At(double hours) => ClaudeUsageTests.At(hours);
        Claude("the recorded answer", ClaudeUsageTests.Recorded(3.5, 100.25));
        Claude("two windows", ClaudeUsageTests.Body(W(23, At(3.5)), W(79, At(100.25))));
        Claude("windows not yet opened", ClaudeUsageTests.Body(W(0, null), W(0, null)));
        Claude("a used window with no reset", ClaudeUsageTests.Body(W(10, null), W(50, At(9))));
        Claude("a reset made up", ClaudeUsageTests.Body(W(0, null), W(4, At(9))));
        Claude("scoped weeks and the server's rows", ClaudeUsageTests.ScopedWeeks, Unrecognised);
        Claude("a row scoped by a model named with its id", ClaudeUsageTests.ObjectScope("\"claude-model-example\""));
        Claude("a row scoped by a model with no id, as the installed server answers", ClaudeUsageTests.ObjectScope("null"), Unrecognised);
        Claude("not signed in", """{"subscription_type":null,"rate_limits_available":false,"rate_limits":{"five_hour":null,"seven_day":null}}""");
        foreach (var row in ClaudeUsageTests.Unread)
        {
            Claude("unread, " + row[0], (string)row[1], ClaudeRowsLost.Contains((string)row[0]) ? UnrecognisedRows : null);
        }

        Claude("initialize withheld", ClaudeUsageTests.Recorded(3.5, 100.25), mode: UsageServerMode.WithholdsInitialize);
        Claude("initialize refused", ClaudeUsageTests.Recorded(3.5, 100.25), mode: UsageServerMode.RefusesInitialize);

        Codex("the recorded answer", CodexUsageTests.Recorded(3.5, 100.25));
        foreach (var minutes in new[] { 300, 10080, 1440, 60, 301 })
        {
            Codex($"a window of {minutes} minutes", CodexUsageTests.Answer(CodexUsageTests.Window(42, minutes, CodexUsageTests.Seconds(2))));
        }

        Codex("another limit's windows", CodexUsageTests.OtherLimit, Unrecognised);
        Codex("one window unread", CodexUsageTests.Answer(CodexUsageTests.Window(1, 300, null), CodexUsageTests.Window(15, 10080, CodexUsageTests.Seconds(9))));
        foreach (var row in CodexUsageTests.Unread)
        {
            Codex("unread, " + row[0], (string)row[1], CodexRowsLost.Contains((string)row[0]) ? UnrecognisedRows : null);
        }

        Codex("initialize withheld", CodexUsageTests.Recorded(3.5, 100.25), mode: UsageServerMode.WithholdsInitialize);
        Codex("initialize refused", CodexUsageTests.Recorded(3.5, 100.25), mode: UsageServerMode.RefusesInitialize);
        return corpus;
    }

    public static TheoryData<string> Cases => [.. Corpus.Keys];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Both_reads_answer_alike_by_the_oracle_but_for_a_named_gap(string name)
    {
        var each = Corpus[name];
        var daoris = await TodayAsync(each);
        var layer = await LayerAsync(each);

        var found = ParityOracle.Differences(daoris, layer, Relation, Expected(daoris));

        var channels = found.Select(ChannelOf).ToHashSet(StringComparer.Ordinal);
        Assert.True(
            channels.SetEquals(each.Gap),
            $"{name}: differs on [{string.Join(", ", channels.Order())}], its gap names [{string.Join(", ", each.Gap)}]:\n  "
            + string.Join("\n  ", found));
    }

    /// <summary>
    /// The planted regression (§6.3 point 5): a layer reading that loses a window's reset, and one that drops a history row, are each
    /// found on a case the two read alike.
    /// </summary>
    [Fact]
    public async Task A_reset_lost_or_a_row_dropped_on_the_layer_path_is_found()
    {
        var each = Corpus["codex: the recorded answer"];
        var daoris = await TodayAsync(each);
        var layer = await LayerAsync(each);
        Assert.Empty(ParityOracle.Differences(daoris, layer, Relation, Expected(daoris)));

        var resetLost = layer.With(seen => seen.Select(entry => entry.Channel == ParityChannel.Windows
            ? entry with { Fields = [.. entry.Fields.Select(field => field.Name == "reset" ? (field.Name, (string?)null) : field)] }
            : entry));
        var rowDropped = layer.With(seen =>
        {
            var kept = seen.ToList();
            kept.RemoveAt(kept.FindIndex(entry => entry.Channel == ParityChannel.Records));
            return kept;
        });

        Assert.Contains(ParityOracle.Differences(daoris, resetLost, Relation, Expected(daoris)), found => found.StartsWith("Windows", StringComparison.Ordinal));
        Assert.Contains(ParityOracle.Differences(daoris, rowDropped, Relation, Expected(daoris)), found => found.StartsWith("Records", StringComparison.Ordinal));
    }

    private static string ChannelOf(string difference) =>
        difference.StartsWith("provenance", StringComparison.Ordinal) ? "provenance"
        : difference.StartsWith("the writes' order", StringComparison.Ordinal) ? "order"
        : difference[..difference.IndexOf(' ', StringComparison.Ordinal)];

    private static IReadOnlyList<ParityPair> Expected(ParityObservation daoris) =>
        daoris.Seen.FirstOrDefault(entry => entry.What == "reading") is { } reading
            ? [new(reading[ParityProjection.Source]!, "layer:" + reading[ParityProjection.Source]), new("1", "1")]
            : [];

    private string Home(Case each) => Path.Combine(_home, each.Claude ? "claude" : "codex", "account-1");

    private (IReadOnlyList<string> Run, TestAgent Toolchain, UsageQuestion Question) Spawn(Case each)
    {
        var question = each.Claude ? ClaudeUsage.Question : CodexUsage.Question;
        var program = Path.Combine(_home, "stand-ins", each.Claude ? "claude.exe" : "codex.exe");
        var toolchain = new TestAgent(
            Binary: [program], VersionArguments: ["--version"], ProfileVariable: each.Claude ? "CLAUDE_CONFIG_DIR" : "CODEX_HOME", ProbeByPresence: true);
        return ([program, .. question.Command.Skip(1)], toolchain, question);
    }

    /// <summary>Today's read as its question composes it: the spawn its process would start, and its exchange over the stand-in.</summary>
    private async Task<ParityObservation> TodayAsync(Case each)
    {
        var (run, toolchain, _) = Spawn(each);
        var profile = Home(each);
        var info = CodexUsage.Prepare(run, toolchain, profile, managed: null, workingDirectory: each.Claude ? Path.Combine(profile, "usage-read") : null);
        var server = new UsageServerStandIn(each.Claude, each.Body, each.Mode);
        var streams = new UsageServerStreams(server);
        var patience = TimeSpan.FromSeconds(5);
        var answer = each.Claude
            ? await ClaudeUsage.AskWithinAsync(streams.Incoming, streams.Outgoing, patience, each.Now, CancellationToken.None)
            : await CodexUsage.AskWithinAsync(streams.Incoming, streams.Outgoing, patience, CancellationToken.None);
        server.Close();
        var cwd = info.WorkingDirectory is { Length: > 0 } named ? named : Directory.GetCurrentDirectory();
        return Observe("daoris", info.FileName, [.. info.ArgumentList], cwd, info.Environment[toolchain.ProfileVariable!], server.Heard, answer);
    }

    /// <summary>The layer's read, its runner the stand-in's: the spawn as the runner was asked it.</summary>
    private async Task<ParityObservation> LayerAsync(Case each)
    {
        var (run, toolchain, question) = Spawn(each);
        var home = Home(each);
        var server = new UsageServerStandIn(each.Claude, each.Body, each.Mode);
        var runner = new UsageServerRunner(server);
        LauncherSeat? seat = null;
        var read = new LayerUsageRead(question, each.Claude ? ClaudeUsageLayer.Vendor : CodexUsageLayer.Vendor, (given, _) =>
        {
            seat = given;
            return runner;
        });

        var answer = await read.AskAsync(new UsageAsk(run, toolchain, home, null, each.Now) { Profile = "account-1" }, CancellationToken.None)
            .WaitAsync(Poll.Patience);
        var spawned = Assert.Single(runner.Spawns);
        var variable = toolchain.ProfileVariable!;
        var accountHome = spawned.Environment?.GetValueOrDefault(variable) ?? seat!.Environment.GetValueOrDefault(variable);
        return Observe("layer", spawned.Command, spawned.Args, spawned.WorkingDirectory ?? "", accountHome, server.Heard, answer);
    }

    private static ParityObservation Observe(
        string path, string program, IReadOnlyList<string> args, string cwd, string? home, IReadOnlyList<string> heard,
        IReadOnlyList<WindowReading>? answer)
    {
        var seen = new ParityObservation(path, Start);
        seen.Add(ParityChannel.Spawn, "spawn", ("program", program), ("args", string.Join(" ", args)), ("cwd", cwd), ("home", home));
        foreach (var line in heard) seen.Add(ParityChannel.Stdin, "line", ("text", line));

        // What the roster keeps of an answer: its walk's windows where it read any, its history's rows where it said any.
        if (answer is { Count: > 0 })
        {
            foreach (var reading in answer)
            {
                seen.Add(ParityChannel.Windows, "window", ("name", reading.Window), ("used", reading.Used?.ToString("R")), ("reset", reading.Reset.ToString("O")));
            }
        }

        if (answer is UsageAnswer kept && kept.Seen.Count > 0)
        {
            foreach (var row in kept.Seen) seen.Add(ParityChannel.Records, "seen", ("row", row.ToString()));
            seen.Add(ParityChannel.Records, "reading", ("plan", kept.Plan), (ParityProjection.Source, kept.Source), (ParityProjection.Reader, kept.Reader.ToString()));
        }

        seen.Add(ParityChannel.End, "read", ("known", (answer is { Count: > 0 }).ToString()));
        return seen;
    }
}
