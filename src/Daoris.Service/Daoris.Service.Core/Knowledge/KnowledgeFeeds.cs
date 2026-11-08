namespace Daoris.Knowledge;

/// <summary>
/// What a shared deployment takes from a checkout's feed: its declaration, its knowledge and its code map, each judged by the
/// same door and ordered by the commit it is read at.
/// </summary>
internal sealed class KnowledgeFeeds(
    IKnowledgeStore store,
    RegistrationStore? registrations,
    Func<CancellationToken, Task<IReadOnlyList<Registration>>> registered,
    Func<Registration, DateTimeOffset, CancellationToken, Task<Registration>> register,
    Action refuseNewerIndex,
    TimeProvider time,
    SemaphoreSlim feedGate)
{
    /// <summary>One feed judged and written at a time, a retirement taking the same turn: the check and the write are one step.</summary>
    private readonly SemaphoreSlim _feedGate = feedGate;

    /// <summary>
    /// A declaration arriving at a shared deployment from a checkout: the manifest at a commit,
    /// ordered as a feed is, so two checkouts of one repository stop overwriting each other.
    /// </summary>
    /// <remarks>
    /// Three rules are the registration's own. The first is taken from any line and without a commit,
    /// because nothing else of a repository can travel until it is registered. After that, a
    /// declaration is taken only from the line it calls canonical, because one on a feature branch is
    /// not yet the family's. And one naming no commit cannot be ordered against one that does, so it
    /// does not replace it.
    /// </remarks>
    /// <param name="provenance">The commit the manifest is read at, with the held one git says it
    /// descends from; null when the checkout could not name one.</param>
    /// <returns>The judgement, and the registration as it now stands when it is taken or already held.</returns>
    public async Task<(FeedOutcome Outcome, Registration? Registered)> RegisterFedAsync(
        Registration registration, FeedProvenance? provenance, DateTimeOffset now, CancellationToken ct)
    {
        var held = registrations
            ?? throw new InvalidOperationException("a fed registration is held in the registration store, and this service was composed without one");
        await _feedGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var existing = (await registered(ct).ConfigureAwait(false))
                .Named(registration.Repository);
            var standing = await held.RegistrationProvenanceAsync(registration.Repository, ct).ConfigureAwait(false);

            if (provenance is null)
            {
                if (standing is not null)
                {
                    return (new(
                        FeedRefusal.Unordered,
                        $"`{registration.Repository}`'s registration is held at `{standing.ShortCommit}`"
                        + $"{(standing.Origin is null ? "" : $", from {standing.Origin}")}, and this one names no commit, "
                        + "so nothing orders the two and the held one stands. It is registered either way: its "
                        + "records and quests still travel.",
                        Entries: 0), null);
                }

                return (new(FeedRefusal.None, $"Registered `{registration.Repository}`.", 0),
                    await register(registration, now, ct).ConfigureAwait(false));
            }

            // The line the declaration calls canonical, so a repository that renamed its default branch
            // is not held to the name it used before; the stored one where this declaration names none.
            var canonical = string.IsNullOrWhiteSpace(registration.DefaultBranch)
                ? existing?.DefaultBranch
                : registration.DefaultBranch.Trim();
            if (standing is not null
                && canonical is { Length: > 0 }
                && !string.Equals(provenance.Branch, canonical, StringComparison.Ordinal))
            {
                return (new(
                    FeedRefusal.NotDefaultBranch,
                    $"`{registration.Repository}`'s registration came from `{provenance.Branch}`, and its canonical "
                    + $"line is `{canonical}` — a declaration on another line is not yet the family's, so the one "
                    + $"held at `{standing.ShortCommit}` stands. Its records and quests still travel from this checkout.",
                    Entries: 0), null);
            }

            var arriving = provenance with { Digest = FeedDigest.Of(registration) };
            var verdict = FeedOrder.Judge(standing, arriving);
            if (verdict == FeedVerdict.AlreadyHeld)
            {
                return (new(
                    FeedRefusal.None,
                    $"`{registration.Repository}`'s registration is already held at `{arriving.ShortCommit}` as declared.",
                    Entries: 0), existing);
            }

            if (verdict != FeedVerdict.Take)
            {
                return (NotTaken(verdict, registration.Repository, "registration", standing!, arriving), null);
            }

            // Over an index a newer Daoris wrote, a declaration that stops sharing what it shared is refused
            // whole, since what it fed could not be taken back; one that never shared has nothing there to take.
            var withdraws = !(registration.Joined && registration.SharesKnowledge);
            if (withdraws && existing is { Joined: true, SharesKnowledge: true }) refuseNewerIndex();

            var taken = await register(registration, now, ct).ConfigureAwait(false);

            // 🔴 A declaration that no longer joins-and-shares takes back what it fed while it did:
            // from here its knowledge stays home, and a deployment that kept serving it would be the
            // disclosure the declaration exists to prevent. Its held commit goes too, or sharing
            // again from that commit would be "already held" into an index that holds nothing.
            if (!(taken.Joined && taken.SharesKnowledge) && store.Refusal is null)
            {
                await store.ReplaceRepositoryAsync(
                    taken.Repository, [], ReadingWrite.Taken(time.GetUtcNow()),
                    inside => held.ForgetKnowledgeAsync(taken.Repository, inside), ct).ConfigureAwait(false);
            }

            // After the row: a store that failed between the two must never claim a declaration it does not hold.
            await held.RecordRegistrationProvenanceAsync(registration.Repository, arriving, ct).ConfigureAwait(false);
            return (new(
                FeedRefusal.None,
                $"Registered `{registration.Repository}` as declared at `{arriving.ShortCommit}`.",
                Entries: 0), taken);
        }
        finally
        {
            _feedGate.Release();
        }
    }

    /// <summary>
    /// Accept one repository's knowledge content from a feed — the only ingest a remote deployment has,
    /// since it never scans a filesystem. The disclosure judgement runs HERE, at the door,
    /// over what the repository's own reviewed manifest declared: join admits records, knowledge is a
    /// second declaration, and silence refused both. Accepted entries are normalized — the repository
    /// name from the registration, provenance forced Local, because canonical doctrine is distributed
    /// by `sync`, never by the feed — and replace that repository's entries wholesale, so the feed is
    /// idempotent.
    /// </summary>
    public async Task<FeedOutcome> FeedAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, FeedProvenance? provenance, CancellationToken ct)
    {
        refuseNewerIndex();

        // Admission, judgement and write are one step: two feeds cannot both take the same held commit, and one queued behind a
        // declaration that stopped sharing finds it standing.
        await _feedGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var (registration, refused) = await AdmitAsync(repository, provenance, "knowledge", ct).ConfigureAwait(false);
            if (refused is not null) return refused;

            if (entries.Any(entry => entry.Kind == EntryKind.Record))
            {
                return new(
                    FeedRefusal.Malformed,
                    $"`{repository}` fed a finished tree's record, and a record is kept on the machine that made it: no feed carries one.",
                    Entries: 0);
            }

            var normalized = entries
                .Select(entry => entry with
                {
                    Repository = registration!.Repository,
                    Provenance = Provenance.Local,
                    // The RECEIVING deployment's wiring decides the circle, never the feed's claim about it:
                    // a feed that could name its own workspace could write itself into someone
                    // else's, which is the scoping bug that becomes a disclosure.
                    Workspace = registration.InWorkspace,
                })
                .ToList();
            var arriving = provenance! with { Digest = FeedDigest.Of(normalized) };

            // Read before the held commit is judged, so another connector's write between the judgement and this one refuses it.
            var generations = await store.GenerationsAsync(ct).ConfigureAwait(false);
            var held = registrations is null
                ? null
                : await registrations.ProvenanceAsync(registration!.Repository, ct).ConfigureAwait(false);

            var verdict = FeedOrder.Judge(held, arriving);
            if (verdict == FeedVerdict.AlreadyHeld)
            {
                return new(
                    FeedRefusal.None,
                    $"`{registration!.Repository}` is already held at `{arriving.ShortCommit}` with this content.",
                    normalized.Count);
            }

            if (verdict != FeedVerdict.Take) return NotTaken(verdict, registration!.Repository, "knowledge", held!, arriving);

            // 🔴 The provenance is recorded in the entries' own write: a fault between the two would leave new bodies
            // under the old commit, and a store that failed mid-replace claiming a commit it does not hold would refuse the
            // next feed from that commit as a duplicate of work that never happened.
            var named = registration!.Repository;
            var wrote = await store.ReplaceRepositoryAsync(
                named, normalized,
                new ReadingWrite(generations.TryGetValue(named, out var generation) ? generation : 0, arriving.Commit, null, Fed: true, time.GetUtcNow()),
                registrations is null ? null : inside => registrations.RecordProvenanceAsync(named, arriving, inside),
                ct).ConfigureAwait(false);
            if (!wrote)
            {
                return new(
                    FeedRefusal.Moved,
                    $"`{named}` was written here by another connector while this feed was judged, so nothing of it was "
                    + "taken. The next pass judges it again.",
                    Entries: 0);
            }

            return new(
                FeedRefusal.None,
                $"Indexed {normalized.Count} entries from `{registration.Repository}` at `{arriving.ShortCommit}`.",
                normalized.Count);
        }
        finally
        {
            _feedGate.Release();
        }
    }

    /// <summary>
    /// Accept one repository's code map from a feed — the map a deployment with no checkout
    /// answers with. The same gates and the same ordering as knowledge, each held at its own commit.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Judged whole again here</b>, by the reader that judges the file on disk: the feeding
    /// machine's reading is a claim, and a map this deployment stores is one it has checked. What is
    /// kept is the map rewritten in its canonical form, so its digest is over what it says.</para>
    ///
    /// <para>A null map is the repository saying it keeps none at that commit. The held map goes and
    /// the commit stays, so a checkout from before the deletion cannot bring the map back.</para>
    /// </remarks>
    /// <param name="file">Which candidate the map is read from; ignored when there is no map.</param>
    /// <param name="map">The file's text, or null when the checkout keeps no map.</param>
    public async Task<FeedOutcome> FeedCodeMapAsync(
        string repository, string? file, string? map, FeedProvenance? provenance, CancellationToken ct)
    {
        await _feedGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var (registration, refused) = await AdmitAsync(repository, provenance, "a code map", ct).ConfigureAwait(false);
            if (refused is not null) return refused;

            // A composition without the store has nowhere to hold what is fed. Every real one has it
            // (ServiceFactory), so this is a wiring mistake, said as one.
            var held = registrations
                ?? throw new InvalidOperationException("a fed code map is held in the registration store, and this service was composed without one");

            string? body = null;
            string? name = null;
            var modules = 0;
            if (map is not null)
            {
                if (file is null || !CodeMapReader.Candidates.Contains(file, StringComparer.Ordinal))
                {
                    return new(
                        FeedRefusal.Malformed,
                        $"`{registration!.Repository}` fed a code map from `{file}`, and a code map is read from "
                        + $"{string.Join(" or ", CodeMapReader.Candidates.Select(c => $"`{c}`"))} — nowhere else.",
                        Entries: 0);
                }

                if (System.Text.Encoding.UTF8.GetByteCount(map) > CodeMapReader.MaxBytes)
                {
                    return new(
                        FeedRefusal.Malformed,
                        $"`{file}` is over {CodeMapReader.MaxBytes / 1024 / 1024} MB — a code map is a small file.",
                        Entries: 0);
                }

                var (parsed, problem) = CodeMapReader.Parse(map, file);
                if (problem is not null) return new(FeedRefusal.Malformed, problem, Entries: 0);

                body = CodeMapReader.Write(parsed!);
                name = file;
                modules = parsed!.Modules.Count;
            }

            var arriving = provenance! with { Digest = FeedDigest.Of($"{name}\n{body}") };
            var standing = await held.CodeMapProvenanceAsync(registration!.Repository, ct).ConfigureAwait(false);
            var verdict = FeedOrder.Judge(standing, arriving);
            if (verdict == FeedVerdict.AlreadyHeld)
            {
                return new(
                    FeedRefusal.None,
                    $"`{registration.Repository}`'s code map is already held at `{arriving.ShortCommit}` as it reads.",
                    modules);
            }

            if (verdict != FeedVerdict.Take) return NotTaken(verdict, registration.Repository, "code map", standing!, arriving);

            await held.RecordCodeMapAsync(registration.Repository, name, body, arriving, ct).ConfigureAwait(false);
            return new(
                FeedRefusal.None,
                body is null
                    ? $"`{registration.Repository}` keeps no code map at `{arriving.ShortCommit}`; none is held here now."
                    : $"Held `{registration.Repository}`'s code map ({modules} modules) at `{arriving.ShortCommit}`.",
                modules);
        }
        finally
        {
            _feedGate.Release();
        }
    }

    /// <summary>
    /// The door every feed from a checkout passes: joined, sharing, naming a commit, on the canonical
    /// line. Phrased once here so the knowledge feed and the code map feed cannot drift on it.
    /// </summary>
    /// <param name="what">What is fed, as the refusal says it: "knowledge", "a code map".</param>
    private async Task<(Registration? Registration, FeedOutcome? Refused)> AdmitAsync(
        string repository, FeedProvenance? provenance, string what, CancellationToken ct)
    {
        var registration = (await registered(ct).ConfigureAwait(false))
            .Named(repository);

        if (registration is null || !registration.Joined)
        {
            return (null, new(
                FeedRefusal.NotJoined,
                $"`{repository}` has not joined this deployment — the manifest's `remote.join` is the "
                + "declaration that admits it, and silence means local.",
                Entries: 0));
        }

        if (!registration.SharesKnowledge)
        {
            return (null, new(
                FeedRefusal.NotSharing,
                $"`{registration.Repository}` joined without sharing knowledge — its records travel, its "
                + "knowledge and its code map stay home. `remote.knowledge` is the declaration that changes that.",
                Entries: 0));
        }

        // ——— Which point in the history is speaking. Wholesale replacement is the right
        // shape — the index is derived data, and merging two machines' derivations invents a second
        // truth beside git — but it is only safe once the deployment can tell a newer view from an
        // older one. Unguarded, two machines feeding one repository are a flapping generator.
        if (provenance is null)
        {
            return (null, new(
                FeedRefusal.NoProvenance,
                $"`{registration.Repository}` fed {what} without naming a commit. This deployment "
                + "replaces what a repository fed wholesale, so it takes a feed only from a point in "
                + "the history it can compare with what it already holds.",
                Entries: 0));
        }

        if (registration.DefaultBranch is { Length: > 0 } canonical
            && !string.Equals(provenance.Branch, canonical, StringComparison.Ordinal))
        {
            // The branch, the PR and the review already display work in flight better than an index
            // would. Records and quests still travel from any checkout: they are records of activity,
            // not claims of truth.
            return (null, new(
                FeedRefusal.NotDefaultBranch,
                $"`{registration.Repository}` fed {what} from `{provenance.Branch}`, and its canonical "
                + $"line is `{canonical}` — what is unmerged is not yet the family's. Its session records "
                + "and quests still travel from this checkout.",
                Entries: 0));
        }

        return (registration, null);
    }

    /// <summary>A feed the ordering did not take, said as the information it is.</summary>
    private static FeedOutcome NotTaken(
        FeedVerdict verdict, string repository, string what, FeedProvenance held, FeedProvenance arriving)
    {
        var from = held.Origin is null ? "" : $", from {held.Origin}";
        return verdict switch
        {
            FeedVerdict.ContentDiffers => new(
                FeedRefusal.ContentDiffers,
                $"`{repository}`'s {what} is held at `{held.ShortCommit}` as {held.Origin ?? "it was first"} read it, "
                + "and this machine read the same commit differently. The first reading stands: two readings of "
                + "one commit are two tools disagreeing, not two histories.",
                Entries: 0),
            FeedVerdict.Moved => new(
                FeedRefusal.Moved,
                $"`{repository}`'s {what} moved here since this machine checked: it asked git about "
                + $"`{Clipped.ShortCommit(arriving.Base!, 8)}`, and this deployment now holds `{held.ShortCommit}`{from}. "
                + "Nothing is lost — the next pass asks git again.",
                Entries: 0),
            _ => new(
                FeedRefusal.Stale,
                $"`{repository}`'s {what} is already fed from a newer commit "
                // `.UtcDateTime`, because the sentence writes `Z`. A commit time carries the
                // committer's own offset, so formatting it directly printed a local wall clock and
                // called it UTC — an explanation that contradicted the correct ordering underneath it.
                + $"(`{held.ShortCommit}`, {held.CommittedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z{from}) — this feed is from "
                + $"`{arriving.ShortCommit}`, {arriving.CommittedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z, so this "
                + "deployment keeps what it has. Nothing is wrong: this checkout is simply behind.",
                Entries: 0),
        };
    }
}
