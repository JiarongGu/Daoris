namespace Daoris.Knowledge;

/// <summary>What a step's session's record is opened with: the attempt it performs, the run's place and its agent.</summary>
public sealed record StepSessionOpen(string Performs, string Repository, string? Workspace, string Door)
{
    public string? Account { get; init; }

    public string? DoorVersion { get; init; }
}

/// <summary>
/// The local host's door for the record of the session that performs a plugin's step: a chat on no quest at the run's place, with no tree
/// of its own, marked <c>performs</c> with the step's attempt. The mark keeps it out of the place's lease and its active sessions, since it
/// works the plugin's platform and never the place's files, and the work's rule reads it as the step's, with no place under the cap.
/// </summary>
public sealed class StepSessionDesk(SessionStore sessions, bool local = true)
{
    /// <summary>Opens the record, or why not: a shared host's refusal, or a blank part.</summary>
    public async Task<HelperOutcome> OpenAsync(StepSessionOpen open, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return new(HelperRefusal.Shared, "A step's session works on this machine, and its record is opened only on this machine.");
        if (string.IsNullOrWhiteSpace(open.Performs) || string.IsNullOrWhiteSpace(open.Repository) || string.IsNullOrWhiteSpace(open.Door))
        {
            return new(HelperRefusal.BadShape, "A step's session needs the attempt it performs, the run's repository and its agent.");
        }

        var step = await sessions.CreateAsync(
                quest: null, open.Repository.Trim(), open.Door.Trim(), now, open.Workspace, SessionKind.Chat, doorVersion: open.DoorVersion,
                profile: open.Account, ct: ct, ownEntry: true, performs: open.Performs.Trim())
            .ConfigureAwait(false);
        return new HelperOutcome(HelperRefusal.None, $"Session `{step.Id}` performs the step's attempt `{open.Performs.Trim()}`.") { Session = step };
    }
}
