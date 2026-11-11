namespace Daoris.Driver;

/// <summary>
/// The part of a chain the person's changes on its review go to, named by its quest: kept on the run's record as a mark of the rejected
/// attempt, as a request's send is, so the pass tells that part's session, whichever way the rejection is routed. Unnamed, or naming no
/// part the run holds, the changes go to the newest part.
/// </summary>
public static class ChangesParts
{
    private const string Prefix = "changes-part:";

    /// <summary>The record's mark of <paramref name="node"/>'s changes on <paramref name="attempt"/> going to <paramref name="part"/>.</summary>
    public static string Key(string node, int attempt, string part) => $"{Prefix}{node}:{attempt}:{part}";

    /// <summary>The part <paramref name="record"/> says the changes of <paramref name="node"/> on <paramref name="attempt"/> go to; null where none is named.</summary>
    public static string? Of(NodeRecord? record, string node, int attempt)
    {
        var lead = $"{Prefix}{node}:{attempt}:";
        return record?.Fired.LastOrDefault(mark => mark.StartsWith(lead, StringComparison.Ordinal)) is { } mark ? mark[lead.Length..] : null;
    }

    /// <summary>The session that made <paramref name="part"/> in <paramref name="scope"/>'s chain, its handback's; null where the run holds no such part.</summary>
    public static string? SessionOf(RunScope scope, string? part) =>
        part is null ? null : scope.Quests.FirstOrDefault(quest => string.Equals(quest.Id, part.Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase))?.Submission?.Session;
}
