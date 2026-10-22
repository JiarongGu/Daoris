using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TERMTRUST1a part (i) (D201 point 4; the terminal-trust design §3.3 point 3, §8 case 2): a start made the launcher's in place, this
/// process's agents' job first and a piece's beneath it; one that cannot be contained made as it was, said why, and holding the process's
/// open event until it ends; the line it leaves on the machine log. No process is started here: <see cref="AgentStartsProcessTests"/> starts
/// the built launcher. Each test's jobs are its own, under a process id that is not this one's.
/// </summary>
public sealed class AgentStartsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-agent-starts-" + Guid.NewGuid().ToString("N")[..8]);

    public AgentStartsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string Launcher = @"C:\Daoris\app\bin\daoris-piece-start.exe";

    /// <summary>A process id no process here holds a job under: high, and its own per test.</summary>
    private static int StandInId() => 1_900_000_000 + Random.Shared.Next(100_000_000);

    /// <summary>A program that is there, named by its full path.</summary>
    private static string Program => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.SystemDirectory, "cmd.exe")
        : "/bin/sh";

    private static ProcessStartInfo Harness(string? file = null)
    {
        var info = new ProcessStartInfo(file ?? Program)
        {
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("/d");
        info.Environment["AGENT_WORD"] = "kept";
        return info;
    }

    private static bool Open(AgentJobs jobs)
    {
        if (!EventWaitHandle.TryOpenExisting(jobs.OpenName, out var open)) return false;
        open.Dispose();
        return true;
    }

    [Fact]
    public void A_start_is_made_the_launchers_in_place_its_harness_its_words_and_the_rest_kept()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var jobs = new AgentJobs(StandInId());
        var info = Harness();

        using var start = jobs.Contain(info, ContainedKind.Agent, Launcher);

        Assert.True(start.Contained);
        Assert.Null(start.Why);
        Assert.Equal(Launcher, info.FileName);
        Assert.Equal(PieceLauncher.Arguments([jobs.Name], Program, ["/d"]), info.ArgumentList);
        Assert.Equal((Path.GetTempPath(), true, true, true), (info.WorkingDirectory, info.RedirectStandardInput, info.RedirectStandardOutput, info.CreateNoWindow));
        Assert.Equal("kept", info.Environment["AGENT_WORD"]);
        // Made empty at the first start that needed it, and held: the launcher opens it by name.
        Assert.True(ProcessJob.Exists(jobs.Name));
        Assert.Equal(0, ProcessJob.ActiveOf(jobs.Name));
        Assert.Equal((0, false), (jobs.Uncontained, Open(jobs)));
    }

    [Fact]
    public void A_piece_joins_the_agents_job_first_and_its_own_beneath_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var jobs = new AgentJobs(StandInId());
        var info = Harness();

        using var start = jobs.Contain(info, ContainedKind.Piece, Launcher, PieceJobs.NameOf("p1"));

        Assert.True(start.Contained);
        Assert.Equal(PieceLauncher.Arguments([jobs.Name, PieceJobs.NameOf("p1")], Program, ["/d"]), info.ArgumentList);
    }

    [Fact]
    public void A_start_already_the_launchers_is_left_as_it_is()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var jobs = new AgentJobs(StandInId());
        var info = Harness();
        using var first = jobs.Contain(info, ContainedKind.Agent, Launcher);
        var words = info.ArgumentList.ToList();

        using var again = jobs.Contain(info, ContainedKind.Agent, Launcher);

        Assert.True(again.Contained);
        Assert.Equal(Launcher, info.FileName);
        Assert.Equal(words, info.ArgumentList);
    }

    [Fact]
    public void With_no_launcher_laid_out_the_start_is_left_as_it_was_and_holds_the_open_event_until_let_go()
    {
        using var jobs = new AgentJobs(StandInId());
        var info = Harness();

        var start = jobs.Contain(info, ContainedKind.Agent, launcher: null);

        Assert.Equal((false, ContainedWhy.NoLauncher), (start.Contained, start.Why));
        Assert.Equal(Program, info.FileName);
        Assert.Equal(["/d"], info.ArgumentList);
        Assert.Equal(1, jobs.Uncontained);
        if (OperatingSystem.IsWindows()) Assert.True(Open(jobs), "the open event is held while an uncontained start lives");
        // No job is made where nothing can join it.
        Assert.False(ProcessJob.Exists(jobs.Name));

        start.Dispose();

        Assert.Equal((0, false), (jobs.Uncontained, Open(jobs)));
    }

    [Fact]
    public void The_open_event_is_held_while_any_uncontained_start_lives_and_let_go_with_the_last()
    {
        using var jobs = new AgentJobs(StandInId());
        var one = jobs.Contain(Harness(), ContainedKind.Agent, launcher: null);
        var two = jobs.Contain(Harness(), ContainedKind.Plugin, launcher: null);

        one.Dispose();
        one.Dispose();
        Assert.Equal(1, jobs.Uncontained);
        if (OperatingSystem.IsWindows()) Assert.True(Open(jobs));

        two.Lives(null);
        Assert.Equal((0, false), (jobs.Uncontained, Open(jobs)));
    }

    [Fact]
    public void A_job_the_system_refuses_leaves_the_start_as_it_was_said_job_refused()
    {
        var asked = 0;
        using var jobs = new AgentJobs(StandInId(), _ => { asked++; return (null, false); });
        var info = Harness();

        using var start = jobs.Contain(info, ContainedKind.Agent, Launcher);
        using var next = jobs.Contain(Harness(), ContainedKind.Agent, Launcher);

        Assert.Equal((false, ContainedWhy.JobRefused), (start.Contained, start.Why));
        Assert.Equal(Program, info.FileName);
        Assert.Equal(2, jobs.Uncontained);
        // Asked again at the next start: a refusal may pass.
        Assert.Equal(2, asked);
    }

    [Fact]
    public void A_name_another_process_holds_leaves_the_start_as_it_was_said_name_held()
    {
        if (!OperatingSystem.IsWindows()) return;
        var id = StandInId();
        using var held = ProcessJob.Create(AgentJobs.NameOf(id));
        Assert.NotNull(held);
        using var jobs = new AgentJobs(id);
        var info = Harness();

        using var start = jobs.Contain(info, ContainedKind.Agent, Launcher);

        Assert.Equal((false, ContainedWhy.NameHeld), (start.Contained, start.Why));
        Assert.Equal(Program, info.FileName);
    }

    [Fact]
    public void An_object_of_another_kind_holding_the_name_is_name_held_too()
    {
        if (!OperatingSystem.IsWindows()) return;
        var id = StandInId();
        using var squatter = new EventWaitHandle(false, EventResetMode.ManualReset, AgentJobs.NameOf(id));
        using var jobs = new AgentJobs(id);

        using var start = jobs.Contain(Harness(), ContainedKind.Agent, Launcher);

        Assert.Equal(ContainedWhy.NameHeld, start.Why);
    }

    [Fact]
    public void A_piece_whose_agents_job_is_refused_still_joins_its_own_and_is_said_uncontained()
    {
        using var jobs = new AgentJobs(StandInId(), _ => (null, false));
        var info = Harness();

        using var start = jobs.Contain(info, ContainedKind.Piece, Launcher, PieceJobs.NameOf("p1"));

        Assert.Equal((false, ContainedWhy.JobRefused), (start.Contained, start.Why));
        Assert.Equal(PieceLauncher.Arguments([PieceJobs.NameOf("p1")], Program, ["/d"]), info.ArgumentList);
        Assert.Equal(1, jobs.Uncontained);
    }

    [Fact]
    public void A_piece_is_never_started_outside_its_job()
    {
        using var jobs = new AgentJobs(StandInId());

        Assert.Throws<DriverException>(() => jobs.Contain(Harness(), ContainedKind.Piece, launcher: null, PieceJobs.NameOf("p1")));
        Assert.Throws<DriverException>(() => jobs.Contain(Harness(Path.Combine(_home, "absent.exe")), ContainedKind.Piece, Launcher, PieceJobs.NameOf("p1")));
        Assert.Equal(0, jobs.Uncontained);
    }

    [Fact]
    public void A_bare_name_is_handed_on_as_the_full_path_its_start_would_find()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var jobs = new AgentJobs(StandInId());
        var info = Harness("cmd");
        info.Environment[Tools.PathVariable] = Environment.SystemDirectory;

        using var start = jobs.Contain(info, ContainedKind.Agent, Launcher);

        Assert.True(start.Contained);
        // As the system's search found it: its extension's casing is the file system's.
        Assert.Equal(PieceLauncher.Arguments([jobs.Name], Program, ["/d"]), info.ArgumentList, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_program_that_is_not_there_is_left_to_fail_as_it_did()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var jobs = new AgentJobs(StandInId());
        var absent = Path.Combine(_home, "absent.exe");
        var info = Harness(absent);

        using var start = jobs.Contain(info, ContainedKind.Plugin, Launcher);

        Assert.Equal((false, ContainedWhy.NotHanded), (start.Contained, start.Why));
        Assert.Equal((absent, "/d"), (info.FileName, info.ArgumentList.Single()));
    }

    [Fact]
    public void Each_start_is_said_on_the_machine_log_by_codes_alone()
    {
        using var jobs = new AgentJobs(StandInId());
        using var uncontained = jobs.Contain(Harness(), ContainedKind.Plugin, launcher: null);

        using (var log = new MachineLog(_home, "driver")) AgentStarts.Said(uncontained, _home, log);
        AgentStarts.Said(new AgentStart(jobs, ContainedKind.Piece, null), _home, log: null);

        var lines = Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == "agent.contained")
            .Select(line => line.GetProperty("data"))
            .ToList();
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, line => line.GetProperty("kind").GetString() == "plugin"
            && !line.GetProperty("contained").GetBoolean() && line.GetProperty("why").GetString() == ContainedWhy.NoLauncher);
        Assert.Contains(lines, line => line.GetProperty("kind").GetString() == "piece"
            && line.GetProperty("contained").GetBoolean() && !line.TryGetProperty("why", out _));
        // Codes only (D156 §5.1): no path, no name of a program.
        Assert.All(lines, line => Assert.DoesNotContain("cmd", line.GetRawText(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_route_reaches_the_gate_with_the_start_made_the_launchers_where_one_is_laid_out()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_home, "install");
        var home = Path.Combine(root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var bin = Path.Combine([root, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, PieceLauncher.Program), "");
        using var jobs = new AgentJobs(StandInId());
        ProcessStartInfo? seen = null;

        using (AgentJobs.Within(jobs))
        using (ProcessStarts.Within(info => { seen = info; return null; }))
        {
            Assert.Null(AgentStarts.StartAsync(Harness(), ContainedKind.Agent, StartClass.Background, home, log: null, CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        Assert.Equal(Path.Combine(bin, PieceLauncher.Program), seen!.FileName);
        Assert.Equal(PieceLauncher.Arguments([jobs.Name], Program, ["/d"]), seen.ArgumentList);
        // A start the gate did not make holds nothing and says nothing.
        Assert.Equal(0, jobs.Uncontained);
        Assert.False(Directory.Exists(DaorisHome.Entry(home, HomeEntry.Logs)) && Directory.EnumerateFiles(DaorisHome.Entry(home, HomeEntry.Logs)).Any());
    }
}
