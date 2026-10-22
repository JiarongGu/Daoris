using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// TERMTRUST1a part (ii) (D201 point 4, the terminal-trust design §3.3 point 3 and §8 case 3): where a terminal's process stands, read
/// from the system. A process in a job of the agents' prefix asks <c>in-agent-job</c>; one outside every such job, each found and read,
/// is covered; anything the look cannot tell (an <c>-open</c> event, a job that will not answer, a namespace not read, an instance whose
/// server holds no job of the prefix, this process's own agents not contained, a client gone or created after the connection) is
/// <c>untold</c>; a job that is no agent's (the terminal panel's is unnamed) takes nothing.
/// </summary>
/// <remarks>
/// <para>Real processes and real named objects of this Windows session: the job and the event are this test's own, named for a process
/// id no Daoris holds, and the stand-in in it is <c>ping</c>, never an agent's binary. The instance pipes this session serves are not
/// opened here (the person's own Daoris may serve one): <see cref="StandingCaller.Servers"/> is a stand-in, its rule held by its own case.</para>
/// <para><b>Elevation is not measured here</b>: a test cannot start an elevated stand-in without the person's consent prompt, so whether a
/// judge that is not elevated opens an elevated Daoris's job (the design's §3.4 says it cannot, which answers untold) is the parent's
/// look on the install (case 14), and stays said in D201's note.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class StandingCallerTests : IDisposable
{
    // A process id no Daoris holds: the prefix's names are this test's own.
    private static readonly string Other = (900_000_000 + Environment.ProcessId).ToString(CultureInfo.InvariantCulture);

    private readonly List<IDisposable> _held = [];
    private readonly List<Process> _started = [];

    public void Dispose()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Gone already.
            }

            process.Dispose();
        }

        foreach (var held in _held) held.Dispose();
    }

    /// <summary>The look with this process's own containment taken as part (i) would give it, and no instance pipes beside it.</summary>
    private static StandingCaller Look() => new() { OwnContained = _ => true, Servers = _ => [] };

    private SafeFileHandle Job(string? name)
    {
        var job = CreateJobObjectW(IntPtr.Zero, name is null ? null : @"Local\" + name);
        Assert.False(job.IsInvalid, $"no job made: {Marshal.GetLastPInvokeError()}");
        _held.Add(job);
        return job;
    }

    /// <summary>A stand-in that waits, started and then put in <paramref name="job"/> where one is named.</summary>
    private Process StandIn(SafeFileHandle? job = null)
    {
        var process = Process.Start(new ProcessStartInfo("ping", "-n 120 127.0.0.1")
        {
            CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true,
        })!;
        _started.Add(process);
        if (job is not null) Assert.True(AssignProcessToJobObject(job, process.Handle), $"not joined: {Marshal.GetLastPInvokeError()}");
        return process;
    }

    private PipeClientProcess Client(int pid)
    {
        var client = PipeClientProcess.Of(pid, DateTime.UtcNow);
        _held.Add(client);
        return client;
    }

    [Fact]
    public void A_process_in_an_agents_job_asks_and_one_outside_every_such_job_is_covered()
    {
        if (!OperatingSystem.IsWindows()) return;
        var job = Job(StandingCaller.Prefix + Other);
        var agent = StandIn(job);

        var listing = StandingCaller.ListSession();

        // Another Daoris process's job, found by listing this session's namespace and read by its handle.
        Assert.NotNull(listing);
        Assert.Contains(StandingCaller.Prefix + Other, listing.Jobs);
        Assert.Equal(StandingPlace.InAgentJob, Look().Place(Client(agent.Id)));
        Assert.Equal(StandingPlace.Outside, Look().Place(Client(Environment.ProcessId)));
    }

    /// <summary>The terminal panel's shell is held in a job of its own with no name (<c>PseudoConsole</c>): it is no agent's, and takes nothing.</summary>
    [Fact]
    public void A_job_that_is_no_agents_takes_no_cover()
    {
        if (!OperatingSystem.IsWindows()) return;
        Job(StandingCaller.Prefix + Other);
        var panel = StandIn(Job(null));
        var named = StandIn(Job("daoris-something-else-" + Other));

        Assert.Equal(StandingPlace.Outside, Look().Place(Client(panel.Id)));
        Assert.Equal(StandingPlace.Outside, Look().Place(Client(named.Id)));
    }

    [Fact]
    public void An_open_event_of_the_prefix_leaves_every_press_untold()
    {
        if (!OperatingSystem.IsWindows()) return;
        var open = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\" + StandingCaller.Prefix + Other + StandingCaller.OpenSuffix);
        _held.Add(open);

        Assert.Contains(StandingCaller.Prefix + Other + StandingCaller.OpenSuffix, StandingCaller.ListSession()!.Open);
        Assert.Equal(StandingPlace.Untold, Look().Place(Client(Environment.ProcessId)));
    }

    [Fact]
    public void Anything_the_look_cannot_tell_is_untold()
    {
        if (!OperatingSystem.IsWindows()) return;
        Job(StandingCaller.Prefix + Other);
        var me = Client(Environment.ProcessId);

        // A job of the prefix that will not open or answer.
        var look = Look();
        look.InJob = (_, _) => null;
        Assert.Equal(StandingPlace.Untold, look.Place(me));
        // The namespace not read.
        Assert.Equal(StandingPlace.Untold, new StandingCaller { OwnContained = _ => true, Servers = _ => [], List = () => null }.Place(me));
        // This process's own agents not contained: until part (i) lands, it holds no job of the prefix.
        Assert.Equal(StandingPlace.Untold, new StandingCaller { Servers = _ => [] }.Place(me));
        // An instance pipe whose server holds no job of the prefix, a Daoris from before TERMTRUST1a; and the pipes not read.
        Assert.Equal(StandingPlace.Untold, new StandingCaller { OwnContained = _ => true, Servers = _ => [424242] }.Place(me));
        Assert.Equal(StandingPlace.Untold, new StandingCaller { OwnContained = _ => true, Servers = _ => null }.Place(me));
        // One whose server holds one is a Daoris of this build, and takes nothing.
        Assert.Equal(StandingPlace.Outside, new StandingCaller { OwnContained = _ => true, Servers = _ => [int.Parse(Other, CultureInfo.InvariantCulture)] }.Place(me));
        // No client at all.
        Assert.Equal(StandingPlace.Untold, Look().Place(null));
        Assert.Equal(StandingPlace.Untold, Look().Place(PipeClientProcess.Unknown));
    }

    [Fact]
    public void A_client_gone_or_created_after_the_connection_was_taken_is_untold()
    {
        if (!OperatingSystem.IsWindows()) return;
        var taken = DateTime.UtcNow;
        Thread.Sleep(20);
        var later = StandIn();
        var created = PipeClientProcess.Of(later.Id, taken);
        _held.Add(created);
        Assert.Null(created.Handle);
        Assert.Equal(StandingPlace.Untold, Look().Place(created));

        var gone = Client(later.Id);
        Assert.Equal(StandingPlace.Outside, Look().Place(gone));
        later.Kill();
        later.WaitForExit();
        Assert.True(gone.Gone);
        Assert.Equal(StandingPlace.Untold, Look().Place(gone));
    }

    [Theory]
    [InlineData("daoris-agents-1234", true, false)]
    [InlineData("daoris-agents-1234-open", false, true)]
    [InlineData("daoris-agents-", false, false)]
    [InlineData("daoris-agents-12x4", false, false)]
    [InlineData("daoris-piece-1234", false, false)]
    public void The_prefixs_names_are_a_process_id_and_its_open_event(string name, bool job, bool open)
    {
        Assert.Equal((job, open), (StandingCaller.IsJobName(name), StandingCaller.IsOpenName(name)));
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateJobObjectW(IntPtr security, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
}
