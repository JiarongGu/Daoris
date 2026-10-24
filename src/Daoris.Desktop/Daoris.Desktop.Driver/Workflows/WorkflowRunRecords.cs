namespace Daoris.Driver;

/// <summary>
/// What a run's read reads under a home beside the service (WORKFLOW1c, the workflow design §5.1): the landing record, the due list,
/// each session's conversation record for its acceptance, and whether a tree stands on this machine. The reads behind
/// <see cref="IWorkflowRunRecords"/> (STRUCT3b, D166).
/// </summary>
public sealed class WorkflowRunRecords(string home) : IWorkflowRunRecords
{
    /// <summary>The landing record's entries.</summary>
    public IReadOnlyList<LandedBranch> Landings() => new LandedBranches(home).Entries();

    /// <summary>The automatic landings due and closed.</summary>
    public IReadOnlyList<AutoLanding> AutoLandings() => new AutoLandings(home).All();

    /// <summary>
    /// When each session's work was accepted into its line (D100): the newest acceptance its conversation's record keeps, read
    /// from the writer's own words (<see cref="LandingRules.IsAcceptance"/>). A teammate's record is on its machine.
    /// </summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Acceptances(IReadOnlyList<TracedSession> sessions)
    {
        var accepted = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
        foreach (var session in sessions.Where(session => !session.Teammate && SessionEventText.IsId(session.Id)))
        {
            try
            {
                if (!File.Exists(events.PathOf(session.Id))) continue;
                var notes = events.After(session.Id, 0).Events
                    .Where(note => note.Kind == SessionEventKind.Note && note.Text is { } text && LandingRules.IsAcceptance(text))
                    .ToList();
                if (notes.Count > 0) accepted[session.Id] = notes[^1].At;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
            {
                // A record that does not read keeps no acceptance: the landing then reads as not yet made, never as made.
            }
        }

        return accepted;
    }

    /// <summary>Whether a session's tree still stands on this machine.</summary>
    public bool Stands(string tree) => Directory.Exists(tree);

    /// <summary>Whether a tree is one this home opened, standing or not.</summary>
    public bool Holds(string tree) => new SessionTrees(home).Holds(tree);

    /// <summary>A run's node record, read as the runtime reads it and never written: this read holds no lease, so it edits nothing.</summary>
    public NodeRecord? Record(string unit) => new NodeRecordFiles(home, () => false).Read(unit);

    /// <summary>This machine's session branches, standing and traces.</summary>
    public IReadOnlyList<GrownBranch> Branches() => new SessionBranches(home).Entries();

    /// <summary>Whether a run is bound here.</summary>
    public bool Bound(string run) => WorkflowRunBindings.Bound(home, run);

    /// <summary>Whether a live conversation is between turns, by what its runner wrote as each turn ended and each word was said.</summary>
    public bool Between(string session)
    {
        try
        {
            return SessionEventText.IsId(session) && new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)).TurnEnded(session);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A record that does not read says no turn ended: the work reads as under way, never as waiting on the person.
            return false;
        }
    }

    /// <summary>A chat's tree's <c>HEAD</c> and its commits beyond the line, as the review's offer counts them.</summary>
    public async Task<ChatTreeTip?> TipAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var head = await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false);
        var work = await new SessionTrees(home).WorkAsync(tree, ct).ConfigureAwait(false);
        int? beyond = work is null || work.Commits is null ? null : LandOffer.Of(tree, work)?.Commits ?? 0;
        return new ChatTreeTip(head, beyond);
    }
}
