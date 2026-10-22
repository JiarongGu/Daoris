using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199's note on the strict start), generalized by TERMTRUST1a (D201 point 4): the launcher takes the driver's words alone,
/// joins only the agents' job and a piece's, in that order, and starts nothing where it could not join. Nothing is started here: the join
/// and the start are handed in; the built program in a real job is <see cref="PieceLauncherProcessTests"/>' and <c>AgentStartsProcessTests</c>'.
/// </summary>
public sealed class PieceLauncherTests
{
    [Fact]
    public void The_drivers_words_read_back_as_the_job_the_harness_and_its_arguments()
    {
        var job = PieceJobs.NameOf("p1");
        var words = PieceLauncher.Arguments(job, @"C:\agents\codex-acp.exe", ["--flag", "a b", "--"]);

        var (call, problem) = PieceLauncher.Read(words);

        Assert.Null(problem);
        Assert.Equal([job], call!.Value.Jobs);
        Assert.Equal(@"C:\agents\codex-acp.exe", call.Value.Harness);
        Assert.Equal(["--flag", "a b", "--"], call.Value.Arguments);
    }

    /// <summary>TERMTRUST1a (D201 point 4): the agents' job first, then a piece's, which nests beneath it; each read back in order.</summary>
    [Fact]
    public void The_agents_job_and_a_pieces_read_back_in_order()
    {
        string[] jobs = [AgentJobs.NameOf(4242), PieceJobs.NameOf("p1")];
        var words = PieceLauncher.Arguments(jobs, @"C:\agents\claude.exe", ["-p", "--"]);

        var (call, problem) = PieceLauncher.Read(words);

        Assert.Null(problem);
        Assert.Equal(jobs, call!.Value.Jobs);
        Assert.Equal(@"C:\agents\claude.exe", call.Value.Harness);
        Assert.Equal(["-p", "--"], call.Value.Arguments);
    }

    [Fact]
    public void The_agents_job_alone_is_taken()
    {
        var (call, problem) = PieceLauncher.Read(PieceLauncher.Arguments(AgentJobs.NameOf(7), "harness.exe", []));

        Assert.Null(problem);
        Assert.Equal([AgentJobs.NameOf(7)], call!.Value.Jobs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--job|Local\\daoris-piece-p1")]
    [InlineData("--job|Local\\daoris-piece-p1|harness.exe")]
    [InlineData("--job|Local\\daoris-piece-p1|-x|harness.exe")]
    [InlineData("--job|Local\\daoris-heavy-1|--|harness.exe")]
    [InlineData("--job|Local\\daoris-piece-|--|harness.exe")]
    [InlineData("--job|Local\\daoris-piece-p1|--| ")]
    // TERMTRUST1a: the agents' prefix takes a process id alone, never its `-open` event's name or any other; it comes first, once.
    [InlineData("--job|Local\\daoris-agents-|--|harness.exe")]
    [InlineData("--job|Local\\daoris-agents-12-open|--|harness.exe")]
    [InlineData("--job|Local\\daoris-agents-x1|--|harness.exe")]
    [InlineData("--job|Global\\daoris-agents-12|--|harness.exe")]
    [InlineData("--job|Local\\daoris-piece-p1|--job|Local\\daoris-agents-12|--|harness.exe")]
    [InlineData("--job|Local\\daoris-agents-12|--job|Local\\daoris-agents-13|--|harness.exe")]
    [InlineData("--job|Local\\daoris-agents-12|--job|Local\\daoris-piece-p1|--job|Local\\daoris-piece-p2|--|harness.exe")]
    [InlineData("--job|Local\\daoris-agents-12|--job|--|harness.exe")]
    [InlineData("--|harness.exe")]
    public void Words_that_are_not_the_drivers_or_name_another_job_are_refused(string words)
    {
        var (call, problem) = PieceLauncher.Read(words.Length == 0 ? [] : words.Split('|'));

        Assert.Null(call);
        Assert.StartsWith("daoris-piece-start: ", problem);
    }

    [Fact]
    public void A_job_that_cannot_be_joined_starts_nothing_and_says_why()
    {
        var error = new StringWriter();
        var started = 0;
        string? joined = null;

        var code = PieceLauncher.Run(
            PieceLauncher.Arguments(PieceJobs.NameOf("p1"), "harness.exe", []), error,
            job => { joined = job; return false; },
            (_, _) => { started++; return 0; });

        Assert.Equal(PieceLauncher.Refused, code);
        Assert.Equal(PieceJobs.NameOf("p1"), joined);
        Assert.Equal(0, started);
        Assert.Contains("could not join the piece's job", error.ToString());
    }

    [Fact]
    public void Refused_words_join_nothing_and_start_nothing()
    {
        var joins = 0;
        var starts = 0;

        var code = PieceLauncher.Run(["harness.exe"], new StringWriter(), _ => { joins++; return true; }, (_, _) => { starts++; return 0; });

        Assert.Equal(PieceLauncher.Refused, code);
        Assert.Equal((0, 0), (joins, starts));
    }

    /// <summary>TERMTRUST1a: each job joined in the order it was given, then the harness; one that will not join starts nothing.</summary>
    [Fact]
    public void Each_job_is_joined_in_order_and_one_refused_starts_nothing()
    {
        string[] jobs = [AgentJobs.NameOf(4242), PieceJobs.NameOf("p1")];
        var joined = new List<string>();
        var starts = 0;

        var code = PieceLauncher.Run(
            PieceLauncher.Arguments(jobs, "harness.exe", []), new StringWriter(),
            job => { joined.Add(job); return true; },
            (_, _) => { starts++; return 3; });

        Assert.Equal((3, 1), (code, starts));
        Assert.Equal(jobs, joined);

        var error = new StringWriter();
        joined.Clear();
        code = PieceLauncher.Run(
            PieceLauncher.Arguments(jobs, "harness.exe", []), error,
            job => { joined.Add(job); return job != jobs[1]; },
            (_, _) => { starts++; return 0; });

        Assert.Equal((PieceLauncher.Refused, 1), (code, starts));
        Assert.Equal(jobs, joined);
        Assert.Contains("could not join", error.ToString());
    }

    /// <summary>
    /// TERMTRUST1a: the harness is named by its full path, as the driver resolves it, never found by a search: a bare name would be looked
    /// for in the launcher's working folder first, which is the session's tree.
    /// </summary>
    [Fact]
    public void A_harness_not_named_by_its_full_path_is_refused_at_the_start()
    {
        var error = new StringWriter();

        var code = PieceLauncher.Start(new System.Diagnostics.ProcessStartInfo("claude"), error);

        Assert.Equal(PieceLauncher.Refused, code);
        Assert.Contains("full path", error.ToString());
    }

    /// <summary>TERMTRUST1a: a start already made the launcher's is known as one, so a second pass leaves it, and the doors' tracking holds no job of its own over it.</summary>
    [Fact]
    public void A_start_made_the_launchers_is_known_as_one()
    {
        var info = new ProcessStartInfo(@"C:\Daoris\app\bin\daoris-piece-start.exe");
        foreach (var word in PieceLauncher.Arguments(AgentJobs.NameOf(9), @"C:\agents\claude.exe", ["-p"])) info.ArgumentList.Add(word);

        Assert.True(PieceLauncher.Carries(info));
        Assert.False(PieceLauncher.Carries(new ProcessStartInfo(@"C:\agents\claude.exe")));
        Assert.False(PieceLauncher.Carries(new ProcessStartInfo(@"C:\Daoris\app\bin\daoris-piece-start.exe")));
    }

    [Fact]
    public void Once_joined_the_harness_is_started_with_its_arguments_its_exit_answered_and_the_stdio_handed_down()
    {
        ProcessStartInfo? seen = null;
        var order = new List<string>();

        var code = PieceLauncher.Run(
            PieceLauncher.Arguments(PieceJobs.NameOf("p1"), "harness.exe", ["acp", "--x"]), new StringWriter(),
            _ => { order.Add("join"); return true; },
            (info, _) => { order.Add("start"); seen = info; return 7; });

        Assert.Equal(7, code);
        Assert.Equal(["join", "start"], order);
        Assert.Equal("harness.exe", seen!.FileName);
        Assert.Equal(["acp", "--x"], seen.ArgumentList);
        Assert.False(seen.UseShellExecute);
        Assert.False(seen.CreateNoWindow);
        Assert.False(seen.RedirectStandardInput || seen.RedirectStandardOutput || seen.RedirectStandardError);
    }

    /// <summary>The driver's half (<see cref="PieceStarts"/>): the harness's start made the launcher's, everything else of it kept.</summary>
    [Fact]
    public void The_harnesss_start_is_made_the_launchers_with_its_environment_folder_and_streams_kept()
    {
        if (!OperatingSystem.IsWindows()) return;
        var piece = Guid.NewGuid().ToString("N")[..12];
        using var job = PieceJobs.Create(piece);
        var info = new ProcessStartInfo(@"C:\agents\codex-acp.exe")
        {
            WorkingDirectory = @"C:\trees\p-1",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("--x");
        info.Environment["PIECE_WORD"] = "s1";
        string[] jobs = [AgentJobs.NameOf(4242), PieceJobs.NameOf(piece)];

        Assert.Null(PieceStarts.Through(info, @"C:\Daoris\app\bin\daoris-piece-start.exe", jobs));

        Assert.Equal(@"C:\Daoris\app\bin\daoris-piece-start.exe", info.FileName);
        Assert.Equal(PieceLauncher.Arguments(jobs, @"C:\agents\codex-acp.exe", ["--x"]), info.ArgumentList);
        Assert.Equal((@"C:\trees\p-1", true, true, true), (info.WorkingDirectory, info.RedirectStandardInput, info.RedirectStandardOutput, info.CreateNoWindow));
        Assert.Equal("s1", info.Environment["PIECE_WORD"]);
    }

    [Fact]
    public void With_no_job_no_launcher_or_words_in_one_string_the_start_is_left_as_it_was()
    {
        var info = new ProcessStartInfo(@"C:\agents\codex-acp.exe");
        info.ArgumentList.Add("--x");

        Assert.Contains("no job", PieceStarts.Through(info, @"C:\bin\daoris-piece-start.exe", []));
        Assert.Contains("launcher", PieceStarts.Through(info, launcher: null, [PieceJobs.NameOf("p1")]));
        Assert.Equal((@"C:\agents\codex-acp.exe", "--x"), (info.FileName, info.ArgumentList.Single()));

        var line = new ProcessStartInfo(@"C:\agents\codex-acp.exe") { Arguments = "--x" };
        Assert.Contains("one string", PieceStarts.Through(line, @"C:\bin\daoris-piece-start.exe", [PieceJobs.NameOf("p1")]));
        Assert.Equal((@"C:\agents\codex-acp.exe", "--x"), (line.FileName, line.Arguments));
    }

    [Fact]
    public void Missing_says_why_a_piece_cannot_start_strictly()
    {
        Assert.Contains("job could not be made", PieceStarts.Missing(@"C:\bin\daoris-piece-start.exe", job: null, "p1"));
        if (OperatingSystem.IsWindows())
        {
            var piece = Guid.NewGuid().ToString("N")[..12];
            using var job = PieceJobs.Create(piece);
            Assert.Contains("launcher", PieceStarts.Missing(launcher: null, job, piece));
            Assert.Null(PieceStarts.Missing(@"C:\bin\daoris-piece-start.exe", job, piece));
        }
    }

    [Fact]
    public void A_home_with_no_install_beside_it_has_no_launcher()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-no-launcher-" + Guid.NewGuid().ToString("N")[..8]);

        Assert.Null(PieceStarts.LauncherPath(home));
        Assert.Null(PieceStarts.LauncherPath(null));
    }

    [Fact]
    public void A_pieces_job_is_named_under_the_launchers_prefix()
    {
        Assert.StartsWith(PieceLauncher.JobPrefix, PieceJobs.NameOf("abc"));
    }

    [Fact]
    public void The_agents_job_is_named_for_the_process_under_the_launchers_agents_prefix_and_its_open_event_beside_it()
    {
        Assert.Equal(PieceLauncher.AgentsPrefix + "4242", AgentJobs.NameOf(4242));
        Assert.Equal(PieceLauncher.AgentsPrefix + "4242-open", AgentJobs.OpenNameOf(4242));
        Assert.Equal("Local\\daoris-agents-", PieceLauncher.AgentsPrefix);
    }

    [Fact]
    public void The_program_is_the_publishs_twin()
    {
        var publish = File.ReadAllText(Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "..", "..", "tools", "desktop-publish.mjs"));

        Assert.Contains($"PIECE_START = '{PieceLauncher.Program}'", publish);
    }
}
