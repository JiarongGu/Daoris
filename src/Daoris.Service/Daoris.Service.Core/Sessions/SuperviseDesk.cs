namespace Daoris.Knowledge;

/// <summary>What a supervisor's turn's record is opened with: the attempt, its role, its place, the session it serves, its agent and copy.</summary>
public sealed record SuperviseOpen(string Decides, string Role, string Repository, string? Workspace, string? Serves, string Door, string Tree)
{
    public string? Account { get; init; }

    public string? DoorVersion { get; init; }

    /// <summary>A machine subject's turn: it names no repository and no workspace, so it takes no workspace's staffing or read scope.</summary>
    public bool Machine { get; init; }
}

/// <summary>
/// The local host's door for a supervisor's turn's record: a chat on no quest marked <c>decides</c> with its attempt, which the work's
/// rule reads before every other mark, so it is served the reading tools alone and holds no place under the cap. Its <c>serves</c> names
/// the session the subject waits on, where there is one, so its record says whose decision it reads for.
/// </summary>
public sealed class SuperviseDesk(SessionStore sessions, bool local = true)
{
    /// <summary>
    /// Opens the record, or why not: a shared host's refusal, a blank part, a machine subject naming a place, or a copy another active
    /// session holds.
    /// </summary>
    public async Task<HelperOutcome> OpenAsync(SuperviseOpen open, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return new(HelperRefusal.Shared, "A supervisor's turn reads a copy this machine made, and is opened only on this machine.");
        if (string.IsNullOrWhiteSpace(open.Decides) || string.IsNullOrWhiteSpace(open.Role) || string.IsNullOrWhiteSpace(open.Door)
            || (!open.Machine && string.IsNullOrWhiteSpace(open.Repository)) || Trees.Normalize(open.Tree) is not { } tree)
        {
            return new(HelperRefusal.BadShape, "A supervisor's turn needs the attempt it decides for, its role, its place, its agent and its own copy.");
        }

        if (open.Machine && (!string.IsNullOrWhiteSpace(open.Repository) || !string.IsNullOrWhiteSpace(open.Workspace)))
        {
            return new(HelperRefusal.BadShape, "A machine subject's turn names no repository and no workspace.");
        }

        var (repository, workspace) = open.Machine ? ("", null) : (open.Repository, open.Workspace);
        return await sessions.ExclusiveAsync(async inside =>
        {
            if (await sessions.ActiveForAsync(repository, tree, inside).ConfigureAwait(false) is { } holder)
            {
                return new HelperOutcome(HelperRefusal.Busy, $"`{tree}` already has an active session, `{holder.Id}`: a supervisor's copy is its own.");
            }

            var turn = await sessions.CreateAsync(
                    quest: null, repository, open.Door.Trim(), now, workspace, SessionKind.Chat, doorVersion: open.DoorVersion,
                    profile: open.Account, tree: tree, ct: inside, serves: open.Serves, ownEntry: true, actor: open.Role.Trim(),
                    decides: open.Decides.Trim())
                .ConfigureAwait(false);
            return new HelperOutcome(HelperRefusal.None, $"Session `{turn.Id}` reads a copy of its own to decide for attempt `{open.Decides.Trim()}`.")
            {
                Session = turn,
            };
        }, ct).ConfigureAwait(false);
    }
}
