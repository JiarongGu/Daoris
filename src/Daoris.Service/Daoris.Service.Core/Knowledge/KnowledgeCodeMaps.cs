namespace Daoris.Knowledge;

/// <summary>A registered repository's code map, read from its checkout or held as a teammate's or a feed's (MAP3).</summary>
internal sealed class KnowledgeCodeMaps(
    Registry? registry, RegistrationStore? registrations, Func<CancellationToken, Task> reloadRegistry)
{
    /// <summary>
    /// A registered repository's code map (MAP3a), read from its checkout on each ask — the person's
    /// machine showing the person's state, as the local index does (WSP4). Null for a repository
    /// nobody registered. One with no checkout here answers with what was fed (MAP3b), judged again on
    /// the way out, or with no file when nothing was.
    /// </summary>
    public async Task<CodeMapRead?> ReadAsync(string repository, CancellationToken ct)
    {
        await reloadRegistry(ct).ConfigureAwait(false);
        var registration = (registry?.Read() ?? []).Named(repository);
        if (registration is null) return null;

        if (!string.IsNullOrWhiteSpace(registration.Root) && Directory.Exists(registration.Root))
        {
            return CodeMapReader.Read(registration.Root);
        }

        // No checkout here: what was fed — at a shared deployment by the machine with the checkout
        // (MAP3b), on a machine by its sync from the circle's remote (MAP3e) — said with where it came
        // from, including a commit that keeps none.
        if (registrations is null) return new CodeMapRead(null, null, null);
        var fed = await registrations.CodeMapProvenanceAsync(registration.Repository, ct).ConfigureAwait(false);
        if (await registrations.FedCodeMapAsync(registration.Repository, ct).ConfigureAwait(false)
            is not { File: { } file, Body: { } body })
        {
            return new CodeMapRead(null, null, null, fed);
        }

        var (map, problem) = CodeMapReader.Parse(body, file);
        return new CodeMapRead(map, file, problem, fed);
    }

    /// <summary>The commit a teammate's code map is held at on this machine, or null where none is (MAP3e).</summary>
    public async Task<string?> FedCommitAsync(string repository, CancellationToken ct) =>
        registrations is null
            ? null
            : (await registrations.CodeMapProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit;

    /// <summary>
    /// Hold a teammate's code map as the circle's remote holds it (MAP3e) — already judged whole by
    /// <see cref="CodeMapWire.Read"/>, and digested here over what is kept, as a fed map is.
    /// </summary>
    public async Task HoldFedAsync(string repository, FedCodeMap map, CancellationToken ct)
    {
        var held = registrations
            ?? throw new InvalidOperationException("a fed code map is held in the registration store, and this service was composed without one");
        await held.RecordCodeMapAsync(
            repository, map.File, map.Body, map.Fed with { Digest = FeedDigest.Of($"{map.File}\n{map.Body}") }, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Stop holding a teammate's code map the remote no longer holds (MAP3e).</summary>
    /// <returns>Whether there was one to forget.</returns>
    public async Task<bool> ForgetFedAsync(string repository, CancellationToken ct) =>
        registrations is not null && await registrations.ForgetCodeMapAsync(repository, ct).ConfigureAwait(false);
}
