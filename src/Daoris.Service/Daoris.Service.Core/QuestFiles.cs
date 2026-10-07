using System.Security.Cryptography;

namespace Daoris.Knowledge;

/// <summary>A file arriving at a door with its bytes — on the machine that has it, and nowhere else.</summary>
/// <param name="Name">The name it was given, which is not yet safe to keep — <see cref="QuestFiles.SafeName"/> is.</param>
/// <param name="Content">Its bytes.</param>
public sealed record QuestUpload(string Name, byte[] Content);

/// <summary>
/// The bytes a quest carries (D65 §2), kept under the home of the machine that has them (D63): one
/// directory per quest, one file per content, named so a person and a session can both read it.
/// </summary>
/// <remarks>
/// <para><b>Machine-local, the transcript's boundary</b> (D47 §4). A remote keeps the record's names
/// and hashes and never the bytes; a machine the quest synced to learns a file exists and that it is
/// not here — which <see cref="Has"/> answers, and which every reader says rather than hides.</para>
///
/// <para><b>The layout is this class's alone</b>: <c>quests/&lt;id&gt;/attachments/&lt;first 12 of the
/// hash&gt;-&lt;name&gt;</c> under the home. Nobody else derives it — the driver's home is not always this
/// one (it is wherever <c>driver.json</c> lives), so a caller on this machine is TOLD where each kept
/// file lies, the way a transcript's path is (D47 §4), and hands a session that directory as
/// <c>DAORIS_QUEST_ATTACHMENTS</c>. The hash in the name is what keeps two pasted screenshots — both
/// called <c>image.png</c> — two files, and the same file dropped twice one; the name after it is what
/// lets a session know which file is the screenshot.</para>
/// </remarks>
public sealed class QuestFiles(string home, string folder = QuestFiles.Folder)
{
    /// <summary>The folder under the home that holds every quest's own.</summary>
    public const string Folder = "quests";

    /// <summary>How a record's folder is removed whole: from the disk, unless a test hands one that refuses (HIST1j).</summary>
    private readonly Action<string> _remove = path => Directory.Delete(path, recursive: true);

    /// <summary>
    /// A keeper whose removals go through <paramref name="remove"/>: a test's disk that will not let go of a file, as a process
    /// holding one makes the real disk refuse, so a refused removal is held on every platform (HIST1j).
    /// </summary>
    internal QuestFiles(string home, string folder, Action<string> remove) : this(home, folder) => _remove = remove;

    /// <summary>
    /// The same keeper for another kind of record under the same home — an ask keeps its files beside
    /// the quests it may become (D65 §1a), with the same layout and the same naming rules.
    /// </summary>
    public QuestFiles For(string otherFolder) => new(home, otherFolder, _remove);

    /// <summary>A kept name's longest — a path under a deep home still has to open on Windows.</summary>
    public const int MaxNameLength = 100;

    /// <summary>How much of the hash the file name carries: 48 bits, among one quest's few files.</summary>
    private const int HashInName = 12;

    /// <summary>The keeper for the home the environment names, or null where there is none (D63).</summary>
    public static QuestFiles? FromEnvironment() =>
        DaorisHome.Resolve() is { } home ? new QuestFiles(home) : null;

    /// <summary>The directory a session is handed for one quest.</summary>
    public string DirectoryOf(string questId) => Path.Combine(home, folder, questId, "attachments");

    /// <summary>Where one attachment of one quest lies, whether or not it is there.</summary>
    public string PathOf(string questId, QuestAttachment attachment) =>
        Path.Combine(DirectoryOf(questId), FileName(attachment));

    /// <summary>The kept file's own name — the hash's head, then the name the record carries.</summary>
    public static string FileName(QuestAttachment attachment) =>
        $"{attachment.Sha256[..Math.Min(HashInName, attachment.Sha256.Length)]}-{attachment.Name}";

    /// <summary>
    /// Remove everything kept for one record — its quest or ask was deleted (D95) or cleared (D153). Best
    /// effort: a file the disk will not let go of stays where it was, and the record is gone either way.
    /// Says whether nothing of it is left, so a clear can report what stayed (HIST1j); D95's delete reads
    /// nothing from it.
    /// </summary>
    /// <param name="id">The record's own id, as its store holds it — never a caller's words.</param>
    /// <returns>True where its folder is gone or was never here; false where the disk kept any of it.</returns>
    public bool Forget(string id)
    {
        var directory = Path.Combine(home, folder, id);
        try
        {
            if (Directory.Exists(directory)) _remove(directory);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Held open by another process, or refused by the disk: nothing names these bytes any more, and a
            // folder deleted whole may have let go of the rest before it refused.
            return !Directory.Exists(directory);
        }
    }

    /// <summary>Whether this machine has the bytes — false for a file of a quest published elsewhere, honestly.</summary>
    public bool Has(string questId, QuestAttachment attachment) => File.Exists(PathOf(questId, attachment));

    /// <summary>What the record will carry for an upload: a safe name, the content's hash, its size.</summary>
    public static QuestAttachment Describe(QuestUpload upload) => new(
        SafeName(upload.Name),
        Convert.ToHexString(SHA256.HashData(upload.Content)).ToLowerInvariant(),
        upload.Content.LongLength);

    /// <summary>
    /// Keep an upload's bytes where the layout says. Idempotent: the same content under the same
    /// quest is already kept, so a retried publish writes nothing new. Written beside and renamed, so
    /// a half-written file is never what a session finds.
    /// </summary>
    public async Task<QuestAttachment> KeepAsync(string questId, QuestUpload upload, CancellationToken ct = default)
    {
        var attachment = Describe(upload);
        var path = PathOf(questId, attachment);
        if (File.Exists(path)) return attachment;

        Directory.CreateDirectory(DirectoryOf(questId));
        var beside = path + ".tmp";
        await File.WriteAllBytesAsync(beside, upload.Content, ct).ConfigureAwait(false);
        File.Move(beside, path, overwrite: true);
        return attachment;
    }

    /// <summary>
    /// A file named by its path on this machine — the MCP door's shape, where an agent attaches what
    /// its session can see. Relative paths are the session's repository's, which is where its MCP host
    /// was started. Too large is refused from the size alone: a quest never loads what it cannot carry.
    /// </summary>
    public static async Task<(QuestUpload? Upload, string? Refusal)> ReadAsync(
        string path, string relativeTo, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path, relativeTo);
        if (!File.Exists(full))
        {
            return (null, $"`{path}` is not a file on this machine — an attachment is the path of a file the session can read.");
        }

        var size = new FileInfo(full).Length;
        if (size > QuestExchange.MaxAttachmentBytes)
        {
            return (null,
                $"`{path}` is {size / (1024.0 * 1024.0):0.#} MB, and a quest's files come to at most "
                + $"{QuestExchange.MaxAttachmentBytes / (1024 * 1024)} MB together. Carry a link to it instead.");
        }

        return (new QuestUpload(Path.GetFileName(full), await File.ReadAllBytesAsync(full, ct).ConfigureAwait(false)), null);
    }

    /// <summary>
    /// A name made safe to keep. It is the person's name for the file, but a PATH is not theirs to give:
    /// any directory in it is dropped, and a character no file system takes becomes an underscore — so
    /// a kept file can never land outside its quest's directory, whatever a door was sent.
    /// </summary>
    public static string SafeName(string name)
    {
        // Both separators, whichever machine this runs on: a Windows path sent to a Linux remote is
        // still somebody's directory, and still not part of the name.
        var leaf = name.Replace('\\', '/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..];

        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '|', '?', '*']).ToHashSet();
        var safe = new string(leaf.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray())
            // Windows drops a trailing dot or space when it opens a file, so a name ending in one
            // would be kept under one name and found under another.
            .Trim().TrimEnd('.', ' ');

        if (safe.Length == 0 || safe.All(c => c == '.')) return "attachment";
        if (safe.Length <= MaxNameLength) return safe;

        var extension = Path.GetExtension(safe);
        return extension.Length is > 0 and < 16
            ? safe[..(MaxNameLength - extension.Length)] + extension
            : safe[..MaxNameLength];
    }
}
