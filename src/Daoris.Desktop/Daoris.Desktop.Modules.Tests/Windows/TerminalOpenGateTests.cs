using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// TERMSTART1 (UIFREEZE1, D56's SWEEPFLIGHT1 and PROCSTART2 note): a terminal's shell is started through the start gate, off
/// the caller's thread. <c>OPEN</c> started it with <c>CreateProcessW</c> in the route's synchronous part, which runs on the
/// window's thread, where an antivirus hook on process creation held the window.
/// </summary>
/// <remarks>The fast half: the factory is a stand-in that notes the thread it was called on, and no shell starts.</remarks>
public sealed class TerminalOpenGateTests : Bridge
{
    // True on the thread making the request, while it makes it: a start seen there was made on the caller's thread.
    [ThreadStatic]
    private static bool t_calling;

    private sealed class Terminal : ITerminal
    {
        private int _disposed;

        public bool Disposed => Volatile.Read(ref _disposed) != 0;

        public void Write(string data)
        {
        }

        public void Resize(int columns, int rows)
        {
        }

        public void Dispose() => Volatile.Write(ref _disposed, 1);
    }

    private sealed class Terminals : ITerminalFactory
    {
        private readonly List<(Terminal Terminal, bool OnCaller)> _started = [];

        /// <summary>Set to hold each start until it is released: a start the antivirus holds.</summary>
        public ManualResetEventSlim? Held { get; init; }

        /// <summary>Set once a start has begun.</summary>
        public ManualResetEventSlim Begun { get; } = new(false);

        /// <summary>Set to refuse each start as the system does.</summary>
        public Exception? Refuse { get; init; }

        public IReadOnlyList<(Terminal Terminal, bool OnCaller)> Started
        {
            get
            {
                lock (_started) return [.. _started];
            }
        }

        public IReadOnlyList<TerminalShell> Shells() => [new(TerminalShells.Cmd, @"C:\system\cmd.exe", [])];

        public ITerminal Start(TerminalLaunch launch, Action<string> output, Action<int> exited)
        {
            var onCaller = t_calling;
            Begun.Set();
            // Bounded, so a start made on the caller's thread fails its test rather than holding it.
            Held?.Wait(TimeSpan.FromSeconds(10));
            if (Refuse is not null) throw Refuse;
            var terminal = new Terminal();
            lock (_started) _started.Add((terminal, onCaller));
            return terminal;
        }
    }

    private static Task<IpcResponse> Calling(Func<Task<IpcResponse>> call)
    {
        t_calling = true;
        try
        {
            return call();
        }
        finally
        {
            t_calling = false;
        }
    }

    [Fact]
    public async Task A_terminals_shell_starts_off_the_callers_thread()
    {
        var terminals = new Terminals();
        using var module = new TerminalModule(Bus, terminals);

        var answer = await Calling(() => AskAsync(module, "OPEN"));

        Assert.True(answer.Success, answer.Error?.Message);
        Assert.False(Assert.Single(terminals.Started).OnCaller, "the shell was started on the caller's thread");
    }

    /// <summary>The system's refusal, met on the thread pool now, is still the page's refusal with the system's reason.</summary>
    [Fact]
    public async Task A_shell_the_system_would_not_start_off_the_callers_thread_is_still_refused_by_its_code()
    {
        var terminals = new Terminals { Refuse = new System.ComponentModel.Win32Exception(5) };
        using var module = new TerminalModule(Bus, terminals);

        var refusal = await RefusalAsync(module, "OPEN");

        Assert.StartsWith(Refusals.TerminalNotStarted, refusal);
        Assert.Contains($"problem={new System.ComponentModel.Win32Exception(5).Message}", refusal);
        Assert.True(terminals.Begun.IsSet);
    }

    /// <summary>
    /// The application closing while a start waits its turn at the gate: the shell that starts after is ended at once, never
    /// left running with no tab and no module to close it.
    /// </summary>
    [Fact]
    public async Task A_shell_that_starts_after_the_terminals_were_ended_is_ended_too()
    {
        using var held = new ManualResetEventSlim(false);
        var terminals = new Terminals { Held = held };
        var module = new TerminalModule(Bus, terminals);

        var opening = Calling(() => AskAsync(module, "OPEN"));
        Assert.True(terminals.Begun.Wait(TimeSpan.FromSeconds(10)), "the start never began");
        module.Dispose();
        held.Set();
        await opening;

        Assert.True(Assert.Single(terminals.Started).Terminal.Disposed, "a shell started after the close was left running");
    }
}
