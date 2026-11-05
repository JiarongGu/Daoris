using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where <c>daoris agent</c> acts (PUSH1f2d): the terminal it says its words on and finds the running Daoris from, and what a
/// verb reads the machine by: the folder a relative folder is named from, the person's own profile folder an agent's own home falls
/// back to, the agents this machine runs, and the moment an answer is settled at. A test hands its own of each.
/// </summary>
public sealed record AgentsWorld
{
    /// <summary>The terminal: its home (<c>DAORIS_HOME</c>, null where none is named), its variables, its streams, and where it finds the instance.</summary>
    public required InstanceWorld Instance { get; init; }

    /// <summary>The folder the terminal is in: a folder <c>trust</c> names by a relative path is this one's.</summary>
    public string Folder { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>The person's own profile folder: an agent's own configuration home where it names none of its own.</summary>
    public string UserHome { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The agents this machine runs, read from a home: the build's and those its plugins declare (D64), as the window's roster reads them.</summary>
    public Func<string, AdapterSet> Adapters { get; init; } = home =>
    {
        var built = AdapterSet.Built();
        return built.WithPlugins(AgentPluginReads.Current.Load(home, built.Names));
    };

    /// <summary>The moment the person's answer to a proposal is settled at.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The machine's zone, a cool-off's end and a window's reset are said in (<c>profile use</c>), named.</summary>
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;

    /// <summary>Called once a long verb's publication finds another act holding the home (<c>key</c>): a test's sign that it waits.</summary>
    public Action? Waiting { get; init; }

    /// <summary>How a browser sign-in's port is tried before its flow starts (<c>login</c>, CODEXLOGIN1): taken for a moment, unless a test's.</summary>
    public Func<int, PortHold> Ports { get; init; } = SignInPorts.Hold;

    /// <summary>How a maker's release channel is reached (<c>pin</c>, <c>update</c>): the network, unless a test holds its own.</summary>
    public HttpMessageHandler? Transport { get; init; }

    /// <summary>The package manager a package pin asks and installs with (<c>pin</c>, <c>update</c>): npm, unless a test holds a stand-in.</summary>
    public IReadOnlyList<string> Npm { get; init; } = ["npm"];
}
