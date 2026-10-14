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
    private sealed class Open(string id) : IDisposable
    {
        private int _quiet;

        public string Id { get; } = id;
        public ITerminal? Terminal { get; set; }
        public BatchRelay<string>? Relay { get; set; }
        public Action<string, string>? Sink { get; set; }

        public void Say(string data) => Sink?.Invoke(Id, data);

        /// <summary>Send what is left and stop: once, whether the shell ended or the tab closed first.</summary>
        public void Quiet()
        {
            if (Interlocked.Exchange(ref _quiet, 1) == 0) Relay?.Dispose();
        }

        public void Dispose()
        {
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
                return Task.FromResult<object?>(Start(
                    PayloadHelper.GetOptionalValue<string>(request.Payload, "shell"),
                    PayloadHelper.GetOptionalValue<string>(request.Payload, "cwd"),
                    PayloadHelper.GetOptionalValue<int>(request.Payload, "cols"),
                    PayloadHelper.GetOptionalValue<int>(request.Payload, "rows")));

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
                // Out of the table first: the closed shell's own end is then nobody's news.
                var closed = _open.TryRemove(id, out var open);
                open?.Dispose();
                return Task.FromResult<object?>(new { Closed = closed });
            }

            default:
                throw UnknownType(request);
        }
    }

    private object Start(string? named, string? cwd, int columns, int rows)
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
        try
        {
            open.Terminal = _terminals.Start(
                new TerminalLaunch(shell, directory, environment, columns > 0 ? columns : 80, rows > 0 ? rows : 24),
                open.Say, code => Ended(open, code));
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

        return new { open.Id, Shell = shell.Id, Cwd = directory };
    }

    /// <summary>A shell ended: its last output goes out first, then its end — unless the page closed it, and knows.</summary>
    private void Ended(Open open, int code)
    {
        open.Quiet();
        if (!_open.TryRemove(new KeyValuePair<string, Open>(open.Id, open))) return;
        _ = _events.EmitAsync("DAORIS", "TERMINAL_EXITED", new { open.Id, Code = code });
    }

    /// <summary>End every terminal: the application is going.</summary>
    public void Dispose()
    {
        foreach (var id in _open.Keys.ToArray())
        {
            if (_open.TryRemove(id, out var open)) open.Dispose();
        }
    }
}
