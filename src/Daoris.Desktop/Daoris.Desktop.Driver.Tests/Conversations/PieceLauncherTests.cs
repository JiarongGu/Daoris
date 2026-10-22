using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199's note on the strict start): the partner's launcher takes the driver's words alone, joins only a piece's job, and
/// starts nothing where it could not join. Nothing is started here: the join and the start are handed in; the built program in a real
/// job is <see cref="PieceLauncherProcessTests"/>'.
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
        Assert.Equal(job, call!.Value.Job);
        Assert.Equal(@"C:\agents\codex-acp.exe", call.Value.Harness);
        Assert.Equal(["--flag", "a b", "--"], call.Value.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--job|Local\\daoris-piece-p1")]
    [InlineData("--job|Local\\daoris-piece-p1|harness.exe")]
    [InlineData("--job|Local\\daoris-piece-p1|-x|harness.exe")]
    [InlineData("--job|Local\\daoris-heavy-1|--|harness.exe")]
    [InlineData("--job|Local\\daoris-piece-|--|harness.exe")]
    [InlineData("--job|Local\\daoris-piece-p1|--| ")]
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

        Assert.Null(PieceStarts.Through(info, @"C:\Daoris\app\bin\daoris-piece-start.exe", job, piece));

        Assert.Equal(@"C:\Daoris\app\bin\daoris-piece-start.exe", info.FileName);
        Assert.Equal(PieceLauncher.Arguments(PieceJobs.NameOf(piece), @"C:\agents\codex-acp.exe", ["--x"]), info.ArgumentList);
        Assert.Equal((@"C:\trees\p-1", true, true, true), (info.WorkingDirectory, info.RedirectStandardInput, info.RedirectStandardOutput, info.CreateNoWindow));
        Assert.Equal("s1", info.Environment["PIECE_WORD"]);
    }

    [Fact]
    public void With_no_job_or_no_launcher_the_start_is_refused_and_left_as_it_was()
    {
        var info = new ProcessStartInfo(@"C:\agents\codex-acp.exe");
        info.ArgumentList.Add("--x");

        Assert.Contains("job could not be made", PieceStarts.Through(info, @"C:\bin\daoris-piece-start.exe", job: null, "p1"));
        if (OperatingSystem.IsWindows())
        {
            var piece = Guid.NewGuid().ToString("N")[..12];
            using var job = PieceJobs.Create(piece);
            Assert.Contains("launcher", PieceStarts.Through(info, launcher: null, job, piece));
        }

        Assert.Equal((@"C:\agents\codex-acp.exe", "--x"), (info.FileName, info.ArgumentList.Single()));
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
    public void The_program_is_the_publishs_twin()
    {
        var publish = File.ReadAllText(Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "..", "..", "tools", "desktop-publish.mjs"));

        Assert.Contains($"PIECE_START = '{PieceLauncher.Program}'", publish);
    }
}
