using System.Diagnostics;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A stand-in for a command a test wants held: a node that waits for the test to write a file, then runs the real command with the
/// arguments it was given (the review-leave tests' git). PROCLEAK1: it never waits for ever. A stand-in that polled for its file
/// with no end outlived its killed test by 51 hours, polling every 50 ms; this one takes the pid of the process that owns it and
/// gives up when that process is gone, and gives up at a bound of its own besides.
/// </summary>
internal static class ReleaseStandIn
{
    /// <summary>The stand-in's exit code where it gave up: its owner went, or its bound passed, before the file came.</summary>
    public const int GaveUp = 3;

    // argv: the file, the owner's pid, the bound in milliseconds, the command, its arguments.
    private static readonly string Script =
        "const fs=require('fs'),cp=require('child_process');const [released,owner,bound,git,...args]=process.argv.slice(1);"
        + "const gone=()=>{try{process.kill(Number(owner),0);return false}catch(e){return e.code!=='EPERM'}};"
        + "const until=Date.now()+Number(bound);"
        + "const go=()=>{if(fs.existsSync(released)){const r=cp.spawnSync(git,args,{stdio:'inherit'});process.exit(r.status===null?1:r.status)}"
        + $"if(gone()||Date.now()>until)process.exit({GaveUp});setTimeout(go,50)}};go()";

    /// <summary>
    /// The start of the stand-in for the command <paramref name="info"/> names: it runs that command in the same folder, with the
    /// same streams and environment, once <paramref name="released"/> exists, and ends without running it where
    /// <paramref name="owner"/> (the process that owns the stand-in) is gone or <paramref name="bound"/> has passed.
    /// </summary>
    public static ProcessStartInfo For(ProcessStartInfo info, string released, int owner, TimeSpan bound)
    {
        var stand = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = info.RedirectStandardOutput,
            RedirectStandardError = info.RedirectStandardError,
            RedirectStandardInput = info.RedirectStandardInput,
            StandardOutputEncoding = info.StandardOutputEncoding,
            StandardErrorEncoding = info.StandardErrorEncoding,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = info.WorkingDirectory,
        };
        foreach (var (name, value) in info.Environment) stand.Environment[name] = value;
        stand.ArgumentList.Add("-e");
        stand.ArgumentList.Add(Script);
        stand.ArgumentList.Add(released);
        stand.ArgumentList.Add(owner.ToString());
        stand.ArgumentList.Add(((long)bound.TotalMilliseconds).ToString());
        stand.ArgumentList.Add(info.FileName);
        foreach (var argument in info.ArgumentList) stand.ArgumentList.Add(argument);
        return stand;
    }
}

/// <summary>
/// PROCLEAK1: a stand-in that waits on a file ends with the test that started it. The merge tool's owner found one still polling
/// 51 hours after its test was killed, its file never written.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ReleaseStandInTests() : Bridge("release-stand-in"), IDisposable
{
    private readonly List<Process> _started = [];

    // The processes first, then the home: a stand-in still running holds its folder.
    void IDisposable.Dispose()
    {
        foreach (var started in _started)
        {
            try
            {
                if (!started.HasExited) started.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { /* gone */ }

            started.Dispose();
        }

        base.Dispose();
    }

    private Process Start(ProcessStartInfo info)
    {
        var started = Process.Start(info)!;
        _started.Add(started);
        return started;
    }

    /// <summary>The test's own process, in a fixture's shape: a node that does nothing until it is killed.</summary>
    private Process Owner() => Start(new ProcessStartInfo("node", ["-e", "setInterval(() => {}, 1000)"]) { UseShellExecute = false, CreateNoWindow = true });

    private Process StandIn(int owner, TimeSpan bound, out string released)
    {
        released = Path.Combine(Home, "released");
        var git = new ProcessStartInfo("git", ["--version"]) { UseShellExecute = false, RedirectStandardOutput = true };
        return Start(ReleaseStandIn.For(git, released, owner, bound));
    }

    [Fact]
    public async Task A_stand_in_waiting_for_its_file_ends_when_the_process_that_owned_it_is_gone()
    {
        var owner = Owner();
        var stand = StandIn(owner.Id, Poll.Patience, out _);
        Assert.False(stand.WaitForExit(TimeSpan.FromMilliseconds(500)), "the stand-in ended with its owner still running and no file written");

        // The test is killed: its fixture process goes, and the file is never written.
        owner.Kill(entireProcessTree: true);

        await stand.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal(ReleaseStandIn.GaveUp, stand.ExitCode);
    }

    [Fact]
    public async Task A_stand_in_gives_up_at_its_bound_though_its_owner_still_runs()
    {
        var owner = Owner();
        var stand = StandIn(owner.Id, TimeSpan.FromMilliseconds(300), out _);

        await stand.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal(ReleaseStandIn.GaveUp, stand.ExitCode);
        Assert.False(owner.HasExited);
    }

    [Fact]
    public async Task A_stand_in_whose_file_comes_runs_the_command_it_stands_for_and_answers_its_code()
    {
        var owner = Owner();
        var stand = StandIn(owner.Id, Poll.Patience, out var released);
        File.WriteAllText(released, "go");

        var said = await stand.StandardOutput.ReadToEndAsync().WaitAsync(Poll.Patience);
        await stand.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal(0, stand.ExitCode);
        Assert.StartsWith("git version", said);
    }
}
