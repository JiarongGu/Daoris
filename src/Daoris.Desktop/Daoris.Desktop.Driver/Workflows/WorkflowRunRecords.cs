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
}
