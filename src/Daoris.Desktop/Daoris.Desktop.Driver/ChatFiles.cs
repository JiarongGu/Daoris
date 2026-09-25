using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>A file a person attached to a message, with its bytes, as it arrived at a door.</summary>
/// <param name="Name">The name it was given, which is not yet safe to keep — <see cref="ChatFiles.SafeName"/> is.</param>
public sealed record ChatUpload(string Name, byte[] Content);

/// <summary>An attached file as kept for its session: the person's name for it, and where it lies.</summary>
public sealed record KeptFile(string Name, string Path);

/// <summary>One message of a conversation: the person's words and the files they attached (CONV4c).</summary>
public sealed record ChatMessage(string Text, IReadOnlyList<KeptFile> Files)
{
    /// <summary>Words alone — most messages.</summary>
    public static ChatMessage Of(string text) => new(text, []);
}

/// <summary>
/// What a person attaches to a conversation (CONV4c), kept under the home for the session it was sent
/// to: <c>sessions/&lt;id&gt;/files/&lt;first 12 of the hash&gt;-&lt;name&gt;</c>.
/// </summary>
/// <remarks>
/// <para><b>Transcript-class and machine-local</b> (D47 §4), beside the session's transcript and record.
/// Never written into the tree: the tree is the repository's, and an attachment is the person's.</para>
///
/// <para><b>Read by the agent where it lies</b>, under a read granted at spawn for exactly this folder
/// (INT4j's rule) — measured on both doors (docs/2026-09-25-message-content-evidence.md).</para>
///
/// <para><b>The layout and the name rules are the service's for a quest's files</b> (<c>QuestFiles</c>),
/// twinned here because the artefacts share no code: the hash keeps two screenshots both called
/// <c>image.png</c> two files and the same file dropped twice one, and a name loses any directory it
/// arrived with. The limits are a quest's too, so the composer offers one set.</para>
/// </remarks>
public static class ChatFiles
{
    /// <summary>How many files one message carries — a quest's limit.</summary>
    public const int MaxFiles = 10;

    /// <summary>How many bytes one message's files come to together — a quest's limit, 20 MB.</summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>A kept name's longest — a path under a deep home still has to open on Windows.</summary>
    public const int MaxNameLength = 100;

    private const int HashInName = 12;

    /// <summary>The folder a session's attached files are kept in, and the one its agent may read.</summary>
    public static string Folder(string home, string sessionId) =>
        SessionEvents.IsId(sessionId)
            ? Path.Combine(home, "sessions", sessionId, "files")
            : throw new DriverException($"`{sessionId}` is not a session id, so it names no folder.");

    /// <summary>
    /// Keep a message's files, or refuse all of them in a sentence and keep none. Idempotent per
    /// content; written beside and renamed, so a half-written file is never what an agent reads.
    /// </summary>
    public static IReadOnlyList<KeptFile> Keep(string home, string sessionId, IReadOnlyList<ChatUpload> uploads)
    {
        if (uploads.Count == 0) return [];
        var folder = Folder(home, sessionId);

        var distinct = uploads
            .Select(upload => (Upload: upload, Hash: Convert.ToHexString(SHA256.HashData(upload.Content)).ToLowerInvariant()))
            // The same content twice is one file — its hash is its identity, and the first name given wins.
            .GroupBy(entry => entry.Hash, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (distinct.Count > MaxFiles)
        {
            throw new DriverException($"A message carries at most {MaxFiles} files — this one was given {distinct.Count}.");
        }

        var total = distinct.Sum(entry => entry.Upload.Content.LongLength);
        if (total > MaxBytes)
        {
            throw new DriverException(
                $"A message's files come to at most {MaxBytes / (1024 * 1024)} MB together — these come to "
                + $"{total / (1024.0 * 1024.0):0.#} MB.");
        }

        Directory.CreateDirectory(folder);
        var kept = new List<KeptFile>();
        foreach (var (upload, hash) in distinct)
        {
            var name = SafeName(upload.Name);
            var path = Path.Combine(folder, $"{hash[..HashInName]}-{name}");
            if (!File.Exists(path))
            {
                var beside = path + ".tmp";
                File.WriteAllBytes(beside, upload.Content);
                File.Move(beside, path, overwrite: true);
            }

            kept.Add(new KeptFile(name, path));
        }

        return kept;
    }

    /// <summary>
    /// A name made safe to keep: any directory in it dropped, a character no file system takes made an
    /// underscore, a trailing dot or space gone (Windows drops them when it opens a file), and a name
    /// that is nothing becoming <c>attachment</c> — <c>QuestFiles.SafeName</c>'s rules.
    /// </summary>
    public static string SafeName(string name)
    {
        var leaf = name.Replace('\\', '/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..];

        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '|', '?', '*']).ToHashSet();
        var safe = new string(leaf.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray())
            .Trim().TrimEnd('.', ' ');

        if (safe.Length == 0 || safe.All(c => c == '.')) return "attachment";
        if (safe.Length <= MaxNameLength) return safe;

        var extension = Path.GetExtension(safe);
        return extension.Length is > 0 and < 16
            ? safe[..(MaxNameLength - extension.Length)] + extension
            : safe[..MaxNameLength];
    }

    /// <summary>
    /// The lines that name a message's files to an agent that reads them by path — the native door's
    /// reference, and a text door's (measured: Claude Code reads a text file or an image by its path).
    /// Empty for a message with none. Written for the agent, never recorded as the person's words.
    /// </summary>
    public static string PathLines(IReadOnlyList<KeptFile> files) =>
        files.Count == 0
            ? ""
            : "\n\nAttached to this message, kept outside your working tree — read each from its path:\n"
              + string.Join("\n", files.Select(file => $"- {file.Path}"));
}
