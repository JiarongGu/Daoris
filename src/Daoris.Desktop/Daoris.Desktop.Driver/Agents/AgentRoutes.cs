using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The routes <c>daoris agent</c> sends while Daoris runs, each the window twin of the
/// change it makes, the verbs no route takes, each with why it acts alone, and which home each
/// writes: the CLI's <c>AGENT_VERBS</c> rows for these verbs. Held to the census by <c>PipeCensusTests</c>.
/// </summary>
public static class AgentRoutes
{
    private static InstanceRoute Person(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Person);

    /// <summary><c>agent rules allow|ask|deny|remove|default</c>: the rules place's change.</summary>
    public static InstanceRoute Rule { get; } = Person("RULE_ACTION");

    /// <summary><c>agent rules accept|decline &lt;id&gt;</c>: the person's answer to an agent's proposal.</summary>
    public static InstanceRoute Proposal { get; } = Person("RULE_PROPOSAL");

    /// <summary><c>agent settings &lt;agent&gt; model|effort …</c>: an account's own model and effort.</summary>
    public static InstanceRoute Settings { get; } = Person("SET_AGENT_SETTINGS");

    /// <summary><c>agent unpin</c> and <c>agent profile add|remove|default|rename|join</c>: the Agents place's own actions.</summary>
    public static InstanceRoute Harness { get; } = Person("HARNESS_ACTION");

    /// <summary><c>agent profile order|ready|use</c>: the Accounts place's order, <i>Try now</i> and how a list is used.</summary>
    public static InstanceRoute Account { get; } = Person("ACCOUNT_USE");

    /// <summary>
    /// Each verb that changes a home, in the verb's words: the route it sends while Daoris runs, or null with why it acts alone, and which
    /// home it writes.
    /// </summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason, AgentHome Home)> Verbs { get; } =
    [
        ("agent rules allow|ask|deny|remove|default", Rule, "the rules place's change", AgentHome.Daoris),
        ("agent rules accept|decline <id>", Proposal, "the person's answer to an agent's proposal, the only way a widening applies", AgentHome.Daoris),
        ("agent settings <agent> model|effort …", Settings, "an account's own model and effort, in the tool's own settings file", AgentHome.Wiring),
        ("agent trust <agent> <folder> --yes", null,
            "alone: the window's TRUST_FOLDER grants only a folder a hold names, and this names any folder", AgentHome.Wiring),
        ("agent profile add|remove|default|rename|join", Harness, "the Accounts place's `profile-*` actions", AgentHome.Wiring),
        ("agent unpin <agent>", Harness, "the machine's pin taken back", AgentHome.Wiring),
        ("agent profile order|ready", Account, "the Accounts place's order and *Try now*", AgentHome.Wiring),
        ("agent profile use <agent> …", Account, "the Accounts place's *How accounts are used*", AgentHome.Wiring),
        ("agent unpin <agent> --workspace <name>", null, "alone: HARNESS_ACTION's unpin names no workspace", AgentHome.Wiring),
        ("agent install <agent>", null,
            "alone: the agent's own installer, run at this terminal with its streams, writes the machine and not the home", AgentHome.Wiring),
        ("agent key <agent> [--account <account>]", null,
            "alone: the key is read from this terminal's standard input, and no press carries a key across the pipe", AgentHome.Wiring),
        ("agent login <agent> [--new|--profile <account>] …", null,
            "alone: the agent's own sign-in runs at this terminal, the person answering it; its end is published under the lease", AgentHome.Wiring),
        ("agent pin <agent> <version> [--workspace <name>]", null,
            "alone: a maker's release is downloaded here, or npm installs it at this terminal; the pin is published under the lease", AgentHome.Wiring),
        ("agent update <agent> [--workspace <name>]", null,
            "alone: a pin's move is a pin, published under the lease; unpinned, the agent's own updater runs at this terminal and writes the machine",
            AgentHome.Wiring),
    ];

    /// <summary>The home a verb of this program's writes: its row's, by its first word.</summary>
    public static AgentHome HomeOf(string verb) =>
        Verbs.First(row => row.Verb.Split(' ')[1] == verb).Home;
}
