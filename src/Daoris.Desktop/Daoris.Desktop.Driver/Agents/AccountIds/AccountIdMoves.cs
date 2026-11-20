namespace Daoris.Driver;

/// <summary>One account's id moving to its hash form.</summary>
public sealed record AccountIdRename(string Agent, string From, string To);

/// <summary>Where a moving id is referenced: the file or store under the Daoris folder, and the place in it.</summary>
public sealed record AccountIdReference(AccountIdRename Rename, string Place, string Where);

/// <summary>One id's move with every reference it has; <see cref="Failed"/> where it did not move, <see cref="HeldBy"/> what holds its folder.</summary>
public sealed record AccountIdMove(
    AccountIdRename Rename, IReadOnlyList<AccountIdReference> References, string? Failed = null, IReadOnlyList<FileHolder>? HeldBy = null);

/// <summary>An entry in <c>accounts.json</c> named like an account that is none (a lock's), dropped by the move.</summary>
public sealed record AccountIdStray(string Agent, string Entry);

/// <summary>What a move would do or did: each id's move and each stray entry; nothing where every id is the hash form already.</summary>
public sealed record AccountIdPlan(IReadOnlyList<AccountIdMove> Moves, IReadOnlyList<AccountIdStray> Strays)
{
    public bool Nothing => Moves.Count == 0 && Strays.Count == 0;
}

/// <summary>The home a move reads and writes, the Daoris folder its places are said under, and how a door names its agent.</summary>
internal sealed record AccountIdHome(string Home, string Folder, string? KnowledgeDb, AgentOfDoor Doors, IReadOnlyList<string> NativeHosts)
{
    /// <summary>A path as a place under the Daoris folder, with forward slashes; one outside the folder (a browser's) in full.</summary>
    public string Place(string path)
    {
        var relative = Path.GetRelativePath(Folder, path);
        return (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative).Replace('\\', '/');
    }
}

/// <summary>One store a moving id is referenced in: each reference found, written only where <c>write</c> says.</summary>
internal interface IAccountIdStore
{
    IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write);
}

/// <summary>
/// The one-time move of every account id that is not the hash form (<see cref="AccountIdForm"/>) to the id
/// <see cref="AccountIdForm.Moved"/> derives, with every reference it has, and of each entry in <c>accounts.json</c> named like an account
/// that is none. Not a second reader of an older id: once moved, nothing reads the old one, and a second move finds nothing.
/// </summary>
/// <remarks>
/// <para><b>The folder first</b>: an id whose folder does not move (a file held open in it) keeps every reference, so a home never
/// names an account by an id it does not have. Then each store, in <see cref="Stores"/>' order, every rename at once.</para>
/// <para><b>Plan and apply are one walk</b>: the plan is the apply with nothing written, so the two cannot disagree. The verb,
/// <c>daoris agent ids</c> (<see cref="AccountIdsCommand"/>), applies only while nothing of Daoris runs on the home.</para>
/// <para>A record that says what happened keeps the id it names: a move's row, a hand-over's file, a session's events
/// and the machine log. The probe locks under <c>run/</c> are a process's, and go with it.</para>
/// </remarks>
public static class AccountIdMoves
{
    /// <summary>The stores walked after the folders, in the order a plan lists their references.</summary>
    private static readonly IAccountIdStore[] Stores =
    [
        new AccountIdNamesStore(),
        new AccountIdWiringStore(),
        new AccountIdNativeHostStore(),
        new AccountIdKeyedStore(HomeEntry.Cooling),
        new AccountIdKeyedStore(HomeEntry.Windows),
        new AccountIdReadsStore(),
        new AccountIdKeyedStore(HomeEntry.Keys),
        new AccountIdOffersStore(),
        new AccountIdUsageStore(),
        new AccountIdHistoryStore(),
        new AccountIdSessionStores(),
    ];

    /// <summary>What a move would do, changing nothing.</summary>
    /// <param name="knowledgeDb">Where the environment moved the knowledge store to (<c>DAORIS_KNOWLEDGE_DB</c>), or null for the home's own.</param>
    /// <param name="nativeHosts">The browser's native-messaging manifests to follow a moved account, or null for the machine's (<see cref="NativeHostManifests.Located"/>).</param>
    public static AccountIdPlan Plan(string home, string? knowledgeDb = null, IReadOnlyList<string>? nativeHosts = null) =>
        Walk(home, knowledgeDb, nativeHosts, write: false);

    /// <summary>
    /// The move done: each id's folder, then every reference; what it did, each id that did not move saying why. Planned first, so a store
    /// that does not read stops it before anything moves.
    /// </summary>
    /// <exception cref="DriverException">A store does not read; nothing changes.</exception>
    public static AccountIdPlan Apply(string home, string? knowledgeDb = null, IReadOnlyList<string>? nativeHosts = null) =>
        Walk(home, knowledgeDb, nativeHosts, write: false).Nothing ? new AccountIdPlan([], []) : Walk(home, knowledgeDb, nativeHosts, write: true);

    /// <summary>Each account id of each agent here that is not the hash form, with the id it moves to, in agent then id order.</summary>
    public static IReadOnlyList<AccountIdRename> Renames(string home)
    {
        var settings = HarnessSettings.Load(DaorisHome.Entry(home, HomeEntry.Harnesses));
        var renames = new List<AccountIdRename>();
        foreach (var (agent, accounts) in HarnessAccountFolders.AccountsOf(home, settings).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var taken = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
            foreach (var legacy in accounts.Where(account => !AccountIdForm.IsHash(account)).Order(StringComparer.Ordinal))
            {
                var to = AccountIdForm.Moved(agent, legacy, taken);
                taken.Add(to);
                renames.Add(new AccountIdRename(agent, legacy, to));
            }
        }

        return renames;
    }

    private static AccountIdPlan Walk(string home, string? knowledgeDb, IReadOnlyList<string>? nativeHosts, bool write)
    {
        var renames = Renames(home);
        var strays = AccountIdNamesStore.Strays(home);
        if (renames.Count == 0 && strays.Count == 0) return new AccountIdPlan([], []);

        var at = new AccountIdHome(home, DaorisHome.FolderOf(home), knowledgeDb, new AgentOfDoor(Registry(home)), nativeHosts ?? NativeHostManifests.Located());
        var folders = new Dictionary<AccountIdRename, AccountIdReference>();
        var failed = new Dictionary<AccountIdRename, string>();
        var holders = new Dictionary<AccountIdRename, IReadOnlyList<FileHolder>>();
        foreach (var rename in renames)
        {
            var from = HarnessSettings.ProfileHome(home, rename.Agent, rename.From);
            var to = HarnessSettings.ProfileHome(home, rename.Agent, rename.To);
            if (HeldBy(from) is { Count: > 0 } held)
            {
                holders[rename] = held;
                failed[rename] = $"held by {string.Join(", ", held.Select(holder => holder.Said))}, so nothing of it changed; "
                    + "the next `daoris home account-ids --yes` moves it once they end";
                continue;
            }

            try
            {
                if (write) Directory.Move(from, to);
                folders[rename] = new AccountIdReference(rename, at.Place(from), "folder");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                failed[rename] = $"its folder did not move ({error.Message.TrimEnd('.')}), so nothing of it changed";
                if (HeldBy(from) is { Count: > 0 } now) holders[rename] = now;
            }
        }

        var moving = renames.Where(folders.ContainsKey).ToList();
        var found = Stores.SelectMany(store => moving.Count == 0 ? [] : store.Moved(at, moving, write)).ToList();
        if (write) AccountIdNamesStore.Drop(home, strays);

        return new AccountIdPlan(
            [
                .. renames.Select(rename => failed.TryGetValue(rename, out var why)
                    ? new AccountIdMove(rename, [], why, holders.GetValueOrDefault(rename))
                    : new AccountIdMove(rename, [folders[rename], .. found.Where(reference => reference.Rename == rename)])),
            ],
            strays);
    }

    /// <summary>The processes holding <paramref name="folder"/> or a file in it, each once; none where the system cannot tell (<see cref="HeldFiles"/>).</summary>
    private static IReadOnlyList<FileHolder> HeldBy(string folder) =>
        [.. HeldFiles.Find(folder, most: HoldersMost).SelectMany(entry => entry.By).DistinctBy(holder => holder.Pid)];

    /// <summary>How many held entries a folder's search reads at most: enough to name every process holding it, never every file.</summary>
    private const int HoldersMost = 50;

    /// <summary>The built doors and every installed plugin's, so a session through a plugin's door is its agent's.</summary>
    private static ProviderRegistry Registry(string home)
    {
        var built = ProviderRegistry.Built();
        return built.WithPlugins(AgentPluginReads.Current.Load(home, built.Names));
    }
}
