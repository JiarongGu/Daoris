using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// What a repository declared itself to be, read from its manifest.
/// </summary>
/// <param name="Repository">Its name — the directory, which is also how quests address it.</param>
/// <param name="Adopted">Whether it carries a manifest at all. Only an adopter can be addressed.</param>
/// <param name="Summary">One line, for someone who has never opened it.</param>
/// <param name="Owns">Areas it owns: a change in one of these belongs there rather than anywhere else.</param>
/// <param name="Accepts">Kinds of quest it welcomes. Guidance for the asker, not a contract.</param>
/// <param name="Packs">Canonical packs it carries — a decent proxy for its stack.</param>
/// <param name="Entries">How much knowledge it contributes to the bank.</param>
/// <param name="Root">
/// Where the working tree is ON THIS MACHINE — the one field spawning a session needs (D46). Machine-local
/// by nature: it is answered only to loopback callers and is stripped from anything that syncs off-machine.
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
/// <para><b>It is read from the repositories, not configured centrally.</b> A repository declares itself
/// in its own manifest, which keeps the declaration next to the thing it describes and reviewable by
/// the people it describes. A central list would drift the moment a repository changed and nobody
/// remembered to update the server.</para>
///
/// <para><b>Registration is deliberately not enforced.</b> An adopted repository with an empty `domain`
/// is still addressable — it simply tells an asker less, and the asker is told that. Refusing quests
/// until a form is filled in would make adoption a chore, and the whole arrangement rests on adoption
/// being easy.</para>
/// </remarks>
public sealed class Registry(string repositoryRoot)
{
    /// <summary>
    /// Registrations a client sent with `daoris connect`, keyed by repository.
    /// </summary>
    /// <remarks>
    /// A service on this machine can read manifests off disk and does. One running anywhere else has no
    /// such option — it cannot see the repositories at all — so a client has to be able to tell it.
    /// Pushed registrations win over scanned ones: the client knows its own manifest, and a remote
    /// deployment has nothing else to go on.
    /// </remarks>
    private readonly Dictionary<string, Registration> _pushed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Record what a repository said about itself.</summary>
    public void Register(Registration registration) => _pushed[registration.Repository] = registration;

    /// <summary>Every repository this service knows of — scanned from disk, plus anything pushed.</summary>
    public IReadOnlyList<Registration> Read(IReadOnlyDictionary<string, int> entryCounts)
    {
        var registrations = new List<Registration>();
        var scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(repositoryRoot))
        {
            registrations.AddRange(Scan(entryCounts, scanned));
        }

        // Anything that registered itself but is not on this disk — which is every repository, when the
        // service runs somewhere else.
        registrations.AddRange(_pushed.Values.Where(r => !scanned.Contains(r.Repository)));

        return registrations
            .Select(r => _pushed.TryGetValue(r.Repository, out var sent)
                ? sent.Registered
                    // The push wins on what the repository SAID; the scanned root survives a push that
                    // carried none, because a declaration should not cost the driver a path it already knew.
                    ? sent with { Entries = r.Entries, Root = sent.Root ?? r.Root }
                    // A push that declared nothing still carries the WIRING. The workspace is not
                    // something a scan can know — it is a row on this machine (D48 §2) — so a scanned
                    // repository must never silently fall back into `default` just because its
                    // declaration was thin.
                    : r with { Workspace = sent.Workspace ?? r.Workspace }
                : r)
            .OrderBy(r => r.Repository, StringComparer.Ordinal)
            .ToList();
    }

    private IEnumerable<Registration> Scan(IReadOnlyDictionary<string, int> entryCounts, HashSet<string> scanned)
    {
        var registrations = new List<Registration>();
        foreach (var directory in Directory.GetDirectories(repositoryRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = new DirectoryInfo(directory).Name;
            var manifest = Path.Combine(directory, "daoris.json");
            entryCounts.TryGetValue(name, out var entries);

            if (!File.Exists(manifest))
            {
                // Present in the family, not adopted. Worth listing rather than hiding: "who could I
                // ask, and who cannot be asked yet" is the same question, and a silent omission reads
                // as the repository not existing.
                registrations.Add(new Registration(name, false, null, [], [], [], entries, directory));
                continue;
            }

            registrations.Add(ReadManifest(name, manifest, entries, directory));
        }

        foreach (var registration in registrations) scanned.Add(registration.Repository);
        return registrations;
    }

    private static Registration ReadManifest(string name, string manifest, int entries, string directory)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = document.RootElement;
            var domain = root.TryGetProperty("domain", out var d) && d.ValueKind == JsonValueKind.Object
                ? d
                : (JsonElement?)null;
            var remote = root.TryGetProperty("remote", out var m) && m.ValueKind == JsonValueKind.Object
                ? m
                : (JsonElement?)null;
            var joined = remote is not null && Bool(remote.Value, "join");

            return new Registration(
                name,
                Adopted: true,
                Summary: domain is null ? null : String(domain.Value, "summary"),
                Owns: domain is null ? [] : Strings(domain.Value, "owns"),
                Accepts: domain is null ? [] : Strings(domain.Value, "accepts"),
                Packs: Strings(root, "packs"),
                Entries: entries,
                Root: directory,
                Joined: joined,
                // Knowledge feeds only from a joined repository (D47 §4). The CLI refuses this
                // manifest; the scanner reads manifests the CLI never validated, so it narrows too.
                SharesKnowledge: joined && remote is not null && Bool(remote.Value, "knowledge"));
        }
        catch (JsonException)
        {
            // A manifest that will not parse is the repository's own problem and its own tooling will
            // say so. Here it means only that we cannot read the declaration — which is not a reason to
            // drop the repository off the map.
            return new Registration(name, true, null, [], [], [], entries, directory);
        }
    }

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() is { Length: > 0 } text ? text : null
            : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }
}
