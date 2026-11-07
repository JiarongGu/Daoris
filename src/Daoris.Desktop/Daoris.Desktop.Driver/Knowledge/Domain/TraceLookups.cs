namespace Daoris.Driver;

/// <summary>The small reads every part of the chain shares: a record by its id, ids and paths compared as the stores spell them.</summary>
internal static class TraceLookups
{
    internal static TracedQuest? QuestOf(TraceFacts facts, string id) => facts.Quests.FirstOrDefault(quest => Same(quest.Id, id));

    internal static TracedSession? SessionOf(TraceFacts facts, string id) => facts.Sessions.FirstOrDefault(session => Same(session.Id, id));

    internal static bool Same(string? a, string? b) => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    internal static bool SamePath(string a, string b) =>
        string.Equals(a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>A tree's last segment: the name Daoris gave it (D51 §2), never the path, which is machine-local.</summary>
    internal static string? FolderOf(string? tree)
    {
        if (tree is null) return null;
        var trimmed = tree.Replace('\\', '/').TrimEnd('/');
        var name = trimmed[(trimmed.LastIndexOf('/') + 1)..];
        return name.Length == 0 ? null : name;
    }

    /// <summary>A record's state, with what its flags add to it.</summary>
    internal static TraceState StateOf(TracedSession session) => new(session.State)
    {
        Limit = session.State == "failed" && session.Limit,
        Interrupted = session.State == "stopped" && session.Interrupted,
        Answered = session.State == "awaiting-person" && session.Answer is not null,
    };

    /// <summary>What ran it, as its record names it.</summary>
    internal static TraceAgent AgentOf(TracedSession session) => new()
    {
        Door = session.Door,
        DoorVersion = session.DoorVersion,
        Account = session.Teammate ? null : session.Profile,
        Teammate = session.Teammate,
    };
}
