using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The closure graphs this machine bound a quest to, kept by digest beside the node records (FABRIC-CLOSE1l; the fabric events design §E5's
/// cutover: "removing `quest.close` from a graph changes nothing for runs already bound"). A quest bound to a graph since edited or removed is
/// closed by the graph it was bound to, read from here; a home that never bound one keeps nothing, and nothing here is read.
/// </summary>
public static class ClosureGraphs
{
    /// <summary>The folder the kept graphs sit in, beside the node records (<see cref="NodeRecordFiles.FolderOf"/>).</summary>
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "closures");

    /// <summary>Whether this home keeps any closure graph: one stat, so a home that never bound a quest reads no record.</summary>
    public static bool Any(string home) => Directory.Exists(FolderOf(home));

    /// <summary>Keeps the graph <paramref name="json"/> spells under its digest, once; a graph whose digest is not that is never kept.</summary>
    public static void Keep(string home, string digest, string json)
    {
        if (!IsDigest(digest) || File.Exists(PathOf(home, digest))) return;
        if (NodeGraphs.Read(json).Graph is not { } graph || NodeGraphs.Digest(graph) != digest || !QuestClosing.Holds(graph)) return;
        Directory.CreateDirectory(FolderOf(home));
        AtomicFile.WriteText(PathOf(home, digest), json.ReplaceLineEndings("\n"));
    }

    /// <summary>The graph kept under <paramref name="digest"/>, or null where none is, or it does not read as that digest.</summary>
    public static NodeGraph? Read(string home, string digest)
    {
        if (!IsDigest(digest) || !File.Exists(PathOf(home, digest))) return null;
        try
        {
            return NodeGraphs.Read(AtomicFile.ReadText(PathOf(home, digest))).Graph is { } graph && NodeGraphs.Digest(graph) == digest ? graph : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Drops each kept graph <paramref name="needed"/> says nothing needs, and the folder once it is empty (FABRIC-CLOSE1m), so a home that
    /// no longer binds a quest is not kept armed by it. A graph written within <paramref name="grace"/> of <paramref name="now"/> stays: it is
    /// kept before the service binds its quest (<see cref="ClosureBinding.AtTakeAsync"/>), and no read shows that quest bound yet.
    /// </summary>
    public static void Prune(string home, Func<string, bool> needed, DateTimeOffset now, TimeSpan grace)
    {
        if (!Directory.Exists(FolderOf(home))) return;
        try
        {
            foreach (var path in Directory.GetFiles(FolderOf(home), "*.json"))
            {
                var digest = Path.GetFileNameWithoutExtension(path);
                if (!IsDigest(digest) || needed(digest) || now - new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) < grace) continue;
                File.Delete(path);
            }

            if (!Directory.EnumerateFileSystemEntries(FolderOf(home)).Any()) Directory.Delete(FolderOf(home));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next pass: a graph kept is a graph that closes, never a lost one.
        }
    }

    private static string PathOf(string home, string digest) => Path.Combine(FolderOf(home), digest + ".json");

    /// <summary>A digest as <see cref="NodeGraphs.Digest"/> writes it, so no name the service answers is read as a path.</summary>
    private static bool IsDigest(string digest) =>
        digest.StartsWith("gph-", StringComparison.Ordinal) && digest.Length > 4 && digest[4..].All(char.IsAsciiHexDigitLower);
}
