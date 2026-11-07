using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's harness wiring: which named profile each harness runs as, per machine and optionally
/// per workspace. <b>Machine-local and tracked by nothing</b>, exactly like the remotes map.
/// </summary>
/// <remarks>
/// <para><b>The FILE is the contract</b> (`harnesses.json` under the Daoris home), and so is the directory layout
/// beside it — the CLI's `daoris agent` and this class share no code, because the driver links
/// against no CLI and the CLI has no .NET. The twins move together, the same rule the remotes map's
/// three copies established. The file is read and written by <see cref="HarnessSettingsFile"/>, changed under its lock by
/// <see cref="HarnessSettingsChange"/>, and the folders it names are <see cref="HarnessAccountFolders"/>'.</para>
///
/// <para><b>Each writer keeps the other's sections</b>: the four here, the orders (<see cref="Rotation"/>,
/// <see cref="WorkspaceRotation"/>), how each is used (<see cref="Uses"/>, <see cref="WorkspaceUses"/>), and
/// whatever a newer build wrote (<see cref="Kept"/>), which <c>RotationOrderTests</c> holds.</para>
///
/// <para><b>Silence means the harness's own home.</b> A machine that has never named a profile spawns
/// exactly as it did before this existed — the environment seam is not set at all, and the harness
/// uses the configuration home it always has. That is what keeps "Daoris works alone" true
/// through a feature about accounts: pointing someone who never asked for profiles at a fresh
/// configuration directory would log them out of their own tool.</para>
/// </remarks>
/// <param name="Defaults">Harness → profile, for this machine.</param>
/// <param name="Workspaces">Workspace → harness → profile. The natural cut: a work account for the work circle.</param>
/// <param name="Versions">
/// Harness → pinned version, for this machine — which binary a session spawns when
/// Daoris manages it. Absent means <c>PATH</c>.
/// </param>
/// <param name="WorkspaceVersions">Workspace → harness → pinned version. The same cut as the profiles.</param>
public sealed record HarnessSettings(
    IReadOnlyDictionary<string, string>? Defaults = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? Workspaces = null,
    IReadOnlyDictionary<string, string>? Versions = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? WorkspaceVersions = null)
{
    public IReadOnlyDictionary<string, string> Defaults { get; init; } =
        Defaults ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Workspaces { get; init; } =
        Workspaces ?? new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc cref="Versions"/>
    public IReadOnlyDictionary<string, string> Versions { get; init; } =
        Versions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc cref="WorkspaceVersions"/>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> WorkspaceVersions { get; init; } =
        WorkspaceVersions ?? new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Agent → the accounts rotation may use on this machine, in order. See <see cref="ResolveRotationFrom"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Rotation { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Workspace → agent → order: a work circle rotates among its own accounts, a personal one staying out.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> WorkspaceRotation { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What the file held that this build has no field for, in the order written: an editor keeps what it has
    /// no field for, as the CLI's writer keeps its <c>rest</c>, so a section a newer build writes outlives this one's save.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, JsonElement>> Kept { get; init; } = [];

    /// <summary>
    /// Why the file could not be read — null when it could, or when there is none. Carried through
    /// every <c>With…</c> so an edit made over the empty read is refused at <see cref="Save"/>.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>
    /// The file as <see cref="Load"/> understood it, written (each settings entry as the file holds it), or null for
    /// wiring built in memory: what <see cref="Save"/>'s edit is measured from.
    /// </summary>
    internal string? Understood { get; init; }

    /// <summary>Whether a file is there at <see cref="Load"/>, for <see cref="JsonKeeping.Save"/>.</summary>
    internal bool Found { get; init; }

    /// <summary>
    /// Agent → what each fold of its accounts into one set changed, oldest first, each
    /// kept until the person presses <i>Got it</i>. An older build keeps the section as one it does not know.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PoolReceipt>> Pooled { get; init; } =
        new Dictionary<string, IReadOnlyList<PoolReceipt>>(StringComparer.OrdinalIgnoreCase);

    public const string PathVariable = HarnessWiringFile.PathVariable;

    /// <summary>
    /// The conventional home, beside the driver's own config and the remotes map — under the Daoris
    /// home. A machine with no home and no override is refused, naming what to set.
    /// </summary>
    public static string DefaultPath => DaorisHome.Require(HomeEntry.Harnesses);

    /// <summary>The file every surface reads and writes — the override, or the conventional home.</summary>
    public static string ResolvePath() =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath;

    /// <summary>
    /// The same file for a reader handed its home (a driver, a loop, a chat runner): the override where set, as every surface reads
    /// it, else that home's own file, never the one the process's home variable names.
    /// </summary>
    public static string ResolvePath(string home) => HarnessWiringFile.ResolvePath(home);

    /// <summary>
    /// The Daoris home the wiring file is in, which the profile tree is under: the home whose <see cref="HomeEntry.Harnesses"/>
    /// it is, at either place of the layout.
    /// </summary>
    public static string HomeOf(string settingsPath) => DaorisHome.HomeOf(settingsPath, HomeEntry.Harnesses);

    /// <summary>A missing or unreadable file is the empty default (<see cref="HarnessSettingsFile.Load"/>), never what stops a driver coming up.</summary>
    public static HarnessSettings Load(string path) => HarnessSettingsFile.Load(path);

    /// <summary>
    /// Write the wiring back — atomically, beside-then-rename, like every write in this family — keeping what a newer build, the
    /// person or another writer put there: the edit from what it read made onto the file as it stands
    /// (<see cref="JsonKeeping.Save"/>), so a key this build has no field for stays where it is at every level, a change another writer
    /// made since the read stays, and no edit writes nothing. Each settings entry is understood as the file holds it, so a save writes
    /// an old or retired name as the new. Wiring built in memory is written whole.
    /// </summary>
    /// <exception cref="DriverException">
    /// The file could not be read when these settings were loaded: what they hold is the empty default,
    /// and writing it would replace every choice the file had. The CLI refuses the same edit. Or the file there now does not read,
    /// holds a key twice, or made an object this edit changes no longer one.
    /// </exception>
    public void Save(string path)
    {
        using var held = KeepLock.Take(path);
        SaveHolding(path);
    }

    /// <summary><see cref="Save"/> for a writer already holding the file's <see cref="KeepLock"/>.</summary>
    internal void SaveHolding(string path) => HarnessSettingsFile.SaveHolding(this, path);

    /// <inheritdoc cref="HarnessSettingsChange.Change"/>
    public static HarnessSettings Change(
        string path, Func<HarnessSettings, HarnessSettings> edit, Action? read = null, AccountChange? account = null, MachineLog? log = null,
        TimeSpan? within = null, IReadOnlyCollection<string>? agents = null) =>
        HarnessSettingsChange.Change(path, edit, read, account, log, within, agents);

    /// <inheritdoc cref="HarnessSettingsChange.Placed"/>
    public static void Placed(string path, string agent, string account, IReadOnlyCollection<string>? agents = null) =>
        HarnessSettingsChange.Placed(path, agent, account, agents);

    /// <inheritdoc cref="HarnessSettingsChange.LoadSet"/>
    public static HarnessSettings LoadSet(string path, IReadOnlyCollection<string>? agents = null) => HarnessSettingsChange.LoadSet(path, agents);

    /// <inheritdoc cref="HarnessSettingsChange.FoldNow"/>
    public static bool FoldNow(string path, MachineLog? log = null, TimeSpan? within = null, IReadOnlyCollection<string>? agents = null) =>
        HarnessSettingsChange.FoldNow(path, log, within, agents);

    /// <inheritdoc cref="HarnessSettingsChange.Pin"/>
    public static HarnessSettings Pin(
        string path, string home, string harness, string version, string? workspace = null, IReadOnlyCollection<string>? agents = null) =>
        HarnessSettingsChange.Pin(path, home, harness, version, workspace, agents);

    /// <inheritdoc cref="HarnessAccountFolders.AccountsOf"/>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AccountsOf(
        string home, HarnessSettings settings, IReadOnlyCollection<string>? agents = null) =>
        HarnessAccountFolders.AccountsOf(home, settings, agents);

    /// <inheritdoc cref="HarnessAccountFolders.AgentsKnown"/>
    public static IReadOnlyList<string> AgentsKnown(string home) => HarnessAccountFolders.AgentsKnown(home);

    /// <inheritdoc cref="HarnessAccountFolders.StrayAgents"/>
    public static IReadOnlyList<string> StrayAgents(string home, IReadOnlyCollection<string> agents) => HarnessAccountFolders.StrayAgents(home, agents);

    /// <summary>
    /// The receipts named by <paramref name="ids"/> marked said (<i>Got it</i>): a receipt
    /// appended after the page drew is not among them and stays unsaid; an id no receipt holds is passed over.
    /// </summary>
    public HarnessSettings WithSaid(string agent, IReadOnlyCollection<string> ids) =>
        Pooled.GetValueOrDefault(agent) is { } receipts
            ? WithPooled(agent, [.. receipts.Select(receipt => ids.Contains(receipt.Id) ? receipt with { Said = true } : receipt)])
            : this;

    /// <summary>
    /// An agent Daoris does not know taken out of what a fold writes: its <c>rotation</c> order and its <c>pooled</c>
    /// receipts, which a fold that read a stray folder as an agent wrote. Its other sections were written by a person or another build,
    /// and stay.
    /// </summary>
    internal HarnessSettings WithoutStray(string agent)
    {
        var rotation = new Dictionary<string, IReadOnlyList<string>>(Rotation, StringComparer.OrdinalIgnoreCase);
        rotation.Remove(agent);
        var pooled = new Dictionary<string, IReadOnlyList<PoolReceipt>>(Pooled, StringComparer.OrdinalIgnoreCase);
        pooled.Remove(agent);
        return this with { Rotation = rotation, Pooled = pooled };
    }

    /// <summary>An agent's receipts replaced; none takes its entry out.</summary>
    internal HarnessSettings WithPooled(string agent, IReadOnlyList<PoolReceipt> receipts)
    {
        var next = new Dictionary<string, IReadOnlyList<PoolReceipt>>(Pooled, StringComparer.OrdinalIgnoreCase);
        if (receipts.Count == 0) next.Remove(agent);
        else next[agent] = receipts;
        return this with { Pooled = next };
    }

    /// <summary>Said receipts beyond the last <see cref="AccountSet.SaidKept"/> dropped, each unsaid one kept.</summary>
    internal HarnessSettings WithSaidKept()
    {
        var settings = this;
        foreach (var (agent, receipts) in Pooled)
        {
            var said = receipts.Where(receipt => receipt.Said).ToList();
            if (said.Count <= AccountSet.SaidKept) continue;
            var dropped = said.Take(said.Count - AccountSet.SaidKept).ToHashSet();
            settings = settings.WithPooled(agent, [.. receipts.Where(receipt => !dropped.Contains(receipt))]);
        }

        return settings;
    }

    /// <summary>
    /// An agent's scopes taken out: its default, and its entries in every workspace's defaults,
    /// lists and settings; a workspace left with nothing is dropped. Its pinned versions are untouched.
    /// </summary>
    internal HarnessSettings WithoutScopes(string agent)
    {
        var settings = WithDefault(agent, null);
        foreach (var workspace in Workspaces.Keys.ToList()) settings = settings.WithWorkspaceDefault(workspace, agent, null);
        var rotations = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (workspace, orders) in settings.WorkspaceRotation)
        {
            var kept = Ordered(orders, agent, []);
            if (kept.Count > 0) rotations[workspace] = kept;
        }

        var uses = new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (workspace, entries) in settings.WorkspaceUses)
        {
            var kept = Placed(entries, agent, UseEntry.Empty);
            if (kept.Count > 0) uses[workspace] = kept;
        }

        return settings with { WorkspaceRotation = rotations, WorkspaceUses = uses };
    }

    /// <summary>
    /// Which profile a spawn runs as: the person's pick, then the workspace's default, then the
    /// machine's, then <b>none at all</b> — and none means the harness's own configuration home.
    /// </summary>
    public string? Resolve(string harness, string? workspace, string? chosen) =>
        ResolveFrom(harness, workspace, chosen).Value;

    /// <summary>
    /// <see cref="Resolve"/>, saying which rung answered — the one function both the spawn and
    /// the wiring panel read, so the panel cannot show an account a start would not take.
    /// </summary>
    public (string? Value, ChoiceFrom From) ResolveFrom(string harness, string? workspace, string? chosen) =>
        Choose(harness, workspace, chosen, Workspaces, Defaults);

    /// <summary>The resolution order both twins share: pick, workspace, machine, unset.</summary>
    private static (string? Value, ChoiceFrom From) Choose(
        string harness, string? workspace, string? chosen,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> circles,
        IReadOnlyDictionary<string, string> machine)
    {
        if (!string.IsNullOrWhiteSpace(chosen)) return (chosen.Trim(), ChoiceFrom.Picked);

        if (!string.IsNullOrWhiteSpace(workspace)
            && circles.TryGetValue(workspace.Trim(), out var circle)
            && circle.TryGetValue(harness, out var perCircle)
            && !string.IsNullOrWhiteSpace(perCircle))
        {
            return (perCircle, ChoiceFrom.Workspace);
        }

        return machine.TryGetValue(harness, out var held) && !string.IsNullOrWhiteSpace(held)
            ? (held, ChoiceFrom.Machine)
            : (null, ChoiceFrom.Unset);
    }

    /// <summary>Set or clear this machine's default profile for a harness. Null clears.</summary>
    public HarnessSettings WithDefault(string harness, string? profile)
    {
        var defaults = new Dictionary<string, string>(Defaults, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(profile)) defaults.Remove(harness);
        else defaults[harness] = profile.Trim();

        return this with { Defaults = defaults };
    }

    /// <summary>Set or clear one workspace's default profile for a harness. Null clears.</summary>
    public HarnessSettings WithWorkspaceDefault(string workspace, string harness, string? profile)
    {
        var workspaces = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            Workspaces, StringComparer.OrdinalIgnoreCase);
        var circle = workspaces.TryGetValue(workspace, out var existing)
            ? new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(profile)) circle.Remove(harness);
        else circle[harness] = profile.Trim();

        if (circle.Count == 0) workspaces.Remove(workspace);
        else workspaces[workspace] = circle;

        return this with { Workspaces = workspaces };
    }

    /// <summary>
    /// Which version a spawn runs at: the person's pick, the workspace's pin, the machine's, or
    /// <b>none at all</b> — and none means whatever the machine has on <c>PATH</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 The exact twin of <see cref="Resolve"/>, including what absence means. A machine that
    /// installed the harness itself, and a contributor who never ran Daoris, both keep working
    /// — which is why nothing pinned may ever come to mean an empty managed directory.
    /// </remarks>
    public string? ResolveVersion(string harness, string? workspace, string? chosen) =>
        ResolveVersionFrom(harness, workspace, chosen).Value;

    /// <summary><see cref="ResolveVersion"/>, saying which rung answered — unset is <c>PATH</c>.</summary>
    public (string? Value, ChoiceFrom From) ResolveVersionFrom(string harness, string? workspace, string? chosen) =>
        Choose(harness, workspace, chosen, WorkspaceVersions, Versions);

    /// <summary>Set or clear this machine's pinned version for a harness. Null clears.</summary>
    public HarnessSettings WithVersion(string harness, string? version)
    {
        var versions = new Dictionary<string, string>(Versions, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(version)) versions.Remove(harness);
        else versions[harness] = version.Trim();

        return this with { Versions = versions };
    }

    /// <summary>Set or clear one workspace's pinned version for a harness. Null clears.</summary>
    public HarnessSettings WithWorkspaceVersion(string workspace, string harness, string? version)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            WorkspaceVersions, StringComparer.OrdinalIgnoreCase);
        var circle = circles.TryGetValue(workspace, out var existing)
            ? new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(version)) circle.Remove(harness);
        else circle[harness] = version.Trim();

        if (circle.Count == 0) circles.Remove(workspace);
        else circles[workspace] = circle;

        return this with { WorkspaceVersions = circles };
    }

    /// <summary>
    /// Which accounts a start may rotate to, per agent, in the person's order: <c>rotation</c> for the machine and
    /// <c>workspaceRotation</c> for one workspace. Resolved as a default is: the workspace's order
    /// for the agent, else the machine's, else none — and none is no rotation at all.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>The terminal writes these too</b> (<c>daoris agent account order</c>), so <see cref="Save"/> writes them,
    /// or a screen edit would delete the person's order. <c>RotationOrderTests</c> holds the reading, the edits, the refusals
    /// and the file written.</para>
    /// <para>An account the order does not list is never rotated into: one list, not a list and a mark. A start
    /// reads its one scope instead (<see cref="Set"/>), where a workspace that names a default
    /// and no list takes no list at all; this is the file's own reading, held by its twin.</para>
    /// </remarks>
    public (IReadOnlyList<string> Order, ChoiceFrom From) ResolveRotationFrom(string agent, string? workspace)
    {
        if (!string.IsNullOrWhiteSpace(workspace)
            && WorkspaceRotation.TryGetValue(workspace.Trim(), out var circle)
            && circle.TryGetValue(agent, out var own) && own.Count > 0)
        {
            return (own, ChoiceFrom.Workspace);
        }

        return Rotation.TryGetValue(agent, out var machine) && machine.Count > 0
            ? (machine, ChoiceFrom.Machine)
            : ([], ChoiceFrom.Unset);
    }

    /// <summary>
    /// Set an agent's order, the machine's or one workspace's, replacing it whole; null or none clears it, and a
    /// workspace left with no order is dropped. The names are kept trimmed; whether each is an account here, once, is
    /// <see cref="OrderProblem"/>'s question, which a door asks first.
    /// </summary>
    /// <remarks>
    /// The set's settings are the agent's and no list's: clearing the machine's order clears none
    /// of them, and <see cref="Change"/> writes the order whole again, by id. A workspace's list cleared still takes its settings with it;
    /// no door writes one, and the next change folds one an older build wrote.
    /// </remarks>
    public HarnessSettings WithRotation(string agent, IReadOnlyList<string>? order, string? workspace = null)
    {
        IReadOnlyList<string> kept = order is null ? [] : [.. order.Select(name => name.Trim()).Where(name => name.Length > 0)];
        if (string.IsNullOrWhiteSpace(workspace)) return this with { Rotation = Ordered(Rotation, agent, kept) };

        var circles = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            WorkspaceRotation, StringComparer.OrdinalIgnoreCase);
        var circle = Ordered(circles.GetValueOrDefault(workspace.Trim()) ?? new Dictionary<string, IReadOnlyList<string>>(), agent, kept);
        if (circle.Count == 0) circles.Remove(workspace.Trim());
        else circles[workspace.Trim()] = circle;
        var scoped = this with { WorkspaceRotation = circles };
        return kept.Count > 0 ? scoped : scoped.WithUse(agent, null, workspace);
    }

    /// <summary>
    /// Why an order cannot be written, or null when it can: the first name that is no account here — the
    /// directories that exist, compared exactly, as a default's name is — or the first named twice, in any case.
    /// </summary>
    /// <param name="accounts">The agent's accounts on this machine: <see cref="Profiles"/>.</param>
    public static RotationProblem? OrderProblem(IReadOnlyCollection<string> accounts, IReadOnlyList<string> order)
    {
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in order)
        {
            var name = raw.Trim();
            if (!accounts.Contains(name, StringComparer.Ordinal)) return new RotationProblem(name, Twice: false);
            if (!named.Add(name)) return new RotationProblem(name, Twice: true);
        }

        return null;
    }

    /// <summary>
    /// The wiring with an account removed gone from it: no default names it, the machine's or a workspace's,
    /// no order does, and no scope keeps it. The rest of each order keeps its place, and an order or a workspace
    /// left naming none goes, its settings with it.
    /// </summary>
    public HarnessSettings WithoutAccount(string agent, string profile)
    {
        var settings = this;
        if (Defaults.TryGetValue(agent, out var machine) && machine == profile) settings = settings.WithDefault(agent, null);
        foreach (var (workspace, circle) in Workspaces)
        {
            if (circle.TryGetValue(agent, out var held) && held == profile) settings = settings.WithWorkspaceDefault(workspace, agent, null);
        }

        if (Rotation.TryGetValue(agent, out var order)) settings = settings.WithRotation(agent, [.. order.Where(name => name != profile)]);
        foreach (var (workspace, circle) in WorkspaceRotation)
        {
            if (circle.TryGetValue(agent, out var own))
            {
                settings = settings.WithRotation(agent, [.. own.Where(name => name != profile)], workspace);
            }
        }

        bool Keeps(UseEntry? entry) =>
            entry?["keep"] is { ValueKind: JsonValueKind.String } keep && keep.GetString()!.Trim() == profile;
        // A keep left alone in a set of one is dropped too: driven work would have none.
        var alone = Rotation.ContainsKey(agent) && (settings.Rotation.GetValueOrDefault(agent)?.Count ?? 0) < 2
            && settings.Uses.GetValueOrDefault(agent)?["keep"] is not null;
        if (Keeps(settings.Uses.GetValueOrDefault(agent)) || alone) settings = settings.WithUse(agent, new UseChange(NoKeep: true));
        foreach (var (workspace, uses) in settings.WorkspaceUses)
        {
            if (Keeps(uses.GetValueOrDefault(agent))) settings = settings.WithUse(agent, new UseChange(NoKeep: true), workspace);
        }

        return settings;
    }

    private static Dictionary<string, IReadOnlyList<string>> Ordered(
        IReadOnlyDictionary<string, IReadOnlyList<string>> orders, string agent, IReadOnlyList<string> order)
    {
        var next = new Dictionary<string, IReadOnlyList<string>>(orders, StringComparer.OrdinalIgnoreCase);
        if (order.Count == 0) next.Remove(agent);
        else next[agent] = order;
        return next;
    }

    /// <inheritdoc cref="HarnessAccountFolders.ManagedHome"/>
    public static string ManagedHome(string home, string harness, string version) => HarnessAccountFolders.ManagedHome(home, harness, version);

    /// <inheritdoc cref="HarnessAccountFolders.ManagedBinary"/>
    public static string? ManagedBinary(string home, string harness, string? version, IReadOnlyList<string> binary) =>
        HarnessAccountFolders.ManagedBinary(home, harness, version, binary);

    /// <inheritdoc cref="HarnessAccountFolders.ProfileHome"/>
    public static string ProfileHome(string home, string harness, string profile) => HarnessAccountFolders.ProfileHome(home, harness, profile);

    /// <inheritdoc cref="HarnessAccountFolders.AccountAt"/>
    public static (string Home, string Agent, string Account)? AccountAt(string profileHome) => HarnessAccountFolders.AccountAt(profileHome);

    /// <inheritdoc cref="HarnessAccountFolders.Profiles"/>
    public static IReadOnlyList<string> Profiles(string home, string harness) => HarnessAccountFolders.Profiles(home, harness);

    /// <inheritdoc cref="HarnessAccountFolders.Strays"/>
    public static IReadOnlyList<string> Strays(string home, string harness) => HarnessAccountFolders.Strays(home, harness);

    /// <inheritdoc cref="HarnessAccountFolders.NotAnAccount"/>
    public static bool NotAnAccount(string name) => HarnessAccountFolders.NotAnAccount(name);

    /// <inheritdoc cref="HarnessAccountFolders.NextAccount"/>
    public static string NextAccount(string home, string harness) => HarnessAccountFolders.NextAccount(home, harness);

    /// <inheritdoc cref="HarnessAccountFolders.RemoveProfile"/>
    public static bool RemoveProfile(string home, string harness, string profile) => HarnessAccountFolders.RemoveProfile(home, harness, profile);

    // How a scope's list is used: `rotationUse` for the machine and `workspaceRotationUse` for one workspace, per agent, beside
    // the lists, which `daoris agent account use` writes through this program; RotationUseTwinTests reads one table,
    // fixtures/rotation-use.json among the driver's tests, row for row. The rules it keeps:
    //  - Read: an entry is an object, kept whole. Each field is a value this build knows, or one it does not, which reads as the
    //    default and is said (RotationScope.Unknown); a field this build has no name for is kept and said too, so a newer build's
    //    setting outlives this one's save. The old names (`early`, `near`, `prefer`, `parallel`) are the migration's (HarnessesStep).
    //  - Written as chosen: a choice equal to the default is still written, so a later default never overturns a person's
    //    choice; absence alone means RotationUse.Default.
    //  - HarnessRoster.SelectAsync reads them through Set on the projection, the agent's one
    //    entry: `use` and `keep` choose a start's account, and the switch and the curve pass an account whose use is over the curve
    //    now. Every reader reads Set.

    /// <summary>Agent → how this machine's list is used, as the file holds it. See <see cref="Set"/>.</summary>
    public IReadOnlyDictionary<string, UseEntry> Uses { get; init; } =
        new Dictionary<string, UseEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Workspace → agent → how that workspace's own list is used, as the file holds it.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> WorkspaceUses { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The agent's one set as a start reads it: its whole order, begun at the order's first,
    /// and its one entry of settings. Read on the projection (<see cref="LoadSet"/>), which holds no default and no workspace's scope,
    /// so every workspace reads this one; on a raw file it is the machine's scope.
    /// </summary>
    /// <remarks>
    /// This reads no per-workspace scope (a workspace's scope where it names a default or a list of its own, else the
    /// machine's); an older build's reading of a file is the tests' own rule (<c>AccountSetTests</c>).
    /// </remarks>
    public RotationScope Set(string agent)
    {
        var machine = Defaults.TryGetValue(agent, out var held) && !string.IsNullOrWhiteSpace(held) ? held.Trim() : null;
        return ScopeOf(ChoiceFrom.Machine, machine, Rotation.TryGetValue(agent, out var mine) ? mine : [], Uses.GetValueOrDefault(agent));
    }

    /// <summary>
    /// Where the set's starts begin: the order's first, which every reader of the machine's default
    /// reads now; none where the agent has no account.
    /// </summary>
    public string? FirstOf(string agent) => Set(agent).Begins;

    /// <summary>
    /// This machine's scope for <paramref name="agent"/> with <paramref name="accounts"/> as its list: what a start walks
    /// where neither the machine nor the workspace names an account, the agent's Daoris
    /// accounts on this machine, begun at the first and used as the machine's use for the agent says.
    /// </summary>
    public RotationScope Over(string agent, IReadOnlyList<string> accounts) =>
        ScopeOf(ChoiceFrom.Machine, null, accounts, Uses.GetValueOrDefault(agent));

    /// <summary>
    /// An agent's settings changed, the machine's or one workspace's; null clears them, and a scope or a workspace left
    /// with none is dropped. What this build does not know is kept, unless the change sets that field.
    /// </summary>
    /// <remarks>Each value is written as given: whether it is a setting at all is the door's question, asked first.</remarks>
    public HarnessSettings WithUse(string agent, UseChange? change, string? workspace = null)
    {
        UseEntry Edited(UseEntry? entry) => change is null ? UseEntry.Empty : (entry ?? UseEntry.Empty).Changed(change);

        if (string.IsNullOrWhiteSpace(workspace)) return this with { Uses = Placed(Uses, agent, Edited(Uses.GetValueOrDefault(agent))) };

        var circle = workspace.Trim();
        var circles = new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(WorkspaceUses, StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, UseEntry> uses = circles.GetValueOrDefault(circle) ?? new Dictionary<string, UseEntry>();
        var placed = Placed(uses, agent, Edited(uses.GetValueOrDefault(agent)));
        if (placed.Count == 0) circles.Remove(circle);
        else circles[circle] = placed;
        return this with { WorkspaceUses = circles };
    }

    private static RotationScope ScopeOf(ChoiceFrom from, string? own, IReadOnlyList<string> list, UseEntry? entry)
    {
        if (list.Count == 0) return new RotationScope(from, own, [], own, RotationUse.Default, []);

        var (use, unknown) = RotationUse.Read(entry);
        if (use.Keep is { } keep && !list.Contains(keep, StringComparer.Ordinal)) use = use with { Keep = null };
        var begins = own is not null && list.Contains(own, StringComparer.Ordinal) ? own : list[0];
        return new RotationScope(from, own, list, begins, use, unknown);
    }

    private static Dictionary<string, UseEntry> Placed(IReadOnlyDictionary<string, UseEntry> uses, string agent, UseEntry entry)
    {
        var next = new Dictionary<string, UseEntry>(uses, StringComparer.OrdinalIgnoreCase);
        if (entry.IsEmpty) next.Remove(agent);
        else next[agent] = entry;
        return next;
    }
}
