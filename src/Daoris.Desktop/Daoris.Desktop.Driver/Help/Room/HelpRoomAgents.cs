using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The agents (HELP1a): each installed or not, and which of Daoris's accounts are signed in, by the tool's own
/// answer — an account by its name, never its key (AGT3). One not installed is said, not dropped.
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
                Accounts = [.. report.Profiles.Select(profile => new HelpAccount(profile.Name, Spell(profile.Login)))],
                Strays = report.Strays,
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
        // Only Daoris's accounts are signed in for Daoris (D175): the tool's own sign-in is not said here, and a start
        // waits for a sign-in when no account is, as the roster's walk holds it (OWNSPACE1b-C).
        var accounts = string.Join("; ", agent.Accounts.Select(account => $"account `{account.Name}` {Login(account.Login)}"));
        var noneSignedIn = agent.Accounts.All(account => account.Login == "out");
        if (accounts.Length > 0) line += accounts;
        if (noneSignedIn) line += (accounts.Length > 0 ? "; " : "") + "no Daoris account is signed in, so its starts wait for a sign-in";
        // HELPSTUCK1: a folder beside the accounts that is no account, said so the helper can answer a person who misses one.
        foreach (var stray in agent.Strays) line += (line.EndsWith(": ", StringComparison.Ordinal) ? "" : "; ") + StrayLine(stray);
        return line;
    }

    /// <summary>A folder beside the accounts that is no account (HELPSTUCK1), with the way to keep a sign-in it holds.</summary>
    private static string StrayLine(StrayFolder stray)
    {
        var kind = stray.Name.StartsWith('.') ? "a hidden folder's name" : "a lock's name";
        return stray.SignedIn
            ? $"the folder `{stray.Name}` beside its accounts holds a sign-in but is {kind}, so it is no account: rename it to an "
              + "account's name while nothing runs on it, and it is one again"
            : $"the folder `{stray.Name}` beside its accounts is {kind}, so it is no account";
    }

    private static string Login(string login) => login switch
    {
        "in" => "signed in",
        "out" => "signed out",
        _ => "sign-in not known",
    };
}

/// <summary>One agent as the room tells it: installed or not, and which of Daoris's accounts are signed in.</summary>
/// <param name="Name">The harness's name, as `daoris driver` takes it.</param>
public sealed record HelpAgent(string Name)
{
    /// <summary>What a person calls the tool, where it says.</summary>
    public string? Product { get; init; }

    public bool Present { get; init; }

    public string? Version { get; init; }

    public IReadOnlyList<HelpAccount> Accounts { get; init; } = [];

    /// <summary>The folders beside its accounts that are no account (HELPSTUCK1), each with whether it holds a sign-in.</summary>
    public IReadOnlyList<StrayFolder> Strays { get; init; } = [];
}

/// <summary>An account Daoris keeps for an agent, by its name — never its key (AGT3).</summary>
public sealed record HelpAccount(string Name, string Login);
