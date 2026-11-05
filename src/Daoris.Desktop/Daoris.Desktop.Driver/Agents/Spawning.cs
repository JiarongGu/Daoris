namespace Daoris.Driver;

/// <summary>
/// Daoris's own variables on a spawn (the agent-provider design §4.6), a list and never a prefix: the identity variables that say whose
/// work a start serves, which the shell (<see cref="Shell"/>) sets, and the session git's, which their owner answers.
/// </summary>
internal static class Spawning
{
    /// <summary>
    /// The variables that say which work a start serves, each set by the start itself for its role (ENVLEAK1): a quest's, an
    /// ask's, a brainstorm's, its own record's, and the repository and service it is the agent for.
    /// </summary>
    internal static readonly IReadOnlyList<string> Roles =
    [
        "DAORIS_QUEST_ID", "DAORIS_QUEST_TITLE", "DAORIS_QUEST_BODY", "DAORIS_QUEST_ASKER", "DAORIS_QUEST_ATTACHMENTS",
        "DAORIS_TARGET", AgentWorkVariables.Ask, ChildMarks.SessionVariable, ChildMarks.LineageProcVariable,
        AgentWorkVariables.Brainstorm, AgentWorkVariables.Turn, AgentWorkVariables.Answer, AgentWorkVariables.Room,
        "DAORIS_REPOSITORY", "DAORIS_SERVICE_URL",
    ];

    /// <summary>
    /// Whether <paramref name="name"/> is Daoris's own: an identity variable, or one the session's git sets by its name or its numbered
    /// family. The session's tokens are not: Daoris only clears them, so a provider may declare one as its key. Asks the installed
    /// <see cref="SessionGit"/>, which throws where none is, so no check passes a git name for want of its answer.
    /// </summary>
    internal static bool Daoris(string name)
    {
        var git = SessionGit.Current;
        if (git.Tokens.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        return Roles.Contains(name, StringComparer.OrdinalIgnoreCase)
            || git.Names.Contains(name, StringComparer.OrdinalIgnoreCase)
            || git.Families.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A start begins from the driver's own environment, and drops every role variable it inherits (ENVLEAK1): a driver run
    /// from inside a session Daoris drives — a test, a rehearsal — carries that session's quest, target and record, and a chat
    /// that inherited them would serve a quest it was never given, absent being the rule. Its own role is set after.
    /// </summary>
    internal static void Unrole(IDictionary<string, string?> environment)
    {
        foreach (var name in Roles) environment.Remove(name);
    }
}
