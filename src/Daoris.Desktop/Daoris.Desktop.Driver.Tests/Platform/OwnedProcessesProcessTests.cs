using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one management of the processes Daoris starts. A start through the gate is joined to its purpose's job and counted in the
/// census; a start held by no job is joined to none; and an owner killed takes what it started, and what that started, with it. The owner
/// is the built stand-in (<c>daoris-owner-stand-in</c>), which starts a node that starts a node of its own, detached.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class OwnedProcessesProcessTests
{
    // A node that starts a detached node, says its id, and waits: both give up at two minutes, so a failing test leaves nothing for long.
    private const string Script =
        "const g=require('child_process').spawn(process.execPath,['-e','setTimeout(()=>{},120000)'],"
        + "{detached:true,stdio:'ignore',windowsHide:true});g.unref();console.log(g.pid);setTimeout(()=>{},120000)";

    /// <summary>The stand-in's build beside this test's own.</summary>
    internal static string OwnerStandIn()
    {
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.OwnerStandIn", "bin", build.Parent.Name, build.Name, "daoris-owner-stand-in.exe");
        Assert.True(File.Exists(built), "the owner stand-in is built before these tests: " + built);
        return built;
    }

    [Fact]
    public async Task An_owner_killed_takes_what_it_started_and_what_that_started_with_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (owner, child, grandchild) = await StartOwnerAsync(ProcessPurpose.Git, joins: true);
        using (owner)
        using (child)
        using (grandchild)
        {
            try
            {
                // The owner alone, never its tree: what it started ends by its job's close, or not at all.
                owner.Kill(entireProcessTree: false);
                await owner.WaitForExitAsync().WaitAsync(Poll.Patience);

                await child.WaitForExitAsync().WaitAsync(Poll.Patience);
                await grandchild.WaitForExitAsync().WaitAsync(Poll.Patience);
            }
            finally
            {
                EndAll(owner, child, grandchild);
            }
        }
    }

    /// <summary>The control: a start held by no job outlives its killed owner, as the update's launcher must.</summary>
    [Fact]
    public async Task A_start_held_by_no_job_outlives_its_killed_owner()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (owner, child, grandchild) = await StartOwnerAsync(ProcessPurpose.Outlives, joins: false);
        using (owner)
        using (child)
        using (grandchild)
        {
            try
            {
                owner.Kill(entireProcessTree: false);
                await owner.WaitForExitAsync().WaitAsync(Poll.Patience);
                await Task.Delay(TimeSpan.FromSeconds(1));

                Assert.False(child.HasExited);
                Assert.False(grandchild.HasExited);
            }
            finally
            {
                EndAll(owner, child, grandchild);
            }
        }
    }

    [Fact]
    public async Task A_start_through_the_gate_is_held_by_its_purposes_job_and_counted_in_the_census()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var process = await StartWaitingAsync(ProcessPurpose.Read);
        try
        {
            Assert.Equal(ProcessPurpose.Read, OwnedProcesses.Shared.Holding(process));
            Assert.Contains(process.Id, OwnedProcesses.Shared.Census().Single(each => each.Purpose == ProcessPurpose.Read).Ids);
        }
        finally
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        }
    }

    [Fact]
    public async Task A_start_through_the_gate_held_by_no_job_is_joined_to_none()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var process = await StartWaitingAsync(ProcessPurpose.EndsItself);
        try
        {
            Assert.Null(OwnedProcesses.Shared.Holding(process));
        }
        finally
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        }
    }

    /// <summary>A <c>cmd</c> through the gate as <paramref name="purpose"/>'s, waiting for a line on its input.</summary>
    private static async Task<Process> StartWaitingAsync(ProcessPurpose purpose)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        foreach (var word in new[] { "/d", "/c", "set /p line=" }) info.ArgumentList.Add(word);
        return (await ProcessStarts.Shared.StartAsync(info, purpose, StartClass.Background, CancellationToken.None))!;
    }

    /// <summary>The stand-in owner, its node and the node's detached node, each read as it says its id.</summary>
    private static async Task<(Process Owner, Process Child, Process Grandchild)> StartOwnerAsync(ProcessPurpose purpose, bool joins)
    {
        var info = new ProcessStartInfo(OwnerStandIn())
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var word in new[] { purpose.ToString(), "node", "-e", Script }) info.ArgumentList.Add(word);
        var owner = Process.Start(info)!;
        var said = (await owner.StandardOutput.ReadLineAsync().WaitAsync(Poll.Patience))!.Split(' ');
        var child = Process.GetProcessById(int.Parse(said[0]));
        var grandchild = Process.GetProcessById(int.Parse((await owner.StandardOutput.ReadLineAsync().WaitAsync(Poll.Patience))!));
        Assert.Equal(joins.ToString(), said[1]);
        return (owner, child, grandchild);
    }

    private static void EndAll(params Process[] processes)
    {
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Gone once stopped.
            }
        }
    }
}
