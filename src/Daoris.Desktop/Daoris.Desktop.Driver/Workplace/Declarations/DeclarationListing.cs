using System.Collections.Immutable;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A commit's listing, proven by its bytes: one start of <c>cat-file --batch</c> asked the commit, its tree and each folder on a path's
/// way, admitted only where the commit's bytes hash to its id, the tree's to the id the commit's bytes name and each folder's to the id
/// its parent's entry names. The entries and an absence are read from those hashed trees, so what is kept is a fact of the object store,
/// true in every tree and context of every repository where git answers an object with its own bytes, and is kept by id alone.
/// </summary>
internal static class DeclarationListing
{
    // A tree's room in the start's answer: a folder of thousands of entries.
    private const int Room = 4 * 1024 * 1024;

    /// <summary>What a listing answered for a path's folder.</summary>
    /// <param name="Entries">The folder's entries; null where a folder on the way stands as no folder, so the path is absent.</param>
    /// <param name="Unread">Why it did not read; null where it read.</param>
    /// <param name="Unproven">Git answered bytes that do not hash to their ids, so nothing is admitted and git is asked by path.</param>
    internal sealed record Listed(ImmutableDictionary<string, DeclarationEntry>? Entries, DeclarationAnswer.Unread? Unread, bool Unproven);

    private sealed record CommitKey(string Id);

    private sealed record TreeKey(string Id);

    /// <summary>The entries of the folder holding <paramref name="path"/> at <paramref name="commit"/>, kept or listed now.</summary>
    internal static async Task<Listed> FolderAsync(string root, string commit, string path, CancellationToken ct)
    {
        var folders = path.Split('/')[..^1];
        if (Kept(commit, folders) is { } kept) return kept;

        List<GitObjectName> asked = [commit, commit + "^{tree}"];
        for (var depth = 1; depth <= folders.Length; depth++) asked.Add(GitObjectName.At(commit, string.Join('/', folders[..depth])));
        if (RepositoryDeclarations.BeforeRead.Value is { } before) await before().ConfigureAwait(false);
        var answers = await GitObjectReads.BatchAsync(root, asked, Room, ct).ConfigureAwait(false);
        if (answers is null) return Failed(DeclarationUnread.Failed, $"git could not list {commit} in {root}");
        if (answers[0].Answer.Missing) return Failed(DeclarationUnread.Missing, $"git holds no commit {commit} in {root}");
        if (answers[0].Answer.Type != "commit") return Failed(DeclarationUnread.Failed, $"{commit} is no commit");

        var (commitAnswer, commitBytes) = answers[0];
        var body = commitBytes.AsSpan(commitAnswer.Start, commitAnswer.Count);
        if (!GitObjectHash.Proves("commit", body, commit) || TreeOf(body) is not { } tree) return new Listed(null, null, Unproven: true);
        var admitted = new List<(object Key, object Value, long Weight)> { (new CommitKey(commit), tree, DeclarationBound.Entry + DeclarationBound.Text(tree)) };

        var id = tree;
        ImmutableDictionary<string, DeclarationEntry>? entries = null;
        var reached = false;
        for (var at = 1; at < answers.Count; at++)
        {
            var (answer, bytes) = answers[at];
            if (answer.Missing || answer.Type != "tree") return new Listed(null, null, Unproven: true);
            var listed = bytes.AsSpan(answer.Start, answer.Count);
            if (!GitObjectHash.Proves("tree", listed, id) || Entries(listed, commit.Length / 2) is not { } read) return new Listed(null, null, Unproven: true);
            entries = read;
            admitted.Add((new TreeKey(id), entries, DeclarationBound.Entry + Weigh(entries)));
            if (at == answers.Count - 1)
            {
                reached = true;
                break;
            }

            if (!entries.TryGetValue(folders[at - 1], out var folder) || folder.Type != "tree") break;
            id = folder.Id;
        }

        foreach (var (key, value, weight) in admitted) DeclarationBound.Indexes.Keep(key, value, weight);
        // A listing too heavy to keep, or let go beside this call, is the read's own: what the walk reached is the answer.
        return Kept(commit, folders) ?? new Listed(reached ? entries : null, null, Unproven: false);
    }

    /// <summary>The entries of a tree's bytes, its ids <paramref name="idLength"/> bytes long; null where they are not a tree's.</summary>
    internal static ImmutableDictionary<string, DeclarationEntry>? Entries(ReadOnlySpan<byte> tree, int idLength)
    {
        var entries = ImmutableDictionary.CreateBuilder<string, DeclarationEntry>(StringComparer.Ordinal);
        while (!tree.IsEmpty)
        {
            var space = tree.IndexOf((byte)' ');
            var end = tree.IndexOf((byte)0);
            if (space <= 0 || end <= space || tree.Length < end + 1 + idLength) return null;
            var mode = Encoding.ASCII.GetString(tree[..space]);
            var name = Encoding.UTF8.GetString(tree[(space + 1)..end]);
            var id = Convert.ToHexStringLower(tree.Slice(end + 1, idLength));
            entries[name] = new DeclarationEntry(mode, mode == "40000" ? "tree" : mode == "160000" ? "commit" : "blob", id);
            tree = tree[(end + 1 + idLength)..];
        }

        return entries.ToImmutable();
    }

    /// <summary>What a tree's entries hold in memory.</summary>
    internal static long Weigh(ImmutableDictionary<string, DeclarationEntry> entries) =>
        64 + entries.Sum(entry => 80 + DeclarationBound.Text(entry.Key) + DeclarationBound.Text(entry.Value.Mode) + DeclarationBound.Text(entry.Value.Id));

    private static Listed? Kept(string commit, string[] folders)
    {
        if (!DeclarationBound.Indexes.TryGet<string>(new CommitKey(commit), out var id)) return null;
        foreach (var folder in folders)
        {
            if (!DeclarationBound.Indexes.TryGet<ImmutableDictionary<string, DeclarationEntry>>(new TreeKey(id), out var parent)) return null;
            if (!parent.TryGetValue(folder, out var entry) || entry.Type != "tree") return new Listed(null, null, Unproven: false);
            id = entry.Id;
        }

        return DeclarationBound.Indexes.TryGet<ImmutableDictionary<string, DeclarationEntry>>(new TreeKey(id), out var entries)
            ? new Listed(entries, null, Unproven: false)
            : null;
    }

    private static string? TreeOf(ReadOnlySpan<byte> commit)
    {
        var line = commit.IndexOf((byte)'\n');
        var first = Encoding.ASCII.GetString(line < 0 ? commit : commit[..line]);
        return first.StartsWith("tree ", StringComparison.Ordinal) && GitObjectHash.IsId(first[5..]) ? first[5..] : null;
    }

    private static Listed Failed(DeclarationUnread cause, string why) => new(null, new DeclarationAnswer.Unread(cause, why), Unproven: false);
}
