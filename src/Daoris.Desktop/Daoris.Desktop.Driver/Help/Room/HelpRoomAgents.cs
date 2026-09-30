using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The agents (HELP1a): each installed or not, and who is signed in, by the tool's own answer — an account
/// by its name, never its key (AGT3). One not installed is said, not dropped.
/// </summary>
internal sealed class HelpRoomAgents : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources)
    {
        static string Spell(LoginState login) => login.ToString().ToLowerInvariant();
        return machine with
        {
            Agents = [.. sources.Roster.Select(report => new HelpAgent(report.Adapter)
            {
                Product = sources.Product(report.Adapter),
                Present = report.Present,
                Version = report.Version,
                Login = Spell(report.OwnLogin),
                Accounts = [.. report.Profiles.Select(profile => new HelpAccount(profile.Name, Spell(profile.Login)))],
            })],
        };
    }

    public string Render(HelpMachine machine)
    {
        if (machine.Agents.Count == 0) return "";

        var text = new StringBuilder();
        text.Append("### Agents\n\n");
        foreach (var agent in machine.Agents) text.Append($"- {AgentLine(agent)}\n");
        text.Append('\n');
        return text.ToString();
    }

    private static string AgentLine(HelpAgent agent)
    {
        if (!agent.Present) return $"`{agent.Name}`: not installed";

        var named = string.Join(" ", new[] { agent.Product, agent.Version }.Where(part => part is { Length: > 0 }));
        var line = named.Length > 0 ? $"`{agent.Name}` ({named}): " : $"`{agent.Name}`: ";
        line += Login(agent.Login);
        foreach (var account in agent.Accounts) line += $"; account `{account.Name}` {Login(account.Login)}";
        return line;
    }

    private static string Login(string login) => login switch
    {
        "in" => "signed in",
        "out" => "signed out",
        _ => "sign-in not known",
    };
}

/// <summary>One agent as the room tells it: installed or not, and who is signed in, by the tool's own answer.</summary>
/// <param name="Name">The harness's name, as `daoris driver` takes it.</param>
public sealed record HelpAgent(string Name)
{
    /// <summary>What a person calls the tool, where it says.</summary>
    public string? Product { get; init; }

    public bool Present { get; init; }

    public string? Version { get; init; }

    /// <summary>The tool's own home's sign-in: `in`, `out` or `unknown`.</summary>
    public string Login { get; init; } = "unknown";

    public IReadOnlyList<HelpAccount> Accounts { get; init; } = [];
}

/// <summary>An account Daoris keeps for an agent, by its name — never its key (AGT3).</summary>
public sealed record HelpAccount(string Name, string Login);

public sealed partial record HelpMachine
{
    public IReadOnlyList<HelpAgent> Agents { get; init; } = [];
}
