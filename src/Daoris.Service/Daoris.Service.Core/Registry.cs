namespace Daoris.Knowledge;

/// <summary>
/// What a repository declared itself to be.
/// </summary>
/// <param name="Repository">Its name — the directory, which is also how quests address it.</param>
/// <param name="Adopted">Whether it carries a manifest at all. Only an adopter can be addressed.</param>
/// <param name="Summary">One line, for someone who has never opened it.</param>
/// <param name="Owns">Areas it owns: a change in one of these belongs there rather than anywhere else.</param>
/// <param name="Accepts">Kinds of quest it welcomes. Guidance for the asker, not a contract.</param>
/// <param name="Packs">Canonical packs it carries — a decent proxy for its stack.</param>
/// <param name="Entries">How much knowledge it contributes to the bank.</param>
/// <param name="Root">
/// Where the working tree is ON THIS MACHINE — the one field spawning a session needs (D46), and what
/// the index reads from since the registry became the authority (D48 §3). Machine-local by nature: it
/// is answered only to loopback callers and is stripped from anything that syncs off-machine.
/// </param>
/// <param name="Joined">
/// Declared for a remote deployment (D47 §4): its registration, quests and session records may leave
/// the machine. From the manifest's own tracked `remote` block; silence means false.
/// </param>
/// <param name="SharesKnowledge">
/// Its indexed knowledge content may feed a remote too. Never true without <paramref name="Joined"/> —
/// the CLI refuses that manifest, and every reader here narrows it the same way.
/// </param>
/// <param name="Workspace">
/// Which circle this repository shares with ON THIS MACHINE (D48) — wiring, like a git remote, never a
/// tracked declaration. <b>Null means unstated</b>, which is what makes "preserved on upsert" possible:
/// an ordinary re-registration says nothing about the workspace and must not re-point the row. Every
/// reader gets a concrete name through <see cref="Workspaces.Normalize"/>; only a writer sees the null.
/// </param>
public sealed record Registration(
    string Repository,
    bool Adopted,
    string? Summary,
    IReadOnlyList<string> Owns,
    IReadOnlyList<string> Accepts,
    IReadOnlyList<string> Packs,
    int Entries,
    string? Root = null,
    bool Joined = false,
    bool SharesKnowledge = false,
    string? Workspace = null)
{
    /// <summary>The workspace this repository is wired to, with silence resolved to the default.</summary>
    public string InWorkspace => Workspaces.Normalize(Workspace);

    /// <summary>Whether this repository has said anything useful about what it can be asked for.</summary>
    public bool Registered => Adopted && (!string.IsNullOrWhiteSpace(Summary) || Owns.Count > 0 || Accepts.Count > 0);
}

/// <summary>
/// Who is out there, what each one owns, and what is worth asking of them.
/// </summary>
/// <remarks>
/// <para>This is what turns the index from a pile of documents into something an agent can navigate.
/// Search answers "has anyone solved this"; the registry answers "whose problem is this" — and those
/// are different questions with different answers.</para>
///
/// <para><b>It is an explicit list, not a view over a folder</b> (D48 §3). It used to be the union of a
/// folder scan and whatever clients had pushed, and that failed in the way scans fail: the
/// ghost-repository fix showed that what a scan does not say governs as much as what it says, and
/// nobody reviews a silence. Workspaces finished the argument — one root folder cannot express "these
/// repositories, in these workspaces, wherever they live". The scan survives as
/// <see cref="RegistryImport"/>, a bootstrap a person runs, and everything else goes through
/// <see cref="Register"/> and <see cref="Retire"/>.</para>
///
/// <para><b>Registration is deliberately not enforced.</b> An adopted repository with an empty `domain`
/// is still addressable — it simply tells an asker less, and the asker is told that. Refusing quests
/// until a form is filled in would make adoption a chore, and the whole arrangement rests on adoption
/// being easy.</para>
/// </remarks>
public sealed class Registry
{
    private readonly Dictionary<string, Registration> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Record what a repository said about itself. Re-registering replaces: the name is the identity.</summary>
    public void Register(Registration registration) => _known[registration.Repository] = registration;

    /// <summary>
    /// Take a repository off the map. <b>Nothing on disk is touched</b> — retiring is the registration
    /// lifecycle's end, not a delete, and its knowledge leaves the index on the next refresh by the
    /// ghost rule.
    /// </summary>
    /// <returns>Whether there was a row to retire; false is an answer, not a failure.</returns>
    public bool Retire(string repository) => _known.Remove(repository);

    /// <summary>Every repository this service knows of, with the index's entry counts applied.</summary>
    public IReadOnlyList<Registration> Read(IReadOnlyDictionary<string, int> entryCounts) =>
        _known.Values
            .Select(r => entryCounts.TryGetValue(r.Repository, out var entries) ? r with { Entries = entries } : r)
            .OrderBy(r => r.Repository, StringComparer.Ordinal)
            .ToList();
}
