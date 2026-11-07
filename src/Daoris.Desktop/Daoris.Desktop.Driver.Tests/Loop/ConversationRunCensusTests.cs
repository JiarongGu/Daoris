using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The conversation's runner holds the run over the transports for its start, its end, its
/// stop and its close, and keeps its words beside it on the session and the process the run holds. A source scan, line comments read
/// out: the runner's files bracket, spawn, capture and keep no downloads themselves, and the one protocol session in the driver is the
/// run's, which the runner reaches through <c>TransportRun.Session</c>.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ConversationRunCensusTests
{
    /// <summary>What a start does that the run does for every executor: each a second way where the conversation's runner writes it.</summary>
    private static readonly (string Pattern, string Is)[] RunsOwn =
    [
        (@"\bLineageBrackets\.Bracketed\s*\(", "a lineage bracket"),
        (@"\bLineageBrackets\.Spawned\s*\(", "a spawn under a token"),
        (@"\bAgentStarts\.StartAsync\s*\(", "an agent's start"),
        (@"\bKeepHandedAsync\s*\(", "a start's downloads kept"),
        (@"\bHarnessCapture\.Capture\w*Async\s*\(", "a capture of the harness's streams"),
        (@"\bnew\s+AcpSession\s*\(", "a protocol session"),
        (@"\bProcesses\.Track\s*\(|_processes\.Track\s*\(", "a process tracked"),
        (@"\bWaitForExitAsync\s*\(", "a wait on the exit"),
    ];

    /// <summary>The runner's files and its transports', which it hands each conversation's turns.</summary>
    private static readonly string[] Transports = ["ProtocolChat.cs", "NativeChat.cs", "ChatRun.cs", "ChatWatch.cs", "ProtocolHeld.cs"];

    private static IEnumerable<(string File, string Source)> Runner() =>
        SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Where(file => Path.GetFileName(file).StartsWith("ChatRunner", StringComparison.Ordinal) || Transports.Contains(Path.GetFileName(file)))
            .Select(file => (Path.GetFileName(file), Regex.Replace(File.ReadAllText(file).Replace("\r\n", "\n"), @"//[^\n]*", "")));

    [Fact]
    public void The_conversations_runner_starts_nothing_itself_and_holds_the_run_for_it()
    {
        var runner = Runner().ToList();
        var found = runner
            .SelectMany(each => RunsOwn.Where(own => Regex.IsMatch(each.Source, own.Pattern)).Select(own => $"{each.File}: {own.Is}"))
            .ToList();

        Assert.NotEmpty(runner);
        Assert.True(found.Count == 0, "the conversation's runner does what its run does:\n" + string.Join('\n', found));
        Assert.Contains(runner, each => each.Source.Contains("agent.Run(", StringComparison.Ordinal));
    }

    /// <summary>
    /// A conversation's words reach its process through the run's <c>DoorInput</c> alone, so the one file that writes a line to a
    /// tracked process's stdin is the run's; no executor, turn or hand-over does.
    /// </summary>
    [Fact]
    public void Nothing_above_the_run_writes_a_line_to_a_tracked_process()
    {
        var writing = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Where(file => Regex.IsMatch(Regex.Replace(File.ReadAllText(file).Replace("\r\n", "\n"), @"//[^\n]*", ""), @"\bProcesses\.Send\s*\("))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Equal(["TransportRun.cs"], writing);
    }

    /// <summary>
    /// On the protocol door the conversation's turns say a prompt, a word into a running turn and a steer on the run's
    /// <c>ProtocolWords</c>, never through the session, which they read for its options alone.
    /// </summary>
    [Fact]
    public void The_protocol_conversations_words_go_through_the_runs_words_and_not_the_session()
    {
        var chat = Runner().Single(each => each.File == "ProtocolChat.cs").Source;
        var writing = Regex.Matches(chat, @"\b[Ss]ession\??\.(PromptAsync|SayAsync|SteeredAsync|SteerAsync|RetriedAsync|RecoveredAsync|Retries)\b")
            .Select(match => match.Value)
            .ToArray();

        Assert.True(writing.Length == 0, "ProtocolChat writes through the session:\n" + string.Join('\n', writing));
        Assert.Contains("Words is not { NextStep: true }", chat, StringComparison.Ordinal);
    }

    /// <summary>
    /// The conversation reads its agent through <c>ChatAgent</c> and the run it makes, as the run reads its door: no file of the conversation's
    /// runner, its turns, its account reads or what it is told names the adapter seam, which row t replaces beneath them.
    /// </summary>
    [Fact]
    public void No_file_of_the_conversation_names_the_adapter_seam()
    {
        var chat = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Where(file => file.Replace('\\', '/').Contains("/Loop/Chat/", StringComparison.Ordinal))
            .ToList();
        var naming = chat
            .Where(file => Regex.IsMatch(Regex.Replace(File.ReadAllText(file).Replace("\r\n", "\n"), @"//[^\n]*", ""), @"\bISessionAdapter\b"))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Contains(chat, file => Path.GetFileName(file) == "ChatRun.cs");
        Assert.True(naming.Length == 0, "a file of the conversation names the adapter seam:\n" + string.Join('\n', naming));
    }

    [Fact]
    public void The_one_protocol_session_in_the_driver_is_the_runs()
    {
        var making = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Where(file => Regex.IsMatch(Regex.Replace(File.ReadAllText(file).Replace("\r\n", "\n"), @"//[^\n]*", ""), @"\bnew\s+AcpSession\s*\("))
            .Select(file => Path.GetFileName(file)!)
            .ToArray();

        Assert.Equal(["TransportRun.cs"], making);
    }

    /// <summary>The scan itself, red on each planted start beside the run.</summary>
    [Theory]
    [InlineData("var proc = LineageBrackets.Bracketed(_home, id, info, null);")]
    [InlineData("var process = await LineageBrackets.Spawned(proc, () => AgentStarts.StartAsync(info, ContainedKind.Agent, whose, home, null, ct));")]
    [InlineData("await DownloadKeeping.KeepHandedAsync(_service, _home, id, info, ct);")]
    [InlineData("var session = new AcpSession(stdout, stdin, Line);")]
    [InlineData("using var tracked = _processes.Track(id, process);")]
    public void A_start_planted_in_the_runner_is_named(string planted) =>
        Assert.Contains(RunsOwn, own => Regex.IsMatch(planted, own.Pattern));
}
