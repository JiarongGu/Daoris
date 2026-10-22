using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A piece's frozen ref (COAGENT1b, D199 point 3; the co-agent design §2.4): <c>refs/daoris/pieces/&lt;id&gt;</c>, created once at the
/// keeper's commit and never moved. Piece refs are classed beside the recovery refs (<see cref="ContentHolds.DiscardedPrefix"/>): no
/// tidy, clear, sync or landing mutates one, and no proof of holding counts one as a holder, since a piece's commit contains the
/// lead's history up to its base and would otherwise let a lead's branch be forgotten as held.
/// </summary>
public static class PieceRefs
{
    public const string Prefix = "refs/daoris/pieces/";

    public static string Of(string id) => Prefix + id;

    public static bool IsPiece(string reference) => reference.StartsWith(Prefix, StringComparison.Ordinal);
}

/// <summary>A path the import refused or left out, and why (<see cref="PieceImport"/>'s codes).</summary>
public sealed record PieceLeftOut(string Path, string Why);

/// <summary>One entry of a partner's copy as the keeper's walk finds it, its path repository-relative with <c>/</c>.</summary>
/// <param name="Full">Where its bytes are, for a <see cref="PieceEntryKind.File"/>.</param>
public sealed record PieceEntry(string Path, PieceEntryKind Kind, long Size, string? Full);

public enum PieceEntryKind
{
    /// <summary>A regular file, the only kind imported.</summary>
    File,

    /// <summary>A symbolic link, a junction or another reparse point: never followed, never imported.</summary>
    Link,

    /// <summary>A folder holding a <c>.git</c> of its own: a nested repository, never entered.</summary>
    Repository,
}

/// <summary>
/// The import's rules (the design §7.9, Codex's read 24), pure over what the walk found and what the base holds: regular files only;
/// a link, a junction or another reparse point, a submodule or a nested repository refused, and what the base held under it left as
/// it was; a new file the repository's own ignore rules ignore left out; a file over <see cref="FileBound"/> refused, and a piece over
/// <see cref="PieceBound"/> refused whole. Paths outside the piece's are kept and named beyond it, never refused.
/// </summary>
public static class PieceImport
{
    /// <summary>One file at most, in bytes: a first value, read again in COAGENT1f.</summary>
    public const long FileBound = 10L * 1024 * 1024;

    /// <summary>The piece at most, in bytes: a first value, read again in COAGENT1f.</summary>
    public const long PieceBound = 50L * 1024 * 1024;

    public const string WhyLink = "link";
    public const string WhySubmodule = "submodule";
    public const string WhyRepository = "repository";
    public const string WhyIgnored = "ignored";
    public const string WhyTooBig = "too-big";
    public const string WhyPieceTooBig = "piece-too-big";

    /// <summary>What the import keeps and leaves.</summary>
    /// <param name="Changed">Each file kept that is new or differs from the base, with its mode.</param>
    /// <param name="Deleted">Each base file the copy no longer holds.</param>
    /// <param name="Refused">True where the whole piece was refused (over its bound), so nothing is imported.</param>
    public sealed record Judged(
        IReadOnlyList<(PieceEntry Entry, string Mode)> Changed, IReadOnlyList<string> Deleted, IReadOnlyList<PieceLeftOut> LeftOut,
        bool Refused)
    {
        public bool Empty => Changed.Count == 0 && Deleted.Count == 0;
    }

    /// <summary>A base file, its mode and object id as <c>ls-tree</c> said them.</summary>
    public sealed record BaseFile(string Mode, string Object);

    /// <param name="found">The copy as walked (<see cref="PieceWalk"/>).</param>
    /// <param name="base">The base's files by path; a gitlink's mode is <c>160000</c>.</param>
    /// <param name="objectOf">The object id the repository's git would give a found file's bytes, with no filters.</param>
    /// <param name="ignored">Whether the repository's own ignore rules ignore a new path.</param>
    public static Judged Judge(
        IReadOnlyList<PieceEntry> found, IReadOnlyDictionary<string, BaseFile> @base, Func<PieceEntry, string> objectOf,
        Func<string, bool> ignored)
    {
        var left = new List<PieceLeftOut>();
        var changed = new List<(PieceEntry, string)>();
        var untouchable = new List<string>();
        foreach (var gitlink in @base.Where(pair => pair.Value.Mode == "160000")) untouchable.Add(gitlink.Key);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var entry in found.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            seen.Add(entry.Path);
            if (Under(entry.Path, untouchable) is not null)
            {
                left.Add(new(entry.Path, WhySubmodule));
                continue;
            }

            switch (entry.Kind)
            {
                case PieceEntryKind.Link:
                    left.Add(new(entry.Path, WhyLink));
                    untouchable.Add(entry.Path);
                    continue;
                case PieceEntryKind.Repository:
                    left.Add(new(entry.Path, WhyRepository));
                    untouchable.Add(entry.Path);
                    continue;
            }

            @base.TryGetValue(entry.Path, out var was);
            if (was is null && ignored(entry.Path))
            {
                left.Add(new(entry.Path, WhyIgnored));
                continue;
            }

            if (entry.Size > FileBound)
            {
                left.Add(new(entry.Path, WhyTooBig));
                if (was is not null) untouchable.Add(entry.Path);
                continue;
            }

            var id = objectOf(entry);
            if (was is not null && string.Equals(was.Object, id, StringComparison.OrdinalIgnoreCase)) continue;
            total += entry.Size;
            // A changed file keeps the base's mode (an executable stays one); a new file is a regular one, since this machine's
            // file system cannot be trusted to say which is executable.
            changed.Add((entry, was?.Mode is "100755" ? "100755" : "100644"));
        }

        var deleted = @base
            .Where(pair => pair.Value.Mode != "160000" && !seen.Contains(pair.Key) && Under(pair.Key, untouchable) is null)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (total > PieceBound)
        {
            return new([], [], [.. left, new PieceLeftOut("", WhyPieceTooBig)], Refused: true);
        }

        return new(changed, deleted, left, Refused: false);
    }

    /// <summary>The changed paths outside the piece's own (<i>beyond its piece</i>), never refused; none where the ask named no paths.</summary>
    public static IReadOnlyList<string> Beyond(IEnumerable<string> touched, IReadOnlyList<string> paths) =>
        paths.Count == 0 ? [] : [.. touched.Where(path => Under(path, paths) is null).Order(StringComparer.Ordinal)];

    private static string? Under(string path, IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            var trimmed = root.Trim('/');
            if (trimmed.Length == 0) return root;
            if (string.Equals(path, trimmed, StringComparison.Ordinal) || path.StartsWith(trimmed + "/", StringComparison.Ordinal)) return root;
        }

        return null;
    }

    /// <summary>The object id git gives these bytes as a blob, SHA-1 (what <c>hash-object --no-filters</c> answers in a SHA-1 repository).</summary>
    public static string BlobId(ReadOnlySpan<byte> bytes)
    {
        var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha.AppendData(header);
        sha.AppendData(bytes);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}

/// <summary>
/// The keeper's walk of a partner's copy (the design §7.9): its folder as bytes, never entering <c>.git</c>, never following a link,
/// and never entering a folder that holds a repository of its own.
/// </summary>
public static class PieceWalk
{
    public static IReadOnlyList<PieceEntry> Of(string copy)
    {
        var found = new List<PieceEntry>();
        Walk(new DirectoryInfo(copy), "", found, top: true);
        return found;
    }

    private static void Walk(DirectoryInfo folder, string under, List<PieceEntry> found, bool top)
    {
        foreach (var info in folder.EnumerateFileSystemInfos())
        {
            var path = under.Length == 0 ? info.Name : $"{under}/{info.Name}";
            if (top && string.Equals(info.Name, ".git", StringComparison.OrdinalIgnoreCase)) continue;
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null)
            {
                found.Add(new(path, PieceEntryKind.Link, 0, null));
                continue;
            }

            if (info is DirectoryInfo inner)
            {
                if (Directory.Exists(Path.Combine(inner.FullName, ".git")) || File.Exists(Path.Combine(inner.FullName, ".git")))
                {
                    found.Add(new(path, PieceEntryKind.Repository, 0, null));
                    continue;
                }

                Walk(inner, path, found, top: false);
                continue;
            }

            found.Add(new(path, PieceEntryKind.File, ((FileInfo)info).Length, info.FullName));
        }
    }
}

/// <summary>
/// The partner's copy (COAGENT1b, the co-agent design §2.2, §2.3 step 5): a clone of the repository with no remote, its own refs,
/// configuration, hooks and objects, checked out at the ask's exact base, refused where that commit is not in the repository (no
/// fallback to another base). Made by Daoris before the partner's turn, the only git Daoris runs in it.
/// </summary>
public static class PieceCopy
{
    /// <summary>
    /// What the copy's checkout is told, above every attribute the repository's own files set: no line-ending conversion, filter,
    /// identifier or encoding, so its files are the base's bytes exactly, and the keeper, which compares with no filters, reads only
    /// what the partner changed. Found on this machine, where a global <c>core.autocrlf</c> made every untouched text file read as changed.
    /// </summary>
    public const string Attributes = "* -text -filter -ident -working-tree-encoding\n";

    /// <returns>Null where the copy stands at the base; otherwise its code (<see cref="PartnerWhy.BaseMissing"/>) and git's reason.</returns>
    public static async Task<(string Code, string Why)?> MakeAsync(string root, string copy, string @base, CancellationToken ct)
    {
        var (has, _, missing) = await WorkingTree.GitAsync(root, ["cat-file", "-e", @base + "^{commit}"], ct).ConfigureAwait(false);
        if (has != 0) return (PartnerWhy.BaseMissing, missing.Trim());
        var parent = Path.GetDirectoryName(Path.GetFullPath(copy))!;
        Directory.CreateDirectory(parent);
        var (cloned, _, cloneError) = await WorkingTree.GitAsync(
            parent, ["clone", "--quiet", "--no-local", "--no-checkout", "-c", "core.autocrlf=false", root, copy], ct).ConfigureAwait(false);
        if (cloned != 0) return (PartnerWhy.CopyFailed, cloneError.Trim());
        var info = Path.Combine(copy, ".git", "info");
        Directory.CreateDirectory(info);
        await File.WriteAllTextAsync(Path.Combine(info, "attributes"), Attributes, ct).ConfigureAwait(false);
        var (checkedOut, _, checkoutError) = await WorkingTree.GitAsync(copy, ["checkout", "--quiet", "--detach", @base], ct).ConfigureAwait(false);
        if (checkedOut != 0) return (PartnerWhy.BaseMissing, checkoutError.Trim());
        var (removed, _, removeError) = await WorkingTree.GitAsync(copy, ["remote", "remove", "origin"], ct).ConfigureAwait(false);
        return removed == 0 ? null : (PartnerWhy.CopyFailed, removeError.Trim());
    }
}

/// <summary>What the keeper did: the commit and its frozen ref, or why it held or found nothing.</summary>
/// <param name="State">The state the box writes: <see cref="PartnerStates.Imported"/>, <see cref="PartnerStates.Held"/> or <see cref="PartnerStates.Uncertain"/>; null where the copy held no work.</param>
public sealed record PieceKept(string? State, string? Commit, string? Code, PieceImport.Judged? Judged, string? Why);

/// <summary>
/// The keeper and the import (COAGENT1b, the design §7.9, Codex's read 23): once the partner's job is empty its copy is data the
/// partner controlled, <c>.git</c> included, so Daoris never runs git in it again. It reads the files, judges them by
/// <see cref="PieceImport"/>, and writes one commit onto the ask's base in the repository itself through a temporary index, with no
/// filters, no hooks and no signing, by the repository's identity, dated by the keeper's moment, so the same inputs make the same
/// commit. Then it creates the piece's ref, create-only. The partner's own commits never reach the repository.
/// </summary>
public static class PieceKeeper
{
    /// <param name="root">The repository's registered root, its own trusted configuration: every git here runs there.</param>
    /// <param name="copy">The partner's copy, read as files only.</param>
    /// <param name="index">A temporary index file under the home, removed after.</param>
    /// <param name="kept">The keeper's moment, written before keeping.</param>
    public static async Task<PieceKept> KeepAsync(
        string root, string copy, PartnerAsk ask, string index, DateTimeOffset kept, string message, CancellationToken ct)
    {
        var (lsCode, listing, lsError) = await WorkingTree.GitAsync(root, ["ls-tree", "-r", "-z", "--full-tree", ask.Base], ct).ConfigureAwait(false);
        if (lsCode != 0) return new(PartnerStates.Uncertain, null, PartnerWhy.BaseMissing, null, lsError.Trim());
        var @base = BaseFiles(listing);

        var found = PieceWalk.Of(copy);
        var newFiles = found.Where(entry => entry.Kind == PieceEntryKind.File && !@base.ContainsKey(entry.Path)).Select(entry => entry.Path).ToList();
        var ignored = await IgnoredAsync(root, newFiles, ct).ConfigureAwait(false);
        var judged = PieceImport.Judge(found, @base, entry => PieceImport.BlobId(File.ReadAllBytes(entry.Full!)), ignored.Contains);
        if (judged.Refused || (judged.Empty && judged.LeftOut.Count > 0))
        {
            return new(PartnerStates.Held, null, PartnerWhy.ImportRefused, judged, null);
        }

        if (judged.Empty) return new(null, null, PartnerWhy.NoWork, judged, null);

        var environment = new Dictionary<string, string>
        {
            ["GIT_INDEX_FILE"] = index,
            ["GIT_AUTHOR_DATE"] = kept.ToUnixTimeSeconds() + " +0000",
            ["GIT_COMMITTER_DATE"] = kept.ToUnixTimeSeconds() + " +0000",
        };
        try
        {
            if (await GitAsync(root, ["read-tree", ask.Base], environment, ct).ConfigureAwait(false) is { Code: not 0 } read)
            {
                return new(PartnerStates.Uncertain, null, PartnerWhy.BaseMissing, judged, read.Stderr.Trim());
            }

            // Each kept file's bytes into the repository's objects with no filters; the copy's attributes and configuration play no part.
            var lines = new StringBuilder();
            foreach (var (entry, mode) in judged.Changed)
            {
                var (code, id, error) = await WorkingTree.GitAsync(root, ["hash-object", "-w", "--no-filters", "--", entry.Full!], ct).ConfigureAwait(false);
                if (code != 0) return new(PartnerStates.Uncertain, null, PartnerWhy.ImportRefused, judged, error.Trim());
                lines.Append(mode).Append(' ').Append(id.Trim()).Append('\t').Append(entry.Path).Append('\0');
            }

            foreach (var path in judged.Deleted) lines.Append("0 ").Append(new string('0', 40)).Append('\t').Append(path).Append('\0');
            var (infoCode, _, infoError) = await WorkingTree.GitAsync(
                root, ["update-index", "-z", "--index-info"], environment, lines.ToString(), ct).ConfigureAwait(false);
            if (infoCode != 0) return new(PartnerStates.Uncertain, null, PartnerWhy.ImportRefused, judged, infoError.Trim());

            var tree = await GitAsync(root, ["write-tree"], environment, ct).ConfigureAwait(false);
            if (tree.Code != 0) return new(PartnerStates.Uncertain, null, PartnerWhy.ImportRefused, judged, tree.Stderr.Trim());

            var commit = await GitAsync(
                root, ["commit-tree", "--no-gpg-sign", tree.Stdout.Trim(), "-p", ask.Base, "-m", message], environment, ct).ConfigureAwait(false);
            if (commit.Code != 0) return new(PartnerStates.Uncertain, null, PartnerWhy.ImportRefused, judged, commit.Stderr.Trim());
            var made = commit.Stdout.Trim();

            return await FreezeAsync(root, ask.Id, made, ct).ConfigureAwait(false) switch
            {
                PartnerStates.Imported => new(PartnerStates.Imported, made, null, judged, null),
                var other => new(other, made, PartnerWhy.RefMoved, judged, null),
            };
        }
        finally
        {
            try { File.Delete(index); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Create the piece's ref at <paramref name="commit"/>, create-only (<c>update-ref</c> with an empty old value). A ref already at
    /// that commit is the same import done before a restart; one at another commit is never moved, and reads uncertain.
    /// </summary>
    public static async Task<string> FreezeAsync(string root, string id, string commit, CancellationToken ct)
    {
        var name = PieceRefs.Of(id);
        var (_, stands, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], ct).ConfigureAwait(false);
        if (PartnerReconcile.AfterRef(stands.Trim() is { Length: > 0 } at ? at : null, commit) is { } already) return already;
        var (code, _, _) = await WorkingTree.GitAsync(root, ["update-ref", "-m", $"daoris: piece {id}", name, commit, ""], ct).ConfigureAwait(false);
        if (code == 0) return PartnerStates.Imported;
        var (_, now, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], ct).ConfigureAwait(false);
        return PartnerReconcile.AfterRef(now.Trim() is { Length: > 0 } after ? after : null, commit) ?? PartnerStates.Uncertain;
    }

    private static Dictionary<string, PieceImport.BaseFile> BaseFiles(string listing)
    {
        var files = new Dictionary<string, PieceImport.BaseFile>(StringComparer.Ordinal);
        foreach (var row in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = row.IndexOf('\t');
            if (tab < 0) continue;
            var parts = row[..tab].Split(' ');
            if (parts.Length < 3) continue;
            files[row[(tab + 1)..]] = new(parts[0], parts[2]);
        }

        return files;
    }

    /// <summary>
    /// The new paths the repository's own ignore rules ignore, asked of its registered root (its trusted configuration, never the
    /// copy's). Read as the root's checkout holds its ignore files, which is the base's where the root stands at it.
    /// </summary>
    private static async Task<HashSet<string>> IgnoredAsync(string root, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        if (paths.Count == 0) return ignored;
        var (_, said, _) = await WorkingTree.GitAsync(
            root, ["check-ignore", "--no-index", "-z", "--stdin"], string.Join('\0', paths) + "\0", ct).ConfigureAwait(false);
        foreach (var path in said.Split('\0', StringSplitOptions.RemoveEmptyEntries)) ignored.Add(path);
        return ignored;
    }

    private static async Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, CancellationToken ct) =>
        await WorkingTree.GitAsync(root, arguments, environment, timeout: null, ct).ConfigureAwait(false);
}
