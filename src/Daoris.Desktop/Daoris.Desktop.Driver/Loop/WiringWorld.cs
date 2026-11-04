using System.Reflection;

namespace Daoris.Driver;

/// <summary>
/// What the wiring read is handed (WIRING1b): every fact of the program that asks, each injectable, so a test reads no
/// machine's environment, account or home but its own.
/// </summary>
public sealed record WiringWorld : IToolHomeWorld
{
    /// <summary>The home this program runs on; null where it has none.</summary>
    public string? Home { get; init; }

    /// <summary>The folder the asking program runs from, which says whether an install carries it.</summary>
    public required string BaseDirectory { get; init; }

    /// <summary>This process's variables.</summary>
    public required Func<string, string?> Process { get; init; }

    /// <summary>The account's variables, which a new terminal gets; null where the account keeps none (off Windows).</summary>
    public Func<string, string?>? Account { get; init; }

    /// <summary>The machine's variables; null where the machine keeps none.</summary>
    public Func<string, string?>? Machine { get; init; }

    /// <summary>What only the window knows; null for a terminal.</summary>
    public WindowWiring? Window { get; init; }

    /// <summary>
    /// How a terminal asks an address's status door; null asks nothing. 🔴 The window is handed none: its service's status is its
    /// supervisor's last probe, and the page's open asks no network, starts no program and probes no git (the second opinion).
    /// </summary>
    public Func<string, ServiceProbe?>? AskStatus { get; init; }

    /// <summary>Whether the process an address file names still runs as the process it named.</summary>
    public Func<int, long, bool> Lives { get; init; } = RunningService.Lives;

    /// <summary>The sizes measured, kept for the process.</summary>
    public WiringSizes Sizes { get; init; } = WiringSizes.Shared;

    /// <summary>The person's own tool home (OWNSPACE2); null where none is named: a world made by hand reads no machine's, a test names its scratch one, and the real worlds are handed <see cref="ToolHomeLeftovers.OwnHome(IToolHomeWorld)"/>.</summary>
    public string? ToolHome { get; init; }

    /// <summary>Where <see cref="ToolHome"/> was read from: <c>process</c>, <c>account</c> or <c>default</c> as the environment gave it, <c>named</c> where a caller named it.</summary>
    public string ToolHomeFrom { get; init; } = "named";

    /// <summary>Why no tool home is named though the environment named one: <c>relative</c> or <c>inside-daoris</c>; null otherwise.</summary>
    public string? ToolHomeWhy { get; init; }

    /// <summary>
    /// The working directories Daoris's own records hold (<see cref="ToolHomeLeftovers.Origins"/>), read by the window's route
    /// from the service's records; null where none was read, which lists the rooms with every folder kept <c>records-unread</c>.
    /// </summary>
    public IReadOnlyList<ToolOrigin>? ToolOrigins { get; init; }

    /// <summary>The asking terminal's own version, where it is the driver's verbs.</summary>
    public string? DriverVersion { get; init; }

    /// <summary>A terminal's world: this process, its account and machine, its status door, its own version.</summary>
    public static WiringWorld OfThisTerminal(string? home)
    {
        var account = Stored(EnvironmentVariableTarget.User);
        var (tool, from, toolWhy) = ToolHomeLeftovers.OwnHome(
            Environment.GetEnvironmentVariable, account, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), home);
        return new()
        {
            Home = home,
            BaseDirectory = AppContext.BaseDirectory,
            Process = Environment.GetEnvironmentVariable,
            Account = account,
            Machine = Stored(EnvironmentVariableTarget.Machine),
            AskStatus = url => ServiceProbe.Ask(url),
            DriverVersion = SystemWiring.VersionOf(Assembly.GetEntryAssembly()),
            ToolHome = tool,
            ToolHomeFrom = from,
            ToolHomeWhy = toolWhy,
        };
    }

    /// <summary>The account's or the machine's stored variables, on Windows, where they are kept; null elsewhere.</summary>
    public static Func<string, string?>? Stored(EnvironmentVariableTarget target) =>
        OperatingSystem.IsWindows() ? name => Environment.GetEnvironmentVariable(name, target) : null;
}
