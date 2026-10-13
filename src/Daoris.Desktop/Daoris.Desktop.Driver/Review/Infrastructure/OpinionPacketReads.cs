using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The packet read from git and from the copy (XAGENT1d): the candidate, the diff written beside the opinion, and the rules
/// named. Nothing here is read from the copy after its reviewer starts; the rules are read before it does.
/// </summary>
/// <remarks>
/// <para><b>Named where its callers always named it</b> (STRUCT3c, D166's note): <c>OpinionPackets.ReadAsync</c>,
/// <c>WriteDiffAsync</c> and <c>RulesIn</c> are static members of a C# 14 extension block on the Domain's
/// <see cref="OpinionPackets"/>, whose folder rule and git parses are pure; git and the tree's files are this layer's. Only the
/// loop and the tests ask them.</para>
/// </remarks>
internal static class OpinionPacketReads
{
    /// <summary>The root files a repository's agents are told to read first, in the order named: one harness reads each (D59).</summary>
    private static readonly string[] Instructions = ["AGENTS.md", "CLAUDE.md"];

    private const string Manifest = "daoris.json";

    /// <summary>The gates file by its own name, where a manifest declares none (D122).</summary>
    private const string GatesFile = "daoris.gates.json";

    extension(OpinionPackets)
    {
        /// <summary>
        /// The candidate from <paramref name="root"/>'s git: <paramref name="base"/> and <paramref name="tip"/> as commits by their
        /// full ids, the commits reachable from the tip and not the base, oldest first, and the paths the work changes since the
        /// two met.
        /// </summary>
        /// <exception cref="DriverException">Git cannot name a commit, there is no work between them, or more than the service keeps.</exception>
        public static async Task<OpinionCandidateRead> ReadAsync(
            string root, string repository, string @base, string tip, CancellationToken ct = default)
        {
            var from = await CommitAsync(root, repository, @base, ct).ConfigureAwait(false);
            var to = await CommitAsync(root, repository, tip, ct).ConfigureAwait(false);
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                throw new DriverException($"`{repository}` holds no work between `{@base}` and `{tip}`: a second opinion reads commits.");
            }

            var (listed, commits, listError) = await WorkingTree.GitAsync(root, ["rev-list", "--reverse", $"{from}..{to}"], ct).ConfigureAwait(false);
            if (listed != 0) throw new DriverException($"git could not list `{repository}`'s commits from `{@base}` to `{tip}`: {listError.Trim()}");
            var read = OpinionPackets.Commits(commits);
            if (read.Count == 0)
            {
                throw new DriverException($"`{repository}` holds no work between `{@base}` and `{tip}`: the tip adds no commit to the base.");
            }

            if (read.Count > OpinionPackets.MostCommits)
            {
                throw new DriverException($"`{repository}`'s work from `{@base}` to `{tip}` is {read.Count} commits; a second opinion reads at most {OpinionPackets.MostCommits}.");
            }

            var (named, paths, nameError) = await WorkingTree.GitAsync(
                root, ["-c", "core.quotepath=false", "diff", "--name-status", "-M", "-z", "--no-ext-diff", $"{from}...{to}"], ct).ConfigureAwait(false);
            if (named != 0) throw new DriverException($"git could not name what `{repository}`'s work changes: {nameError.Trim()}");

            return new OpinionCandidateRead(repository, from, to, read, OpinionPackets.Paths(paths));
        }

        /// <summary>
        /// The whole diff of the candidate, from where its base and tip met to its tip, written to <paramref name="file"/> by git
        /// itself, never through a console. False where git could not, and the reviewer reads it from git in its copy.
        /// </summary>
        public static async Task<bool> WriteDiffAsync(string root, OpinionCandidateRead candidate, string file, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var (code, _, _) = await WorkingTree.GitAsync(
                root, ["diff", "--no-color", "--no-ext-diff", "--no-textconv", "-M", $"--output={file}", $"{candidate.Base}...{candidate.Tip}"], ct)
                .ConfigureAwait(false);
            return code == 0 && File.Exists(file);
        }

        /// <summary>
        /// The repository's own rules as they stand in <paramref name="tree"/> (design §4): its root instructions, then the indexes
        /// it keeps (<see cref="RepositoryIndexes"/>), each once; then where its manifest says its decisions and its safe work are,
        /// only where something is there, and only a path inside the tree.
        /// </summary>
        public static OpinionRulesRead RulesIn(string tree)
        {
            var doctrine = new List<string>();
            foreach (var path in Instructions.Where(name => File.Exists(Path.Combine(tree, name))).Concat(RepositoryIndexes.Find(tree)))
            {
                if (!doctrine.Contains(path, StringComparer.OrdinalIgnoreCase)) doctrine.Add(path);
            }

            var declared = Declared(tree);
            var decisions = Inside(tree, declared.GetValueOrDefault("decisions"));
            var gates = Inside(tree, declared.GetValueOrDefault("gates")) ?? Inside(tree, GatesFile);
            return new OpinionRulesRead(doctrine, decisions, gates);
        }
    }

    /// <summary>A commit's full id as git names it, or the refusal naming what it could not.</summary>
    private static async Task<string> CommitAsync(string root, string repository, string name, CancellationToken ct)
    {
        // A name git would read as an option names no commit.
        var (code, id, _) = name.StartsWith('-')
            ? (1, "", "")
            : await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], ct).ConfigureAwait(false);
        var full = id.Trim();
        if (code != 0 || !EvidenceCodes.IsObjectId(full))
        {
            throw new DriverException($"`{repository}` holds no commit `{name}`, so a second opinion has nothing to read there.");
        }

        return full.ToLowerInvariant();
    }

    /// <summary>The manifest's <c>documents</c>, each role's path: a string, or an object's <c>path</c>. None where it cannot be read.</summary>
    private static IReadOnlyDictionary<string, string> Declared(string tree)
    {
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        var manifest = Path.Combine(tree, Manifest);
        if (!File.Exists(manifest)) return declared;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Object)
            {
                return declared;
            }

            foreach (var role in documents.EnumerateObject())
            {
                var path = role.Value.ValueKind switch
                {
                    JsonValueKind.String => role.Value.GetString(),
                    JsonValueKind.Object when role.Value.TryGetProperty("path", out var named) && named.ValueKind == JsonValueKind.String => named.GetString(),
                    _ => null,
                };
                if (path is { Length: > 0 }) declared[role.Name] = path;
            }
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // A manifest that cannot be read declares nothing; the files found by their own names are still named.
        }

        return declared;
    }

    /// <summary>
    /// <paramref name="path"/> as the tree holds it, forward slashes and no trailing one, where it is a relative path inside the
    /// tree and something is there; null otherwise.
    /// </summary>
    private static string? Inside(string tree, string? path)
    {
        if (path is not { Length: > 0 } given || Path.IsPathRooted(given) || given.Contains(':')) return null;
        var relative = given.Replace('\\', '/').Trim().TrimEnd('/');
        if (relative.Length == 0 || relative.Split('/').Any(segment => segment is ".." or "")) return null;

        var full = Path.GetFullPath(Path.Combine(tree, relative));
        var within = Path.GetFullPath(tree).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(within, StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(full) || Directory.Exists(full) ? relative : null;
    }
}
