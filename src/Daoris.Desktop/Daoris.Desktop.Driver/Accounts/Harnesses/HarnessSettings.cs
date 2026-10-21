using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's harness wiring: which named profile each harness runs as, per machine and optionally
/// per workspace (D49 §4). <b>Machine-local and tracked by nothing</b>, exactly like the remotes map.
/// </summary>
/// <remarks>
/// <para><b>The FILE is the contract</b> (`harnesses.json` under the Daoris home), and so is the directory layout
/// beside it — the CLI's `daoris agent` and this class share no code, because the driver links
/// against no CLI and the CLI has no .NET. The twins move together, the same rule the remotes map's
/// three copies established (WSP3).</para>
///
/// <para><b>Each writer keeps the other's sections</b> (TOOL4e): the four here, the orders (<see cref="Rotation"/>,
/// <see cref="WorkspaceRotation"/>), how each is used (<see cref="Uses"/>, <see cref="WorkspaceUses"/>, TOOL6a), and
/// whatever a newer build wrote (<see cref="Kept"/>). For the same wiring the two write the same bytes, which
/// <c>RotationTwinTests</c> and the CLI's <c>rotation.test.ts</c> hold row for row.</para>
///
/// <para><b>Silence means the harness's own home.</b> A machine that has never named a profile spawns
/// exactly as it did before this existed — the environment seam is not set at all, and the harness
/// uses the configuration home it always has. That is what keeps "Daoris works alone" (D48 §2a) true
/// through a feature about accounts: pointing someone who never asked for profiles at a fresh
/// configuration directory would log them out of their own tool.</para>
/// </remarks>
/// <param name="Defaults">Harness → profile, for this machine.</param>
/// <param name="Workspaces">Workspace → harness → profile. The natural cut: a work account for the work circle.</param>
/// <param name="Versions">
/// Harness → pinned version, for this machine (TOOL2/D57) — which binary a session spawns when
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

    /// <summary>Agent → the accounts rotation may use on this machine, in order (TOOL4e, D125 §3.1). See <see cref="ResolveRotationFrom"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Rotation { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Workspace → agent → order: a work circle rotates among its own accounts, a personal one staying out.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> WorkspaceRotation { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What the file held that this build has no field for, in the order written (TOOL4e): an editor keeps what it has
    /// no field for, as the CLI's writer keeps its <c>rest</c>, so a section a newer build writes outlives this one's save.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, JsonElement>> Kept { get; init; } = [];

    /// <summary>
    /// Why the file could not be read — null when it could, or when there was none. Carried through
    /// every <c>With…</c> so an edit made over the empty read is refused at <see cref="Save"/> (REV3).
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>
    /// The file as <see cref="Load"/> understood it, written (<see cref="Layout"/>, each settings entry as the file holds it), or null for
    /// wiring built in memory (KEEPALL1b): what <see cref="Save"/>'s edit is measured from.
    /// </summary>
    internal string? Understood { get; init; }

    /// <summary>Whether a file was there at <see cref="Load"/>, for <see cref="JsonKeeping.Save"/>.</summary>
    internal bool Found { get; init; }

    /// <summary>The sections this build reads and writes; anything else is <see cref="Kept"/>. Spelled as the CLI's are.</summary>
    private static readonly HashSet<string> Sections = new(StringComparer.Ordinal)
    {
        "defaults", "workspaces", "versions", "workspaceVersions", "rotation", "workspaceRotation", "rotationUse", "workspaceRotationUse",
    };

    public const string PathVariable = "DAORIS_HARNESS_CONFIG";

    /// <summary>
    /// The conventional home, beside the driver's own config and the remotes map — under the Daoris
    /// home (D63). A machine with no home and no override is refused, naming what to set.
    /// </summary>
    public static string DefaultPath => DaorisHome.Require(HomeEntry.Harnesses);

    /// <summary>The file every surface reads and writes — the override, or the conventional home.</summary>
    public static string ResolvePath() =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath;

    /// <summary>
    /// The same file for a reader handed its home (a driver, a loop, a chat runner): the override where set, as every surface reads
    /// it, else that home's own file, never the one the process's home variable names.
    /// </summary>
    public static string ResolvePath(string home) =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DaorisHome.Entry(home, HomeEntry.Harnesses);

    /// <summary>
    /// The Daoris home (D63) the wiring file is in, which the profile tree is under: the home whose <see cref="HomeEntry.Harnesses"/>
    /// it is, at its new place or its old (HOMELAYOUT3).
    /// </summary>
    public static string HomeOf(string settingsPath) => DaorisHome.HomeOf(settingsPath, HomeEntry.Harnesses);

    /// <summary>
    /// A missing file is a machine that named no profiles — not an error. Neither is an unreadable
    /// one: this is wiring, and absent wiring is the documented default (D21), so a hand-mangled file
    /// must never be what stops a driver coming up.
    /// </summary>
    public static HarnessSettings Load(string path)
    {
        var nothing = new HarnessSettings();
        if (!File.Exists(path)) return nothing with { Understood = nothing.Layout(asRead: true) };

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            // A file that is no object reads as none, and an edit over it is refused at the write, as the CLI refuses it.
            if (document.RootElement.ValueKind != JsonValueKind.Object) return nothing with { Understood = nothing.Layout(asRead: true), Found = true };

            var defaults = ReadMap(document.RootElement, "defaults");
            var workspaces = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            if (document.RootElement.TryGetProperty("workspaces", out var circles)
                && circles.ValueKind == JsonValueKind.Object)
            {
                foreach (var circle in circles.EnumerateObject())
                {
                    if (circle.Value.ValueKind != JsonValueKind.Object) continue;
                    workspaces[circle.Name] = ReadMap(circle.Value, null);
                }
            }

            var settings = new HarnessSettings(
                defaults, workspaces,
                ReadMap(document.RootElement, "versions"),
                ReadCircles(document.RootElement, "workspaceVersions"))
            {
                Rotation = ReadOrders(document.RootElement, "rotation"),
                WorkspaceRotation = ReadOrderCircles(document.RootElement, "workspaceRotation"),
                Uses = ReadUses(document.RootElement, "rotationUse"),
                WorkspaceUses = ReadUseCircles(document.RootElement, "workspaceRotationUse"),
                Kept = [.. document.RootElement.EnumerateObject()
                    .Where(property => !Sections.Contains(property.Name))
                    .Select(property => KeyValuePair.Create(property.Name, property.Value.Clone()))],
                Found = true,
            };
            return settings with { Understood = settings.Layout(asRead: true) };
        }
        catch (Exception error)
            when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HarnessSettings { Problem = $"{Path.GetFileName(path)} could not be read ({error.Message})" };
        }
    }

    /// <summary>
    /// Write the wiring back — atomically, beside-then-rename, like every write in this family — keeping what a newer build, the
    /// person or another writer put there: the edit from what was read made onto the file as it stands (KEEPALL1b, D173's note;
    /// <see cref="JsonKeeping.Save"/>), so a key this build has no field for stays where it is at every level, a change another writer
    /// made since the read stays, and no edit writes nothing. Each settings entry was understood as the file holds it, so a save writes
    /// an old or retired name as the new (D165 §6). Wiring built in memory is written whole. The CLI's <c>writeHarnessSettings</c> does
    /// the same.
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

    /// <summary>
    /// One change of the wiring under its keep lock (PUSH1f2e, the alone-path design §1.4): the lock, the file read, the edit decided
    /// from it as it stands, the whole write, so a change another writer lands meanwhile, the window's or a terminal's, waits its turn
    /// rather than being decided against. Every writer of <c>harnesses.json</c> goes through it: the window's routes, Ask Daoris's apply,
    /// and the terminal's account verbs.
    /// </summary>
    /// <param name="edit">The change, from the wiring as read under the lock; it may throw to refuse it, nothing written.</param>
    /// <param name="read">Called once the file is read, before the edit is written: a test's other writer, held off by the lock.</param>
    /// <returns>The wiring as written.</returns>
    /// <exception cref="DriverException">The file does not read, or the edit is refused; nothing was written.</exception>
    /// <exception cref="IOException">Another writer held the file's lock for the whole of the wait; nothing was written.</exception>
    public static HarnessSettings Change(string path, Func<HarnessSettings, HarnessSettings> edit, Action? read = null)
    {
        using var held = KeepLock.Take(path);
        var settings = Load(path);
        read?.Invoke();
        var changed = edit(settings);
        changed.SaveHolding(path);
        return changed;
    }

    /// <summary>
    /// This machine's pin of <paramref name="harness"/> at <paramref name="version"/>, written through <see cref="Change"/> once the version's
    /// folder is found there again under the file's lock (KEEP1b, the cleanup design §3.5; Fable F2.2, F2.3): the downloads' rule judges a
    /// version under the same lock and renames it before letting go, so a pin that waited on it finds its version gone and is refused,
    /// rather than written naming nothing every later start refuses.
    /// </summary>
    /// <param name="home">The home whose managed versions the pin names.</param>
    /// <param name="workspace">The workspace whose own pin it is (PUSH1f2e: <c>agent pin --workspace</c>), or null for the machine's.</param>
    /// <exception cref="DriverException">Nothing is installed at that version now, or the file does not read; nothing was written.</exception>
    public static HarnessSettings Pin(string path, string home, string harness, string version, string? workspace = null) =>
        Change(path, read => Directory.Exists(ManagedHome(home, harness, version))
            ? workspace is null ? read.WithVersion(harness, version) : read.WithWorkspaceVersion(workspace, harness, version)
            : throw new DriverException(
                $"`{harness}` {version} is no longer installed where Daoris keeps it — it was removed as the pin waited — so the pin was "
                + $"not written. `daoris agent pin {harness} {version}` installs it again."));

    /// <summary><see cref="Save"/> for a writer already holding the file's <see cref="KeepLock"/>.</summary>
    private void SaveHolding(string path)
    {
        if (Problem is not null)
        {
            throw new DriverException(
                $"{Problem}, so this edit was not written — it would have replaced every choice in it. "
                + "Fix the file or remove it, then make the change again.");
        }

        if (Understood is { } understood) JsonKeeping.SaveHolding(path, Found, understood, Layout(asRead: false));
        // LF on every platform, as the CLI's writer writes it: both write this file, the same wiring as the same bytes.
        else JsonFile.Write(path, writer => Write(writer, asRead: false));
    }

    /// <summary>The wiring in the writer's layout; <paramref name="asRead"/> writes each settings entry as the file held it.</summary>
    private string Layout(bool asRead) => JsonFile.Serialize(writer => Write(writer, asRead));

    private void Write(Utf8JsonWriter writer, bool asRead)
    {
        writer.WriteStartObject();

        // What this build has no field for goes first, as the CLI's writer puts its `rest` first (TOOL4e).
        foreach (var (name, value) in Kept)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }

        writer.WriteStartObject("defaults");
        foreach (var (harness, profile) in Defaults.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            writer.WriteString(harness, profile);
        }

        writer.WriteEndObject();

        WriteCircles(writer, "workspaces", Workspaces);

        // 🔴 The pins go out too, or this write DELETES what `daoris agent pin` put there.
        // Both artefacts write this one file, and a save that knew only about profiles would
        // compile, pass every profile test, and silently lose somebody's toolchain (TOOL2).
        writer.WriteStartObject("versions");
        foreach (var (harness, version) in Versions.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            writer.WriteString(harness, version);
        }

        writer.WriteEndObject();
        WriteCircles(writer, "workspaceVersions", WorkspaceVersions);

        // 🔴 The orders go out too (TOOL4e), or a screen edit DELETES what `daoris agent profile order` wrote, the
        // way a save that knew only profiles once would have deleted the pins. Written only when set: absent is no
        // rotation, and a file that never chose an order should not start carrying an empty one.
        if (Rotation.Any(order => order.Value.Count > 0))
        {
            writer.WriteStartObject("rotation");
            WriteOrders(writer, Rotation);
            writer.WriteEndObject();
        }

        // TOOL6a: how each list is used goes out beside it, for the same reason the orders do.
        WriteUses(writer, "rotationUse", Uses, asRead);

        if (WorkspaceRotation.Any(circle => circle.Value.Any(order => order.Value.Count > 0)))
        {
            writer.WriteStartObject("workspaceRotation");
            foreach (var (workspace, orders) in WorkspaceRotation.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (!orders.Any(order => order.Value.Count > 0)) continue;
                writer.WriteStartObject(workspace);
                WriteOrders(writer, orders);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        WriteUseCircles(writer, "workspaceRotationUse", WorkspaceUses, asRead);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Which profile a spawn runs as: the person's pick, then the workspace's default, then the
    /// machine's, then <b>none at all</b> — and none means the harness's own configuration home.
    /// </summary>
    public string? Resolve(string harness, string? workspace, string? chosen) =>
        ResolveFrom(harness, workspace, chosen).Value;

    /// <summary>
    /// <see cref="Resolve"/>, saying which rung answered (MAP1b) — the one function both the spawn and
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
    /// <b>none at all</b> — and none means whatever the machine has on <c>PATH</c> (TOOL2/D57).
    /// </summary>
    /// <remarks>
    /// 🔴 The exact twin of <see cref="Resolve"/>, including what absence means. A machine that
    /// installed the harness itself, and a contributor who never ran Daoris, both keep working
    /// (D48 §2a) — which is why nothing pinned may ever come to mean an empty managed directory.
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
    /// <c>workspaceRotation</c> for one workspace (TOOL4e, D125 §3.1). Resolved as a default is: the workspace's order
    /// for the agent, else the machine's, else none — and none is no rotation at all.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>The CLI writes these too</b> (<c>daoris agent profile order</c>), so <see cref="Save"/> writes them,
    /// or a screen edit would delete the person's order. <c>RotationTwinTests</c> and the CLI's <c>rotation.test.ts</c>
    /// hold the reading, the edits, the refusals and the file both write, row for row.</para>
    /// <para>An account the order does not list is never rotated into (§3.1): one list, not a list and a mark. A start
    /// reads its one scope instead (<see cref="ResolveScope"/>, TOOL6b, D130 §3.1), where a workspace that names a default
    /// and no list takes no list at all; this is D125's reading of the file, held by its twin.</para>
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
    /// <remarks>A list cleared takes its scope's settings with it (TOOL6a, D130 §2): they come with the list.</remarks>
    public HarnessSettings WithRotation(string agent, IReadOnlyList<string>? order, string? workspace = null)
    {
        IReadOnlyList<string> kept = order is null ? [] : [.. order.Select(name => name.Trim()).Where(name => name.Length > 0)];
        if (string.IsNullOrWhiteSpace(workspace))
        {
            var machine = this with { Rotation = Ordered(Rotation, agent, kept) };
            return kept.Count > 0 ? machine : machine.WithUse(agent, null);
        }

        var circles = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            WorkspaceRotation, StringComparer.OrdinalIgnoreCase);
        var circle = Ordered(circles.GetValueOrDefault(workspace.Trim()) ?? new Dictionary<string, IReadOnlyList<string>>(), agent, kept);
        if (circle.Count == 0) circles.Remove(workspace.Trim());
        else circles[workspace.Trim()] = circle;
        var scoped = this with { WorkspaceRotation = circles };
        return kept.Count > 0 ? scoped : scoped.WithUse(agent, null, workspace);
    }

    /// <summary>
    /// Why an order cannot be written, or null when it can (D125 §3.1): the first name that is no account here — the
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
    /// The wiring with an account removed gone from it (D66 §3): no default names it, the machine's or a workspace's,
    /// no order does, and no scope keeps it (TOOL6a). The rest of each order keeps its place, and an order or a workspace
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
        if (Keeps(settings.Uses.GetValueOrDefault(agent))) settings = settings.WithUse(agent, new UseChange(NoKeep: true));
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

    /// <summary>Where a managed version of a harness lives. Daoris owns this location, binary and all.</summary>
    public static string ManagedHome(string home, string harness, string version) =>
        DaorisHome.Entry(home, HomeEntry.Toolchain, Name(harness, "agent name"), Name(version, "version"));

    /// <summary>
    /// The executable inside a managed install, or null when nothing is pinned or nothing is
    /// installed at the pin.
    /// </summary>
    /// <remarks>
    /// <b>Two layouts, the vendor's first</b> (AGT2b). A pin from a maker's own channel lands as
    /// <c>&lt;dir&gt;/bin/&lt;binary&gt;</c> — <c>.exe</c> on Windows — and is moved there only once it
    /// verified, so finding it is the proof. Then <b>npm's layout, for what ships only there</b>:
    /// <c>--prefix &lt;dir&gt;</c> puts the shims in <c>&lt;dir&gt;/node_modules/.bin</c>, with a
    /// <c>.cmd</c> beside the shell script on Windows. A pin npm made before AGT2b still resolves. The
    /// CLI's <c>managedBinary</c> is the twin.
    ///
    /// <para>🔴 A pin whose directory is not there answers null, and every caller falls back to
    /// <c>PATH</c> and <b>says so</b>. Silently running a different tool than the one the person
    /// pinned, and reporting success, is the failure this shape exists to prevent.</para>
    /// </remarks>
    public static string? ManagedBinary(
        string home, string harness, string? version, IReadOnlyList<string> binary)
    {
        if (string.IsNullOrWhiteSpace(version) || binary.Count == 0) return null;

        var managed = ManagedHome(home, harness, version);
        var vendor = Path.Combine(managed, "bin", OperatingSystem.IsWindows() ? binary[0] + ".exe" : binary[0]);
        if (File.Exists(vendor)) return vendor;

        var bin = Path.Combine(managed, "node_modules", ".bin");
        foreach (var candidate in new[]
                 {
                     Path.Combine(bin, binary[0] + ".cmd"),
                     Path.Combine(bin, binary[0]),
                 })
        {
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Where a named profile's configuration home is. <b>Daoris owns this location and nothing
    /// inside it.</b>
    /// </summary>
    public static string ProfileHome(string home, string harness, string profile) =>
        DaorisHome.Entry(home, HomeEntry.AgentHomes, Name(harness, "agent name"), Name(profile, "profile name"));

    /// <summary>
    /// The home, the agent and the account a profile's folder is, read back from the layout <see cref="ProfileHome"/> makes, at
    /// either place of <see cref="HomeEntry.AgentHomes"/>: <c>&lt;home&gt;/harnesses/&lt;agent&gt;/&lt;account&gt;</c> where an
    /// account was made before HOMELAYOUT3, <c>&lt;Daoris folder&gt;/profiles/agents/&lt;agent&gt;/&lt;account&gt;</c> since. Null
    /// for a folder outside both.
    /// </summary>
    public static (string Home, string Agent, string Account)? AccountAt(string profileHome)
    {
        var account = new DirectoryInfo(Path.GetFullPath(profileHome));
        if (account.Parent is not { Parent: { } holder } agent) return null;
        var home = holder.Name == "harnesses" ? holder.Parent?.FullName
            : holder is { Name: "agents", Parent: { Name: "profiles", Parent: { } folder } } ? folder.FullName
            : null;
        return home is null ? null : (home, agent.Name, account.Name);
    }

    /// <summary>
    /// The profiles that exist for a harness — <b>the directories that exist</b>, sorted. There is no
    /// register of profiles to disagree with the disk, which is the same reason the registry became
    /// the authority rather than a view over a scan (D48 §3): one truth, not two. Both places of the
    /// account homes are read, as a list of an entry's items is (D172 point 5, HOMELAYOUT2). A lock's
    /// folder or a hidden one is no account (<see cref="NotAnAccount"/>, HELPSTUCK1): <see cref="Strays"/> names them.
    /// </summary>
    public static IReadOnlyList<string> Profiles(string home, string harness) =>
        [.. Folders(home, harness).Where(name => !NotAnAccount(name))];

    /// <summary>
    /// The folders beside a harness's accounts that are no account (HELPSTUCK1), sorted: never listed, chosen, walked or removed
    /// as one, and never touched here either, since one may hold a sign-in a person made in it while it was listed. The
    /// roster's report names them (<see cref="HarnessReport.Strays"/>), and Ask Daoris's room says them.
    /// </summary>
    public static IReadOnlyList<string> Strays(string home, string harness) =>
        [.. Folders(home, harness).Where(NotAnAccount)];

    /// <summary>
    /// Whether a folder beside the accounts is no account (HELPSTUCK1, twin rule 1): a lock's, its name ending <c>.lock</c> in
    /// any case, since a lockfile library locks a folder by making <c>&lt;folder&gt;.lock</c> beside it, and Claude Code locks
    /// its configuration home so; or a hidden one, its name starting <c>.</c>. Listed as accounts, two such lock folders on the
    /// owner's machine were walked to, signed in to and run in, and a lock of the account each is named after then met a home.
    /// The CLI's <c>notAnAccount</c> is the twin, and the two <c>profiles</c> tables hold the same rows.
    /// </summary>
    public static bool NotAnAccount(string name) =>
        name.StartsWith('.') || name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);

    /// <summary>Every folder under a harness's account homes, in both places, by name, sorted.</summary>
    private static IEnumerable<string> Folders(string home, string harness) =>
        DaorisHome.Places(home, HomeEntry.AgentHomes, Name(harness, "agent name"))
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>
    /// The id an account made by signing in, or by a key, gets (D66 §3, ACCT2): a fresh one, never reused
    /// (<see cref="AccountNames.NewId"/>), where it was the first free <c>account-N</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A neutral id, never who signed in.</b> It is needed before the sign-in starts, when nobody knows whose it is;
    /// and renaming the directory afterwards would move a home a harness may have keyed its credential to. What a person
    /// reads is the account's name, offered at the sign-in's end as who signed in and kept only where the person keeps it
    /// (<see cref="AccountNames"/>). Twin rule 5: the CLI's <c>login --new</c> and <c>key</c> draw the same way.
    /// </remarks>
    public static string NextAccount(string home, string harness) => AccountNames.NewId(home, harness);

    /// <summary>
    /// Delete an account: its directory, credentials included (D66 §3). True when there was one.
    /// </summary>
    /// <remarks>
    /// <para>The name is judged first (<see cref="ProfileHome"/>), so a name pointing anywhere else is
    /// refused before anything is touched — the tool's own configuration home is not under here and
    /// can never be named.</para>
    ///
    /// <para>🔴 <b>Read-only files are cleared first.</b> A harness that clones into its home leaves
    /// read-only git objects there, and a recursive delete refuses them on Windows — an account
    /// removed on screen would stay on disk because of an attribute. Links are not followed: a
    /// directory link inside is removed as a link, and what it points at is not Daoris's.</para>
    /// </remarks>
    public static bool RemoveProfile(string home, string harness, string profile)
    {
        var directory = ProfileHome(home, harness, profile);
        if (!Directory.Exists(directory))
        {
            // A key whose directory someone removed by hand still goes: nothing is left behind here. Nor its name (ACCT2).
            HarnessKeys.Remove(home, harness, profile);
            AccountCooling.End(home, harness, profile, DateTimeOffset.UtcNow);
            AccountNames.Forget(home, harness, profile);
            return false;
        }

        var walk = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };
        foreach (var file in Directory.EnumerateFiles(directory, "*", walk))
        {
            var attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(directory, recursive: true);
        // An account that was a key goes with its key (AGT3): a removed account keeps nothing here. Nor its cool-off
        // (TOOL4e): an account made later by `profile add` under its name starts afresh. Nor its name (ACCT2).
        HarnessKeys.Remove(home, harness, profile);
        AccountCooling.End(home, harness, profile, DateTimeOffset.UtcNow);
        AccountNames.Forget(home, harness, profile);
        return true;
    }

    /// <summary>
    /// A name that will become a directory. Refused rather than normalised: a name carrying a
    /// separator or a traversal is a request to own a location somewhere else, and quietly rewriting
    /// it would put a profile where nobody would look for it.
    /// </summary>
    private static string Name(string value, string what)
    {
        var trimmed = value?.Trim() ?? "";
        var bad = trimmed.Length == 0
            || trimmed is "." or ".."
            || trimmed.Contains('/') || trimmed.Contains('\\')
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

        return bad
            ? throw new DriverException(
                $"`{value}` is not a usable {what} — letters, digits, dashes and dots. It becomes a "
                + "directory Daoris owns the location of, so it may not point anywhere else.")
            : trimmed;
    }

    /// <summary>One workspace → harness → value map, read from a named property.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadCircles(
        JsonElement root, string property)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var circle in element.EnumerateObject())
            {
                if (circle.Value.ValueKind != JsonValueKind.Object) continue;
                circles[circle.Name] = ReadMap(circle.Value, null);
            }
        }

        return circles;
    }

    /// <summary>The write half of <see cref="ReadCircles"/>. An empty circle is not written at all.</summary>
    private static void WriteCircles(
        Utf8JsonWriter writer, string property,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> circles)
    {
        writer.WriteStartObject(property);
        foreach (var (workspace, map) in circles.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (map.Count == 0) continue;
            writer.WriteStartObject(workspace);
            foreach (var (harness, value) in map.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                writer.WriteString(harness, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// One agent → order map (TOOL4e): each list's names trimmed, a blank or a name that is not text skipped, a name
    /// written twice in any case read once where first written; a list that is not one, or names nobody, is none.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadOrders(JsonElement parent, string? property)
    {
        var orders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null
            && (!parent.TryGetProperty(property, out element) || element.ValueKind != JsonValueKind.Object))
        {
            return orders;
        }

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Array) continue;
            var order = new List<string>();
            foreach (var item in entry.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString()!.Trim() is { Length: > 0 } name
                    && !order.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    order.Add(name);
                }
            }

            if (order.Count > 0) orders[entry.Name] = order;
        }

        return orders;
    }

    /// <summary>One workspace → agent → order map, read as <see cref="ReadOrders"/> reads each; a workspace naming none is none.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadOrderCircles(
        JsonElement root, string property)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object) return circles;

        foreach (var circle in element.EnumerateObject())
        {
            if (circle.Value.ValueKind != JsonValueKind.Object) continue;
            var orders = ReadOrders(circle.Value, null);
            if (orders.Count > 0) circles[circle.Name] = orders;
        }

        return circles;
    }

    /// <summary>The write half of <see cref="ReadOrders"/>: agents in order of name, each list in the person's order.</summary>
    private static void WriteOrders(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<string>> orders)
    {
        foreach (var (agent, order) in orders.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (order.Count == 0) continue;
            writer.WriteStartArray(agent);
            foreach (var name in order) writer.WriteStringValue(name);
            writer.WriteEndArray();
        }
    }

    private static IReadOnlyDictionary<string, string> ReadMap(JsonElement parent, string? property)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null
            && (!parent.TryGetProperty(property, out element) || element.ValueKind != JsonValueKind.Object))
        {
            return map;
        }

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.String
                && entry.Value.GetString() is { Length: > 0 } profile)
            {
                map[entry.Name] = profile;
            }
        }

        return map;
    }

    // How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14 as §16.6 amends them; D165 point 2 and
    // docs/2026-10-02-account-use-design.md, docs/2026-10-12-account-switch-design.md §2 and §6, SWITCH1a): `rotationUse` for
    // the machine and `workspaceRotationUse` for one workspace, per agent, beside the lists, which `daoris agent profile use` writes
    // through this program (PUSH1f2e3; the CLI's rotation.ts, its twin, left with PUSH1f2e); RotationUseTwinTests reads one table,
    // fixtures/rotation-use.json among the driver's tests, row for row. The rules it keeps:
    //  - Read: an entry is an object, kept whole; `prefer` and `parallel`, which §16.6 retired, are read as nothing and said as
    //    nothing, and go at the next write (KEEPALL1b: held as read, so the save's edit takes them out). Each other field is a value this build knows, or one it does not, which reads as today's default and is said
    //    (RotationScope.Unknown); a field this build has no name for is kept and said too, so a newer build's setting outlives
    //    this one's save.
    //  - The old names read on (D165 §6): `early` and `near`, which only an older build writes, are taken out and applied as a
    //    change applies them (UseChange), over the entry's own switch and curve; a value of theirs that does not read is said, and goes.
    //  - Resolved as one scope (§2 rule 1): the workspace's, when it names a default or a list of its own for the agent, else the
    //    machine's. A scope's settings are read only with its list; with none it is its default alone.
    //  - Written as chosen: a choice equal to today's default is still written, so a later default never overturns a person's
    //    choice; absence alone means RotationUse.Default. The old names are never written: a writer writes `switch` and `curve`,
    //    and since a save is measured from each entry as the file held it (KEEPALL1b, D173's note), every save does so.
    //  - HarnessRoster.SelectAsync reads them through ResolveScope (TOOL6b): `use` and `keep` choose a start's account, and the
    //    switch and the curve pass an account whose use is over the curve now (SWITCH1b-ii).

    /// <summary>Agent → how this machine's list is used, as the file holds it. See <see cref="ResolveScope"/>.</summary>
    public IReadOnlyDictionary<string, UseEntry> Uses { get; init; } =
        new Dictionary<string, UseEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Workspace → agent → how that workspace's own list is used, as the file holds it.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> WorkspaceUses { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The scope a start for <paramref name="agent"/> reads (D130 §2 rule 1): the workspace's, when it names a default or a
    /// list of its own for the agent, else the machine's — its own default, its list, where it begins, and how it is used.
    /// </summary>
    /// <remarks>What <see cref="HarnessRoster.SelectAsync"/> reads for every start but a pick (TOOL6b).</remarks>
    public RotationScope ResolveScope(string agent, string? workspace)
    {
        var circle = workspace?.Trim();
        if (!string.IsNullOrEmpty(circle))
        {
            var own = Workspaces.TryGetValue(circle, out var defaults) && defaults.TryGetValue(agent, out var named)
                && !string.IsNullOrWhiteSpace(named) ? named.Trim() : null;
            IReadOnlyList<string> list = WorkspaceRotation.TryGetValue(circle, out var orders) && orders.TryGetValue(agent, out var order)
                ? order : [];
            if (own is not null || list.Count > 0)
            {
                return ScopeOf(ChoiceFrom.Workspace, own, list, WorkspaceUses.TryGetValue(circle, out var uses) ? uses.GetValueOrDefault(agent) : null);
            }
        }

        var machine = Defaults.TryGetValue(agent, out var held) && !string.IsNullOrWhiteSpace(held) ? held.Trim() : null;
        return ScopeOf(ChoiceFrom.Machine, machine, Rotation.TryGetValue(agent, out var mine) ? mine : [], Uses.GetValueOrDefault(agent));
    }

    /// <summary>
    /// This machine's scope for <paramref name="agent"/> with <paramref name="accounts"/> as its list (OWNSPACE1a, D175's
    /// OWNSPACE1a note): what a start walks where neither the machine nor the workspace names an account, the agent's Daoris
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

    /// <summary>One agent → settings map: each entry an object kept whole; anything else, or an entry naming nothing, is none.</summary>
    private static IReadOnlyDictionary<string, UseEntry> ReadUses(JsonElement parent, string? property)
    {
        var uses = new Dictionary<string, UseEntry>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null && !parent.TryGetProperty(property, out element)) return uses;
        if (element.ValueKind != JsonValueKind.Object) return uses;

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            List<KeyValuePair<string, JsonElement>> fields = [.. entry.Value.EnumerateObject()
                .Select(field => KeyValuePair.Create(field.Name, field.Value.Clone()))];
            if (fields.Count > 0) uses[entry.Name] = new UseEntry(fields);
        }

        return uses;
    }

    /// <summary>One workspace → agent → settings map, each read as <see cref="ReadUses"/> reads one; a workspace naming none is none.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> ReadUseCircles(JsonElement root, string property)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object) return circles;

        foreach (var circle in element.EnumerateObject())
        {
            var uses = ReadUses(circle.Value, null);
            if (uses.Count > 0) circles[circle.Name] = uses;
        }

        return circles;
    }

    /// <summary>A settings section, written only where an entry is set: agents in order of name.</summary>
    private static void WriteUses(Utf8JsonWriter writer, string property, IReadOnlyDictionary<string, UseEntry> uses, bool asRead)
    {
        var written = Written(uses, asRead);
        if (written.Count == 0) return;
        writer.WriteStartObject(property);
        WriteEntries(writer, written, asRead);
        writer.WriteEndObject();
    }

    /// <summary>The workspaces' settings section, written only where one is set: workspaces in order of name.</summary>
    private static void WriteUseCircles(
        Utf8JsonWriter writer, string property, IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> circles, bool asRead)
    {
        var written = circles.OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(circle => (Workspace: circle.Key, Uses: Written(circle.Value, asRead)))
            .Where(circle => circle.Uses.Count > 0)
            .ToList();
        if (written.Count == 0) return;
        writer.WriteStartObject(property);
        foreach (var (workspace, uses) in written)
        {
            writer.WriteStartObject(workspace);
            WriteEntries(writer, uses, asRead);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Each agent's entry as written (<see cref="UseEntry.Written"/>), or as the file held it where <paramref name="asRead"/> says, its old
    /// and retired names included (KEEPALL1b): in order of name, one written as nothing left out.
    /// </summary>
    private static List<(string Agent, UseEntry Entry)> Written(IReadOnlyDictionary<string, UseEntry> uses, bool asRead) =>
        [.. uses.OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(entry => (Agent: entry.Key, Entry: asRead ? entry.Value : entry.Value.Written()))
            .Where(entry => !entry.Entry.IsEmpty)];

    private static void WriteEntries(Utf8JsonWriter writer, List<(string Agent, UseEntry Entry)> uses, bool asRead)
    {
        foreach (var (agent, entry) in uses)
        {
            writer.WritePropertyName(agent);
            if (asRead) entry.WriteAsRead(writer);
            else
            {
                writer.WriteStartObject();
                entry.WriteTo(writer);
                writer.WriteEndObject();
            }
        }
    }

    // An account put into a scope's list at a person's word, and where each account runs (ACCT1, D125's ACCT1 note; D130 §3.1):
    // a new account's sign-in ends by saying which lists and defaults hold it, both doors can add it to a list in the same step,
    // and an account no list and no default holds says so where it is listed. A twin of the CLI's rotation.ts (joinProblem,
    // withJoined, placesOf); AccountJoinTwinTests and the CLI's account-join.test.ts hold the same tables, row for row.
    // Accounts compare exactly, as the wiring compares a default or a list's names with a folder.
    // Found on the install, 2026-10-04: a person signed in to re-enable a signed-out account, got a new account no list held,
    // and the work kept starting on the empty one. A new account in no list is now said at its sign-in's end, with the list it
    // may join, and listed as running nowhere.

    /// <summary>
    /// Why an account cannot join <paramref name="workspace"/>'s list, or null where it can: a workspace that names no default
    /// and no list of its own for the agent takes this machine's scope (D130 §2 rule 1), so its list is this machine's, and a
    /// list of its own would move its starts off every account the machine's holds. Null is this machine's list, which any
    /// account may join.
    /// </summary>
    public JoinProblem? JoinProblemOf(string agent, string? workspace) =>
        workspace?.Trim() is { Length: > 0 } circle && ResolveScope(agent, circle).From != ChoiceFrom.Workspace
            ? new JoinProblem(circle)
            : null;

    /// <summary>
    /// The wiring with <paramref name="account"/> in the scope's own list (D130 §3.1): appended where the list lacks it, a list
    /// begun at the scope's default where it has none, or of the account alone where the scope names nobody. The list's
    /// settings stay. Whether the scope may be joined is <see cref="JoinProblemOf"/>'s question, which a door asks first.
    /// </summary>
    public HarnessSettings WithJoined(string agent, string account, string? workspace)
    {
        var scope = ResolveScope(agent, workspace);
        IReadOnlyList<string> list = scope.List.Count > 0
            ? scope.List.Contains(account, StringComparer.Ordinal) ? scope.List : [.. scope.List, account]
            : scope.Default is { } begins && begins != account ? [begins, account] : [account];
        return WithRotation(agent, list, workspace?.Trim() is { Length: > 0 } circle ? circle : null);
    }

    /// <summary>
    /// Where <paramref name="account"/> runs: each scope whose own list holds it or whose own default names it, this machine
    /// first and then each workspace by name. None is an account no start runs on, which both doors say.
    /// </summary>
    public IReadOnlyList<AccountPlace> PlacesOf(string agent, string account)
    {
        var places = new List<AccountPlace>();
        Add(null, Rotation.GetValueOrDefault(agent), Defaults.GetValueOrDefault(agent));

        foreach (var workspace in Workspaces.Keys.Concat(WorkspaceRotation.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            Add(
                workspace,
                WorkspaceRotation.TryGetValue(workspace, out var orders) ? orders.GetValueOrDefault(agent) : null,
                Workspaces.TryGetValue(workspace, out var defaults) ? defaults.GetValueOrDefault(agent) : null);
        }

        return places;

        void Add(string? workspace, IReadOnlyList<string>? list, string? named)
        {
            var listed = list?.Contains(account, StringComparer.Ordinal) == true;
            var defaulted = named?.Trim() == account;
            if (listed || defaulted) places.Add(new AccountPlace(workspace, listed, defaulted));
        }
    }
}
