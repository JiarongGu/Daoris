using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a start hands its <see cref="LauncherRunner"/> of Daoris's own (the agent-lyntai design §5.5): the launcher's arguments containment
/// reads (its kind, class, home and log), the environment its shell composed before the layer's door was registered, and where its process
/// and its raw lines go: the process registered so a stop is Daoris's kill of the tree and the watch hears it, each raw line stamped before
/// any reader takes it, and the lines its harness says outside a frame and on stderr kept as the transcript's lines and its own words.
/// </summary>
internal sealed record LauncherSeat(ContainedKind Kind, StartClass Whose, string? Home, MachineLog? Log)
{
    /// <summary>The start's environment as its shell composed it, a null value removing an inherited variable.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Told the process the launcher answered: the session's processes and the watch register it.</summary>
    public Action<Process> Started { get; init; } = _ => { };

    /// <summary>Told each raw line read, stdout's and stderr's, before any reader takes it: the watch's hearing stamp.</summary>
    public Action Heard { get; init; } = () => { };

    /// <summary>
    /// The program a start resolved (its file and the arguments before the door's own), started in place of the command the layer's door
    /// names; null starts that command. A driven start's resolution is the shell's (the pin, the Windows shim), as today's spawn's is.
    /// </summary>
    public (string File, IReadOnlyList<string> Head)? Program { get; init; }

    /// <summary>Told once the write of a turn's stdin returned, before its input closes: where its words are sent.</summary>
    public Action Wrote { get; init; } = () => { };

    /// <summary>Told each raw stdout line's frame, read once, before any reader takes it: what the harness's own ending reads of its stream.</summary>
    public Action<NativeFrame> Framed { get; init; } = _ => { };

    /// <summary>Told each raw stdout line that is no JSON object, by the one test today's mapper makes: the harness's own words.</summary>
    public Action<string> Outside { get; init; } = _ => { };

    /// <summary>Told each stderr line: the transcript's line and the harness's own words, as today's capture keeps them.</summary>
    public Action<string> Error { get; init; } = _ => { };
}
