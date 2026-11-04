using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1h-iii (AGENTPROVIDER1m-i part 4): the conversation's runner holds the run over today's transports for its start, its end, its
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
        (@"\bDriver\.Bracketed\s*\(", "a lineage bracket"),
        (@"\bDriver\.Spawned\s*\(", "a spawn under a token"),
        (@"\bAgentStarts\.StartAsync\s*\(", "an agent's start"),
        (@"\bKeepHandedAsync\s*\(", "a start's downloads kept"),
        (@"\bHarnessCapture\.Capture\w*Async\s*\(", "a capture of the harness's streams"),
        (@"\bnew\s+AcpSession\s*\(", "a protocol session"),
        (@"\bProcesses\.Track\s*\(|_processes\.Track\s*\(", "a process tracked"),
        (@"\bWaitForExitAsync\s*\(", "a wait on the exit"),
    ];

    /// <summary>The runner's files and its transports', which it hands each conversation's turns.</summary>
    private static readonly string[] Transports = ["ProtocolChat.cs", "NativeChat.cs"];

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
        Assert.Contains(runner, each => each.Source.Contains("new TransportRun(", StringComparison.Ordinal));
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
    [InlineData("var proc = Driver.Bracketed(_home, id, info, null);")]
    [InlineData("var process = await Driver.Spawned(proc, () => AgentStarts.StartAsync(info, ContainedKind.Agent, whose, home, null, ct));")]
    [InlineData("await DownloadKeeping.KeepHandedAsync(_service, _home, id, info, ct);")]
    [InlineData("var session = new AcpSession(stdout, stdin, Line);")]
    [InlineData("using var tracked = _processes.Track(id, process);")]
    public void A_start_planted_in_the_runner_is_named(string planted) =>
        Assert.Contains(RunsOwn, own => Regex.IsMatch(planted, own.Pattern));
}
