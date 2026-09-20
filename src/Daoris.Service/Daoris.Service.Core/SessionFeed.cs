namespace Daoris.Knowledge;

/// <summary>One record as a feed carries it — already judged where the process lived (D47 §6).</summary>
public sealed record FedSessionRecord(
    string? Id, string? Quest, string? Repository, string? Adapter, string? State,
    string? Note, string? Evidence, DateTimeOffset Created, DateTimeOffset Updated);

/// <summary>Why a feed of records was not taken — or <see cref="None"/> when it was.</summary>
public enum SessionFeedRefusal
{
    None,

    /// <summary>A record missing what a record is — id, quest, repository, a known state.</summary>
    Malformed,

    /// <summary>A record for a repository that has not joined this deployment.</summary>
    NotJoined,
}

/// <param name="Refusal"><see cref="SessionFeedRefusal.None"/> when the records were mirrored.</param>
/// <param name="Message">The full answer, phrased once here so no two doors can drift on it.</param>
/// <param name="Records">How many records were mirrored, when accepted.</param>
public sealed record SessionFeedOutcome(SessionFeedRefusal Refusal, string Message, int Records);

/// <summary>
/// The session feed's judgement (D47 §4/§6), in Core for the same reason <see cref="SessionLedger"/>
/// and <see cref="KnowledgeService.FeedAsync"/> are: the entries door already judged in Core while
/// this judgement sat in one host — the exact asymmetry D36 exists to prevent, one new door away from
/// a second copy. Records upsert whole, keyed by origin + id, and are never re-judged: the judgement
/// ran where the process lived. Only joined repositories' records are taken.
/// </summary>
public sealed class SessionFeed(KnowledgeService service, SessionStore sessions)
{
    public async Task<SessionFeedOutcome> FeedAsync(
        string origin, IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default)
    {
        var joined = (await service.RegistryAsync(ct).ConfigureAwait(false))
            .Where(r => r.Joined).Select(r => r.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Judge everything before mirroring anything, so a refused feed changes nothing at all.
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Id) || string.IsNullOrWhiteSpace(record.Quest)
                || string.IsNullOrWhiteSpace(record.Repository) || !Session.TryParse(record.State ?? "", out _))
            {
                return new(
                    SessionFeedRefusal.Malformed,
                    $"record `{record.Id}` is not a session record — id, quest, repository and a known state are required",
                    Records: 0);
            }

            if (!joined.Contains(record.Repository))
            {
                return new(
                    SessionFeedRefusal.NotJoined,
                    $"`{record.Repository}` has not joined this deployment — the manifest's `remote.join` "
                    + "is the declaration that admits its records, and silence means local.",
                    Records: 0);
            }
        }

        foreach (var record in records)
        {
            Session.TryParse(record.State!, out var state);
            await sessions.MirrorAsync(new Session(
                $"{origin}/{record.Id}", record.Quest!, record.Repository!, record.Adapter ?? "unknown",
                state, record.Note, record.Evidence, Transcript: null, record.Created, record.Updated), ct)
                .ConfigureAwait(false);
        }

        return new(
            SessionFeedRefusal.None,
            $"{records.Count} session record(s) mirrored from `{origin}`.",
            records.Count);
    }
}
