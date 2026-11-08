namespace Daoris.Knowledge;

/// <summary>Writes the registry: a registration kept, moved between workspaces, or taken off the map.</summary>
internal sealed class KnowledgeRegistrar(
    IKnowledgeStore store, Registry? registry, RegistrationStore? registrations, TimeProvider time, Action refuseNewerIndex, SemaphoreSlim feedGate)
{
    /// <summary>
    /// Take a repository off the map.
    /// </summary>
    /// <remarks>
    /// <b>Nothing on disk is touched.</b> This ends a registration: the repository stops being
    /// addressable and stops being indexed here, and its entries leave the index now. Its files, its
    /// history and its doctrine are its own — the one thing a person must be able
    /// to trust about a remove is what it does not do.
    /// </remarks>
    /// <returns>Whether there is a registration to retire; false is an answer, not a failure.</returns>
    public async Task<bool> RetireAsync(string repository, CancellationToken ct)
    {
        // Refused whole over an index a newer Daoris wrote: the registration alone would go, and the index
        // would keep serving what is taken off the map.
        refuseNewerIndex();
        await feedGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var stored = registrations is not null
                && await registrations.DeleteAsync(repository, ct).ConfigureAwait(false);
            var known = registry?.Retire(repository) ?? false;

            // 🔴 Its entries go with it. "They leave on the next refresh" holds only for a host that
            // refreshes — a fed deployment never does, so it would serve a retired repository's knowledge to
            // every keyed caller for as long as it runs. It reads no source, so it always writes, moving the generation a
            // refresh begun before it read.
            if (stored || known)
            {
                await store.ReplaceRepositoryAsync(repository, [], ReadingWrite.Taken(time.GetUtcNow()), ct: ct).ConfigureAwait(false);
            }

            return stored || known;
        }
        finally
        {
            feedGate.Release();
        }
    }

    /// <summary>
    /// Record what a repository said about itself, when it told us rather than we found it.
    /// </summary>
    /// <remarks>
    /// <para>Persisted before it is served: a pushed registration is the only registration a remote
    /// service has, and one that evaporates on restart looks exactly like a repository that never
    /// connected.</para>
    ///
    /// <para>What is served is what the store DECIDED, not what arrived — because the workspace is
    /// preserved rather than overwritten when a registration says nothing about it. Serving
    /// the incoming record instead would re-point every repository to `default` in memory the moment
    /// an ordinary sync tick re-registered it, and the store on disk would keep saying otherwise.</para>
    /// </remarks>
    /// <returns>The registration as it now stands, workspace resolved.</returns>
    public async Task<Registration> RegisterAsync(Registration registration, DateTimeOffset now, CancellationToken ct)
    {
        // Where it stood before, so a move is known as one.
        var before = registrations is not null
            ? (await registrations.AllAsync(ct).ConfigureAwait(false)).Named(registration.Repository)
            : (registry?.Read() ?? []).Named(registration.Repository);

        var effective = registrations is not null
            ? await registrations.UpsertAsync(registration, now, ct).ConfigureAwait(false)
            : registration with { Workspace = registration.InWorkspace };

        registry?.Register(effective);
        if (before is not null && !Workspaces.Same(before.InWorkspace, effective.InWorkspace))
        {
            await RestampAsync(effective.Repository, effective.InWorkspace, ct).ConfigureAwait(false);
        }

        return effective;
    }

    /// <summary>
    /// A moved repository's entries carried into its new workspace.
    /// </summary>
    /// <remarks>
    /// An entry is stamped with the workspace it is indexed under, and a local host reads its checkouts again only
    /// into an empty store or on request: a repository moved between workspaces kept its old stamp, and the
    /// workspace it moved to counted none of its knowledge. Re-stamped in one write of the store's, which leaves the bodies
    /// as they are and moves the generation, so a reading that took the old wiring is refused after it, whichever
    /// connector took it; over an index a newer Daoris wrote, nothing is rewritten.
    /// </remarks>
    private async Task RestampAsync(string repository, string workspace, CancellationToken ct)
    {
        if (store.Refusal is not null) return;
        await store.RestampAsync(repository, workspace, ct).ConfigureAwait(false);
    }
}
