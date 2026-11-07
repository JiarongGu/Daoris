namespace Daoris.Driver;

/// <summary>
/// A change of <c>harnesses.json</c> under its keep lock, and the fold that makes each agent's order whole with the accounts on this
/// machine.
/// </summary>
internal static class HarnessSettingsChange
{
    /// <summary>
    /// One change of the wiring under its keep lock: the lock, the file read, the edit decided
    /// from it as it stands, the whole write, so a change another writer lands meanwhile, the window's or a terminal's, waits its turn
    /// rather than being decided against. Every writer of <c>harnesses.json</c> goes through it: the window's routes, Ask Daoris's apply,
    /// and the terminal's account verbs.
    /// </summary>
    /// <param name="edit">The change, from the wiring as read under the lock; it may throw to refuse it, nothing written.</param>
    /// <param name="read">Called once the file is read, before the edit is written: a test's other writer, held off by the lock.</param>
    /// <returns>The wiring as written.</returns>
    /// <exception cref="DriverException">The file does not read, or the edit is refused; nothing is written.</exception>
    /// <exception cref="IOException">Another writer holds the file's lock for the whole of the wait; nothing is written.</exception>
    /// <remarks>
    /// Each agent's order is made whole after the edit (<see cref="AccountSet.Whole"/>), so a new account goes last.
    /// An agent the wiring's <c>rotation</c> or <c>pooled</c> names that Daoris does not know is dropped from both, and its
    /// <c>accounts.strayDropped</c> line is the receipt; its other sections are the person's and stay.
    /// </remarks>
    /// <param name="account">
    /// An account the change itself adds or removes (a sign-in's new folder, a removal's): an added one is placed last by the edit's whole
    /// order, a removed one taken out by the edit.
    /// </param>
    /// <param name="log">Where the strays' lines go; the driver's log under the file's home where none is handed.</param>
    /// <param name="agents">The agents Daoris knows (<see cref="ProviderRegistry.Agents"/>); the home's (<see cref="HarnessAccountFolders.AgentsKnown"/>) where none is handed.</param>
    public static HarnessSettings Change(
        string path, Func<HarnessSettings, HarnessSettings> edit, Action? read = null, AccountChange? account = null, MachineLog? log = null,
        TimeSpan? within = null, IReadOnlyCollection<string>? agents = null)
    {
        using var held = KeepLock.Take(path, within);
        var settings = HarnessSettings.Load(path);
        read?.Invoke();
        if (settings.Problem is not null)
        {
            var refused = edit(settings);
            refused.SaveHolding(path);
            return refused;
        }

        var home = HarnessSettings.HomeOf(path);
        // An agent whose account the change adds or removes is one a door named.
        IReadOnlyCollection<string> known = account is null
            ? agents ?? HarnessAccountFolders.AgentsKnown(home)
            : [.. agents ?? HarnessAccountFolders.AgentsKnown(home), account.Agent];
        var dropped = AccountSet.Strays(settings, known);
        foreach (var stray in dropped) settings = settings.WithoutStray(stray.Agent);
        var changed = AccountSet.Whole(settings, edit(settings), HarnessAccountFolders.AccountsOf(home, settings, known)).WithSaidKept();
        changed.SaveHolding(path);
        Logged(home, log, Unsaid(home, known), dropped, []);
        return changed;
    }

    /// <summary>
    /// A new account placed last in its agent's order, whatever its id, by the write its sign-in or key
    /// makes; nothing is said of it. Never the reason the account is not kept: a file that does not read, or a lock held, leaves it
    /// for the next change, which places it and says so.
    /// </summary>
    public static void Placed(string path, string agent, string account, IReadOnlyCollection<string>? agents = null)
    {
        try
        {
            Change(path, read => read, account: new AccountChange(agent, account, Added: true), agents: agents);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // The account stands; the next change of the file places it.
        }
    }

    /// <summary>
    /// The wiring as every reader takes it: the file at <paramref name="path"/>, each agent's order
    /// made whole with the accounts on this machine (<see cref="AccountSet.Whole"/>), nothing written.
    /// </summary>
    public static HarnessSettings LoadSet(string path, IReadOnlyCollection<string>? agents = null)
    {
        var settings = HarnessSettings.Load(path);
        return settings.Problem is not null
            ? settings
            : AccountSet.Whole(settings, settings, HarnessAccountFolders.AccountsOf(HarnessSettings.HomeOf(path), settings, agents));
    }

    /// <summary>
    /// The driver's come-up write: <see cref="Change"/> with no edit, for a file whose orders are not whole
    /// or that names a stray, so a file nobody edits is made whole too. True when the file is written now, or had nothing to write; false
    /// where its lock is held for the whole wait, which is logged once and tried again at the next look.
    /// </summary>
    /// <remarks>
    /// A folder under the account homes no known agent names is said once to the machine log (<c>accounts.stray</c>) and
    /// never folded; a wiring that names such an agent in <c>rotation</c> or <c>pooled</c> has something to fold, and drops it.
    /// </remarks>
    public static bool FoldNow(string path, MachineLog? log = null, TimeSpan? within = null, IReadOnlyCollection<string>? agents = null)
    {
        var settings = HarnessSettings.Load(path);
        if (settings.Problem is not null) return true;
        var home = HarnessSettings.HomeOf(path);
        var known = agents ?? HarnessAccountFolders.AgentsKnown(home);
        var whole = AccountSet.Whole(settings, settings, HarnessAccountFolders.AccountsOf(home, settings, known)).Rotation;
        if (AccountSet.Strays(settings, known).Count == 0
            && whole.All(each => settings.Rotation.TryGetValue(each.Key, out var order) && order.SequenceEqual(each.Value, StringComparer.Ordinal)))
        {
            Logged(home, log, Unsaid(home, known), [], []);
            return true;
        }

        try
        {
            Change(path, read => read, log: log, within: within, agents: known);
            return true;
        }
        catch (IOException error)
        {
            (log ?? MachineLog.None).Warn("accounts.pooled", ("held", true), ("refusal", error.Message));
            return false;
        }
    }

    /// <summary>
    /// This machine's pin of <paramref name="harness"/> at <paramref name="version"/>, written through <see cref="Change"/> once the version's
    /// folder is found there again under the file's lock: the downloads' rule judges a
    /// version under the same lock and renames it before letting go, so a pin that waits on it finds its version gone and is refused,
    /// rather than written naming nothing every later start refuses.
    /// </summary>
    /// <param name="home">The home whose managed versions the pin names.</param>
    /// <param name="workspace">The workspace whose own pin it is (<c>agent pin --workspace</c>), or null for the machine's.</param>
    /// <exception cref="DriverException">Nothing is installed at that version now, or the file does not read; nothing is written.</exception>
    public static HarnessSettings Pin(
        string path, string home, string harness, string version, string? workspace = null, IReadOnlyCollection<string>? agents = null) =>
        Change(path, read => Directory.Exists(HarnessAccountFolders.ManagedHome(home, harness, version))
            ? workspace is null ? read.WithVersion(harness, version) : read.WithWorkspaceVersion(workspace, harness, version)
            : throw new DriverException(
                $"`{harness}` {version} is no longer installed where Daoris keeps it — it was removed as the pin waited — so the pin was "
                + $"not written. `daoris agent pin {harness} {version}` installs it again."), agents: agents);

    /// <summary>Each stray folder already said to the machine log in this process, by home and name: said once.</summary>
    private static readonly HashSet<string> StraysSaid = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The stray folders under <paramref name="home"/> not yet said in this process, each marked said.</summary>
    private static IReadOnlyList<(string Agent, int Folders)> Unsaid(string home, IReadOnlyCollection<string> agents)
    {
        var unsaid = new List<(string, int)>();
        foreach (var stray in HarnessAccountFolders.StrayAgents(home, agents))
        {
            lock (StraysSaid)
            {
                if (!StraysSaid.Add($"{Path.GetFullPath(home)}|{stray}")) continue;
            }

            unsaid.Add((stray, HarnessAccountFolders.FolderCount(home, stray)));
        }

        return unsaid;
    }

    /// <summary>
    /// The machine log's lines of one fold: each stray folder not said before (<c>accounts.stray</c>), each
    /// stray agent dropped from the wiring with what it held, by count (<c>accounts.strayDropped</c>, the drop's receipt), and each
    /// agent folded (<c>accounts.pooled</c>). Nothing to say opens no log.
    /// </summary>
    private static void Logged(
        string home, MachineLog? log, IReadOnlyList<(string Agent, int Folders)> unsaid, IReadOnlyList<StrayAgent> dropped,
        IReadOnlyList<FoldCount> counts)
    {
        if (unsaid.Count == 0 && dropped.Count == 0 && counts.Count == 0) return;
        var own = log is null ? new MachineLog(home, "driver") : null;
        try
        {
            foreach (var (agent, folders) in unsaid)
            {
                (log ?? own!).Warn("accounts.stray", ("agent", agent), ("folders", folders), ("folded", false));
            }

            foreach (var stray in dropped)
            {
                (log ?? own!).Warn("accounts.strayDropped", ("agent", stray.Agent), ("accounts", stray.Accounts), ("receipts", stray.Receipts));
            }

            foreach (var count in counts)
            {
                (log ?? own!).Info("accounts.pooled", ("agent", count.Agent), ("accounts", count.Accounts), ("scopes", count.Scopes), ("notKept", count.NotKept));
            }
        }
        finally
        {
            own?.Dispose();
        }
    }
}
