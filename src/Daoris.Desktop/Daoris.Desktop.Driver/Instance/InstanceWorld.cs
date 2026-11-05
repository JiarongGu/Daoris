using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where a verb looks for the running Daoris, and what it says while it waits: the process's own, or a test's. It is the terminal the
/// settings' command is said on and pressed from (<see cref="IChoiceTerminal"/>), and the tools' (<see cref="IToolTerminal"/>), pressing
/// through the door.
/// </summary>
public sealed class InstanceWorld : IToolTerminal
{
    /// <summary>The home the verb acts on, or null where it has none.</summary>
    public required string? Home { get; init; }

    /// <summary>The variables, read by name.</summary>
    public Func<string, string?> Environment { get; init; } = System.Environment.GetEnvironmentVariable;

    /// <summary>Where the program runs from, which says whether an install carries it.</summary>
    public string BaseDirectory { get; init; } = AppContext.BaseDirectory;

    /// <summary>What an address's status door answers as a Daoris host (<see cref="RunningService.Status"/>), or a test's.</summary>
    public Func<string, ServiceStatus?> Status { get; init; } = url => RunningService.Status(url);

    /// <summary>Where the answer and the progress are said.</summary>
    public TextWriter Out { get; init; } = Console.Out;

    /// <summary>Where a refusal is said.</summary>
    public TextWriter Error { get; init; } = Console.Error;

    /// <summary>What the person types or pipes in: <c>agent key</c>'s key, never an argument.</summary>
    public TextReader In { get; init; } = Console.In;

    /// <summary>The verb, as its sentences begin (<c>trees</c>).</summary>
    public string Verb { get; init; } = "daoris";

    /// <summary>How long the pipe is given to connect and answer the hello (§7.2: five seconds).</summary>
    public TimeSpan Answer { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a press is waited on before its answer is asked for by <c>RESULT</c>: <c>--wait &lt;minutes&gt;</c>, which the host's
    /// entry takes (the confirm-wait design §4.1). Zero is <c>--no-wait</c>: the verb returns once the instance says the
    /// press waits for the window (<c>CONFIRMING</c>), and a press that asks no window, or a read, is waited on as long as by default.
    /// </summary>
    public TimeSpan Wait { get; init; } = InstanceDoor.LongestWait;

    /// <summary>The bound a request is waited on within: <see cref="Wait"/>, or the default where it is <c>--no-wait</c>'s zero.</summary>
    public TimeSpan Within => Wait > TimeSpan.Zero ? Wait : InstanceDoor.LongestWait;

    /// <summary>
    /// The SIDs whose pipe this terminal takes as its user's: <see cref="InstanceClient.ThisUser"/>'s where null (PIPEOWNER1), or a
    /// test's, which needs no second elevation to meet a pipe it does not take.
    /// </summary>
    public IReadOnlySet<string>? Owners { get; init; }

    /// <summary>
    /// A check of the process serving the pipe, read from the pipe itself, made before anything is sent (SUPERVISOR1a-ii, D208 §2.2):
    /// false refuses it (<see cref="InstanceDoor.NotInstall"/>). Null checks nothing, as every verb but the supervisor's answer.
    /// </summary>
    public Func<int?, bool>? ServedBy { get; init; }

    /// <summary>
    /// Whether this verb runs in a role's terminal (D209 §3.3): the entry's marker, <see cref="TerminalRole.Held"/>, unless a test
    /// names its own. Such a verb sends no person's press and acts nothing alone (<see cref="TerminalRole"/>).
    /// </summary>
    public Func<bool> RoleTerminal { get; init; } = () => TerminalRole.Held;

    /// <summary>A person's press sent through <see cref="InstanceDoor.PressAsync"/> from this terminal.</summary>
    public Task<int> PressAsync(InstanceRoute route, object? payload, Func<Task<int>> alone, Func<JsonElement, int> words, CancellationToken ct) =>
        InstanceDoor.PressAsync(route, payload, alone, words, this, ct);
}
