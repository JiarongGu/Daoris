using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One entry of a commit's tree, as <c>git ls-tree -r -z</c> lists it: its mode, type, object and path.</summary>
public sealed record LineEntry(string Mode, string Type, string Object, string Path)
{
    /// <summary>A symbolic link: git keeps its target as the blob, whatever a checkout makes of it.</summary>
    public bool IsLink => Mode == "120000";

    /// <summary>A file of the repository's own: a blob, plain or executable. Not a link, and not a submodule's commit.</summary>
    public bool IsFile => Type == "blob" && Mode is "100644" or "100755";

    /// <summary>
    /// <c>git ls-tree -r -z</c>'s output: <c>&lt;mode&gt; SP &lt;type&gt; SP &lt;object&gt; TAB &lt;path&gt; NUL</c>, the
    /// path unquoted under <c>-z</c>. A record of any other shape is skipped.
    /// </summary>
    public static IReadOnlyList<LineEntry> Parse(string output)
    {
        var entries = new List<LineEntry>();
        foreach (var record in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf('\t');
            if (tab < 0) continue;
            var fields = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 3 || record.Length == tab + 1) continue;
            entries.Add(new LineEntry(fields[0], fields[1], fields[2], record[(tab + 1)..]));
        }

        return entries;
    }
}

/// <summary>How an instruction file stands on the line.</summary>
public enum InstructionState
{
    /// <summary>Not on the line.</summary>
    Absent,

    /// <summary>A file; where a folder must go (<c>.claude/skills</c>), a file of some other content.</summary>
    File,

    /// <summary>A link (mode 120000): a checkout with links hands an agent what it points at, one without hands it the path.</summary>
    Link,

    /// <summary>A file whose whole content is a path: a link checked out as text and committed so.</summary>
    HeldAsText,
}

/// <summary>One instruction file on the line (D117 §6.2).</summary>
/// <param name="OwnLines">The lines with words on them outside Daoris's region: the repository's own.</param>
/// <param name="Region">Whether Daoris's region is in it (the rules in <c>AGENTS.md</c>, the import in <c>CLAUDE.md</c>).</param>
/// <param name="Imports">Whether it imports <c>AGENTS.md</c> (<c>@AGENTS.md</c> on a line of its own), in the region or out of it.</param>
/// <param name="Target">A link's target, or the path a file held as text holds.</param>
public sealed record InstructionFile(
    InstructionState State, int OwnLines = 0, bool Region = false, bool Imports = false, string? Target = null)
{
    public static readonly InstructionFile Absent = new(InstructionState.Absent);

    /// <summary>A link, or a link held as text: an agent on some checkout reads a path where instructions should be.</summary>
    public bool IsLinkLike => State is InstructionState.Link or InstructionState.HeldAsText;
}

/// <summary>The layouts the screen names (D117 §6.5).</summary>
public static class AgentLayout
{
    /// <summary>Adopted on the agents layout: <c>.agents/</c>, a mirror for the agent that reads <c>.claude/</c>.</summary>
    public const string Agents = "agents";

    /// <summary>Adopted on the older layout, where one agent reads the skills and the others the instructions only.</summary>
    public const string Claude = ".claude";

    /// <summary>Instruction files, skills or rules of its own, and no doctrine.</summary>
    public const string Own = "its own";

    /// <summary>Nothing for agents at all.</summary>
    public const string None = "none";
}

/// <summary>
/// What a repository's LINE holds for agents (LAYOUT7, D117 §6.2 and §6.5): read as git objects at a named
/// commit, never the checkout's working files, since the line is what the next session's tree grows from.
/// </summary>
public sealed record LayoutFacts
{
    /// <summary>The branch the line is.</summary>
    public required string Line { get; init; }

    /// <summary>The commit the line names, which every fact here was read at.</summary>
    public required string Commit { get; init; }

    /// <summary>Whether <c>daoris.json</c> is on the line.</summary>
    public bool Adopted { get; init; }

    /// <summary>Whether that manifest reads as a JSON object; one that does not declares nothing and names no layout.</summary>
    public bool ManifestReads { get; init; }

    /// <summary>Whether its <c>domain</c> says anything (<c>connect</c>'s <c>isDeclared</c>).</summary>
    public bool Declares { get; init; }

    /// <summary>Whether <c>daoris.lock</c> is on the line: <c>sync</c> has run there.</summary>
    public bool Locked { get; init; }

    /// <summary>The layout the lock was written under, when a lock is on the line and names one this build knows.</summary>
    public string? LockHarness { get; init; }

    /// <summary>The layout the manifest names, when it is on the line: <c>claude-code</c> when it names none.</summary>
    public string? ManifestHarness { get; init; }

    /// <summary>One of <see cref="AgentLayout"/>'s.</summary>
    public string Layout { get; init; } = AgentLayout.None;

    /// <summary>The root <c>AGENTS.md</c>.</summary>
    public InstructionFile Agents { get; init; } = InstructionFile.Absent;

    /// <summary>The root <c>CLAUDE.md</c>.</summary>
    public InstructionFile Claude { get; init; } = InstructionFile.Absent;

    /// <summary><c>.claude/skills</c> itself, when it is not a folder: a link, one held as text, or a file. Absent otherwise.</summary>
    public InstructionFile ClaudeSkillsRoot { get; init; } = InstructionFile.Absent;

    /// <summary>This checkout's <c>core.symlinks</c>; null where it is unset, which is git's own default, links.</summary>
    public bool? Symlinks { get; init; }

    /// <summary>Whether this checkout holds a link as a file of its target's path.</summary>
    public bool LinksHeldAsText => Symlinks is false;

    /// <summary>The files under <c>.claude/rules/</c>, read by one agent alone.</summary>
    public IReadOnlyList<string> Rules { get; init; } = [];

    /// <summary>The skills under <c>.claude/skills/</c>, by folder.</summary>
    public IReadOnlyList<string> ClaudeSkills { get; init; } = [];

    /// <summary>The files under <c>.claude/knowledge/</c>.</summary>
    public IReadOnlyList<string> ClaudeKnowledge { get; init; } = [];

    /// <summary>The skills under <c>.agents/skills/</c>, by folder.</summary>
    public IReadOnlyList<string> AgentsSkills { get; init; } = [];

    /// <summary>The files under <c>.agents/knowledge/</c>.</summary>
    public IReadOnlyList<string> AgentsKnowledge { get; init; } = [];

    /// <summary>The skills under <c>.claude/skills/</c> the lock records as Daoris's mirrors.</summary>
    public IReadOnlyList<string> Mirrors { get; init; } = [];

    /// <summary>The manifest's <c>rooms</c>, as declared.</summary>
    public IReadOnlyList<string> DeclaredRooms { get; init; } = [];

    /// <summary>The folders holding an <c>AGENTS.md</c> of their own, outside the doctrine's folders.</summary>
    public IReadOnlyList<string> Rooms { get; init; } = [];

    /// <summary>The declared rooms whose <c>AGENTS.md</c> is not on the line as a file.</summary>
    public IReadOnlyList<string> RoomsWithout { get; init; } = [];

    /// <summary>
    /// On the agents layout and finished, as far as the line shows: the lock and the manifest both on it, the region
    /// in the root <c>AGENTS.md</c>, no instruction file or skills root a link, nothing left in the old knowledge tier,
    /// and every declared room with its instructions. What else <c>check</c> holds is the tool's to say, in that
    /// repository's own gate.
    /// </summary>
    public bool Clean { get; init; }
}

/// <summary>A line read: its facts, or why it could not be read.</summary>
public sealed record LineReading(LayoutFacts? Facts, string? Problem);

/// <summary>
/// Reads a repository's layout from its line (LAYOUT7, D117 §6.5): the listing of the line's commit, the few
/// texts that say the layout, and this checkout's <c>core.symlinks</c>.
/// </summary>
/// <remarks>
/// <para><b>Pure, and handed what git answered</b> (<see cref="Read"/>), with the git half apart
/// (<see cref="ReadAsync"/>): plan and apply, as everywhere, so every case is held without a process.</para>
///
/// <para>🔴 <b>A twin</b> (<c>.claude/knowledge/twins.md</c>, *the layout* and *a link held as text*): the CLI's
/// <c>layout.ts</c> and <c>links.ts</c> write and refuse what this reads, and the service's <c>RepositoryLayout</c>
/// and <c>RepositoryLinks</c> read it from disk. The three share no code. The order is theirs, <i>the lock, then the
/// manifest, then <c>.claude</c></i>, and a lock naming a layout this build does not know is read as none, as the
/// service reads it. A link is read from its MODE first; a file is then held as text by the CLI's content cases,
/// which <c>LayoutReaderTests</c> holds in the CLI's order, with what sits beside read from the line's tree.</para>
/// </remarks>
public static class LayoutReader
{
    /// <summary>The layout every lock written before the agents layout was written under.</summary>
    private const string Older = "claude-code";

    /// <summary>The layouts the CLI writes: a copy of its descriptors' names, kept in step by hand.</summary>
    private static readonly string[] Known = [Older, AgentLayout.Agents];

    private const string Manifest = "daoris.json";
    private const string LockFile = "daoris.lock";
    private const string AgentsFile = "AGENTS.md";
    private const string ClaudeFile = "CLAUDE.md";
    private const string SkillsRoot = ".claude/skills";

    /// <summary>The characters a token cannot start with: an import, a heading, markup, a list, a quote. The CLI's.</summary>
    private const string NotAPath = "@#<>![-*|`";

    /// <summary>How much of one text git is asked for: a manifest, a lock or an instruction file is far smaller.</summary>
    private const int TextLimit = 512 * 1024;

    /// <summary>
    /// The facts of <paramref name="line"/> in the checkout at <paramref name="root"/>, read as git objects: the
    /// line's commit, its tree, the texts <see cref="Wanted"/> names, and the checkout's <c>core.symlinks</c>. The
    /// working files and the index are never asked, so what the person has in flight there is neither read nor touched.
    /// </summary>
    /// <returns>The facts, or why the line could not be read: no repository there, or a line that names no commit.</returns>
    public static async Task<LineReading> ReadAsync(string root, string line, CancellationToken ct = default)
    {
        // 🔴 git walks UP (FIX-LOG): a folder that is not a repository's top would answer for the one above it.
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, "its checkout here is not a git repository");
        }

        if (!BranchName.IsValid(line)) return new(null, $"its line `{line}` is not a branch name git would take");

        var (found, sha, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        var commit = sha.Trim();
        if (found != 0 || !WorkingTree.IsCommitId(commit)) return new(null, $"its line `{line}` names no commit here");

        var (listed, output, problem) = await WorkingTree.GitAsync(root, ["ls-tree", "-r", "-z", commit], ct).ConfigureAwait(false);
        if (listed != 0) return new(null, $"git could not list `{line}` at `{commit[..7]}`: {problem.Trim()}");

        var tree = LineEntry.Parse(output);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Wanted(tree))
        {
            var entry = tree.First(item => item.Path == path);
            if (!WorkingTree.IsCommitId(entry.Object)) continue;
            if (await WorkingTree.GitBytesAsync(root, ["cat-file", "blob", entry.Object], TextLimit, ct).ConfigureAwait(false) is { } read)
            {
                texts[path] = Encoding.UTF8.GetString(read.Bytes, 0, read.Count);
            }
        }

        // Unset answers 1: git's own default then, which is links. `--type=bool` reads `yes`, `on` and `1` as git does.
        var (set, symlinks, _) = await WorkingTree.GitAsync(root, ["config", "--type=bool", "--get", "core.symlinks"], ct).ConfigureAwait(false);
        return new(Read(line, commit, tree, texts, set == 0 ? symlinks.Trim() == "true" : null), null);
    }

    /// <summary>The paths whose text the reader reads: the manifest, the lock, the two instruction files, and the skills root where it is not a folder.</summary>
    public static IReadOnlyList<string> Wanted(IReadOnlyList<LineEntry> tree) =>
    [
        .. tree.Where(entry => entry.IsFile || entry.IsLink)
            .Select(entry => entry.Path)
            .Where(path => path is Manifest or LockFile or AgentsFile or ClaudeFile or SkillsRoot),
    ];

    /// <summary>The facts, from the listing of <paramref name="commit"/> and the texts <see cref="Wanted"/> named.</summary>
    /// <param name="texts">Each wanted path's text, as far as it was read; a path missing here is read as empty.</param>
    /// <param name="symlinks">This checkout's <c>core.symlinks</c>, or null where it is unset.</param>
    public static LayoutFacts Read(
        string line, string commit, IReadOnlyList<LineEntry> tree, IReadOnlyDictionary<string, string> texts, bool? symlinks)
    {
        string Text(string path) => texts.TryGetValue(path, out var text) ? text : "";
        var at = tree.Where(entry => entry.IsFile || entry.IsLink)
            .GroupBy(entry => entry.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var adopted = at.TryGetValue(Manifest, out var manifestEntry) && manifestEntry.IsFile;
        using var manifest = adopted ? Json(Text(Manifest)) : null;
        var locked = at.TryGetValue(LockFile, out var lockEntry) && lockEntry.IsFile;
        using var lockDocument = locked ? Json(Text(LockFile)) : null;

        var manifestHarness = adopted ? KnownOr(String(manifest, "harness"), Older) : null;
        var lockHarness = lockDocument is null ? null : String(lockDocument, "harness") ?? Older;
        if (lockHarness is not null && !Known.Contains(lockHarness, StringComparer.Ordinal)) lockHarness = null;

        var agents = Instruction(AgentsFile, at, Text, tree, "rules");
        var claude = Instruction(ClaudeFile, at, Text, tree, "import");
        var skillsRoot = at.ContainsKey(SkillsRoot) ? Instruction(SkillsRoot, at, Text, tree, region: null) : InstructionFile.Absent;

        var rules = Under(tree, ".claude/rules/");
        var claudeSkills = Skills(tree, ".claude/skills/");
        var claudeKnowledge = Under(tree, ".claude/knowledge/");
        var agentsSkills = Skills(tree, ".agents/skills/");
        var agentsKnowledge = Under(tree, ".agents/knowledge/");
        var mirrors = Mirrored(lockDocument).Intersect(claudeSkills, StringComparer.Ordinal).ToList();

        var rooms = tree
            .Where(entry => (entry.IsFile || entry.IsLink) && entry.Path.EndsWith("/" + AgentsFile, StringComparison.Ordinal))
            .Select(entry => entry.Path[..^(AgentsFile.Length + 1)])
            .Where(folder => !Within(folder, ".agents") && !Within(folder, ".claude"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var declaredRooms = Strings(manifest, "rooms").Select(Declared).Where(room => room.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var roomsWithout = declaredRooms
            .Where(room => !(at.TryGetValue($"{room}/{AgentsFile}", out var file) && file.IsFile))
            .ToList();

        // The lock, then the manifest, then the older layout (D117 §5.1): the lock says where the files ARE.
        var harness = lockHarness ?? manifestHarness;
        var ownThings = agents.State != InstructionState.Absent || claude.State != InstructionState.Absent
            || skillsRoot.State != InstructionState.Absent || rules.Count > 0 || claudeSkills.Count > 0
            || claudeKnowledge.Count > 0 || agentsSkills.Count > 0 || agentsKnowledge.Count > 0 || rooms.Count > 0;
        var layout = adopted
            ? harness == AgentLayout.Agents ? AgentLayout.Agents : AgentLayout.Claude
            : ownThings ? AgentLayout.Own : AgentLayout.None;

        var clean = layout == AgentLayout.Agents
            && lockHarness == AgentLayout.Agents && manifestHarness == AgentLayout.Agents
            && agents is { State: InstructionState.File, Region: true }
            && !claude.IsLinkLike && skillsRoot.State == InstructionState.Absent
            && claudeKnowledge.Count == 0 && roomsWithout.Count == 0;

        return new LayoutFacts
        {
            Line = line,
            Commit = commit,
            Adopted = adopted,
            ManifestReads = manifest is not null,
            Declares = Declares(manifest),
            Locked = locked,
            LockHarness = lockHarness,
            ManifestHarness = manifestHarness,
            Layout = layout,
            Agents = agents,
            Claude = claude,
            ClaudeSkillsRoot = skillsRoot,
            Symlinks = symlinks,
            Rules = rules,
            ClaudeSkills = claudeSkills,
            ClaudeKnowledge = claudeKnowledge,
            AgentsSkills = agentsSkills,
            AgentsKnowledge = agentsKnowledge,
            Mirrors = mirrors,
            DeclaredRooms = declaredRooms,
            Rooms = rooms,
            RoomsWithout = roomsWithout,
            Clean = clean,
        };
    }

    /// <summary>
    /// Whether a file's text is a link checked out as text (the CLI's <c>heldAsText</c>): its whole content one token
    /// that is its partner's name (<c>AGENTS.md</c> for <c>CLAUDE.md</c>, and back), a relative path starting
    /// <c>./</c> or <c>../</c>, or the name of something beside it on the line.
    /// </summary>
    /// <remarks>A probable, as the CLI's is, and refusing on a probable is safe: a one-word file that names nothing beside it is prose.</remarks>
    public static bool HeldAsText(string path, string text, IReadOnlyList<LineEntry> tree)
    {
        var held = Token(text);
        if (held is null) return false;

        var slash = path.LastIndexOf('/');
        var name = path[(slash + 1)..];
        var partner = name == ClaudeFile ? AgentsFile : name == AgentsFile ? ClaudeFile : null;
        if (held == partner || held.StartsWith("./", StringComparison.Ordinal) || held.StartsWith("../", StringComparison.Ordinal))
        {
            return true;
        }

        var beside = Declared(slash < 0 ? held : $"{path[..slash]}/{held}");
        return tree.Any(entry => entry.Path == beside || entry.Path.StartsWith(beside + "/", StringComparison.Ordinal));
    }

    /// <summary>One instruction file: absent, a link by its mode, held as text by its content, else a file with its own lines counted.</summary>
    /// <param name="region">The name of Daoris's region in it; null for a path that is no instruction file.</param>
    private static InstructionFile Instruction(
        string path, IReadOnlyDictionary<string, LineEntry> at, Func<string, string> text, IReadOnlyList<LineEntry> tree, string? region)
    {
        if (!at.TryGetValue(path, out var entry)) return InstructionFile.Absent;
        var content = text(path);
        if (entry.IsLink) return new InstructionFile(InstructionState.Link, Target: content.Trim());
        if (HeldAsText(path, content, tree)) return new InstructionFile(InstructionState.HeldAsText, Target: Token(content));
        if (region is null) return new InstructionFile(InstructionState.File);

        var own = 0;
        var inRegion = false;
        var seen = false;
        var imports = false;
        foreach (var raw in content.Replace("\r\n", "\n").TrimStart('﻿').Split('\n'))
        {
            var lineText = raw.Trim();
            if (lineText == "@" + AgentsFile) imports = true;
            if (lineText.StartsWith($"<!-- daoris:{region}", StringComparison.Ordinal))
            {
                inRegion = seen = true;
                continue;
            }

            if (lineText.StartsWith($"<!-- /daoris:{region}", StringComparison.Ordinal))
            {
                inRegion = false;
                continue;
            }

            if (!inRegion && lineText.Length > 0) own++;
        }

        return new InstructionFile(InstructionState.File, own, seen, imports);
    }

    /// <summary>The single token a link held as text is: no whitespace, not prose, not an import, not markup. The CLI's.</summary>
    private static string? Token(string text)
    {
        var trimmed = text.TrimStart('﻿').Trim();
        if (trimmed.Length is 0 or > 255) return null;
        if (trimmed.Any(char.IsWhiteSpace) || NotAPath.Contains(trimmed[0])) return null;
        return trimmed;
    }

    /// <summary>The files below a folder, by their path inside it, in order.</summary>
    private static List<string> Under(IReadOnlyList<LineEntry> tree, string folder) =>
    [
        .. tree.Where(entry => (entry.IsFile || entry.IsLink) && entry.Path.StartsWith(folder, StringComparison.Ordinal))
            .Select(entry => entry.Path[folder.Length..])
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>The skills below a folder: each folder there holding a <c>SKILL.md</c>.</summary>
    private static List<string> Skills(IReadOnlyList<LineEntry> tree, string folder) =>
    [
        .. Under(tree, folder)
            .Where(path => path.EndsWith("/SKILL.md", StringComparison.Ordinal) && path.Count(c => c == '/') == 1)
            .Select(path => path[..path.IndexOf('/')])
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>The skills whose <c>SKILL.md</c> the lock records as a mirror under <c>.claude/skills/</c>.</summary>
    private static IEnumerable<string> Mirrored(JsonDocument? lockDocument)
    {
        if (lockDocument?.RootElement.TryGetProperty("mirrors", out var mirrors) != true || mirrors.ValueKind != JsonValueKind.Array) yield break;
        foreach (var mirror in mirrors.EnumerateArray())
        {
            if (mirror.ValueKind != JsonValueKind.Object || !mirror.TryGetProperty("path", out var path) || path.ValueKind != JsonValueKind.String) continue;
            var file = Declared(path.GetString()!);
            const string prefix = SkillsRoot + "/";
            if (!file.StartsWith(prefix, StringComparison.Ordinal) || !file.EndsWith("/SKILL.md", StringComparison.Ordinal)) continue;
            var skill = file[prefix.Length..^"/SKILL.md".Length];
            if (skill.Length > 0 && !skill.Contains('/')) yield return skill;
        }
    }

    /// <summary><c>connect</c>'s <c>isDeclared</c>: a summary with words, an area owned, or a kind of work accepted.</summary>
    private static bool Declares(JsonDocument? manifest)
    {
        if (manifest?.RootElement.TryGetProperty("domain", out var domain) != true || domain.ValueKind != JsonValueKind.Object) return false;
        var summary = domain.TryGetProperty("summary", out var said) && said.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(said.GetString());
        bool Listed(string name) => domain.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0;
        return summary || Listed("owns") || Listed("accepts");
    }

    /// <summary>A JSON object, or null for text that is not one: a file that does not read says nothing.</summary>
    private static JsonDocument? Json(string text)
    {
        try
        {
            var document = JsonDocument.Parse(text.TrimStart('﻿'));
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? String(JsonDocument? document, string name) =>
        document is not null && document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<string> Strings(JsonDocument? document, string name) =>
        document is not null && document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)
            : [];

    /// <summary>A layout this build knows, else <paramref name="fallback"/>: the service reads an unknown manifest harness as the older one.</summary>
    private static string KnownOr(string? harness, string fallback) =>
        harness is not null && Known.Contains(harness, StringComparer.Ordinal) ? harness : fallback;

    /// <summary>A declared path as the manifest spells it: forward slashes, no <c>./</c>, no trailing slash.</summary>
    private static string Declared(string path)
    {
        var normal = path.Replace('\\', '/');
        while (normal.StartsWith("./", StringComparison.Ordinal)) normal = normal[2..];
        return normal.TrimEnd('/');
    }

    private static bool Within(string child, string parent) =>
        child == parent || child.StartsWith(parent + "/", StringComparison.Ordinal);
}
