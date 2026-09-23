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
    string? KeyVariable = null)
{
    /// <summary>The command this harness actually runs as: the machine's configured one, or the declared one.</summary>
    public IReadOnlyList<string> Command(IReadOnlyList<string>? configured) =>
        configured is { Count: > 0 } ? configured : Binary;
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
    public static string HomeOf(string settingsPath) =>
        Path.GetDirectoryName(Path.GetFullPath(settingsPath))!;

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
                ReadCircles(document.RootElement, "workspaceVersions"));
        }
        catch (Exception error)
            when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HarnessSettings();
        }
    }

    /// <summary>
    /// Write the wiring back — atomically, beside-then-rename, like every write in this family.
    /// </summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

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

            writer.WriteEndObject();
        }

        var beside = path + ".writing";
        File.WriteAllText(beside, System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n");
        File.Move(beside, path, overwrite: true);
    }

    /// <summary>
    /// Which profile a spawn runs as: the person's pick, then the workspace's default, then the
    /// machine's, then <b>none at all</b> — and none means the harness's own configuration home.
    /// </summary>
    public string? Resolve(string harness, string? workspace, string? chosen)
    {
        if (!string.IsNullOrWhiteSpace(chosen)) return chosen.Trim();

        if (!string.IsNullOrWhiteSpace(workspace)
            && Workspaces.TryGetValue(workspace.Trim(), out var circle)
            && circle.TryGetValue(harness, out var perCircle)
            && !string.IsNullOrWhiteSpace(perCircle))
        {
            return perCircle;
        }

        return Defaults.TryGetValue(harness, out var machine) && !string.IsNullOrWhiteSpace(machine)
            ? machine
            : null;
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
    public string? ResolveVersion(string harness, string? workspace, string? chosen)
    {
        if (!string.IsNullOrWhiteSpace(chosen)) return chosen.Trim();

        if (!string.IsNullOrWhiteSpace(workspace)
            && WorkspaceVersions.TryGetValue(workspace.Trim(), out var circle)
            && circle.TryGetValue(harness, out var perCircle)
            && !string.IsNullOrWhiteSpace(perCircle))
        {
            return perCircle;
        }

        return Versions.TryGetValue(harness, out var machine) && !string.IsNullOrWhiteSpace(machine)
            ? machine
            : null;
    }

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

    /// <summary>Where a managed version of a harness lives. Daoris owns this location, binary and all.</summary>
    public static string ManagedHome(string home, string harness, string version) =>
        Path.Combine(home, "toolchain", SafeName(harness, "agent name"), SafeName(version, "version"));

    /// <summary>
    /// The executable inside a managed install, or null when nothing is pinned or nothing is
    /// installed at the pin.
    /// </summary>
    /// <remarks>
    /// <b>npm's layout, because npm is how these harnesses ship</b>: <c>--prefix &lt;dir&gt;</c> puts
    /// the shims in <c>&lt;dir&gt;/node_modules/.bin</c>, with a <c>.cmd</c> beside the shell script
    /// on Windows — whichever exists is the answer.
    ///
    /// <para>🔴 A pin whose directory is not there answers null, and every caller falls back to
    /// <c>PATH</c> and <b>says so</b>. Silently running a different tool than the one the person
    /// pinned, and reporting success, is the failure this shape exists to prevent.</para>
    /// </remarks>
    public static string? ManagedBinary(
        string home, string harness, string? version, IReadOnlyList<string> binary)
    {
        if (string.IsNullOrWhiteSpace(version) || binary.Count == 0) return null;

        var bin = Path.Combine(ManagedHome(home, harness, version), "node_modules", ".bin");
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
        // An account that was a key goes with its key (AGT3): a removed account keeps nothing here.
        HarnessKeys.Remove(home, harness, profile);
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

    /// <inheritdoc cref="Name"/>
    private static string SafeName(string value, string what) => Name(value, what);

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
        var beside = path + ".writing";
        File.WriteAllText(beside, Encoding.UTF8.GetString(stream.ToArray()) + "\n");
        File.Move(beside, path, overwrite: true);
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
                    settings.Defaults.TryGetValue(adapter, out var pinnedDefault) ? pinnedDefault : null,
                    []);
            }
        }

        var version = toolchain.ProbeByPresence
            ? (Ran: resolved.Count > 0 && CommandPresence.Resolvable(resolved[0]), Output: "", Problem: (string?)null)
            : await AskAsync(resolved, toolchain.VersionArguments, profileHome: null, toolchain, ct, isManaged)
                .ConfigureAwait(false);

        var present = version.Ran;
        var profiles = new List<ProfileReport>();

        foreach (var name in HarnessSettings.Profiles(home, adapter))
        {
            var profileHome = HarnessSettings.ProfileHome(home, adapter, name);
            // An account that is a key is asked WITH its key (AGT3), as a session would run it, so
            // the roster says what the tool says — and names it by the key's handle, never the key.
            var key = toolchain.KeyVariable is { Length: > 0 } variable
                && HarnessKeys.Of(home, adapter, name) is { } held
                    ? (Variable: variable, Value: held)
                    : ((string Variable, string Value)?)null;
            var (login, account) = present
                ? await LoginAsync(
                    resolved, toolchain, profileHome, isManaged, ct,
                    key is { } k ? new Dictionary<string, string> { [k.Variable] = k.Value } : null)
                    .ConfigureAwait(false)
                : (LoginState.Unknown, null);
            profiles.Add(new ProfileReport(
                name, profileHome, login, account, key is { } shown ? HarnessKeys.Handle(shown.Value) : null));
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
            settings.Defaults.TryGetValue(adapter, out var machine) ? machine : null,
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
            UseShellExecute = false,
            CreateNoWindow = true, // a probe from a window must not open a console (Adapters.Shell)
        };
        foreach (var part in resolved.Skip(1)) info.ArgumentList.Add(part);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
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

        return await HarnessProbe.AskLoginAsync(
            resolved.Name, toolchain, config.Commands.GetValueOrDefault(resolved.Name), Settings, Home,
            HarnessSettings.ProfileHome(Home, resolved.Name, profile), ct).ConfigureAwait(false);
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
    /// <param name="workspace">The repository's circle, for the per-workspace default (D49 §4).</param>
    /// <param name="chosen">The person's pick for this session, when they made one.</param>
    public async Task<HarnessSelection> SelectAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen,
        CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);

        // An adapter that declares no toolchain has nothing to check: it spawns exactly as it did
        // before this existed. Purely additive, which is what lets a new adapter arrive without
        // answering questions about installers it may not have.
        if (resolved.Toolchain is not { } toolchain)
        {
            return new HarnessSelection(Refusal: null);
        }

        var settings = Settings;
        var profile = settings.Resolve(resolved.Name, workspace, chosen);

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

        if (profile is null)
        {
            return new HarnessSelection(null, null, null, report?.Version, managed, claude);
        }

        var home = HarnessSettings.ProfileHome(Home, resolved.Name, profile);
        var login = report?.Profiles.FirstOrDefault(
            p => string.Equals(p.Name, profile, StringComparison.OrdinalIgnoreCase))?.Login
            ?? LoginState.Unknown;

        if (login == LoginState.Out)
        {
            // Same re-ask as above, and for the same reason: the person may have just logged in.
            report = await ReportAsync(resolved.Name, config, refresh: true, ct).ConfigureAwait(false);
            login = report?.Profiles.FirstOrDefault(
                p => string.Equals(p.Name, profile, StringComparison.OrdinalIgnoreCase))?.Login
                ?? LoginState.Unknown;
        }

        if (login == LoginState.Out)
        {
            return new HarnessSelection(
                $"the `{resolved.Name}` profile `{profile}` is not logged in, so a session would have "
                + $"nothing to run as — `daoris agent login {resolved.Name} --profile {profile}` runs "
                + "the agent's own login flow into it. Daoris manages the directory and the name; the "
                + "credential stays in the agent's own store.");
        }

        // An account that is a key is handed its key through the tool's own variable (AGT3).
        var key = toolchain.KeyVariable is { Length: > 0 } variable
            && HarnessKeys.Of(Home, resolved.Name, profile) is { } held
                ? new Dictionary<string, string> { [variable] = held }
                : null;

        return new HarnessSelection(null, profile, home, report?.Version, managed, claude, key);
    }
}

/// <summary>
/// A harness action while it runs (D49 §4): the one thing a screen may send it, and the way it is
/// stopped. A login prints a prompt and waits — <i>paste the code</i> — and a process nobody can
/// answer or stop is a page with every control disabled until the window closes (owner, 2026-09-23).
/// </summary>
public sealed class HarnessRun
{
    private readonly Process _process;

    internal HarnessRun(Process process) => _process = process;

    /// <summary>Answer the harness's prompt — one line, as a terminal would send it.</summary>
    public void Send(string line)
    {
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
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
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
    public static Task<int> InstallAsync(
        HarnessToolchain toolchain, Action<string> write, CancellationToken ct = default,
        Action<HarnessRun>? started = null) =>
        toolchain.Install is { Count: > 0 } install
            ? RunAsync(install, toolchain, profileHome: null, write, ct, started)
            : throw new DriverException(
                "that agent declares no installer, so Daoris has no sanctioned way to install it. "
                + "Install it with its own tooling; Daoris will find it on the next probe.");

    /// <summary>
    /// Install one version into the directory Daoris owns (TOOL2/D57), leaving the machine's own
    /// install alone. npm's own `--prefix`, aimed somewhere Daoris chose.
    /// </summary>
    public static Task<int> PinAsync(
        HarnessToolchain toolchain, string home, string harness, string version, Action<string> write,
        CancellationToken ct = default, Action<HarnessRun>? started = null) =>
        toolchain.Package is { Length: > 0 } package
            ? RunAsync(
                ["npm", "install", "--prefix", HarnessSettings.ManagedHome(home, harness, version),
                 $"{package}@{version}"],
                toolchain, profileHome: null, write, ct, started)
            : throw new DriverException(
                "that agent declares no package, so Daoris has no sanctioned way to fetch a version "
                + "of it. Install it with its own tooling and Daoris will find it on PATH.");

    /// <summary>Update a present harness through its own updater.</summary>
    public static Task<int> UpdateAsync(
        HarnessToolchain toolchain, IReadOnlyList<string>? command, Action<string> write,
        CancellationToken ct = default, Action<HarnessRun>? started = null) =>
        toolchain.UpdateArguments is { Count: > 0 } update
            ? RunAsync([.. toolchain.Command(command), .. update], toolchain, profileHome: null, write, ct, started)
            : throw new DriverException("that agent declares no updater — it updates itself, or its package manager does.");

    /// <summary>
    /// Run the harness's own login flow INTO a profile. The directory is Daoris's; everything that
    /// lands in it is the harness's.
    /// </summary>
    public static Task<int> LoginAsync(
        HarnessToolchain toolchain, IReadOnlyList<string>? command, string profileHome,
        Action<string> write, CancellationToken ct = default, Action<HarnessRun>? started = null) =>
        toolchain.LoginArguments is { Count: > 0 } login
            ? RunAsync([.. toolchain.Command(command), .. login], toolchain, profileHome, write, ct, started)
            : throw new DriverException(
                "that agent declares no login flow — log in with its own tooling, pointing its "
                + "configuration-home variable at the account's directory.");

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
        HarnessProbe.Apply(info, toolchain, profileHome);

        write($"$ {string.Join(' ', command)}");

        using var process = Process.Start(info)
            ?? throw new DriverException($"`{command[0]}` did not start");
        started?.Invoke(new HarnessRun(process));

        await Task.WhenAll(PumpAsync(process.StandardOutput, write, ct), PumpAsync(process.StandardError, write, ct))
            .ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
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
