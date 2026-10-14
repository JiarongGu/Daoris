namespace Daoris.Knowledge;

/// <summary>
/// What a repository declared itself to be.
/// </summary>
/// <param name="Repository">Its name — the directory, which is also how quests address it.</param>
/// <param name="Adopted">
/// Whether it carries a manifest at all — its doctrine, its declaration and its own connector. Since
/// D70 this is what makes a repository DISCIPLINED, not what makes it addressable: see
/// <see cref="Registration.Addressable"/>.
/// </param>
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
/// <param name="DefaultBranch">
/// The repository's canonical line, as the checkout that registered knows it (D48 §6) — the remote
/// cannot ask git, so the machine holding the tree tells it. <b>Null means unstated</b> and is
/// preserved on upsert, exactly as the workspace is: `daoris connect` says nothing about branches,
/// and a re-registration must not erase what the driver declared. Where it is unstated, any branch may
/// feed — the deployment has not been told which line is canonical, and guessing one would refuse
/// every feed from a repository that simply never said.
/// </param>
/// <param name="Uses">
/// The repositories it says it depends on (D91), by the registry's names, from its manifest's
/// `domain.uses`. Null and empty are the same: nothing declared. Read through <see cref="DependsOn"/>,
/// which holds the rule its CLI twin (`connect.ts`, <c>usesOf</c>) holds too.
/// </param>
/// <param name="Lanes">
/// The lanes it declares (D115 §2.2): the domains inside it a quest may address as
/// `repository:lane`, as their words — never their paths, which stay in the repository's file. <b>Null
/// means unstated</b> and is preserved on upsert, as the workspace is: only `connect` reads the file,
/// and it always says (`[]` for none), while the page's add and an import say nothing of lanes. Read
/// through <see cref="DeclaredLanes"/>, which holds the rule its CLI twin (`lanes.ts`) holds too.
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
    string? Workspace = null,
    string? DefaultBranch = null,
    IReadOnlyList<string>? Uses = null,
    IReadOnlyList<DeclaredLane>? Lanes = null)
{
    /// <summary>The workspace this repository is wired to, with silence resolved to the default.</summary>
    public string InWorkspace => Workspaces.Normalize(Workspace);

    /// <summary>What it says it uses, as the rule reads the declaration (D91).</summary>
    public IReadOnlyList<string> DependsOn => Declared.Uses(Uses, Repository);

    /// <summary>The lanes it declares, as the rule reads them (D115 §2.2); none when unstated.</summary>
    public IReadOnlyList<DeclaredLane> DeclaredLanes => Declared.Lanes(Lanes);

    /// <summary>Whether this repository has said anything useful about what it can be asked for.</summary>
    public bool Registered => Adopted && (!string.IsNullOrWhiteSpace(Summary) || Owns.Count > 0 || Accepts.Count > 0);

    /// <summary>
    /// Whether a quest can be addressed to it: it adopted, or it is registered with a root on this
    /// machine (D70). <b>Registered is addressable; adopted is disciplined.</b>
    /// </summary>
    /// <remarks>
    /// An adopter carries its own connector, so any session there can see a quest. A repository
    /// registered here with a root and no manifest has none in its files, but a session the driver
    /// starts in it over the protocol door is handed one on the wire (ACP4) — nothing written into the
    /// repository — so a quest there has somebody to answer it. Without a root nothing here could start
    /// one, and without a manifest nothing there could see it. One judgement, read by every door, so
    /// no surface offers a receiver the exchange refuses.
    /// </remarks>
    public bool Addressable => Adopted || !string.IsNullOrWhiteSpace(Root);
}

/// <summary>
/// How a declaration's `uses` reads (D91), the one rule every door here applies — and the CLI's
/// `usesOf` holds too, by its own test table (twins): each name trimmed, a blank one dropped, a repeat
/// in any case dropped with the first spelling kept, and the repository's own name dropped, since
/// depending on itself says nothing. Absent is empty.
/// </summary>
public static class Declared
{
    public static IReadOnlyList<string> Uses(IEnumerable<string>? names, string repository)
    {
        var kept = new List<string>();
        foreach (var raw in names ?? [])
        {
            var name = raw?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (string.Equals(name, repository, StringComparison.OrdinalIgnoreCase)) continue;
            if (kept.Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add(name);
        }
        return kept;
    }

    /// <summary>
    /// How a registration's lanes read (D115 §2.2) — the rule the CLI's `laneWords` holds too, by its own
    /// test table (twins): the id, title and summary trimmed; an id that is not a lane's (lower-case
    /// letters, digits and dashes, starting with a letter) dropped, since no quest could address it; a
    /// repeated id dropped, the first kept; the steward's mark kept by the first lane that carries it; a
    /// missing title or summary empty. Absent is none.
    /// </summary>
    public static IReadOnlyList<DeclaredLane> Lanes(IEnumerable<DeclaredLane?>? lanes)
    {
        var kept = new List<DeclaredLane>();
        foreach (var lane in lanes ?? [])
        {
            if (lane is null) continue;
            var id = lane.Id?.Trim() ?? "";
            if (!QuestAddress.IsLaneId(id) || kept.Any(k => k.Id == id)) continue;
            kept.Add(new DeclaredLane(
                id, lane.Title?.Trim() ?? "", lane.Summary?.Trim() ?? "", lane.Steward && !kept.Any(k => k.Steward)));
        }

        return kept;
    }
}

/// <summary>One lane a repository declares (D115 §2.2): its words, never its paths.</summary>
/// <param name="Id">How a quest addresses it: `repository:id`.</param>
/// <param name="Title">What a person calls it.</param>
/// <param name="Summary">One line of what it owns, for whoever addresses it.</param>
/// <param name="Steward">The one lane that keeps the repository's records (§5).</param>
public sealed record DeclaredLane(string Id, string Title, string Summary, bool Steward = false);

/// <summary>A repository looked up by name — in any case, as every door here matches it.</summary>
public static class Registrations
{
    /// <summary>The row naming <paramref name="repository"/>, or null when none does.</summary>
    public static Registration? Named(this IEnumerable<Registration> registry, string? repository) =>
        repository is null
            ? null
            : registry.FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));
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
    // 🔴 Concurrent (REV3): a host's requests register and read at once — an import loop, a sync
    // mirroring a teammate's row — and a plain dictionary enumerated during an insert threw "collection
    // was modified", answering 500 on whichever door was reading.
    private volatile System.Collections.Concurrent.ConcurrentDictionary<string, Registration> _known =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Record what a repository said about itself. Re-registering replaces: the name is the identity.</summary>
    public void Register(Registration registration) => _known[registration.Repository] = registration;

    /// <summary>
    /// Hold exactly these registrations — the store's rows, re-read by the service before it answers.
    /// </summary>
    /// <remarks>
    /// Swapped whole, so a reader sees the old list or the new one and never half of a reload. The
    /// store is the authority, and every host on a machine opens the same one: the desktop's host and
    /// each session's connector. This copy was loaded once at start, so a retire in one host was
    /// invisible in another for as long as it ran (REV3).
    /// </remarks>
    public void Replace(IEnumerable<Registration> registrations)
    {
        var known = new System.Collections.Concurrent.ConcurrentDictionary<string, Registration>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var registration in registrations) known[registration.Repository] = registration;
        _known = known;
    }

    /// <summary>
    /// Take a repository off the map. <b>Nothing on disk is touched</b> — retiring is the registration
    /// lifecycle's end, not a delete. Its knowledge leaves the index at once, by
    /// <see cref="KnowledgeService.RetireAsync"/>, which is the caller that holds the store.
    /// </summary>
    /// <returns>Whether there was a row to retire; false is an answer, not a failure.</returns>
    public bool Retire(string repository) => _known.TryRemove(repository, out _);

    /// <summary>Every repository this service knows of, as registered — for a reader that needs no counts.</summary>
    public IReadOnlyList<Registration> Read() => Read(NoCounts);

    private static readonly IReadOnlyDictionary<string, int> NoCounts = new Dictionary<string, int>();

    /// <summary>Every repository this service knows of, with the index's entry counts applied.</summary>
    public IReadOnlyList<Registration> Read(IReadOnlyDictionary<string, int> entryCounts) =>
        _known.Values
            .Select(r => entryCounts.TryGetValue(r.Repository, out var entries) ? r with { Entries = entries } : r)
            .OrderBy(r => r.Repository, StringComparer.Ordinal)
            .ToList();
}
