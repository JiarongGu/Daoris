using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>One file a capture carries: its repository-relative path (forward slashes), its bytes' SHA-256 and their length.</summary>
public sealed record CapturedFile(string Path, string Hash, long Size);

/// <summary>A path the capture saw and did not carry, and why (<see cref="HelperCaptures.LeftLink"/>), which the manifest says.</summary>
public sealed record CaptureLeftOut(string Path, string Why);

/// <summary>
/// The asker's work in progress as Daoris read it for a helper (COAGENT1a, D199 point 5; the co-agent design §2.5): the manifest,
/// <c>&lt;askId&gt;.capture.json</c>, the driver's alone. The bytes travel beside it in memory, to the helper's copy, and nowhere else.
/// </summary>
/// <param name="Id">The capture's name: the first twelve hex of a SHA-256 over the commit, each file and its hash, and each deletion.</param>
/// <param name="Commit">The commit the asker's tree stood at, which the helper's copy is checked out at.</param>
/// <param name="At">When the capture was read.</param>
/// <param name="Files">Every changed tracked file and every untracked one not ignored, by path.</param>
/// <param name="Deleted">Every tracked path the tree no longer holds.</param>
/// <param name="LeftOut">What was seen and not carried, with why.</param>
public sealed record HelperCaptured(
    string Id, string Commit, DateTimeOffset At, IReadOnlyList<CapturedFile> Files, IReadOnlyList<string> Deleted, IReadOnlyList<CaptureLeftOut> LeftOut)
{
    /// <summary>Each carried file's bytes, by path; never written to the manifest, and empty in one read back from it.</summary>
    public IReadOnlyDictionary<string, byte[]> Bytes { get; init; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

    /// <summary>Whether the asker had nothing uncommitted.</summary>
    public bool Clean => Files.Count == 0 && Deleted.Count == 0;
}

/// <summary>
/// What a capture came to: the captured work, or the code it was refused with (<see cref="HelperWhy"/>'s <c>capture-*</c>, or
/// <see cref="HelperWhy.Unreadable"/> where git could not read the tree) and the path or the fact that refused it.
/// </summary>
public sealed record HelperCaptureRead(HelperCaptured? Captured, string? Code = null, string? Detail = null)
{
    public static HelperCaptureRead Refused(string code, string? detail = null) => new(null, code, detail);
}

/// <summary>One entry of <c>git status --porcelain=v2 -z</c>, as the capture reads it.</summary>
/// <param name="Kind"><c>1</c> changed, <c>2</c> renamed or copied, <c>u</c> unmerged, <c>?</c> untracked.</param>
/// <param name="Submodule">The entry is a submodule (its <c>sub</c> field starts <c>S</c>), or an untracked folder git did not
/// enter, which is a repository of its own.</param>
/// <param name="From">A rename's or a copy's source path.</param>
public sealed record StatusEntry(char Kind, string Path, bool Submodule, string? From = null);

/// <summary>
/// The capture's rules and its pure reads (COAGENT1a; the co-agent design §2.5): git's listing parsed, the bounds, the capture's
/// name, and the words a helper and its asker read of it. The reading of the tree is <c>HelperCapture</c>'s, which runs git.
/// </summary>
/// <remarks>
/// <para><b>Whole, or refused.</b> An unresolved merge, a link that leads out of the tree, a submodule or a nested repository, a
/// file that cannot be read and a capture past its bounds are each refused with a code of their own, never carried in part: a
/// helper that read half the work would answer about work that does not exist. Ignored files are never carried, since git's
/// listing leaves them out.</para>
///
/// <para><b>The bounds are first values</b> (the design's §11: the capture's cost on this machine is not measured): a helper reads,
/// so a work in progress past them is one no helper should be handed whole.</para>
/// </remarks>
public static class HelperCaptures
{
    /// <summary>The most bytes one carried file may hold.</summary>
    public const long FileBound = 10L * 1024 * 1024;

    /// <summary>The most bytes a capture may carry in all.</summary>
    public const long TotalBound = 50L * 1024 * 1024;

    /// <summary>The most paths a capture may carry or delete.</summary>
    public const int PathBound = 5000;

    /// <summary>Why a path was left out: a link that stays inside the tree, which a copy is not handed as a link.</summary>
    public const string LeftLink = "link";

    /// <summary>
    /// <c>git status --porcelain=v2 -z --untracked-files=all</c> read as entries; a header line or an ignored one says nothing here.
    /// Null where the listing does not read as that form.
    /// </summary>
    public static IReadOnlyList<StatusEntry>? ParseStatus(string listing)
    {
        var entries = new List<StatusEntry>();
        var fields = listing.Split('\0');
        for (var at = 0; at < fields.Length; at++)
        {
            var line = fields[at];
            if (line.Length == 0) continue;
            switch (line[0])
            {
                case '#' or '!':
                    continue;
                case '?':
                    if (line.Length < 3) return null;
                    var untracked = line[2..];
                    entries.Add(untracked.EndsWith('/')
                        ? new StatusEntry('?', untracked.TrimEnd('/'), Submodule: true)
                        : new StatusEntry('?', untracked, Submodule: false));
                    continue;
                case '1' or '2' or 'u':
                    // 1 XY sub mH mI mW hH hI path; 2 adds a score before the path, and its source is the next field; u has three
                    // modes and three hashes.
                    var count = line[0] switch { '1' => 9, '2' => 10, _ => 11 };
                    var parts = line.Split(' ', count);
                    if (parts.Length < count) return null;
                    var submodule = parts[2].StartsWith('S');
                    string? from = null;
                    if (line[0] == '2')
                    {
                        if (++at >= fields.Length) return null;
                        from = fields[at];
                    }

                    entries.Add(new StatusEntry(line[0], parts[count - 1], submodule, from));
                    continue;
                default:
                    return null;
            }
        }

        return entries;
    }

    /// <summary>The capture's name: the same commit, files and deletions make the same name, whenever and wherever it is read.</summary>
    public static string IdOf(string commit, IEnumerable<CapturedFile> files, IEnumerable<string> deleted)
    {
        var text = new StringBuilder(commit).Append('\n');
        foreach (var file in files.OrderBy(file => file.Path, StringComparer.Ordinal)) text.Append("+ ").Append(file.Path).Append(' ').Append(file.Hash).Append('\n');
        foreach (var path in deleted.Order(StringComparer.Ordinal)) text.Append("- ").Append(path).Append('\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..12];
    }

    /// <summary>The SHA-256 of <paramref name="bytes"/>, lower-case hex.</summary>
    public static string HashOf(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>What the capture holds, in a clause: <c>3 changed or new files and 1 deletion</c>, or that nothing was uncommitted.</summary>
    public static string Counted(HelperCaptured capture) =>
        capture.Clean
            ? "nothing uncommitted"
            : string.Join(" and ", new[]
            {
                capture.Files.Count > 0 ? $"{capture.Files.Count} changed or new {(capture.Files.Count == 1 ? "file" : "files")}" : null,
                capture.Deleted.Count > 0 ? $"{capture.Deleted.Count} {(capture.Deleted.Count == 1 ? "deletion" : "deletions")}" : null,
            }.Where(part => part is not null));

    /// <summary>
    /// The line an answer's delivery carries (the design §2.5 point 5): which capture the helper read, of which commit, when, and
    /// that the work may have moved since.
    /// </summary>
    public static string Named(HelperCaptured capture) =>
        $"It read your work as Daoris captured it at {capture.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC (capture `{capture.Id}`: commit "
        + $"`{TextBounds.ShortCommit(capture.Commit, 7)}` with {Counted(capture)}). Your work may have moved since.";
}
