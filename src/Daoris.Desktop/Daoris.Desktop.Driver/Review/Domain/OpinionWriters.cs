namespace Daoris.Driver;

/// <summary>
/// The agents that wrote the work a second opinion reads (D155 point 4, the second-agent design §3.1), as the reviewer's walk judges
/// its independence against them (<see cref="ReviewerChoice.ChooseAsync"/>): every agent the line ran on, never only its last
/// part's (LIMITRESUME1j, D206; the limit-resume design §3.6). A line cut off by a limit on one maker's agent and carried on by
/// another's has two makers, and a reviewer of either is the work's own family.
/// </summary>
/// <remarks>
/// Read from this machine's records, as the brief follows a line (<see cref="ContextBriefs.LineOf"/>): the candidate's line by each
/// record's <c>from</c>, and every record on the chain's quests here with its own line, so a part that names no <c>from</c> is
/// still counted. A stand-down ran nothing of the work, so it names no agent, as it is no run (<see cref="ServiceClient.ReadLastRun"/>).
/// </remarks>
internal static class OpinionWriters
{
    /// <summary>
    /// The adapters of every part of <paramref name="candidate"/>'s line and of every record on <paramref name="quests"/>, each once:
    /// the candidate's line oldest first, then the quests' records by when each was made.
    /// </summary>
    /// <param name="records">This machine's records by id (<see cref="ServiceClient.ReadRecords"/>).</param>
    /// <param name="candidate">The working session whose work is read.</param>
    /// <param name="quests">The chain's quests in the repository read; none for work that serves no quest.</param>
    public static IReadOnlyList<string> Agents(
        IReadOnlyDictionary<string, PriorSession> records, string candidate, IEnumerable<string> quests)
    {
        var asked = new HashSet<string>(quests, StringComparer.OrdinalIgnoreCase);
        var seeds = records.Values
            .Where(record => record.Quest is { } quest && asked.Contains(quest)
                             && !string.Equals(record.State, "stood-down", StringComparison.OrdinalIgnoreCase))
            .OrderBy(record => record.Created ?? DateTimeOffset.MinValue)
            .Select(record => record.Session)
            .Prepend(candidate);

        var agents = new List<string>();
        foreach (var part in seeds.SelectMany(seed => ContextBriefs.LineOf(records, seed)))
        {
            if (part.Record.Adapter?.Trim() is { Length: > 0 } adapter && !agents.Contains(adapter, StringComparer.OrdinalIgnoreCase))
            {
                agents.Add(adapter);
            }
        }

        return agents;
    }

    /// <summary>This machine's records by id, the first of an id kept, as <see cref="ServiceClient.ReadRecords"/> keeps them.</summary>
    public static IReadOnlyDictionary<string, PriorSession> ById(IEnumerable<PriorSession> records)
    {
        var byId = new Dictionary<string, PriorSession>(StringComparer.Ordinal);
        foreach (var record in records) byId.TryAdd(record.Session, record);
        return byId;
    }
}
