using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>Whether a profile has been logged into — as the harness itself answers it.</summary>
public enum LoginState
{
    /// <summary>
    /// Nobody could say. The harness has no way to be asked, or answered in a shape this build does
    /// not read. <b>Permissive at spawn</b>, deliberately: refusing work because a tool reworded its
    /// own status line would be a worse failure than letting the harness refuse for itself.
    /// </summary>
    Unknown,

    /// <summary>The harness says this configuration home is logged in.</summary>
    In,

    /// <summary>The harness says it is not. This is the one state that refuses a spawn.</summary>
    Out,
}

/// <summary>
/// How a harness answers "is this configuration home logged in?" — the arguments to ask with, and the
/// two shapes its answer takes.
/// </summary>
/// <remarks>
/// <para><b>Patterns, not exit codes.</b> Both supported harnesses exit 0 whether or not they are
/// logged in, so the exit code says nothing and the OUTPUT is the answer. Verified against the real
/// binaries before this was written, because every clause of it is a claim about somebody else's
/// tool.</para>
///
/// <para><b>Neither pattern matching means <see cref="LoginState.Unknown"/></b>, never logged-out: an
/// answer this build cannot read is not evidence.</para>
/// </remarks>
/// <param name="Arguments">What to run, under the profile's configuration home.</param>
/// <param name="LoggedIn">A pattern whose presence means yes.</param>
/// <param name="LoggedOut">A pattern whose presence means no.</param>
/// <param name="Account">
/// A pattern whose first group is <b>who</b> is signed in there, read from the same answer (D66 §3):
/// an account is named by who signed in rather than by a name typed before anyone knew. Taken only on
/// a yes, and it is the one thing besides the boolean the probe takes — a harness that volunteers an
/// organisation and a tier beside it still has neither kept. Null asks nobody's name.
/// </param>
public sealed record LoginQuestion(
    IReadOnlyList<string> Arguments, string LoggedIn, string LoggedOut, string? Account = null);

/// <summary>
/// What Daoris knows about a harness AS A TOOL (D49 §4) — where its binary is, how to ask its version,
/// which environment variable names its configuration home, and how to run its OWN install, update and
/// login flows.
/// </summary>
/// <remarks>
/// <para><b>Daoris manages directories and names, and a sign-in stays the tool's.</b> Nothing here
/// reads a credential; <see cref="LoginArguments"/> runs the harness's own flow INTO a profile
/// directory, so whatever it stores stays in its own store, under the user's OS account, exactly where
/// it lives without Daoris. The one secret Daoris keeps is an API key a person gives it
/// (<see cref="HarnessKeys"/>, D67 §1).</para>
///
/// <para><b>Install and update are the harness's own mechanism</b>, never a download Daoris invents:
/// a tool that installed its dependencies by a route their authors did not publish is a tool nobody
/// can support. They are person-actions and stay out of every gate.</para>
///
/// <para>A harness with no <see cref="ProfileVariable"/> supports no profiles, and asking for one on
/// it is refused rather than silently ignored — a spawn that quietly ran as the wrong account is the
/// failure this whole feature exists to prevent.</para>
/// </remarks>
/// <param name="Binary">The default command, when the driver config names none.</param>
/// <param name="VersionArguments">How to ask the version. Recorded at spawn, so "which tool produced this" is answerable.</param>
/// <param name="ProfileVariable">The environment seam: the variable naming this harness's configuration home.</param>
/// <param name="Install">
/// A WHOLE command, because the harness may not exist yet — installing one that is absent cannot be
/// arguments to a binary that is not there. It is usually that harness's package manager.
/// </param>
/// <param name="UpdateArguments">
/// Arguments TO the binary: only a harness that is present can update itself, and a present harness
/// updates through its own mechanism rather than through a second copy of its installer.
/// </param>
/// <param name="LoginArguments">
/// Arguments TO the binary, run with the profile's configuration home in the environment. It must be
/// the harness itself, because the credential it obtains belongs in the harness's own store.
/// </param>
/// <param name="LoginCheck">How to ask a profile's login state. Null leaves every profile <see cref="LoginState.Unknown"/>.</param>
/// <param name="AccountOf">
/// The harness whose ACCOUNT this one runs as, when it has none of its own (ACP2). 🔴 Declared, so
/// that a missing login flow is a fact rather than a gap — an unanswerable login question is
/// PERMISSIVE (SES3), so an omission would quietly widen what may spawn.
/// </param>
/// <param name="Package">
/// The package a managed install fetches, when Daoris owns the binary (TOOL2/D57). Declared rather
/// than parsed out of <paramref name="Install"/>, because the two are different questions: one is
/// "how does this tool put itself on a machine", the other is "what do I fetch into a directory I
/// own". A harness that declares none cannot be pinned, and says so.
/// </param>
/// <param name="TrustFile">
/// The file, beside this harness's configuration home, in which it records the workspaces a person
/// has accepted — for a harness that has such a notion (DEPLOY1). Null is "no notion of trust", and
/// that is most of them. It is READ and never written: the flag is the person's grant.
/// </param>
/// <param name="ProfileMustExist">
/// Whether this harness demands its profile directory already be there (ACP3). 🔴 Observed, not
/// assumed, and the two adapters disagree: <c>codex-acp</c> exits 1 before <c>initialize</c>
/// completes when <c>CODEX_HOME</c> names a path that is not there, while the Claude adapter
/// <b>creates</b> <c>CLAUDE_CONFIG_DIR</c> and populates it. Default false, so a harness that says
/// nothing behaves as every harness did before the field existed.
/// </param>
public sealed record HarnessToolchain(
    IReadOnlyList<string> Binary,
    IReadOnlyList<string> VersionArguments,
    string? ProfileVariable = null,
    IReadOnlyList<string>? Install = null,
    IReadOnlyList<string>? UpdateArguments = null,
    IReadOnlyList<string>? LoginArguments = null,
    LoginQuestion? LoginCheck = null,
    string? Package = null,
    string? AccountOf = null,
    bool ProfileMustExist = false,
    string? TrustFile = null,
    // A harness with no version question (a plugin-declared one, D64) is asked whether it is THERE
    // rather than run: an ACP agent started with no arguments waits on its stdin, and twenty seconds
    // of that per roster refresh is not a probe, it is a stall.
    bool ProbeByPresence = false,
    // What a PINNED binary runs with so it stays the version pinned (AGT2) — the tool's own switch,
    // measured rather than assumed, and applied to a managed binary only: a binary off PATH is the
    // machine's, and its updates are the machine's business (D48 §2a). Null declares none.
    IReadOnlyDictionary<string, string>? PinnedEnvironment = null,
    // What a person calls the tool this runs, and who makes it (AGT1): `dsh` meant nothing to the
    // owner until it said whose. A door onto a tool names that tool, not itself. Null for one that
    // declares neither — a plugin's, until it says.
    string? Product = null,
    string? Maker = null,
    // The tool's own variable for an API key (AGT3, D67 §1) — what an account that is a key is
    // handed at spawn. Declared only where measured; null means this agent takes no key from Daoris.
    string? KeyVariable = null,
    // The words this tool prints when its provider refuses the account's credential (AGT3b) — read
    // from a failed session's last lines, so the account is not spent again. Measured, never guessed.
    string? Refused = null,
    // The maker's OWN release channel a pin fetches from (AGT2b), verified end to end — never npm.
    // A toolchain declares this or `Package`, never both: which source a pin came from is not left
    // for anyone to work out. The CLI's `channel` is the twin.
    string? Channel = null,
    // The tool's own settings file in an account's configuration home, where the account's model and
    // effort live (AGT6, D98) — declared only where its keys were read from the tool itself. Null means
    // Daoris does not know this tool's settings and offers none. The CLI's `settingsFile` is the twin.
    string? SettingsFile = null,
    // The words this tool says an account's limit in (TOOL4a, D125 §1.3), read from the door's failure
    // by `AccountLimits.Read` and never from the transcript. Declared only by a tool seen hitting one,
    // each pattern standing on a recorded sentence. A door onto another agent reads its owner's (AGT7).
    // Null reads every failure as a failure. Not a twin: the CLI concludes no session.
    LimitWords? Limits = null,
    // Whether this tool's maker fixes an account's weekly reset at one time each week (TOOL6b, D130 §16.3 step 4): a weekly
    // reset a limit told is then carried a week at a time, where otherwise it is dropped at its reset. Declared only where
    // the maker's own page says so; not in `Limits`, whose entries grow only with a recorded sentence. A door onto another
    // agent reads its owner's (AGT7).
    bool WeekFixed = false)
{
    /// <summary>The command this harness actually runs as: the machine's configured one, or the declared one.</summary>
    public IReadOnlyList<string> Command(IReadOnlyList<string>? configured) =>
        configured is { Count: > 0 } ? configured : Binary;

    /// <summary>
    /// Whose accounts this runs as (AGT7): <see cref="AccountOf"/> for a door onto another agent,
    /// else itself. Accounts, their defaults and their keys live under this name; a pin does not.
    /// </summary>
    public string Owner(string name) => AccountOf is { Length: > 0 } owner ? owner : name;
}

/// <summary>
/// One profile as it stands: its name, the directory Daoris owns the location of, and what the harness
/// says about logging in there. <b>Never anything from inside it.</b>
/// </summary>
/// <param name="Account">
/// Who the harness says is signed in there (D66 §3) — the name a person knows the account by, read
/// fresh on every probe and written nowhere. Null when signed out, or when the tool does not say.
/// </param>
/// <param name="Key">
/// For an account that is an API key (AGT3), the key's handle — its last four characters. Never
/// the key. Null for a sign-in.
/// </param>
public sealed record ProfileReport(
    string Name, string Home, LoginState Login, string? Account = null, string? Key = null);

/// <summary>
/// One harness as this machine has it (D49 §4): present or absent, its version, its profiles.
/// </summary>
/// <param name="Problem">Why it could not be probed — the sentence a person acts on, when absent.</param>
/// <param name="OwnLogin">
/// What the harness says about logging in to its OWN configuration home — the account a person
/// actually has before naming any profile, asked the same read-only way. Unknown when absent.
/// 🔴 The roster called a machine with no named profile "No accounts" while its owner was logged in.
/// </param>
/// <param name="OwnAccount">Who is signed in to the tool's own home, asked the way a profile is.</param>
public sealed record HarnessReport(
    string Adapter,
    bool Present,
    string? Version,
    string? Problem,
    string? ProfileVariable,
    string? MachineDefault,
    IReadOnlyList<ProfileReport> Profiles,
    LoginState OwnLogin = LoginState.Unknown,
    string? OwnAccount = null);

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
public sealed partial record HarnessSettings(
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
    public static string DefaultPath => DaorisHome.Require("harnesses.json");

    /// <summary>The file every surface reads and writes — the override, or the conventional home.</summary>
    public static string ResolvePath() =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath;

    /// <summary>The directory the profile tree and the wiring file share — the Daoris home (D63).</summary>
    public static string HomeOf(string settingsPath) => DriverConfig.HomeOf(settingsPath);

    /// <summary>
    /// A missing file is a machine that named no profiles — not an error. Neither is an unreadable
    /// one: this is wiring, and absent wiring is the documented default (D21), so a hand-mangled file
    /// must never be what stops a driver coming up.
    /// </summary>
    public static HarnessSettings Load(string path)
    {
        if (!File.Exists(path)) return new HarnessSettings();

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new HarnessSettings();

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

            return new HarnessSettings(
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
            };
        }
        catch (Exception error)
            when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HarnessSettings { Problem = $"{Path.GetFileName(path)} could not be read ({error.Message})" };
        }
    }

    /// <summary>
    /// Write the wiring back — atomically, beside-then-rename, like every write in this family.
    /// </summary>
    /// <exception cref="DriverException">
    /// The file could not be read when these settings were loaded: what they hold is the empty default,
    /// and writing it would replace every choice the file had. The CLI refuses the same edit.
    /// </exception>
    public void Save(string path)
    {
        if (Problem is not null)
        {
            throw new DriverException(
                $"{Problem}, so this edit was not written — it would have replaced every choice in it. "
                + "Fix the file or remove it, then make the change again.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var stream = new MemoryStream();
        // LF on every platform, as the CLI's writer writes it: both write this file, the same wiring as the same bytes.
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
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
            WriteUses(writer, "rotationUse", Uses);

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

            WriteUseCircles(writer, "workspaceRotationUse", WorkspaceUses);

            writer.WriteEndObject();
        }

        AtomicFile.WriteText(path, System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n");
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
        Path.Combine(home, "toolchain", Name(harness, "agent name"), Name(version, "version"));

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
        Path.Combine(home, "harnesses", Name(harness, "agent name"), Name(profile, "profile name"));

    /// <summary>
    /// The profiles that exist for a harness — <b>the directories that exist</b>, sorted. There is no
    /// register of profiles to disagree with the disk, which is the same reason the registry became
    /// the authority rather than a view over a scan (D48 §3): one truth, not two.
    /// </summary>
    public static IReadOnlyList<string> Profiles(string home, string harness)
    {
        var root = Path.Combine(home, "harnesses", Name(harness, "agent name"));
        if (!Directory.Exists(root)) return [];

        return [.. Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The name an account made by signing in gets (D66 §3): the first free <c>account-N</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A neutral name, never who signed in.</b> The name is needed before the sign-in starts,
    /// when nobody knows whose it is; and renaming the directory afterwards would move a home a
    /// harness may have keyed its credential to. Who it is stays the TOOL's answer, read by the
    /// roster (<see cref="ProfileReport.Account"/>). Twin rule 5: the CLI's <c>login --new</c> counts
    /// the same way.
    /// </remarks>
    public static string NextAccount(string home, string harness)
    {
        var taken = Profiles(home, harness).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = 1;
        while (taken.Contains($"account-{number}")) number++;
        return $"account-{number}";
    }

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
            // A key whose directory someone removed by hand still goes: nothing is left behind here.
            HarnessKeys.Remove(home, harness, profile);
            AccountCooling.End(home, harness, profile, DateTimeOffset.UtcNow);
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
        // (TOOL4e): the next account made takes the first free name, which may be this one's.
        HarnessKeys.Remove(home, harness, profile);
        AccountCooling.End(home, harness, profile, DateTimeOffset.UtcNow);
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
}

/// <summary>
/// Why an order was refused (TOOL4e, D125 §3.1): the account it names that is not on this machine, or the one it names
/// twice. Both doors refuse it — the terminal's <c>daoris agent profile order</c> and the screen's — as
/// <c>profile default</c> refuses a name that does not exist.
/// </summary>
public sealed record RotationProblem(string Account, bool Twice)
{
    /// <summary>The refusal a person reads: which account, and what is there to name instead.</summary>
    public string Sentence(string agent, IReadOnlyCollection<string> accounts) => Twice
        ? $"`{Account}` is named twice — an order names each account once, in the order rotation tries them."
        : $"`{agent}` has no account `{Account}` on this machine, so an order cannot name it — accounts there: "
          + $"{(accounts.Count > 0 ? string.Join(", ", accounts) : "(none)")}.";
}

/// <summary>
/// The keys of accounts that are API keys (AGT3, D67 §1) — <c>keys.json</c> under the home, keyed by
/// agent and then account.
/// </summary>
/// <remarks>
/// <para><b>Beside the account, never inside it</b>: the account's directory is the tool's (D49 §4).
/// Plaintext at rest, as <c>remotes.json</c>'s deployment keys and the tools' own credential files
/// are; tracked by nothing; no HTTP surface (D47 §4). A person sees a key only as its
/// <see cref="Handle"/>. The CLI's <c>keys</c> helpers are the twin, over the same file.</para>
/// </remarks>
public static class HarnessKeys
{
    public const string FileName = "keys.json";

    /// <summary>The key's last four characters — every Anthropic key begins the same way.</summary>
    public static string Handle(string key) => key.Length > 4 ? $"…{key[^4..]}" : "…";

    /// <summary>
    /// Make an account that is this key: the next free <c>account-N</c>, its directory, and the key
    /// kept beside it. A blank key is refused before anything is made.
    /// </summary>
    public static string Add(string home, string harness, string key)
    {
        var trimmed = key?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            throw new DriverException("that is not an API key — it is blank, or has spaces in it.");
        }

        var account = HarnessSettings.NextAccount(home, harness);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(home, harness, account));

        var keys = Read(home);
        if (!keys.TryGetValue(harness, out var held)) keys[harness] = held = new(StringComparer.Ordinal);
        held[account] = trimmed;
        Write(home, keys);

        // A new key into an account ends a cool-off its name still carried (TOOL4d, D125 §2.3): whatever was spent, it
        // was not this key. Never the reason a key is not kept.
        try
        {
            AccountCooling.End(home, harness, account, DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The key is kept; the cool-off ends at its reset instead.
        }

        return account;
    }

    /// <summary>The key an account is, or null for a sign-in.</summary>
    public static string? Of(string home, string harness, string profile) =>
        Read(home).TryGetValue(harness, out var held) && held.TryGetValue(profile, out var key) ? key : null;

    /// <summary>Forget an account's key. Nothing to forget is an answer.</summary>
    public static void Remove(string home, string harness, string profile)
    {
        var keys = Read(home);
        if (!keys.TryGetValue(harness, out var held) || !held.Remove(profile)) return;
        if (held.Count == 0) keys.Remove(harness);
        Write(home, keys);
    }

    private static Dictionary<string, Dictionary<string, string>> Read(string home)
    {
        var keys = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var path = Path.Combine(home, FileName);
        if (!File.Exists(path)) return keys;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return keys;
            foreach (var agent in document.RootElement.EnumerateObject())
            {
                if (agent.Value.ValueKind != JsonValueKind.Object) continue;
                var held = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in agent.Value.EnumerateObject())
                {
                    if (entry.Value.GetString() is { Length: > 0 } key) held[entry.Name] = key;
                }

                keys[agent.Name] = held;
            }
        }
        catch (JsonException)
        {
            // An unreadable file holds no key Daoris can hand anyone; the account then asks for one.
        }

        return keys;
    }

    private static void Write(string home, Dictionary<string, Dictionary<string, string>> keys)
    {
        Directory.CreateDirectory(home);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var (agent, held) in keys.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(agent);
                foreach (var (account, key) in held.OrderBy(e => e.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(account, key);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        var path = Path.Combine(home, FileName);
        AtomicFile.WriteText(path, Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }
}

/// <summary>
/// Detection (D49 §4): locate a harness, ask its version, and ask each profile whether it is logged
/// in. <b>Free and read-only</b> — it runs the tool's own reporting commands and writes nothing.
/// </summary>
/// <remarks>
/// Acting is the person's click: install, update and login spawn the harness's own mechanism and are
/// never run by a gate, never mid-session, and never unasked. A harness changing under a running loop
/// is the moving-target problem `reaching-in` documents, one layer down.
/// </remarks>
public static class HarnessProbe
{
    /// <summary>How long a reporting command may take before it counts as no answer.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    /// <summary>Probe one harness: present, version, and every profile's login state.</summary>
    public static async Task<HarnessReport> ProbeAsync(
        string adapter,
        HarnessToolchain toolchain,
        IReadOnlyList<string>? command,
        HarnessSettings settings,
        string home,
        CancellationToken ct = default)
    {
        // 🔴 Rule 4, applied HERE and not only where a session spawns: the explicit command, then the
        // managed pin, then PATH. A presence answer computed from PATH while a pin is set is an answer
        // about a different program — measured both ways: the selector vetoed a working pin as "not
        // installed", and the CLI twin reported the machine's own binary as the pinned one.
        var resolved = toolchain.Command(command);
        string? pinned = null;
        var isManaged = false;
        if (command is not { Count: > 0 }
            && settings.ResolveVersion(adapter, workspace: null, chosen: null) is { } version_)
        {
            pinned = version_;
            if (HarnessSettings.ManagedBinary(home, adapter, pinned, toolchain.Binary) is { } managed)
            {
                resolved = [managed, .. toolchain.Binary.Skip(1)];
                isManaged = true;
            }
            else
            {
                // Pinned with nothing installed at it: absent, naming the version. Never a fall back
                // to PATH — running a different version than the one asked for and reporting success
                // is the failure the pin exists to prevent.
                return new HarnessReport(
                    adapter, false, null,
                    $"pinned to {pinned} on this machine, and nothing is installed at that version — "
                    + $"`daoris agent pin {adapter} {pinned}` installs it, and "
                    + $"`daoris agent unpin {adapter}` goes back to PATH",
                    toolchain.ProfileVariable,
                    settings.Defaults.TryGetValue(toolchain.Owner(adapter), out var pinnedDefault) ? pinnedDefault : null,
                    []);
            }
        }

        var version = toolchain.ProbeByPresence
            ? (Ran: resolved.Count > 0 && CommandPresence.Resolvable(resolved[0]), Output: "", Problem: (string?)null)
            : await AskAsync(resolved, toolchain.VersionArguments, profileHome: null, toolchain, ct, isManaged)
                .ConfigureAwait(false);

        var present = version.Ran;
        var profiles = new List<ProfileReport>();

        // A door lists its OWNER's accounts (AGT7): the same directories, so one tool shows one list.
        var owner = toolchain.Owner(adapter);
        foreach (var name in HarnessSettings.Profiles(home, owner))
        {
            var profileHome = HarnessSettings.ProfileHome(home, owner, name);
            var held = HarnessKeys.Of(home, owner, name);
            // An account that is a key is asked WITH its key (AGT3), as a session would run it, so
            // the roster says what the tool says — and names it by the key's handle, never the key.
            var key = toolchain.KeyVariable is { Length: > 0 } variable && held is not null
                ? new Dictionary<string, string> { [variable] = held }
                : null;
            var (login, account) = present
                ? await LoginAsync(resolved, toolchain, profileHome, isManaged, ct, key).ConfigureAwait(false)
                : (LoginState.Unknown, null);
            profiles.Add(new ProfileReport(
                name, profileHome, login, account, held is null ? null : HarnessKeys.Handle(held)));
        }

        // The tool's own home, asked exactly as a profile is — with the seam UNSET, so the tool
        // answers about wherever it keeps its own credential. Read-only; Daoris never logs into it.
        var (own, ownAccount) = present
            ? await LoginAsync(resolved, toolchain, profileHome: null, isManaged, ct).ConfigureAwait(false)
            : (LoginState.Unknown, null);

        return new HarnessReport(
            adapter,
            present,
            present ? FirstLine(version.Output) : null,
            present ? null : version.Problem,
            toolchain.ProfileVariable,
            settings.Defaults.TryGetValue(owner, out var machine) ? machine : null,
            profiles,
            own,
            ownAccount);
    }

    /// <summary>
    /// What the harness says about logging in to ONE home — a profile's, or its own when
    /// <paramref name="profileHome"/> is null — asked of the binary a spawn would run. What a sign-in
    /// asks when it ends (D66 §3), without probing every other account on the machine to learn it.
    /// </summary>
    public static async Task<(LoginState Login, string? Account)> AskLoginAsync(
        string adapter, HarnessToolchain toolchain, IReadOnlyList<string>? command,
        HarnessSettings settings, string home, string? profileHome, CancellationToken ct = default)
    {
        var resolved = toolchain.Command(command);
        if (command is not { Count: > 0 }
            && settings.ResolveVersion(adapter, workspace: null, chosen: null) is { } pinned)
        {
            // The pin, as the probe resolves it; nothing installed at it is nobody to ask.
            if (HarnessSettings.ManagedBinary(home, adapter, pinned, toolchain.Binary) is not { } managed)
            {
                return (LoginState.Unknown, null);
            }

            resolved = [managed, .. toolchain.Binary.Skip(1)];
            return await LoginAsync(resolved, toolchain, profileHome, managed: true, ct).ConfigureAwait(false);
        }

        return await LoginAsync(resolved, toolchain, profileHome, managed: false, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What the harness says about logging in here: the boolean, and — when the toolchain asks and the
    /// answer is yes — <b>who</b> (D66 §3). A real harness volunteers an organisation and a
    /// subscription tier alongside both, and neither is Daoris's to hold, log, or put on a roster.
    /// </summary>
    private static async Task<(LoginState Login, string? Account)> LoginAsync(
        IReadOnlyList<string> resolved, HarnessToolchain toolchain, string? profileHome, bool managed,
        CancellationToken ct, IReadOnlyDictionary<string, string>? account = null)
    {
        if (toolchain.LoginCheck is not { } question) return (LoginState.Unknown, null);

        var answer = await AskAsync(resolved, question.Arguments, profileHome, toolchain, ct, managed, account)
            .ConfigureAwait(false);
        if (!answer.Ran) return (LoginState.Unknown, null);

        // Matched in this order because a "logged in" pattern is the specific one; and neither
        // matching leaves it unknown rather than out, which is what keeps a reworded status line from
        // refusing a spawn that would have worked.
        if (Matches(answer.Output, question.LoggedIn)) return (LoginState.In, Who(answer.Output, question.Account));
        return (Matches(answer.Output, question.LoggedOut) ? LoginState.Out : LoginState.Unknown, null);
    }

    /// <summary>The first group of the account pattern, trimmed — or null when there is none to read.</summary>
    private static string? Who(string output, string? pattern)
    {
        if (pattern is null) return null;
        try
        {
            var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            return match.Success && match.Groups.Count > 1 && match.Groups[1].Value.Trim() is { Length: > 0 } who
                ? who
                : null;
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static bool Matches(string output, string pattern)
    {
        try
        {
            return Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            // A pattern this build cannot run is no evidence, exactly like an answer it cannot read.
            return false;
        }
    }

    /// <summary>The runtime's error code for a binary that is not there — ERROR_FILE_NOT_FOUND and ENOENT alike.</summary>
    private const int FileNotFound = 2;

    /// <summary>
    /// Run one of the harness's reporting commands and collect what it said. <b>Both supported
    /// harnesses exit 0 whether or not they are logged in</b>, so the exit code is deliberately not
    /// consulted — the output is the answer, and "it ran at all" is the only thing the code is asked.
    /// </summary>
    internal static async Task<(bool Ran, string Output, string? Problem)> AskAsync(
        IReadOnlyList<string> resolved,
        IReadOnlyList<string> arguments,
        string? profileHome,
        HarnessToolchain toolchain,
        CancellationToken ct,
        bool managed = false,
        IReadOnlyDictionary<string, string>? account = null)
    {
        if (resolved.Count == 0) return (false, "", "no command to run");

        var info = new ProcessStartInfo
        {
            FileName = resolved[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Read as the tools write, not in the console's code page, which garbled a plugin's words on
            // a Chinese-locale machine (PLUG8); a sign-in status can carry a name.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true, // a probe from a window must not open a console (Adapters.Shell)
        };
        foreach (var part in resolved.Skip(1)) info.ArgumentList.Add(part);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // A probe is a child like a session (TOOLS5): asked on the PATH a session would be started on.
        Tools.Hand(info);
        // A pinned binary is asked the way a session runs it (AGT2), so asking is not when it moves.
        Apply(info, toolchain, profileHome, binary: managed ? resolved[0] : null, account: account);

        try
        {
            using var process = Process.Start(info);
            if (process is null) return (false, "", $"`{resolved[0]}` did not start");

            using var patience = CancellationTokenSource.CreateLinkedTokenSource(ct);
            patience.CancelAfter(Patience);

            var stdout = process.StandardOutput.ReadToEndAsync(patience.Token);
            var stderr = process.StandardError.ReadToEndAsync(patience.Token);
            try
            {
                await process.WaitForExitAsync(patience.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);

                // The two reads are still outstanding, and abandoning them leaves faulted tasks
                // nobody observes — a process that hung is exactly when they fault. Awaited to
                // completion and discarded: the answer is already "it did not answer".
                await Task.WhenAll(stdout, stderr).ContinueWith(
                    _ => { }, TaskScheduler.Default).ConfigureAwait(false);

                return (false, "", $"`{resolved[0]}` did not answer within {Patience.TotalSeconds:0}s");
            }

            // Both streams: a harness that reports its version on stderr is not an absent harness.
            return (true, $"{await stdout.ConfigureAwait(false)}\n{await stderr.ConfigureAwait(false)}", null);
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == FileNotFound)
        {
            // 🔴 The sentence, and only the sentence. The runtime's own message for this case — "An
            // error occurred trying to start process '…' with working directory '<cwd>'. The system
            // cannot find the file specified." — restates the fact and adds a machine path, and the
            // deployed application's roster showed both. Any OTHER failure keeps the runtime's words:
            // a binary that exists and will not start is news the sentence alone does not carry.
            return (false, "", $"`{resolved[0]}` is not on this machine's PATH");
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException)
        {
            return (false, "", $"`{resolved[0]}` is not on this machine's PATH — {error.Message}");
        }
    }

    /// <summary>
    /// Put a spawn's account and its binary onto a process, through the seams the harness itself
    /// provides. <b>Creating the profile directory is part of selecting it</b>: at least one
    /// supported harness refuses to start when its home variable names a path that does not exist,
    /// and Daoris owns that location by design.
    /// </summary>
    /// <param name="binary">
    /// The managed executable to run instead of whatever the adapter resolved (TOOL2/D57), or null
    /// for <c>PATH</c> — which is the unpinned case and therefore the usual one.
    /// </param>
    /// <remarks>
    /// 🔴 <b>Both doors go through here</b>, which is why the pin is applied in this one place rather
    /// than inside each adapter's <c>Prepare</c>: an adapter that forgot it would spawn the wrong
    /// binary and record the pinned version beside it.
    /// </remarks>
    internal static void Apply(
        ProcessStartInfo info, HarnessToolchain toolchain, string? profileHome, string? binary = null,
        string? claudeExecutable = null, IReadOnlyDictionary<string, string>? account = null)
    {
        // What the account itself carries — an API key in the tool's own variable (AGT3). Set here,
        // the one line both doors take, so no spawn of a key account can go out without its key.
        foreach (var (name, value) in account ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        // The arguments the adapter built stay exactly as they are: same tool, different location.
        if (binary is { Length: > 0 })
        {
            info.FileName = binary;

            // 🔴 And the version it was pinned at (AGT2): a pinned Claude Code reported its own
            // auto-updates enabled, and would have moved itself under the pin.
            foreach (var (name, value) in toolchain.PinnedEnvironment ?? new Dictionary<string, string>())
            {
                info.Environment[name] = value;
            }
        }

        // 🔴 USE1f: what Windows can START, by the resolver installers already use. A bare name from
        // PATH starts only as an `.exe`, so an agent npm installed globally (a `.cmd` shim, with a
        // POSIX script beside it) probed absent and every start on it was held, though the CLI found
        // it. Here because the probe and both doors' spawns all come through here, as the pin does;
        // and a shim is held to the same rule about its arguments that a pinned `.cmd` now is.
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList,
            info.Environment.TryGetValue("PATH", out var path) ? path : null);

        // The ACP adapter runs the Agent SDK, which finds its CLI through its own seam (ACP2, §1a).
        // Applied here for the reason everything else here is: one line, both doors, no adapter that
        // can forget it. Null leaves it unset, so the SDK looks where it always did.
        ClaudeAcp.PointAtClaude(info, claudeExecutable);

        if (profileHome is null) return;

        if (toolchain.ProfileVariable is not { Length: > 0 } variable)
        {
            throw new DriverException(
                "that agent has no configuration-home variable, so Daoris cannot run it as a named "
                + "account. Its accounts are managed with its own tooling.");
        }

        // 🔴 Created, not assumed to exist: `codex-acp` exits 1 before `initialize` completes when
        // `CODEX_HOME` names a path that is not there, while the Claude adapter creates its own. The
        // toolchain's `ProfileMustExist` names which harnesses depend on this line (ACP3).
        Directory.CreateDirectory(profileHome);
        info.Environment[variable] = profileHome;
    }

    private static string? FirstLine(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            if (line.Trim() is { Length: > 0 } text) return text;
        }

        return null;
    }
}

/// <param name="Refusal">Why this spawn must not happen — null when it may.</param>
/// <param name="Profile">The profile it runs as, or null for the harness's own configuration home.</param>
/// <param name="ProfileHome">Where that profile lives. Machine-local: it goes into no record and over no wire.</param>
/// <param name="Version">The harness version observed, for the record.</param>
/// <param name="Binary">
/// The managed executable this spawn runs (TOOL2/D57), or <b>null for whatever is on <c>PATH</c></b>
/// — which is the unpinned case and therefore the usual one. Machine-local like the profile home:
/// it is a path, so it goes into no record and over no wire.
/// </param>
/// <param name="ClaudeExecutable">
/// The managed <c>claude</c> the ACP adapter's Agent SDK should run (ACP2, §1a), or null to let it
/// find its own. It reads <b>the pipe door's pin</b> deliberately: the CLI and the account belong to
/// <c>claude-code</c>, and the ACP adapter is a separate package that merely runs it — so a person
/// who pinned <c>claude</c> gets that <c>claude</c> over either door, which is the point of pinning.
/// </param>
/// <param name="Environment">
/// What the account itself puts on the spawn — for an account that is an API key (AGT3), the key in
/// the tool's own variable. Machine-local like the profile home: it goes into no record and over no
/// wire. Null for a sign-in or the tool's own home.
/// </param>
public sealed record HarnessSelection(
    string? Refusal, string? Profile = null, string? ProfileHome = null, string? Version = null,
    string? Binary = null, string? ClaudeExecutable = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public bool Allowed => Refusal is null;

    /// <summary>
    /// The account's cool-off when that is why this spawn must not happen (TOOL4d, D125 §4), and null otherwise: a
    /// hold that waits for a time, where every other refusal waits for a person.
    /// </summary>
    /// <remarks>
    /// Held over an order (TOOL4f), it is the cool-off of the account that is ready first, which is when the start may
    /// run again; for a pick refused, the pick's own.
    /// </remarks>
    public CoolingEntry? Cooling { get; init; }

    /// <summary>
    /// The account the start's scope begins at and why the start runs elsewhere (TOOL4f, D125 §3.3; TOOL6b, D130 §13 as
    /// §16 amends it); null when it runs where its scope begins.
    /// </summary>
    public RotatedStart? Rotated { get; init; }

    /// <summary>
    /// The step of the goal's walk that chose the account, where a list offered a choice (TOOL6b, D130 §16.4): what the
    /// start's record says first. Null under <c>use: order</c>, for a pick, and where the scope offered one account.
    /// </summary>
    public AccountChoice? Choice { get; init; }
}

/// <summary>Which rung of the resolution answered (D49 §4, TOOL2): the order a start asks in.</summary>
public enum ChoiceFrom
{
    /// <summary>The person picked it for this one session.</summary>
    Picked,

    /// <summary>This workspace's default.</summary>
    Workspace,

    /// <summary>This machine's default.</summary>
    Machine,

    /// <summary>Nothing set: the agent's own sign-in for an account, <c>PATH</c> for a version.</summary>
    Unset,
}

/// <summary>
/// What a start in one workspace would run on, and where each part came from (MAP1b) — the wiring
/// panel's answer, never a second judgement. Carries names and versions only: no home, no binary path
/// and no key, so it is safe on any surface the roster already reaches.
/// </summary>
/// <param name="Adapter">The adapter a start spawns — `driver.json`'s choice.</param>
/// <param name="Owner">Whose accounts it runs as (AGT7): the adapter itself, or the tool a door opens onto.</param>
/// <param name="Profile">The account, by its directory name; null is the agent's own sign-in.</param>
/// <param name="ProfileFrom">Which rung chose it.</param>
/// <param name="Version">The version it would run: the probed one, else the pin that was asked for.</param>
/// <param name="VersionFrom">Which rung pinned it; unset is <c>PATH</c>.</param>
/// <param name="Commanded">`driver.json` names the command outright, which outranks any pin.</param>
/// <param name="Refusal">Why a start would be held, in the driver's own words; null when it would run.</param>
public sealed record StartWiring(
    string Adapter, string Owner, string? Profile, ChoiceFrom ProfileFrom,
    string? Version, ChoiceFrom VersionFrom, bool Commanded, string? Refusal)
{
    /// <summary>
    /// The account <see cref="ProfileFrom"/>'s rung named, when the start runs on another account of the person's order
    /// because that one is not ready (TOOL4f, D125 §3.3): <see cref="Profile"/> is then the account the start takes, so
    /// the panel never shows an account a start would not take. Null when nothing rotated.
    /// </summary>
    public string? RotatedFrom { get; init; }
}

/// <summary>
/// What this machine's harnesses are, and which account a spawn runs as (D49 §4) — <b>one judgement
/// for both doors</b>, driven and chat, for the same reason <see cref="Planner"/> is pure and the
/// service has one ledger: two copies of "may this start" drift, and the drift is invisible.
/// </summary>
/// <remarks>
/// <para><b>Probes are cached, and a refusal is re-checked before it is given.</b> Detection spawns a
/// process, so doing it every tick for every quest would be absurd; but a cached "absent" or "logged
/// out" would keep refusing after the person did exactly what the refusal told them to. So a yes is
/// trusted and a no is asked again — which costs one process on the path that was about to fail
/// anyway.</para>
///
/// <para><b>The wiring file is re-read, never held.</b> Same rule as `driver.json`: the file is the
/// truth and the surfaces are editors over it (D50), so a profile default changed from a terminal
/// takes effect on the next spawn without restarting anything.</para>
/// </remarks>
public sealed class HarnessRoster(AdapterSet adapters, string? settingsPath = null)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, HarnessReport> _seen =
        new(StringComparer.OrdinalIgnoreCase);

    // Accounts a provider refused (AGT3b), by owner and profile — "" for the tool's own home — with
    // the sentence that says so. Held in memory until a person looks again; see `Refuse`.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _refused =
        new(StringComparer.OrdinalIgnoreCase);

    private static string AccountKey(string owner, string? profile) => $"{owner}/{profile ?? ""}";

    /// <summary>
    /// Hold further starts on an account its provider refused (AGT3b), with the sentence that says
    /// why and what fixes it.
    /// </summary>
    /// <remarks>
    /// 🔴 Measured: a refused key costs a session over three minutes of the tool's silent retries
    /// before it fails, and every further session on that account would do the same. It is the
    /// ACCOUNT's, so it holds both doors onto it (AGT7). It clears when a person looks again: any
    /// account action, or the roster's own refresh. A new sign-in or key is exactly such an action.
    /// </remarks>
    public void Refuse(string adapter, string? profile, string reason)
    {
        var owner = adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase)
            ? Toolchain(adapter)?.Owner(adapters.Resolve(adapter).Name) ?? adapter
            : adapter;
        _refused[AccountKey(owner, profile)] = reason;
    }

    // The waits already written to the machine log, by account, with the cool-off each was for (TOOL4d): a wait is
    // written once, however many looks it lasts, and a new cool-off on the account is a new wait.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _waited =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What time it is, for reading and writing cool-offs (TOOL4d); the system's, unless a test's.</summary>
    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// This machine's zone (D125 §2.1): it stands in for a zone a limit's sentence names that is not an IANA name, and
    /// every cool-off is said in it, with the zone named.
    /// </summary>
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;

    /// <summary>
    /// Whose accounts a cool-off on this adapter belongs to: its owner, as its accounts are (AGT7); null for an adapter
    /// that declares no toolchain, which has no accounts and reads no limit.
    /// </summary>
    private static string? CoolingAgent(ISessionAdapter resolved) => resolved.Toolchain?.Owner(resolved.Name);

    /// <summary>
    /// The words this adapter says an account's limit in (TOOL4a, D125 §1.3): its own entry, else its owner's, since a
    /// door's limits are its owner's as its accounts are (AGT7). Null reads every failure as a failure.
    /// </summary>
    public LimitWords? LimitsOf(string adapter)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain?.Limits is { } own) return own;
        return CoolingAgent(resolved) is { } owner
               && !string.Equals(owner, resolved.Name, StringComparison.OrdinalIgnoreCase)
               && adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? adapters.Resolve(owner).Toolchain?.Limits
            : null;
    }

    /// <summary>The account's cool-off now, or null when it is ready, a start on this adapter as <paramref name="profile"/> would read it.</summary>
    public CoolingEntry? CoolingOf(string adapter, string? profile) =>
        CoolingAgent(adapters.Resolve(adapter)) is { } agent ? AccountCooling.Of(Home, agent, profile, Clock()) : null;

    /// <summary>
    /// The door's failure, read for an account's limit (TOOL4d, D125 §1, §2): where the adapter's table recognises it, the
    /// account the session ran as cools until the reset the agent named, and what was read is returned. Null is a
    /// failure as today, and nothing is written.
    /// </summary>
    /// <param name="failure">What the door carried apart from the agent's words; never the transcript (D125 §1.4).</param>
    /// <param name="coolOff">
    /// How long the account cools when the agent named no time this reads: the machine's <c>cooloff</c>
    /// (<see cref="DriverConfig.CoolOff"/>, TOOL4e), or <see cref="AccountLimits.DefaultCoolOff"/> when none is handed over.
    /// </param>
    public (CoolingEntry Entry, LimitSeen Seen)? Limited(string adapter, string? profile, string? failure, string session, TimeSpan? coolOff = null)
    {
        var resolved = adapters.Resolve(adapter);
        if (CoolingAgent(resolved) is not { } agent) return null;

        var now = Clock();
        if (AccountLimits.Read(LimitsOf(resolved.Name), failure, now, Zone, coolOff ?? AccountLimits.DefaultCoolOff) is not { } seen) return null;

        var entry = new CoolingEntry(agent, profile, seen.Until, seen.Stated, seen.Window, now, session, seen.AssumedZone, seen.NotBelieved);
        AccountCooling.Cool(Home, entry, now);

        // A weekly reset the agent named is that account's for the weeks after (TOOL6b, D130 §16.3 step 4): a start spends a
        // week about to lapse first. Only a named account's, since the tool's own sign-in is never in a list (D125 §3.7).
        if (profile is not null && seen.Stated && IsWeekly(seen))
        {
            try
            {
                AccountWindows.Told(Home, agent, profile, seen.Until, now, session);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A week not kept costs a start's ranking, never the limit's cool-off.
            }
        }

        return (entry, seen);
    }

    /// <summary>
    /// Whether a limit's reset is its account's weekly one: the window its reset named, or, where it named none, what was
    /// hit (<i>You've hit your weekly limit · resets …</i>).
    /// </summary>
    private static bool IsWeekly(LimitSeen seen) =>
        string.Equals(seen.Window ?? seen.Hit, AccountWindows.Weekly, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the agent that owns <paramref name="agent"/>'s accounts has its weekly reset fixed by its maker (TOOL6b): its
    /// own toolchain's word, a door reading its owner's (AGT7).
    /// </summary>
    private bool WeekFixed(string agent)
    {
        try
        {
            return adapters.Names.Contains(agent, StringComparer.OrdinalIgnoreCase) && adapters.Resolve(agent).Toolchain is { WeekFixed: true };
        }
        catch (DriverException)
        {
            return false;
        }
    }

    /// <summary>
    /// End an account's cool-off early: the person's <i>Try now</i>, since they may know the limit was raised (D125 §2.3).
    /// True when it was cooling; one that was not is told so, and nothing changes.
    /// </summary>
    public bool Ready(string adapter, string? profile) =>
        CoolingAgent(adapters.Resolve(adapter)) is { } agent && AccountCooling.End(Home, agent, profile, Clock());

    /// <summary>Whether this wait is new — an account, and the cool-off it is waiting out — so it is written once.</summary>
    internal bool NewWait(string agent, string? account, DateTimeOffset until)
    {
        var key = AccountKey(agent, account);
        var said = _waited.TryGetValue(key, out var was) && was == until;
        _waited[key] = until;
        return !said;
    }

    /// <summary>The adapters this roster answers for — the build's, plus whatever plugins declare (D64).</summary>
    public AdapterSet Adapters => adapters;

    /// <summary>
    /// Answer for a different set from now on — the built-in adapters plus the plugins a tick just
    /// read. The probe cache is keyed by harness name and survives: a harness that was there before
    /// is the same harness, and one that just arrived has simply not been asked yet.
    /// </summary>
    public void Use(AdapterSet live) => adapters = live;

    /// <summary>Where the wiring lives. The profile tree sits beside it.</summary>
    public string SettingsPath { get; } = settingsPath ?? HarnessSettings.ResolvePath();

    /// <summary>The directory profiles live under — the Daoris home (D63), which is the directory the wiring file sits in.</summary>
    public string Home => HarnessSettings.HomeOf(SettingsPath);

    /// <summary>The person's harness wiring, as it stands on disk right now.</summary>
    public HarnessSettings Settings => HarnessSettings.Load(SettingsPath);

    /// <summary>The harnesses this build knows, whether or not they are installed.</summary>
    public IReadOnlyList<string> Known => adapters.Names;

    /// <summary>This harness's toolchain, or null for an adapter that declares none.</summary>
    public HarnessToolchain? Toolchain(string adapter) => adapters.Resolve(adapter).Toolchain;

    /// <summary>
    /// Which door this adapter holds a session over (D53) — what a surface calls the *tool's* way in
    /// rather than a tool of its own.
    /// </summary>
    /// <remarks>
    /// 🔴 Added for the roster surface after the owner read it as a catalogue of five tools
    /// (2026-09-22: *"`*-acp` really confusing of the scope of this project"*). It is not five tools;
    /// it is three, two of which can be reached two ways. The page cannot say so without knowing
    /// which entry is a door and which is a tool, and <see cref="HarnessToolchain.AccountOf"/>
    /// already answers the second half — this answers the first.
    /// </remarks>
    public SessionWire Wire(string adapter) => adapters.Resolve(adapter).Wire;

    /// <summary>
    /// Whether this harness's door carries a conversation's STRUCTURE (D76 §1): the protocol door, or a
    /// native door whose adapter reads the harness's own structured output (CONV3a).
    /// </summary>
    /// <remarks>
    /// The page reads it to know what an empty record means — nothing said yet on a structured door, a
    /// conversation that lives in the console on a text one — rather than guessing from the emptiness,
    /// which told a fresh chat on a structured door that its door carries only text (CONV3b).
    /// </remarks>
    public bool Structured(string adapter) => Structured(adapters.Resolve(adapter));

    /// <inheritdoc cref="Structured(string)"/>
    public static bool Structured(ISessionAdapter adapter) =>
        adapter.Wire == SessionWire.Acp || adapter.StructuredOutput() is not null;

    /// <summary>
    /// One harness as this machine has it. Cached after the first look; <paramref name="refresh"/>
    /// asks again, which is what the roster surface's refresh and every refusal do.
    /// </summary>
    public async Task<HarnessReport?> ReportAsync(
        string adapter, DriverConfig config, bool refresh = false, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain) return null;

        if (!refresh && _seen.TryGetValue(resolved.Name, out var cached)) return cached;

        var report = await HarnessProbe.ProbeAsync(
            resolved.Name, toolchain, config.Commands.GetValueOrDefault(resolved.Name),
            Settings, Home, ct).ConfigureAwait(false);

        _seen[resolved.Name] = report;
        return report;
    }

    /// <summary>
    /// What the harness says about ONE of its profiles — the question a sign-in asks as it ends
    /// (D66 §3), answered without probing every other account to learn it.
    /// </summary>
    public async Task<(LoginState Login, string? Account)> LoginOfAsync(
        string adapter, DriverConfig config, string profile, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain) return (LoginState.Unknown, null);

        // A door's account is its owner's (AGT7), asked the way the owner asks when this build has it.
        var owner = toolchain.Owner(resolved.Name);
        var asker = AccountAgent(resolved.Name, toolchain);
        return await HarnessProbe.AskLoginAsync(
            asker.Name, asker.Toolchain, config.Commands.GetValueOrDefault(asker.Name), Settings, Home,
            HarnessSettings.ProfileHome(Home, owner, profile), ct).ConfigureAwait(false);
    }

    /// <summary>The toolchain that answers for this adapter's accounts (AGT7) — its owner's, when carried.</summary>
    public HarnessToolchain? AccountToolchain(string adapter) =>
        Toolchain(adapter) is { } toolchain ? AccountAgent(adapters.Resolve(adapter).Name, toolchain).Toolchain : null;

    /// <summary>
    /// Which adapter answers for a door's ACCOUNTS (AGT7): its owner when this build carries one — the
    /// owner has the login question and the key variable — else the door itself.
    /// </summary>
    private (string Name, HarnessToolchain Toolchain) AccountAgent(string name, HarnessToolchain toolchain)
    {
        var owner = toolchain.Owner(name);
        return !string.Equals(owner, name, StringComparison.OrdinalIgnoreCase)
            && adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            && adapters.Resolve(owner).Toolchain is { } ownerToolchain
                ? (adapters.Resolve(owner).Name, ownerToolchain)
                : (name, toolchain);
    }

    /// <summary>Every harness this build knows, probed — what a roster surface renders.</summary>
    /// <remarks>
    /// 🔴 <b>A door with nothing to run on this machine is not on the roster.</b> The stub's toolchain
    /// has no binary of its own — the "binary" is whatever `driver.json` names, which is what lets
    /// the rehearsal gate the roster with no model in it — and a deployed application names nothing,
    /// so it listed an agent tool called <c>stub</c> with an install button and nothing to install.
    /// Structural rather than a name: name a command for it and it is a real door again. The spawn
    /// path is untouched — <see cref="SelectAsync"/> still answers for any adapter it is asked about.
    /// </remarks>
    public async Task<IReadOnlyList<HarnessReport>> RosterAsync(
        DriverConfig config, bool refresh = false, CancellationToken ct = default)
    {
        // A person looking again is asking to try again (AGT3b): a refused account is let through. And the tool's own
        // home's cool-off ends (D125 §3.7): it means whoever was signed in there when the limit came, and a sign-in at
        // the person's own terminal since is not something Daoris sees. A named account's reset stands.
        if (refresh)
        {
            _refused.Clear();
            try
            {
                AccountCooling.EndOwnHomes(Home, Clock());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A cool-off that could not be ended costs a start's wait, never the roster.
            }
        }

        var reports = new List<HarnessReport>();
        foreach (var name in Known)
        {
            if (adapters.Resolve(name).Toolchain is { Binary.Count: 0 }
                && config.Commands.GetValueOrDefault(name) is not { Count: > 0 })
            {
                continue;
            }

            if (await ReportAsync(name, config, refresh, ct).ConfigureAwait(false) is { } report)
            {
                reports.Add(report);
            }
        }

        return reports;
    }

    /// <summary>
    /// May a session spawn on this harness, and as which account? The two refusals mirror each other
    /// deliberately — a missing binary and a logged-out profile are the same kind of answer, and each
    /// <b>names the action that fixes it</b> rather than failing bare.
    /// </summary>
    /// <remarks>
    /// <para><b>One scope</b> (TOOL6b, D130 §2 rule 1, §3.1): the start's workspace's, when it names a default or a list
    /// of its own for the agent, else the machine's (<see cref="HarnessSettings.ResolveScope"/>). Its list is the whole set
    /// of accounts its starts may run on, begun at its default or else its first. A pick is the one account, never moved;
    /// a scope that names one account and no list is that account; a scope that names none is the tool's own sign-in.</para>
    /// <para><b>The walk</b>: <see cref="AccountRotation.Order"/>, by <c>use</c> — the goal's (the default) or D125's —
    /// and the first account still ready runs: not cooling, not refused, not signed out. It says which step chose it
    /// (<see cref="HarnessSelection.Choice"/>) and, where it ran somewhere other than where its scope begins, why
    /// (<see cref="HarnessSelection.Rotated"/>). When no account is ready the start waits: before any probe while each is
    /// cooling or refused, with one sentence naming the first reset, and the accounts the scope does not list.</para>
    /// <para><b>Counted as chosen</b>: the goal reads Daoris's sessions as of the driver's last look (<see cref="Look"/>)
    /// and every start chosen since, so starts in one look spread. A start's choice and its count are made one at a time,
    /// which a probe may make wait.</para>
    /// </remarks>
    /// <param name="workspace">The repository's circle, for the per-workspace default (D49 §4).</param>
    /// <param name="chosen">The person's pick for this session, when they made one.</param>
    /// <param name="kind">Driven work or a conversation: driven work never starts on the scope's kept account (§4.6).</param>
    public Task<HarnessSelection> SelectAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen,
        StartKind kind = StartKind.Driven, CancellationToken ct = default) =>
        ChooseAsync(adapter, config, workspace, chosen, kind, counts: true, ct);

    /// <summary>
    /// <see cref="SelectAsync"/>, or with <paramref name="counts"/> false the same answer for a panel, which starts nothing
    /// and so is never counted as a start chosen.
    /// </summary>
    private async Task<HarnessSelection> ChooseAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, CancellationToken ct)
    {
        if (!counts) return await WalkAsync(adapter, config, workspace, chosen, kind, counts, ct).ConfigureAwait(false);

        await _walking.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await WalkAsync(adapter, config, workspace, chosen, kind, counts, ct).ConfigureAwait(false);
        }
        finally
        {
            _walking.Release();
        }
    }

    private async Task<HarnessSelection> WalkAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, CancellationToken ct)
    {
        var resolved = adapters.Resolve(adapter);

        // An adapter that declares no toolchain has nothing to check: it spawns exactly as it did
        // before this existed. Purely additive, which is what lets a new adapter arrive without
        // answering questions about installers it may not have.
        if (resolved.Toolchain is not { } toolchain) return new HarnessSelection(Refusal: null);

        var settings = Settings;
        // 🔴 A door runs as its OWNER's accounts (AGT7): the owner's default, the owner's directory,
        // the owner's login question and key. The pin below stays the door's own — a door is a
        // different package at a different version (ACP2).
        var owner = toolchain.Owner(resolved.Name);
        var circle = string.IsNullOrWhiteSpace(workspace) ? null : workspace.Trim();
        var scope = settings.ResolveScope(owner, circle);
        var picked = string.IsNullOrWhiteSpace(chosen) ? null : chosen.Trim();

        // The accounts this start may use, in the order it tries them (TOOL6b, D130 §16.3; D125 §3.3 under `order`). A pick,
        // a scope that names one account, and the tool's own sign-in are the one account, and nothing more is read.
        var now = Clock();
        var facts = NoFacts;
        List<string?> order;
        if (picked is not null) order = [picked];
        else if (scope.List.Count == 0) order = [scope.Begins];
        else
        {
            facts = Facts(owner, scope.List, now);
            order = [.. AccountRotation.Order(scope, kind, HarnessSettings.Profiles(Home, owner), facts, now)];
        }

        var states = order.Select(account => Before(owner, account, now)).ToList();

        // 🔴 A cooling account is held FIRST, by a file read, before any probe (TOOL4d, D125 §3.3, §4): a start on a
        // spent account is refused at once and spends nothing, and three of them parked a quest on 1 October whose only
        // fault was its account. An account its provider already refused is not spent again (AGT3b). So when no account
        // the start may use is ready on either count, nothing is spawned or probed: the start waits.
        if (!states.Any(state => state.IsReady))
        {
            // A pick is the person's (§3.3): refused, never rotated, naming what they could pick instead.
            return picked is not null && states[0].Cooling is { } pick
                ? new HarnessSelection(RotationWords.Picked(pick, await ReadyAsync(resolved.Name, toolchain, config, pick.Account, now, ct).ConfigureAwait(false), Zone))
                {
                    Cooling = pick,
                }
                : Unready(owner, states, picked is null ? scope : null, circle, kind, now);
        }

        // Which binary this spawn runs (TOOL2/D57): the explicit command, then the managed pin, then
        // PATH. An explicit `commands` entry is the person naming exactly what to run and has the
        // last word — a pin quietly replacing it would be a standing choice overruling a specific one.
        string? managed = null;
        if (config.Commands.GetValueOrDefault(resolved.Name) is not { Count: > 0 }
            && settings.ResolveVersion(resolved.Name, workspace, chosen: null) is { } pinned)
        {
            managed = HarnessSettings.ManagedBinary(Home, resolved.Name, pinned, toolchain.Binary);
            if (managed is null)
            {
                // 🔴 Refused, never a silent fall back to PATH. Running a different tool than the one
                // that was pinned — and recording its version as though it were the pinned one —
                // is the failure this whole shape exists to prevent.
                return new HarnessSelection(
                    $"`{resolved.Name}` is pinned to {pinned} on this machine, and nothing is "
                    + $"installed at that version — `daoris agent pin {resolved.Name} {pinned}` "
                    + $"installs it, and `daoris agent unpin {resolved.Name}` goes back to PATH. "
                    + "Daoris will not quietly run a different version than the one you asked for.");
            }
        }

        var report = await ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false);
        if (report is { Present: false })
        {
            // Asked again before refusing: the cached answer may predate the install the last refusal
            // asked for.
            report = await ReportAsync(resolved.Name, config, refresh: true, ct).ConfigureAwait(false);
        }

        if (report is { Present: false })
        {
            var install = toolchain.Install is { Count: > 0 }
                ? $"`daoris agent install {resolved.Name}` installs it"
                : $"install it with its own tooling ({string.Join(' ', toolchain.Command(config.Commands.GetValueOrDefault(resolved.Name)))})";

            return new HarnessSelection(
                $"`{resolved.Name}` is not installed on this machine, so there is nothing to spawn — "
                + $"{install}. Daoris never installs an agent unasked: a tool that changed under a "
                + "running loop is a moving target.");
        }

        // The ACP adapter's own seam (ACP2): it runs the Agent SDK, which needs to be told which
        // `claude` to use. Read from the PIPE door's pin, because that is where the CLI lives.
        var claude = resolved.Wire == SessionWire.Acp
            ? HarnessSettings.ManagedBinary(
                Home, "claude-code", settings.ResolveVersion("claude-code", workspace, null), ["claude"])
            : null;

        if (order is [null])
        {
            return new HarnessSelection(null, null, null, report?.Version, managed, claude);
        }

        // The account's state is its owner's answer when this build carries the owner — a door
        // declares no login question of its own, which is what `accountOf` says.
        var asker = AccountAgent(resolved.Name, toolchain);
        var accounts = asker.Name == resolved.Name
            ? report
            : await ReportAsync(asker.Name, config, refresh: false, ct).ConfigureAwait(false);

        // The walk (TOOL4f, D125 §3.3): the first account still ready once the agent has said who is signed in, in the
        // order the start tries them. Signed out (a key gone included) is walked past as cooling and refused are.
        var asked = false;
        var at = -1;
        for (var i = 0; i < states.Count && at < 0; i++)
        {
            if (!states[i].IsReady) continue;
            var account = states[i].Account!;
            var login = LoginOf(accounts, account);
            if (login == LoginState.Out && !asked)
            {
                // Same re-ask as above, and for the same reason: the person may have just signed in. Once per start.
                accounts = await ReportAsync(asker.Name, config, refresh: true, ct).ConfigureAwait(false);
                asked = true;
                login = LoginOf(accounts, account);
            }

            if (login == LoginState.Out)
            {
                states[i] = states[i] with
                {
                    Readiness = AccountReadiness.SignedOut,
                    Refusal = $"the `{owner}` account `{account}` is not signed in, so a session would have "
                        + $"nothing to run as — `daoris agent login {owner} --profile {account}` runs "
                        + "the agent's own sign-in into it. Daoris manages the directory and the name; the "
                        + "credential stays in the agent's own store.",
                };
                continue;
            }

            at = i;
        }

        if (at < 0) return Unready(owner, states, picked is null ? scope : null, circle, kind, now);

        var runs = states[at].Account!;
        var home = HarnessSettings.ProfileHome(Home, owner, runs);

        // An account that is a key is handed its key through the tool's own variable (AGT3) — the
        // owner's variable, for a door, since the door runs the owner's tool.
        var key = asker.Toolchain.KeyVariable is { Length: > 0 } variable
            && HarnessKeys.Of(Home, owner, runs) is { } held
                ? new Dictionary<string, string> { [variable] = held }
                : null;

        // Counted from here on as one of this account's sessions, so the next start chosen before the next look spreads.
        if (counts) Chose(owner, runs);

        var selection = new HarnessSelection(null, runs, home, report?.Version, managed, claude, key);
        if (picked is not null || scope.List.Count == 0 || scope.Begins is not { } begins) return selection;

        // Said by whoever opens the record (TOOL4f, §3.6; TOOL6b, §16.4): its first line and `account.rotated`.
        var choice = AccountRotation.Chose(scope, kind, states, at, facts, now);
        var passed = states.FirstOrDefault(state => string.Equals(state.Account, begins, StringComparison.OrdinalIgnoreCase));
        var ran = facts.GetValueOrDefault(runs) ?? new AccountFacts();
        string Said(WalkChoice said) => RotationWords.Clause(said, owner, runs, scope, scope.From == ChoiceFrom.Workspace ? circle : null, passed, ran, kind, Zone);
        return selection with
        {
            Choice = scope.Use.Use == "goal" && order.Count > 1 ? new AccountChoice(choice.Step, Said(choice)) : null,
            Rotated = string.Equals(runs, begins, StringComparison.OrdinalIgnoreCase)
                ? null
                : new RotatedStart(begins, passed?.Cooling, Said(choice with { Rest = null }))
                {
                    Step = choice.Step,
                    Scope = scope.From == ChoiceFrom.Workspace ? circle : null,
                },
        };
    }

    /// <summary>
    /// What a start on this account would meet before any probe (TOOL4f, D125 §3.3): its cool-off, a file read, then a
    /// refusal its provider gave (AGT3b), held in memory; otherwise ready, until the agent says it is signed out.
    /// </summary>
    private AccountState Before(string owner, string? account, DateTimeOffset now)
    {
        if (AccountCooling.Of(Home, owner, account, now) is { } cooling)
        {
            return new AccountState(account, AccountReadiness.Cooling, cooling, CoolingWords.Hold(cooling, Zone));
        }

        return _refused.TryGetValue(AccountKey(owner, account), out var refused)
            ? new AccountState(account, AccountReadiness.Refused, Refusal: refused)
            : new AccountState(account, AccountReadiness.Ready);
    }

    /// <summary>
    /// No account the start may use is ready (TOOL4f, D125 §4). One account is held as it always was: its own sentence,
    /// and its cool-off where that is why. Over an order, the start waits for the first reset when one is cooling, said
    /// with every account and why; with none cooling, nothing will come ready by itself, so the default's own refusal,
    /// which names the fix, is the answer.
    /// </summary>
    /// <remarks>
    /// A wait for a time also says what the person could do (TOOL6b, D130 §3.3, §4.6), where it applies: a driven start
    /// whose kept account is ready says it is kept for conversations; and a scope that names accounts of its own names the
    /// agent's other accounts that are neither cooling nor refused, with the door that adds one. Read from the cool-offs
    /// and the refusals alone, as a waiting look starts no process. Daoris never takes one itself. A machine that names no
    /// list keeps its sentence byte for byte (D125 §3.1's none).
    /// </remarks>
    /// <param name="scope">The start's scope, or null for a pick, which says nothing more.</param>
    private HarnessSelection Unready(
        string owner, IReadOnlyList<AccountState> states, RotationScope? scope, string? workspace, StartKind kind, DateTimeOffset now)
    {
        var first = states.Where(state => state.Cooling is not null).Select(state => state.Cooling!).MinBy(cooling => cooling.Until);
        var held = states.Count == 1 || first is null
            ? new HarnessSelection(states[0].Refusal) { Cooling = states[0].Cooling }
            : new HarnessSelection(RotationWords.Wait(owner, states, Zone)) { Cooling = first };
        if (held.Cooling is null || scope?.Begins is null) return held;

        var sentence = held.Refusal!;
        if (kind == StartKind.Driven
            && scope.Use.Keep is { } kept
            && scope.List.Contains(kept, StringComparer.OrdinalIgnoreCase)
            && !states.Any(state => string.Equals(state.Account, kept, StringComparison.OrdinalIgnoreCase))
            && Before(owner, kept, now).IsReady)
        {
            sentence += " " + RotationWords.KeptAside(kept);
        }

        if (scope.From == ChoiceFrom.Workspace || scope.List.Count > 0)
        {
            IReadOnlyList<string> listed = scope.List.Count > 0 ? scope.List : [scope.Begins];
            var outside = HarnessSettings.Profiles(Home, owner)
                .Where(name => !listed.Contains(name, StringComparer.OrdinalIgnoreCase) && Before(owner, name, now).IsReady)
                .ToList();
            if (outside.Count > 0)
            {
                sentence += " " + RotationWords.Outside(owner, scope.From == ChoiceFrom.Workspace ? workspace : null, listed, outside);
            }
        }

        return held with { Refusal = sentence };
    }

    // ——— What the goal's walk counts (TOOL6b, D130 §4.2, §16.2): Daoris's sessions as of the driver's last look, and the
    // starts chosen since. One roster serves the driver and the conversations alike, so both count.

    private static readonly IReadOnlyDictionary<string, AccountFacts> NoFacts =
        new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);

    // One choice and its count at a time, so starts begun together in one look see each other's.
    private readonly SemaphoreSlim _walking = new(1, 1);

    private readonly object _load = new();

    // The last look's records, each with its adapter's owner (AGT7), and the starts chosen since, numbered as chosen.
    private IReadOnlyList<(string Owner, SessionStarted Session)> _looked = [];
    private readonly List<(long Number, string Owner, string Account)> _chosen = [];
    private long _numbered;

    /// <summary>
    /// The number of the last start chosen: taken by the driver before it reads a look's records, so <see cref="Look"/>
    /// keeps every start chosen while they were read.
    /// </summary>
    public long Mark()
    {
        lock (_load) return _numbered;
    }

    /// <summary>
    /// The driver's look read this machine's session records (TOOL6b): what the walk counts from now on, with the starts
    /// chosen after <paramref name="mark"/>. A look begins only once the last look's starts have opened their records, so
    /// every start chosen before it is one of them.
    /// </summary>
    public void Look(IReadOnlyList<SessionStarted> sessions, long mark)
    {
        var owned = sessions.Select(session => (OwnerOf(session.Adapter), session)).ToList();
        lock (_load)
        {
            _looked = owned;
            _chosen.RemoveAll(start => start.Number <= mark);
        }
    }

    /// <summary>A start chosen on an account: one of its sessions until the next look reads the records.</summary>
    private void Chose(string owner, string account)
    {
        lock (_load) _chosen.Add((++_numbered, owner, account));
    }

    /// <summary>What the walk knows of each account of a list, without its agent's word (§16.4).</summary>
    private Dictionary<string, AccountFacts> Facts(string owner, IReadOnlyList<string> list, DateTimeOffset now)
    {
        var fixedWeek = WeekFixed(owner);
        var facts = new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);
        lock (_load)
        {
            foreach (var account in list)
            {
                var sessions = _looked.Where(each => Same(each.Owner, owner) && Same(each.Session.Profile, account)).Select(each => each.Session).ToList();
                var chosen = _chosen.Where(start => Same(start.Owner, owner) && Same(start.Account, account)).ToList();
                facts[account] = new AccountFacts(
                    Running: sessions.Count(session => session.Running) + chosen.Count,
                    LastStarted: sessions.Select(session => session.Created).Where(created => created is not null).Max(),
                    Chosen: chosen.Count == 0 ? null : chosen.Max(start => start.Number));
            }
        }

        foreach (var account in list)
        {
            try
            {
                facts[account] = facts[account] with { WeekResets = AccountWindows.WeekOf(Home, owner, account, now, fixedWeek) };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A week not read ranks nothing, as one never told does.
            }
        }

        return facts;

        static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whose accounts a session on <paramref name="adapter"/> ran as (AGT7): its owner, or the adapter where it is unknown.</summary>
    private string OwnerOf(string adapter)
    {
        try
        {
            return adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase) && adapters.Resolve(adapter) is { } resolved
                ? resolved.Toolchain?.Owner(resolved.Name) ?? resolved.Name
                : adapter;
        }
        catch (DriverException)
        {
            return adapter;
        }
    }

    /// <summary>What the agent says about one account's sign-in, from a probe's report; unknown where it says nothing.</summary>
    private static LoginState LoginOf(HarnessReport? report, string account) =>
        report?.Profiles.FirstOrDefault(p => string.Equals(p.Name, account, StringComparison.OrdinalIgnoreCase))?.Login
        ?? LoginState.Unknown;

    /// <summary>
    /// The agent's other accounts a person could pick instead of a cooling one (D125 §3.3): not cooling, not refused, and
    /// not signed out, as the probe the conversation was about to make says. A person's start, so asking is fine.
    /// </summary>
    private async Task<IReadOnlyList<string>> ReadyAsync(
        string adapter, HarnessToolchain toolchain, DriverConfig config, string? picked, DateTimeOffset now, CancellationToken ct)
    {
        var owner = toolchain.Owner(adapter);
        var others = HarnessSettings.Profiles(Home, owner)
            .Where(name => !string.Equals(name, picked, StringComparison.OrdinalIgnoreCase))
            .Where(name => Before(owner, name, now).IsReady)
            .ToList();
        if (others.Count == 0) return [];

        var report = await ReportAsync(AccountAgent(adapter, toolchain).Name, config, refresh: false, ct).ConfigureAwait(false);
        return report is { Present: false } ? [] : [.. others.Where(name => LoginOf(report, name) != LoginState.Out)];
    }

    /// <summary>
    /// What a start in this workspace would run on, and where each part came from (MAP1b, D67 §3).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Read through <see cref="SelectAsync"/>, never beside it.</b> The design's rule is that the
    /// picture cannot disagree with the loop, and a second resolution is exactly how it would: the
    /// account is the selection's — its scope is <see cref="HarnessSettings.ResolveScope"/>, the function
    /// <see cref="SelectAsync"/> reads (TOOL6b) — and whether the start happens, and at which version, is the
    /// selection itself, asked without being counted as a start chosen. Nothing from the selection's
    /// machine-local half (the home, the binary, the key) is kept.
    /// </remarks>
    public async Task<StartWiring> WiringAsync(
        string adapter, DriverConfig config, string? workspace, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        var toolchain = resolved.Toolchain;
        var owner = toolchain?.Owner(resolved.Name) ?? resolved.Name;
        var settings = Settings;

        // The start's one scope (TOOL6b, D130 §2 rule 1): where it begins, and whose rung named it; the tool's own sign-in
        // where it names no account.
        var scope = toolchain is null ? null : settings.ResolveScope(owner, workspace);
        var profileFrom = scope?.Begins is null ? ChoiceFrom.Unset : scope.From;
        // The same precedence `SelectAsync` applies: a command `driver.json` names has the last word.
        var commanded = config.Commands.GetValueOrDefault(resolved.Name) is { Count: > 0 };
        var (pinned, versionFrom) = commanded || toolchain is null
            ? (null, ChoiceFrom.Unset)
            : settings.ResolveVersionFrom(resolved.Name, workspace, chosen: null);

        // Asked as a start would ask, and counted as none: a panel starts nothing.
        var selection = await ChooseAsync(adapter, config, workspace, chosen: null, StartKind.Driven, counts: false, ct).ConfigureAwait(false);

        // The account a start would take (TOOL4f, TOOL6b): the walk's, with where the scope begins beside it when that is
        // another, so the panel never shows an account a start would not take; where begins, when the start is held.
        return new StartWiring(
            resolved.Name, owner, selection.Profile ?? scope?.Begins, profileFrom,
            selection.Version ?? pinned, versionFrom, commanded, selection.Refusal)
        {
            RotatedFrom = selection.Rotated?.From,
        };
    }
}

/// <summary>
/// A harness action while it runs (D49 §4): the one thing a screen may send it, and the way it is
/// stopped. A login prints a prompt and waits — <i>paste the code</i> — and a process nobody can
/// answer or stop is a page with every control disabled until the window closes.
/// </summary>
public sealed class HarnessRun
{
    private readonly Process? _process;
    private readonly CancellationTokenSource? _download;

    internal HarnessRun(Process process) => _process = process;

    /// <summary>
    /// A pin from a maker's channel (AGT2b): no process at all — Daoris downloads it — so there is
    /// nothing to answer, and stopping it is cancelling the download.
    /// </summary>
    internal HarnessRun(CancellationTokenSource download) => _download = download;

    /// <summary>Answer the harness's prompt — one line, as a terminal would send it.</summary>
    public void Send(string line)
    {
        if (_process is null) return; // A download asks nothing.
        try
        {
            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
        {
            // Gone already: the answer arrived after the process stopped needing one.
        }
    }

    /// <summary>End it, and everything it started. The browser page it opened is the browser's and stays.</summary>
    public void Cancel()
    {
        try
        {
            _download?.Cancel();
            if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            // Ended between the look and the kill.
        }
    }
}

/// <summary>
/// The person's explicit actions on a harness (D49 §4): install it, update it, log a profile in —
/// each by <b>that harness's own official mechanism</b>, spawned as a process like any other and
/// streamed line by line to whoever asked.
/// </summary>
/// <remarks>
/// <para><b>Never automatic, never mid-session, never unasked.</b> A gate's tool must not change
/// between two runs nobody diffed, so none of this is reachable from the loop — only from a person
/// pressing something, in the desktop or in a terminal.</para>
///
/// <para><b>No credential passes through here.</b> Login spawns the harness's own flow with the
/// profile's configuration home in the environment; whatever it obtains, it stores itself, where it
/// would have stored it anyway.</para>
/// </remarks>
public static class HarnessActions
{
    /// <summary>Install a harness with its own installer — a whole command, since it may not exist yet.</summary>
    /// <remarks>
    /// 🔴 <b>On the system's npm</b> (TOOLS5, D121 §2.7), whatever Tools runs node as: an install is the agent's own
    /// installer, into the machine, and that is the machine's npm's job. A managed npm's global folder may be its node
    /// version's, which the next version would lose. With no npm on the machine it refuses, naming <c>agent pin</c>.
    /// </remarks>
    public static Task<int> InstallAsync(
        HarnessToolchain toolchain, Action<string> write, CancellationToken ct = default,
        Action<HarnessRun>? started = null) =>
        toolchain.Install is { Count: > 0 } install
            ? RunAsync(OnTheSystem(install, Environment.GetEnvironmentVariable(Tools.PathVariable)), toolchain, profileHome: null, write, ct, started)
            : throw new DriverException(
                "that agent declares no installer, so Daoris has no sanctioned way to install it. "
                + "Install it with its own tooling; Daoris will find it on the next probe.");

    /// <summary>
    /// Install one version into the directory Daoris owns (TOOL2/D57), leaving the machine's own
    /// install alone: from the maker's own channel, verified, where the toolchain declares one
    /// (AGT2b) — else npm's own `--prefix`, aimed somewhere Daoris chose.
    /// </summary>
    /// <param name="transport">How a channel is reached — the network, unless a test holds its own.</param>
    /// <param name="npm">The package manager a package pin runs — npm, unless a test holds a stand-in.</param>
    public static Task<int> PinAsync(
        HarnessToolchain toolchain, string home, string harness, string version, Action<string> write,
        CancellationToken ct = default, Action<HarnessRun>? started = null, HttpMessageHandler? transport = null,
        IReadOnlyList<string>? npm = null) =>
        toolchain.Channel is { Length: > 0 } channel
            ? PinFromChannelAsync(toolchain, channel, home, harness, version, write, ct, started, transport)
            : toolchain.Package is { Length: > 0 } package
            ? RunAsync(
                // The npm Tools resolves (TOOLS5, §2.7): its prefix is named, so the node it installs for is Tools' node.
                ThroughTools(home, [.. npm ?? Npm, "install", "--prefix", HarnessSettings.ManagedHome(home, harness, version),
                 $"{package}@{version}"]),
                toolchain, profileHome: null, write, ct, started)
            : throw new DriverException(
                "that agent declares no package, so Daoris has no sanctioned way to fetch a version "
                + "of it. Install it with its own tooling and Daoris will find it on PATH.");

    /// <summary>
    /// A pin from the maker's channel (AGT2b). 🔴 A version it cannot verify is refused before a byte
    /// is fetched — and never handed to npm instead, which would be the same trust by another road.
    /// An install already in place is the proof it verified, so re-pinning it fetches nothing.
    /// </summary>
    private static async Task<int> PinFromChannelAsync(
        HarnessToolchain toolchain, string channel, string home, string harness, string version,
        Action<string> write, CancellationToken ct, Action<HarnessRun>? started, HttpMessageHandler? transport)
    {
        if (channel != ClaudeReleases.Channel)
        {
            throw new DriverException(
                $"this build installs from no `{channel}` channel, so nothing was fetched or pinned. `daoris agent "
                + $"pin {harness} {version}` in a terminal knows every channel Daoris does.");
        }
        ClaudeReleases.RefuseVersion(version);

        if (HarnessSettings.ManagedBinary(home, harness, version, toolchain.Binary) is { } present)
        {
            write($"{toolchain.Product ?? harness} {version} is already installed where Daoris keeps it — nothing was downloaded.");
            write(present);
            return 0;
        }

        using var download = CancellationTokenSource.CreateLinkedTokenSource(ct);
        started?.Invoke(new HarnessRun(download));
        write($"installing {toolchain.Product ?? harness} {version} from {toolchain.Maker ?? "its maker"}'s own release "
            + "channel, into a directory Daoris owns.");
        await ClaudeReleases.InstallAsync(
            HarnessSettings.ManagedHome(home, harness, version), version, write, download.Token, transport)
            .ConfigureAwait(false);
        return 0;
    }

    private static readonly string[] Npm = ["npm"];

    /// <summary>
    /// A command whose first word a tool answers for, with that word the file Tools resolves (TOOLS5, D121 §2.4):
    /// <c>npm</c> in a pin is the managed node's npm when node is managed. A whole path, or a name no tool answers
    /// for, is left as named; the system's tool PATH does not find keeps its bare name, so the start says so as before.
    /// </summary>
    /// <exception cref="DriverException">The tool's way cannot run: it never falls back to <c>PATH</c>.</exception>
    internal static IReadOnlyList<string> ThroughTools(string home, IReadOnlyList<string> command)
    {
        if (command.Count == 0 || Tools.ResolveCommand(Tools.Read(home), home, command[0]) is not { } tool) return command;
        if (tool.Refused) throw new DriverException($"{tool.Problem} — so `{command[0]}` was not run.");
        return tool.File is { } file ? [file, .. command.Skip(1)] : command;
    }

    /// <summary>
    /// A command whose first word a tool answers for, found on the system's <paramref name="path"/> whatever Tools runs
    /// that tool as (TOOLS5, §2.7): <c>agent install</c> is the machine's npm's job. Any other first word is left as
    /// named.
    /// </summary>
    /// <exception cref="DriverException">The system has no such program: the install is refused, naming <c>agent pin</c>.</exception>
    internal static IReadOnlyList<string> OnTheSystem(IReadOnlyList<string> command, string? path)
    {
        if (command.Count == 0 || !Tools.Declared.Any(tool => tool.Answers.Contains(command[0], StringComparer.Ordinal))) return command;
        var file = CommandPresence.Resolve(command[0], path, startable: true)
            ?? throw new DriverException(
                $"`{command[0]}` is not on this machine's PATH, and installing an agent is the machine's own {command[0]}'s job, "
                + "whatever Daoris runs it as. `daoris agent pin <agent> <version>` installs one where Daoris keeps it instead.");
        return [file, .. command.Skip(1)];
    }

    /// <summary>
    /// Which Update a door has (USE1a): <c>"pin"</c> when it is pinned and declares a package or a
    /// channel, so Update moves the pin to the newest release; <c>"tool"</c> when it is unpinned and
    /// declares an updater of its own; otherwise null, and a surface offers no Update at all.
    /// </summary>
    /// <remarks>
    /// A pinned door's own updater is never the answer: it would change the copy on <c>PATH</c> or the
    /// configured command, not the copy sessions run. The CLI's <c>agent update</c> branches the same way.
    /// </remarks>
    /// <param name="pinned">This machine's pin for the door, or null.</param>
    public static string? UpdateOf(HarnessToolchain toolchain, string? pinned) =>
        !string.IsNullOrWhiteSpace(pinned)
            ? toolchain.Package is { Length: > 0 } || toolchain.Channel is { Length: > 0 } ? "pin" : null
            : toolchain.UpdateArguments is { Count: > 0 } ? "tool" : null;

    /// <summary>
    /// Update a door (USE1a), by <see cref="UpdateOf"/>: move a pin to the newest release, or run the
    /// door's own updater. A door with neither is refused, as it always was.
    /// </summary>
    /// <param name="pinned">This machine's pin for the door, or null.</param>
    /// <param name="pin">
    /// Writes the new pin — called only once the version it names is installed, exactly as the pin
    /// action writes its own.
    /// </param>
    /// <param name="transport">How a channel is reached — the network, unless a test holds its own.</param>
    /// <param name="npm">The package manager a package door asks and installs with — npm, unless a test holds a stand-in.</param>
    public static Task<int> UpdateAsync(
        HarnessToolchain toolchain, IReadOnlyList<string>? command, string home, string harness, string? pinned,
        Action<string> pin, Action<string> write, CancellationToken ct = default, Action<HarnessRun>? started = null,
        HttpMessageHandler? transport = null, IReadOnlyList<string>? npm = null) =>
        UpdateOf(toolchain, pinned) switch
        {
            "pin" => MovePinAsync(toolchain, home, harness, pinned!.Trim(), pin, write, ct, started, transport, npm),
            "tool" => RunAsync(
                [.. toolchain.Command(command), .. toolchain.UpdateArguments!], toolchain, profileHome: null, write, ct, started),
            _ => throw new DriverException(!string.IsNullOrWhiteSpace(pinned)
                ? $"that agent is pinned at {pinned} and declares no package or release channel to find a newer "
                  + $"version in. Pin another with `daoris agent pin {harness} <version>`, or unpin it."
                : "that agent declares no updater — it updates itself, or its package manager does."),
        };

    /// <summary>
    /// Move a pin to the newest release (USE1a): resolve the newest to one exact version, then pin it
    /// through <see cref="PinAsync"/> — the channel's verified install, or npm's. A pin never names a
    /// pointer such as <c>latest</c>.
    /// </summary>
    private static async Task<int> MovePinAsync(
        HarnessToolchain toolchain, string home, string harness, string pinned, Action<string> pin,
        Action<string> write, CancellationToken ct, Action<HarnessRun>? started, HttpMessageHandler? transport,
        IReadOnlyList<string>? npm)
    {
        string newest;
        if (toolchain.Channel is { Length: > 0 } channel)
        {
            if (channel != ClaudeReleases.Channel)
            {
                throw new DriverException(
                    $"this build knows no `{channel}` channel's newest release, so nothing was fetched or pinned. "
                    + $"`daoris agent update {harness}` in a terminal knows every channel Daoris does.");
            }

            write($"{harness} is pinned at {pinned}. Asking {toolchain.Maker ?? "its maker"}'s own release channel "
                + "which release is newest.");
            // No process asks the pointer, so stopping it is cancelling the request.
            using var asking = CancellationTokenSource.CreateLinkedTokenSource(ct);
            started?.Invoke(new HarnessRun(asking));
            try
            {
                newest = await ClaudeReleases.LatestAsync(asking.Token, transport).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                throw new DriverException($"{error.Message} The pin stays at {pinned}.");
            }
        }
        else
        {
            write($"{harness} is pinned at {pinned}. Asking npm which release of {toolchain.Package} is newest.");
            IReadOnlyList<string> asking;
            try
            {
                asking = ThroughTools(home, npm ?? Npm);
            }
            catch (DriverException error)
            {
                throw new DriverException($"{error.Message} The pin stays at {pinned}.");
            }

            newest = await NewestOnNpmAsync(toolchain, asking, pinned, write, ct, started).ConfigureAwait(false);
        }

        if (newest == pinned && HarnessSettings.ManagedBinary(home, harness, pinned, toolchain.Binary) is not null)
        {
            write($"{pinned} is already the newest release — nothing was fetched, and the pin stays.");
            return 0;
        }
        if (CompareReleases(newest, pinned) < 0)
        {
            write($"the pin, {pinned}, is newer than the newest release ({newest}) — nothing was fetched, and the pin "
                + "stays. Update never moves a pin backwards.");
            return 0;
        }

        write(newest == pinned
            ? $"{pinned} is the newest release and is not installed here — installing it."
            : $"{pinned} → {newest}");
        var code = await PinAsync(toolchain, home, harness, newest, write, ct, started, transport, npm).ConfigureAwait(false);
        if (code == 0) pin(newest);
        else write($"nothing was pinned: the pin stays at {pinned}.");
        return code;
    }

    /// <summary>
    /// The newest version npm publishes of a door's package, asked with <c>npm view &lt;package&gt;
    /// version</c> — a spawn of the same kind the pin's <c>npm install</c> is, streamed the same way.
    /// Anything but one version is a refusal that says so.
    /// </summary>
    private static async Task<string> NewestOnNpmAsync(
        HarnessToolchain toolchain, IReadOnlyList<string> npm, string pinned, Action<string> write,
        CancellationToken ct, Action<HarnessRun>? started)
    {
        var package = toolchain.Package!;
        var stays = $"Nothing was fetched or pinned, and the pin stays at {pinned}.";
        var heard = new List<string>();

        int code;
        try
        {
            code = await RunAsync(
                [.. npm, "view", package, "version"], toolchain, profileHome: null,
                line =>
                {
                    // RunAsync echoes the command first; that line is Daoris's, not npm's answer.
                    if (!line.StartsWith("$ ", StringComparison.Ordinal)) heard.Add(line);
                    write(line);
                },
                ct, started).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            throw new DriverException($"{error.Message} {stays}");
        }

        var said = heard.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        var shown = said is null ? "" : $" ({(said.Length > 160 ? said[..160] + "…" : said)})";
        if (code != 0)
        {
            throw new DriverException($"npm could not say which release of {package} is newest — it exited {code}{shown}. {stays}");
        }

        return VersionFromNpm(heard)
            ?? throw new DriverException($"npm answered no single version of {package}{shown}. {stays}");
    }

    /// <summary>One exact release, as npm and the channels write it — a prerelease or build suffix included.</summary>
    private static readonly Regex Release = new(
        @"^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.Compiled);

    /// <summary>
    /// The one version <c>npm view &lt;package&gt; version</c> answered, or null when it answered none
    /// or several (USE1a). A warning npm prints beside it is not a version, and quotes around one are
    /// npm's own. The CLI's <c>versionFromNpm</c> is the twin.
    /// </summary>
    public static string? VersionFromNpm(IEnumerable<string> lines)
    {
        var found = lines
            .Select(line => line.Trim().Trim('\'', '"'))
            .Where(line => Release.IsMatch(line))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>
    /// Which of two releases is newer: negative when <paramref name="a"/> is older, positive when newer,
    /// and null when either is not a version this can order. A release outranks its own prereleases.
    /// </summary>
    private static int? CompareReleases(string a, string b)
    {
        var left = Release.Match(a);
        var right = Release.Match(b);
        if (!left.Success || !right.Success) return null;

        for (var at = 1; at <= 3; at++)
        {
            // Compared as digit strings, so a number too long for any integer type orders rather than throws.
            var (x, y) = (left.Groups[at].Value.TrimStart('0'), right.Groups[at].Value.TrimStart('0'));
            var difference = x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
            if (difference != 0) return difference;
        }

        var (l, r) = (left.Groups[4], right.Groups[4]);
        if (l.Success == r.Success) return l.Success ? string.CompareOrdinal(l.Value, r.Value) : 0;
        return l.Success ? -1 : 1;
    }

    /// <summary>
    /// Run the harness's own login flow INTO a profile. The directory is Daoris's; everything that
    /// lands in it is the harness's.
    /// </summary>
    /// <remarks>
    /// A sign-in that ends well ends that account's cool-off (TOOL4d, D125 §2.3): the directory may now hold another
    /// account. Both the screen's sign-ins come through here.
    /// </remarks>
    public static async Task<int> LoginAsync(
        HarnessToolchain toolchain, IReadOnlyList<string>? command, string profileHome,
        Action<string> write, CancellationToken ct = default, Action<HarnessRun>? started = null)
    {
        if (toolchain.LoginArguments is not { Count: > 0 } login)
        {
            throw new DriverException(
                "that agent declares no sign-in flow — sign in with its own tooling, pointing its "
                + "configuration-home variable at the account's directory.");
        }

        var code = await RunAsync([.. toolchain.Command(command), .. login], toolchain, profileHome, write, ct, started)
            .ConfigureAwait(false);
        if (code == 0)
        {
            try
            {
                AccountCooling.SignedIn(profileHome, DateTimeOffset.UtcNow);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The sign-in stands; the cool-off ends at its reset instead.
            }
        }

        return code;
    }

    /// <summary>
    /// Spawn and relay. Both streams, line by line, in the order they arrive — the same shape the
    /// session console takes, because this is a process like any other.
    /// </summary>
    internal static async Task<int> RunAsync(
        IReadOnlyList<string> command, HarnessToolchain toolchain, string? profileHome,
        Action<string> write, CancellationToken ct, Action<HarnessRun>? started = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = command[0],
            // Its stdin is the screen's: a login asks for the code the browser shows, and a
            // process nobody can answer waits for ever. Its output is relayed to the console
            // below, never a window of its own — and read as UTF-8, because a harness writes it so
            // and the console's codepage is not the transcript (the Adapters.Shell rule).
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var part in command.Skip(1)) info.ArgumentList.Add(part);
        // An agent action is a child like a session (TOOLS5): the tools' PATH, before the file is resolved on it.
        Tools.Hand(info);
        // Resolves the file Windows can start too (WindowsShim, USE1f): one line for every spawn.
        HarnessProbe.Apply(info, toolchain, profileHome);

        write($"$ {string.Join(' ', command)}");

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new DriverException($"`{command[0]}` did not start");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Said by the driver, not by Win32: its message names the working directory, and one that
            // reached the page as a bare exception became the generic "something refused" (REV3).
            throw new DriverException(
                $"`{command[0]}` could not be started — it is not on this machine's PATH, or it could not be run. "
                + "Daoris runs the agent's own tooling; install that first, or run it from a terminal.");
        }

        using var _ = process;
        started?.Invoke(new HarnessRun(process));

        await Task.WhenAll(PumpAsync(process.StandardOutput, write, ct), PumpAsync(process.StandardError, write, ct))
            .ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }

    /// <summary>
    /// The file to start for <paramref name="command"/> — on Windows, the shim a bare name resolves to.
    /// </summary>
    /// <remarks>
    /// 🔴 Every declared installer is `npm …`, and on Windows `npm` is `npm.cmd`: started by its bare
    /// name with no shell, Windows appends only `.exe` and finds nothing (REV3; the CLI's `spawnable` is
    /// the twin, fixed 2026-09-22). An agent npm installed globally is the same shape (USE1f), so every
    /// spawn of a harness comes through here, by <see cref="HarnessProbe.Apply"/>. A shim is then run by
    /// `cmd.exe`, which parses its arguments again and stops at a line break — so an argument it would
    /// reinterpret is refused rather than escaped. An installer's are paths and `package@version`s, and
    /// never carry one; the pipe door's prompt does, and is refused on a shim rather than cut short.
    /// </remarks>
    internal static string WindowsShim(string command, IEnumerable<string> arguments, string? path)
    {
        if (!OperatingSystem.IsWindows()) return command;

        var file = CommandPresence.Resolve(command, path, startable: true) ?? command;
        if (!file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            && !file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return file;
        }

        foreach (var argument in arguments)
        {
            if (argument.IndexOfAny(['"', '%', '&', '|', '<', '>', '^', '\r', '\n']) >= 0)
            {
                // Named by its first line, short: a whole prompt in a refusal is a page, not a sentence.
                var line = argument.Split('\n')[0].TrimEnd('\r');
                var shown = line.Length > 60 || line.Length < argument.Length ? $"{line[..Math.Min(line.Length, 60)]}…" : line;
                throw new DriverException(
                    $"`{shown}` cannot be passed to a Windows command shim safely ({Path.GetFileName(file)} is one), "
                    + "so it was not started. Run the agent's own tooling from a terminal, or point Daoris at the "
                    + "tool's own executable: a pin, or its path in driver.json's `commands`.");
            }
        }

        return file;
    }

    /// <summary>How long a partial line may sit before it is taken for a prompt.</summary>
    internal static readonly TimeSpan PromptQuiet = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Relay a stream line by line — <b>and a line that has no end</b>. A harness that asks something
    /// prints its prompt without a newline and waits; a pump that delivers only whole lines holds
    /// back the one line the person has to answer, and a login sat on a blank console with its
    /// <i>paste the code</i> never shown (measured on the real binary, 2026-09-23). What is buffered
    /// when the stream goes quiet is delivered as a line of its own.
    /// </summary>
    internal static async Task PumpAsync(TextReader reader, Action<string> write, CancellationToken ct)
    {
        var buffer = new char[1024];
        var pending = new StringBuilder();

        void Deliver(bool evenEmpty)
        {
            if (pending.Length == 0 && !evenEmpty) return;
            var line = Clean(pending.ToString());
            pending.Clear();
            lock (write) write(line);
        }

        var read = reader.ReadAsync(buffer, ct).AsTask();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var quiet = Task.Delay(PromptQuiet, ct);
            if (await Task.WhenAny(read, quiet).ConfigureAwait(false) != read)
            {
                Deliver(evenEmpty: false);
                continue;
            }

            var count = await read.ConfigureAwait(false);
            if (count == 0)
            {
                Deliver(evenEmpty: false);
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var ch = buffer[i];
                if (ch == '\n') Deliver(evenEmpty: true);
                else if (ch != '\r') pending.Append(ch);
            }

            read = reader.ReadAsync(buffer, ct).AsTask();
        }
    }

    // OSC (a hyperlink, a title), then CSI (colour, cursor), then any other two-byte escape.
    private static readonly Regex Escapes = new(
        @"\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)|\x1b\[[0-9;?]*[ -/]*[@-~]|\x1b[@-Z\\-_]",
        RegexOptions.Compiled);

    /// <summary>
    /// The text without the terminal's own instructions. A harness writes for a terminal, and a
    /// sign-in link arrives wrapped as a hyperlink escape (OSC 8) — the address, then the address
    /// again as its own label — which a console well renders as the URL twice around a scatter of
    /// brackets and semicolons. The label is kept; the wrapping goes, and colours with it.
    /// </summary>
    internal static string Clean(string text) => Escapes.Replace(text, "");
}
