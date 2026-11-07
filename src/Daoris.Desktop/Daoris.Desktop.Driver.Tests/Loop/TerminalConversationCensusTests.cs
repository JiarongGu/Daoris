using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A terminal's conversation is the running Daoris's, so no terminal path runs one. A source
/// scan, line comments read out: the conversation's runner is made by the instance's loop alone, the terminal's host names neither it nor
/// its adapters and output, and the files the retired runner told its turns through are named by no source but the migration step that
/// removes what an older build left.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class TerminalConversationCensusTests
{
    private const string Host = "Daoris.Desktop.Driver.Host";

    private static readonly string[] Projects =
        [SourceFiles.DriverProject, Host, "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    /// <summary>What a runner of conversations is made of: each a second way where the terminal's host writes it.</summary>
    private static readonly (string Pattern, string Is)[] RunsAConversation =
    [
        (@"\bChatRunner\b", "the conversation's runner"),
        (@"\bTerminalOwner\b", "a terminal that owns a conversation"),
        (@"\bSessionOutput\b", "a session's output buffer"),
    ];

    private static string Bare(string file) => Regex.Replace(File.ReadAllText(file).Replace("\r\n", "\n"), @"//[^\n]*", "");

    private static IEnumerable<(string Project, string File, string Source)> Sources() =>
        Projects
            .Where(project => Directory.Exists(SourceFiles.Project(project)))
            .SelectMany(project => SourceFiles.All(project, "*.cs").Select(file => (project, Path.GetFileName(file), Bare(file))));

    [Fact]
    public void The_terminals_three_doors_name_no_runner_of_conversations()
    {
        var doors = new[] { "ChatConsole.cs", "HeldChatConsole.cs", "SessionsConsole.cs" };
        var found = Sources()
            .Where(source => source.Project == Host && doors.Contains(source.File))
            .SelectMany(source => RunsAConversation.Where(own => Regex.IsMatch(source.Source, own.Pattern)).Select(own => $"{source.File}: {own.Is}"))
            .ToArray();

        Assert.True(found.Length == 0, "a terminal's door runs a conversation:\n" + string.Join('\n', found));
        Assert.Contains(Sources(), source => source.File == "ChatConsole.cs" && source.Source.Contains("HeldChatConsole.ChatAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void The_terminals_host_makes_no_conversation_runner()
    {
        var making = Sources().Where(source => source.Project == Host && Regex.IsMatch(source.Source, @"\bnew\s+ChatRunner\s*\(")).Select(source => source.File).ToArray();

        Assert.True(making.Length == 0, "the terminal's host makes a runner of conversations:\n" + string.Join('\n', making));
    }

    [Fact]
    public void The_instances_loop_makes_the_one_conversation_runner()
    {
        var making = Sources().Where(source => Regex.IsMatch(source.Source, @"\bnew\s+ChatRunner\s*\(")).Select(source => source.File).ToArray();

        Assert.Equal(["LoopRun.cs"], making);
    }

    [Fact]
    public void No_source_names_the_retired_turn_marks_or_their_owner()
    {
        var naming = Sources()
            .Where(source => Regex.IsMatch(source.Source, @"\b(TurnMarks|TerminalOwner)\b"))
            .Select(source => source.File)
            .ToArray();

        Assert.True(naming.Length == 0, "a source names what told a terminal's turns through the request folder:\n" + string.Join('\n', naming));
    }

    [Fact]
    public void Only_the_migration_step_names_a_turn_mark_by_its_ending()
    {
        var naming = Sources()
            .Where(source => Regex.IsMatch(source.Source, @"""\.turn"""))
            .Select(source => source.File)
            .ToArray();

        Assert.Equal(["TurnMarksStep.cs"], naming);
    }

    [Fact]
    public void The_request_folders_watch_hears_no_turn_mark_and_tells_no_window_of_one()
    {
        var watch = Sources().Single(source => source.File == "SessionRequestWatch.cs").Source;

        Assert.DoesNotContain("Turned", watch, StringComparison.Ordinal);
        Assert.DoesNotContain("HearTurns", watch, StringComparison.Ordinal);
        Assert.DoesNotContain("\".turn\"", watch, StringComparison.Ordinal);
    }

    [Fact]
    public void The_queue_a_window_reads_never_leaves_a_conversation_to_another_process()
    {
        var routes = Sources().Single(source => source.File == "ConversationRoutes.cs").Source;

        Assert.DoesNotContain("RunsElsewhere", routes, StringComparison.Ordinal);
    }

    /// <summary>The scan itself, red on each planted second runner.</summary>
    [Theory]
    [InlineData("using var runner = new ChatRunner(service, adapters, home, processes, output);")]
    [InlineData("await using var owner = new TerminalOwner(home, id, runner, processes);")]
    [InlineData("var output = new SessionOutput();")]
    public void A_runner_planted_in_a_terminal_door_is_named(string planted) =>
        Assert.Contains(RunsAConversation, own => Regex.IsMatch(planted, own.Pattern));
}
