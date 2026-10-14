using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// Where the scanner reads a repository's doctrine, and which of its folders are rooms (LAYOUT4; D117,
/// `docs/2026-10-01-agent-layout-design.md` §5.5).
/// </summary>
/// <remarks>
/// <para><b>The roots.</b> 🔴 The lock, not the manifest, says where the files are (§5.1): between a
/// manifest's flip and the sync that moves them, the manifest names the new root and the files are
/// still at the old. So a repository with a lock is read at the lock's root (<see cref="DaorisLock.Target"/>),
/// and on a layout that moved from another root, at that one too: the repository's own documents may
/// still sit there, and a skill it keeps for one agent sits beside the mirrors, which the lock tells
/// apart. A repository with no lock is read at the manifest's root and at both <c>.agents</c> and
/// <c>.claude</c>, since an unadopted repository in the reference's shape keeps its skills in the
/// first, and nothing says which.</para>
///
/// <para><b>The rooms</b> are the manifest's <c>rooms</c>: declared, never found, since a walk of the
/// tree meets build output and worktrees (§2.2). The lock's <c>rooms</c> record the pointers
/// <c>sync</c> wrote, a <c>CLAUDE.md</c> holding one import line, and are not read here: a room's
/// <c>AGENTS.md</c> is the repository's own file, and its declaration is where it is.</para>
///
/// <para><b>The documents</b> are the manifest's <c>documents</c> (DOC5), checked against the same target
/// and mirror root as the rooms, by <see cref="RepositoryDocuments"/>, a twin of the CLI's
/// <c>documents.ts</c>.</para>
///
/// <para>🔴 <b>A twin</b> (<c>.claude/knowledge/twins.md</c>, *the layout*): the CLI's <c>layout.ts</c>
/// and <c>harness.ts</c> write what this reads, and share no code with it. <see cref="Descriptors"/> is
/// a deliberate copy of the CLI's descriptors; <c>RepositoryLayoutTests</c> holds the CLI's rows. The
/// one difference is the direction: the CLI refuses a target or a room that leaves the repository,
/// which this reads as no target and no room, never outside the repository and never failing the
/// whole corpus for one repository's file.</para>
/// </remarks>
public sealed partial class RepositoryLayout
{
    /// <summary>The layout every lock written before D117 was written under.</summary>
    internal const string Older = "claude-code";

    /// <summary>
    /// The layouts the CLI writes, by the manifest's <c>harness</c>: a copy of <c>HARNESSES</c> in
    /// <c>src/Daoris.Cli/src/harness.ts</c> (its <c>defaultTarget</c>, <c>formerly</c> and
    /// <c>mirror.root</c>), kept in step by hand, since the two artefacts share no code.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, Descriptor> Descriptors =
        new Dictionary<string, Descriptor>(StringComparer.Ordinal)
        {
            [Older] = new(".claude", Formerly: null, MirrorRoot: null),
            ["agents"] = new(".agents", Formerly: ".claude", MirrorRoot: ".claude/skills"),
        };

    /// <summary>One layout: its root, the root it moved from, and where it mirrors a tier for one agent.</summary>
    internal sealed record Descriptor(string DefaultTarget, string? Formerly, string? MirrorRoot);

    private RepositoryLayout(
        bool locked, IReadOnlyList<string> roots, IReadOnlyList<string> rooms, IReadOnlyList<RepositoryDocuments.Declared> documents)
    {
        Locked = locked;
        Roots = roots;
        Rooms = rooms;
        Documents = documents;
    }

    /// <summary>
    /// Whether a lock says where daoris wrote. False for a repository with no lock, or one read as none,
    /// which is read at both roots and, until it adopts, by its README (WSSETUP8; D124 §5).
    /// </summary>
    public bool Locked { get; }

    /// <summary>
    /// The roots the on-demand tiers are read at, repository-relative with forward slashes, each once,
    /// in the order they are read. The empty string is the repository's root.
    /// </summary>
    public IReadOnlyList<string> Roots { get; }

    /// <summary>The declared rooms whose <c>AGENTS.md</c> is read, repository-relative with forward slashes.</summary>
    public IReadOnlyList<string> Rooms { get; }

    /// <summary>
    /// The development documents the manifest declares, as the CLI would accept them, in the roles' order
    /// (DOC5, <see cref="RepositoryDocuments"/>). Checked against the same target and mirror root as the
    /// rooms, so the two declarations of one manifest are read by one rule.
    /// </summary>
    public IReadOnlyList<RepositoryDocuments.Declared> Documents { get; }

    /// <summary>The path declared for a role, or null when it declares none the CLI would accept.</summary>
    public string? PathOf(string role) => Documents.FirstOrDefault(document => document.Role == role)?.Path;

    /// <summary>The layout of the repository at <paramref name="repositoryRoot"/>, given its lock.</summary>
    public static RepositoryLayout Of(string repositoryRoot, DaorisLock daorisLock)
    {
        var manifest = Manifest.Read(repositoryRoot);

        string target;
        Descriptor layout;
        List<string> roots;
        if (daorisLock.Target is { } written)
        {
            layout = Descriptors[daorisLock.Harness!];
            target = written;
            roots = [target];
            if (layout.Formerly is { } formerly) roots.Add(formerly);
        }
        else
        {
            // No lock, or one read as none: the layout the manifest names, as the CLI would write it,
            // then both roots.
            layout = manifest.Harness is { } named && Descriptors.TryGetValue(named, out var known) ? known : Descriptors[Older];
            target = manifest.Target ?? layout.DefaultTarget;
            roots = [target, ".agents", ".claude"];
        }

        return new RepositoryLayout(
            daorisLock.Target is not null,
            roots.Distinct(StringComparer.Ordinal).ToList(),
            ReadableRooms(manifest.Rooms, target, layout),
            RepositoryDocuments.Read(manifest.Documents, target, layout.MirrorRoot));
    }

    /// <summary>
    /// The declared rooms the CLI would accept (its <c>checkRooms</c>), as a skip rather than a refusal:
    /// not one that leaves the repository or is its root, and not one inside the target or the mirror
    /// root, whose files are the tiers'. Each once.
    /// </summary>
    private static List<string> ReadableRooms(IReadOnlyList<string> declared, string target, Descriptor layout)
    {
        var rooms = new List<string>();
        foreach (var raw in declared)
        {
            var room = Declared(raw);
            if (Escapes(room) || room is "" or ".") continue;
            if (Within(room, target)) continue;
            if (layout.MirrorRoot is { } mirror && Within(room, mirror)) continue;
            if (!rooms.Contains(room, StringComparer.Ordinal)) rooms.Add(room);
        }
        return rooms;
    }

    /// <summary>A declared path as the manifest and the lock spell it: forward slashes, no <c>./</c>, no trailing slash.</summary>
    internal static string Declared(string path) =>
        LeadingDot().Replace(path.Replace('\\', '/'), string.Empty).TrimEnd('/');

    /// <summary>Whether a declared path leaves the repository: absolute on either platform, or climbing out.</summary>
    internal static bool Escapes(string path)
    {
        if (path.StartsWith('/') || DriveLetter().IsMatch(path)) return true;
        var depth = 0;
        foreach (var part in path.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (depth == 0) return true;
                depth--;
            }
            else
            {
                depth++;
            }
        }
        return false;
    }

    /// <summary>Whether <paramref name="child"/> is <paramref name="parent"/> or sits below it, both declared paths.</summary>
    internal static bool Within(string child, string parent) =>
        child == parent || child.StartsWith(parent + "/", StringComparison.Ordinal);

    /// <summary>A folder below a root: the root itself when it is the repository's.</summary>
    internal static string Under(string root, string folder) => root.Length == 0 ? folder : $"{root}/{folder}";

    [GeneratedRegex(@"^(\./)+")]
    private static partial Regex LeadingDot();

    [GeneratedRegex("^[A-Za-z]:")]
    private static partial Regex DriveLetter();

    /// <summary>
    /// What the scanner needs of <c>daoris.json</c>, read leniently: a field of the wrong shape is
    /// absent, and a manifest that cannot be read at all declares nothing, rather than failing the
    /// corpus for one repository's file (REV3's reasoning, as <see cref="DaorisLock"/> reads its own).
    /// </summary>
    /// <param name="Harness">The descriptor it names; null when it names none.</param>
    /// <param name="Target">Its target, declared; null when absent or leaving the repository.</param>
    /// <param name="Rooms">Its rooms as written, before any is checked.</param>
    /// <param name="Documents">
    /// Its <c>documents</c> as written, before any role is checked (DOC5); null when absent, or when the
    /// manifest holds the field twice, since which was meant is not this reader's guess.
    /// </param>
    internal sealed record Manifest(string? Harness, string? Target, IReadOnlyList<string> Rooms, JsonElement? Documents = null)
    {
        private static readonly Manifest None = new(null, null, []);

        /// <summary>Whether it names the older layout: by name, or by naming none.</summary>
        public bool OnOlderLayout => Harness is null or Older;

        public static Manifest Read(string repositoryRoot)
        {
            var file = Path.Combine(repositoryRoot, "daoris.json");
            if (!File.Exists(file)) return None;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.ValueKind != JsonValueKind.Object) return None;
                var root = document.RootElement;

                var harness = root.TryGetProperty("harness", out var h) && h.ValueKind == JsonValueKind.String
                    ? h.GetString()
                    : null;
                var target = root.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.String
                    ? Declared(t.GetString()!)
                    : null;
                var rooms = root.TryGetProperty("rooms", out var r) && r.ValueKind == JsonValueKind.Array
                    ? r.EnumerateArray().Where(room => room.ValueKind == JsonValueKind.String).Select(room => room.GetString()!).ToList()
                    : [];
                // Cloned, so it outlives the document; held twice, it is read as none (the CLI refuses it).
                var documents = root.EnumerateObject().Count(member => member.Name == "documents") == 1
                    ? root.GetProperty("documents").Clone()
                    : (JsonElement?)null;

                return new Manifest(harness, target is not null && !Escapes(target) ? target : null, rooms, documents);
            }
            catch (JsonException)
            {
                return None;
            }
        }
    }
}
