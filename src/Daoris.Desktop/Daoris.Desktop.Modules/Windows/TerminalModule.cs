using System.Collections.Concurrent;
using System.ComponentModel;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The person's own shells, for the page's terminal view (CONSOLE4a, D96,
/// <c>docs/2026-09-30-terminal-design.md</c> §3): each a real shell under a pseudo-console, typed at and read
/// over this bridge.
/// </summary>
/// <remarks>
/// <para><b>Requests</b>: <c>SHELLS</c> (what the machine has, and the default), <c>OPEN</c> {shell?, cwd?,
/// cols?, rows?} → {id, shell, cwd}, <c>INPUT</c> {id, data}, <c>RESIZE</c> {id, cols, rows} and <c>CLOSE</c>
/// {id}. <b>Events</b>: <c>TERMINAL_OUTPUT</c> {id, data}, batched, and <c>TERMINAL_EXITED</c> {id, code} when
/// a shell ends on its own.</para>
///
/// <para><b>Where it starts is the page's to say, and the home's where it cannot</b>: the page knows the
/// attended session and the workspace in scope, which are the viewer's; this knows whether the folder is on
/// this machine. A folder that is not there opens the terminal in the home, and the answer names where it
/// did open, so the tab never claims a place it is not.</para>
///
/// <para><b>Its environment is this process's plus the home</b> (<c>DAORIS_HOME</c>), so the <c>daoris</c> CLI
/// typed in it answers for this machine (D63).</para>
///
/// <para><b>Desktop-only</b> (D47 §4): the keystrokes and the output ride the bridge and never HTTP. And
/// 🔴 <b>nothing here writes to the machine log</b> (D94): what is typed and printed is the person's own,
/// and a refusal is logged by its code alone, by the dispatcher's middleware, as every module's is.</para>
///
/// <para><b>Every terminal ends when the application does</b>: the container that made this disposes it,
/// and each shell's job object ends with this process besides.</para>
///
/// <para><b>A shell starts through the start gate</b> (<see cref="ProcessStarts"/>, TERMSTART1), never on the window's
/// thread; one whose terminal was ended while it waited there is ended as it starts.</para>
/// </remarks>
public sealed class TerminalModule : ModuleBase, IDisposable
{
    private readonly IEventBus _events;
    private readonly ITerminalFactory _terminals;

    /// <remarks>The bus is held as well as handed to the base: this module raises events of its own, as the driver's does.</remarks>
    public TerminalModule(IEventBus events, ITerminalFactory terminals) : base(events: events)
    {
        _events = events;
        _terminals = terminals;
    }

    /// <summary>
    /// How long output gathers before it goes to the page. Shorter than the console's window
    /// (<see cref="ConsoleRelay.DefaultWindow"/>), because here the output is the echo of a keystroke, and
    /// a tenth of a second between a key and its letter reads as a slow terminal.
    /// </summary>
    public static readonly TimeSpan OutputWindow = TimeSpan.FromMilliseconds(16);

    /// <summary>The shells this build offers, whatever the machine has.</summary>
    private static readonly IReadOnlySet<string> Offered = new HashSet<string>(StringComparer.Ordinal)
    {
        TerminalShells.Pwsh, TerminalShells.WindowsPowerShell, TerminalShells.Cmd, TerminalShells.GitBash,
    };

    private readonly ConcurrentDictionary<string, Open> _open = new(StringComparer.Ordinal);

    public override string ModuleName => "DAORIS.TERMINAL";

    /// <summary>One terminal as the module holds it: the shell's process, and the relay its output goes out on.</summary>
    private sealed class Open(string id, bool holder = false) : IDisposable
    {
        private int _quiet;
        private int _ended;
        private int _told;
        private ITerminal? _terminal;

        public string Id { get; } = id;

        /// <summary>That this is a role holder's tab (STEWARDROLE1a-i), whose every end the judge is told of once.</summary>
        public bool Holder { get; } = holder;

        /// <summary>True the first time it is asked: the holder's end is told once, whichever path ends it first.</summary>
        public bool FirstEnd() => Interlocked.Exchange(ref _told, 1) == 0;
        public ITerminal? Terminal => Volatile.Read(ref _terminal);
        public BatchRelay<string>? Relay { get; set; }
        public Action<string, string>? Sink { get; set; }

        public void Say(string data) => Sink?.Invoke(Id, data);

        /// <summary>
        /// The shell, once started: ended at once where the terminal was ended while its start waited at the gate
        /// (TERMSTART1), so a shell never runs on with no tab and no module to close it. Each side writes with a full fence
        /// before it reads the other's, so one of them always sees the other.
        /// </summary>
        public void Hold(ITerminal terminal)
        {
            Interlocked.Exchange(ref _terminal, terminal);
            if (Volatile.Read(ref _ended) != 0) terminal.Dispose();
        }

        /// <summary>Send what is left and stop: once, whether the shell ended or the tab closed first.</summary>
        public void Quiet()
        {
            if (Interlocked.Exchange(ref _quiet, 1) == 0) Relay?.Dispose();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _ended, 1);
            Terminal?.Dispose();
            Quiet();
        }
    }

    protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "SHELLS":
            {
                var shells = _terminals.Shells();
                return Task.FromResult<object?>(new
                {
                    Shells = shells.Select(shell => new { Shell = shell.Id }).ToArray(),
                    Default = TerminalShells.Default(shells)?.Id,
                });
            }

            case "OPEN":
                return StartAsync(
                    PayloadHelper.GetOptionalValue<string>(request.Payload, "shell"),
                    PayloadHelper.GetOptionalValue<string>(request.Payload, "cwd"),
                    PayloadHelper.GetOptionalValue<int>(request.Payload, "cols"),
                    PayloadHelper.GetOptionalValue<int>(request.Payload, "rows"),
                    cancellationToken);

            case "INPUT":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var data = PayloadHelper.GetOptionalValue<string>(request.Payload, "data") ?? "";
                var open = _open.TryGetValue(id, out var found) ? found.Terminal : null;
                open?.Write(data);
                return Task.FromResult<object?>(new { Written = open is not null });
            }

            case "RESIZE":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var open = _open.TryGetValue(id, out var found) ? found.Terminal : null;
                open?.Resize(
                    PayloadHelper.GetRequiredValue<int>(request.Payload, "cols"),
                    PayloadHelper.GetRequiredValue<int>(request.Payload, "rows"));
                return Task.FromResult<object?>(new { Resized = open is not null });
            }

            case "CLOSE":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // Out of the table first: the closed shell's own end is then nobody's news. A holder's end is the judge's news, told
                // before the tab goes, since its own end then emits nothing (STEWARDROLE1a-i, D209 §2.2).
                var closed = _open.TryRemove(id, out var open);
                if (open is not null) TellHolderEnded(open);
                open?.Dispose();
                return Task.FromResult<object?>(new { Closed = closed });
            }

            default:
                throw UnknownType(request);
        }
    }

    private async Task<object?> StartAsync(string? named, string? cwd, int columns, int rows, CancellationToken ct)
    {
        var available = _terminals.Shells();
        TerminalShell shell;
        if (string.IsNullOrWhiteSpace(named))
        {
            shell = TerminalShells.Default(available) ?? throw Refusals.Because(
                Refusals.TerminalNoShell,
                "This machine has no shell on PATH for a terminal: PowerShell, Command Prompt or Git Bash.");
        }
        else if (!Offered.Contains(named))
        {
            throw Refusals.Because(
                Refusals.TerminalShellUnknown,
                $"There is no shell called `{named}` here: a terminal runs `pwsh`, `powershell`, `cmd` or `bash`.",
                ("shell", named));
        }
        else
        {
            shell = available.FirstOrDefault(candidate => candidate.Id == named) ?? throw Refusals.Because(
                Refusals.TerminalShellMissing,
                $"`{named}` is not on this machine's PATH.",
                ("shell", named));
        }

        var home = DaorisHome.Resolve();
        var directory = Directory.Exists(cwd) ? cwd! : home is not null && Directory.Exists(home)
            ? home
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (home is not null) environment[DaorisHome.Variable] = home;

        var open = new Open($"t{Guid.NewGuid():N}"[..9]);
        // The relay and the table entry come before the shell: a shell that prints and ends at once is
        // heard and told as surely as one that runs for an hour.
        open.Relay = new BatchRelay<string>(
            handler => open.Sink = handler, _ => open.Sink = null,
            (id, items) => _events.EmitAsync("DAORIS", "TERMINAL_OUTPUT", new { Id = id, Data = string.Concat(items) }),
            OutputWindow);
        _open[open.Id] = open;
        var launch = new TerminalLaunch(shell, directory, environment, columns > 0 ? columns : 80, rows > 0 ? rows : 24);
        try
        {
            // 🔴 TERMSTART1: through the start gate, on the thread pool. The pseudo-console's CreateProcessW ran in this route's
            // synchronous part, on the window's thread, where an antivirus hook on process creation held the window.
            ITerminal? started = null;
            await ProcessStarts.Shared.RunAsync(() => started = _terminals.Start(launch, open.Say, code => Ended(open, code)), ct)
                .ConfigureAwait(false);
            open.Hold(started!);
        }
        catch (Exception error) when (error is Win32Exception or IOException or PlatformNotSupportedException)
        {
            _open.TryRemove(open.Id, out _);
            open.Quiet();
            throw Refusals.Because(
                Refusals.TerminalNotStarted,
                $"`{shell.Id}` would not start: {error.Message}",
                ("shell", shell.Id), ("problem", error.Message));
        }
        catch (OperationCanceledException)
        {
            // Given up while it waited for a place: nothing started.
            _open.TryRemove(open.Id, out _);
            open.Quiet();
            throw;
        }

        return new { open.Id, Shell = shell.Id, Cwd = directory };
    }

    /// <summary>
    /// Told the id of a role holder's tab once it ends, by whichever path ends it first (STEWARDROLE1a-i, D209 §2.2's
    /// <c>HolderEnded</c>): the page's <c>CLOSE</c>, the harness's own exit, this module's disposal at Daoris's close, and an exit
    /// while its appointment is still opening it. Handed by the application as it composes the window; the page's
    /// <c>TERMINAL_EXITED</c> stays the page's.
    /// </summary>
    public Action<string>? HolderEnded { get; set; }

    /// <summary>The folder Daoris's own executables are in, which the holder's composed <c>PATH</c> names: this application's, unless a test names one.</summary>
    public string App { get; set; } = AppContext.BaseDirectory;

    /// <summary>Where the holder's harness is found and checked: Daoris's managed, verified pin (<see cref="HolderHarness.Find"/>), unless a test hands its own.</summary>
    public Func<string, (string? Binary, string? Why)> Harness { get; set; } = HolderHarness.Find;

    /// <summary>
    /// A role holder's tab (SUPERVISOR1a-ii, D208 §2.2; STEWARDROLE1a-i, D209 §2.2 and §6.1): a tab of this panel, never a console window
    /// of the system's, <b>whose process is Daoris's own pinned harness</b>, by its absolute path, its hash checked immediately before
    /// the start, with <see cref="HolderTerminal.SessionArguments"/> as its argument vector and the environment Daoris composes, whole;
    /// in the holder's own new folder, the configuration directory's owned files written again first. Its first lines are written to
    /// the tab before the harness's own; every window is told <c>TERMINAL_OPENED</c> {id, shell, cwd, title}. Refused
    /// <see cref="HolderHarness.Refused"/> with nothing started where the pin is not Daoris's verified own, never PATH's <c>claude</c>
    /// instead; <c>unstarted</c> where it would not start.
    /// </summary>
    public async Task<HolderOpened> OpenHolderAsync(string home, HolderOpen holder, CancellationToken ct = default)
    {
        var (binary, why) = Harness(home);
        if (binary is null) return new HolderOpened(null, null, HolderHarness.Refused, why);

        var folder = HolderTerminal.Started(home, holder.Holder, holder.Living);
        var config = HolderTerminal.Own(home);
        var open = new Open($"t{Guid.NewGuid():N}"[..9], holder: true);
        open.Relay = new BatchRelay<string>(
            handler => open.Sink = handler, _ => open.Sink = null,
            (id, items) => _events.EmitAsync("DAORIS", "TERMINAL_OUTPUT", new { Id = id, Data = string.Concat(items) }),
            OutputWindow);
        _open[open.Id] = open;
        open.Say(HolderTerminal.Intro(holder.At, holder.Roles));
        var launch = new TerminalLaunch(
            new TerminalShell(HolderTerminal.Harness, binary, HolderTerminal.SessionArguments), folder,
            HolderTerminal.Environment(home, holder.Key, App), Whole: true);
        ITerminal? started = null;
        string? refused = null;
        try
        {
            await ProcessStarts.Shared.RunAsync(() =>
            {
                // Immediately before the start: the bytes the pin's verified install recorded, or nothing starts.
                var (still, changed) = Harness(home);
                if (!string.Equals(still, binary, StringComparison.OrdinalIgnoreCase))
                {
                    refused = changed ?? "the pinned harness changed";
                    return;
                }

                started = _terminals.Start(launch, open.Say, code => Ended(open, code));
            }, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is Win32Exception or IOException or PlatformNotSupportedException or OperationCanceledException)
        {
            started = null;
        }

        if (started is null)
        {
            _open.TryRemove(open.Id, out _);
            open.Quiet();
            return refused is not null
                ? new HolderOpened(null, null, HolderHarness.Refused, refused)
                : new HolderOpened(null, null, StandingConfirmation.Unstarted, null);
        }

        open.Hold(started);
        await _events.EmitAsync("DAORIS", "TERMINAL_OPENED", new
        {
            open.Id, Shell = HolderTerminal.Harness, Cwd = folder, Title = HolderTerminal.Title(holder.Roles),
        }).ConfigureAwait(false);
        return new HolderOpened(open.Id, new HolderProcess(started.ProcessId, started.Created, binary, config), null, null);
    }

    /// <summary>
    /// A line written to a holder's tab, and its title said again where one is named (STEWARDROLE1a-i, D209 §2.2): a role added to it,
    /// or its last role turned off. The owner's to read; the session reads its verbs' answers. False where the tab is gone.
    /// </summary>
    public bool TellHolder(string tab, string line, string? title = null)
    {
        if (!_open.TryGetValue(tab, out var open) || !open.Holder) return false;
        open.Say(line);
        if (title is not null)
        {
            _ = _events.EmitAsync("DAORIS", "TERMINAL_OPENED", new { open.Id, Shell = HolderTerminal.Harness, Cwd = "", Title = title });
        }

        return true;
    }

    /// <summary>A holder's tab closed by Daoris (the walk found its configuration changed): its end told to the judge first. False where it is gone.</summary>
    public bool CloseHolder(string tab)
    {
        if (!_open.TryRemove(tab, out var open)) return false;
        TellHolderEnded(open);
        open.Dispose();
        return true;
    }

    private void TellHolderEnded(Open open)
    {
        if (!open.Holder || !open.FirstEnd()) return;
        try
        {
            HolderEnded?.Invoke(open.Id);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // The judge's own trouble: the tab ends whatever it does.
        }
    }

    /// <summary>A shell ended: its last output goes out first, then its end — unless the page closed it, and knows.</summary>
    private void Ended(Open open, int code)
    {
        open.Quiet();
        TellHolderEnded(open);
        if (!_open.TryRemove(new KeyValuePair<string, Open>(open.Id, open))) return;
        _ = _events.EmitAsync("DAORIS", "TERMINAL_EXITED", new { open.Id, Code = code });
    }

    /// <summary>End every terminal: the application is going.</summary>
    public void Dispose()
    {
        foreach (var id in _open.Keys.ToArray())
        {
            if (!_open.TryRemove(id, out var open)) continue;
            TellHolderEnded(open);
            open.Dispose();
        }
    }
}
