namespace Daoris.Driver;

/// <summary>
/// The one store of a repository's and a home's declarations: a checkout's or a home's file by its stat, a file at a commit from the
/// commit's hashed listing, a blob by its id, a commit's tracked list under its fence, and each kind's parse and each view kept by the
/// content keys they are made from. It keeps a value only where it is a function of the key it is kept under, so a read's failure is
/// kept for none.
/// </summary>
/// <remarks>
/// <para><b>While a replacement can be in force</b> (<see cref="GitReplacements.CanBeInForce"/>) every id-keyed part is passed through:
/// git is asked by path, its bytes answer kept by their content key alone and carrying no id, and nothing is looked up or admitted.</para>
/// <para><b>An ask by blob id</b> is answered only by bytes that hash to that id, else <i>unread: replaced</i>; an ask by path takes
/// git's bytes, with no id, where they do not.</para>
/// </remarks>
internal static class RepositoryDeclarations
{
    /// <summary>The most of a content kept: fifty times the largest declaration of this repository. A larger one is read at each ask.</summary>
    internal const int Bound = 1024 * 1024;

    /// <summary>The test seam: where set, run before a listing's start and a tracked list's, after the gate and the fence are read.</summary>
    internal static readonly AsyncLocal<Func<Task>?> BeforeRead = new();

    private sealed record BlobKey(string Id);

    private sealed record ParseKey(ContentKey Content, Type Kind);

    private sealed record ViewKey(Type View, object Key);

    private sealed record TrackedKey(string Commit);

    /// <summary>A checkout's or a home's file at <paramref name="path"/>, by its stat: a warm ask is that stat.</summary>
    internal static DeclarationAnswer OnDisk(string path, int limit = Bound)
    {
        using var _ = DeclarationScope.Enter(DeclarationAsker.Store);
        var stamp = FileStamp.Of(path);
        if (stamp is { Bytes: null, Unread: false, Content: { } held, Stat.Length: var length } && length <= limit
            && DeclarationBound.Contents.TryGet<DeclarationBytes>(new ContentKey(held), out var kept))
        {
            return new DeclarationAnswer.Present(kept, null);
        }

        if (stamp is { Bytes: null, Unread: false, Stat: { Folder: false } stat } && stat.Length <= limit) stamp = FileStamp.Of(path, bytes: true);

        if (stamp.Stat is not { } at)
        {
            return Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(path)))
                ? DeclarationAnswer.Absent.One
                : new DeclarationAnswer.Unread(DeclarationUnread.FolderGone, $"the folder of {path} is gone");
        }

        if (at.Folder) return new DeclarationAnswer.Unread(DeclarationUnread.NotAFile, $"{path} is a folder");
        if (at.Length > limit || stamp.Bytes?.Length > limit) return TooLarge(path, limit);
        if (stamp.Unread || stamp.Bytes is not { } bytes) return new DeclarationAnswer.Unread(DeclarationUnread.Io, $"{path} does not read");

        var content = new DeclarationBytes(new ContentKey(stamp.Content!), System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(bytes), null);
        Keep(content);
        return new DeclarationAnswer.Present(content, null);
    }

    /// <summary>The file <paramref name="path"/> (from the top) at the commit id <paramref name="commit"/>, from its hashed listing.</summary>
    internal static async Task<DeclarationAnswer> AtAsync(string root, string commit, string path, CancellationToken ct, int limit = Bound)
    {
        if (!GitObjectHash.IsId(commit)) throw new ArgumentException($"the store is asked at a commit id, never '{commit}'", nameof(commit));
        using var _ = DeclarationScope.Enter(DeclarationAsker.Store);
        if (RefFiles.CommonDirOf(root) is not { } common) return new DeclarationAnswer.Unread(DeclarationUnread.Failed, $"{root} is no repository's folder");
        if (GitReplacements.CanBeInForce(common)) return await PassedAsync(root, commit, path, limit, ct).ConfigureAwait(false);

        var listed = await DeclarationListing.FolderAsync(root, commit, path, ct).ConfigureAwait(false);
        if (listed.Unread is { } unread) return unread;
        if (listed.Unproven) return await PassedAsync(root, commit, path, limit, ct).ConfigureAwait(false);
        if (listed.Entries is null || !listed.Entries.TryGetValue(path.Split('/')[^1], out var entry)) return DeclarationAnswer.Absent.One;
        if (!entry.Regular) return new DeclarationAnswer.Present(null, entry);

        var (blob, proven) = await BlobAsync(root, new BlobId(entry.Id), limit, admit: true, ct).ConfigureAwait(false);
        return blob is DeclarationAnswer.Present present && proven ? present with { Entry = entry } : blob;
    }

    /// <summary>
    /// The blob <paramref name="id"/>: the kept bytes where no replacement can be in force, else git's, and either answered only where they
    /// hash to the id; any other answer is unread, replaced.
    /// </summary>
    internal static async Task<DeclarationAnswer> ByIdAsync(string root, BlobId id, CancellationToken ct, int limit = Bound)
    {
        using var _ = DeclarationScope.Enter(DeclarationAsker.Store);
        if (RefFiles.CommonDirOf(root) is not { } common) return new DeclarationAnswer.Unread(DeclarationUnread.Failed, $"{root} is no repository's folder");
        var (blob, proven) = await BlobAsync(root, id, limit, admit: !GitReplacements.CanBeInForce(common), ct).ConfigureAwait(false);
        return blob is DeclarationAnswer.Present && !proven
            ? new DeclarationAnswer.Unread(DeclarationUnread.Replaced, $"git answers other bytes for {id} in {root}: a replacement stands")
            : blob;
    }

    /// <summary>
    /// Every path git tracks at <paramref name="commit"/>, kept by commit under the fence of the replacement gate's files: an answer git's
    /// listing hands back no bytes to prove, so it is admitted only where those files' stats held across it.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> TrackedAsync(string root, string commit, CancellationToken ct)
    {
        using var _ = DeclarationScope.Enter(DeclarationAsker.Store);
        if (DeclarationBound.Lists.TryGet<IReadOnlyList<string>>(new TrackedKey(commit), out var kept)) return kept;
        var common = RefFiles.CommonDirOf(root);
        var fence = new StatFence();
        if (common is not null)
        {
            fence.Add(GitPackedRefs.PathIn(common));
            fence.Add(Path.Combine(common, "reftable"));
            fence.AddFolders(Path.Combine(common, "refs", "replace"));
        }

        var passed = common is null || GitReplacements.CanBeInForce(common) || !fence.Settled();
        if (BeforeRead.Value is { } before) await before().ConfigureAwait(false);
        var tracked = await FileGit.TrackedAsync(root, commit, ct).ConfigureAwait(false);
        if (!passed && GitObjectHash.IsId(commit) && fence.Holds())
        {
            DeclarationBound.Lists.Keep(new TrackedKey(commit), tracked, DeclarationBound.Entry + DeclarationBound.Texts(tracked));
        }

        return tracked;
    }

    /// <summary>The parse of <paramref name="answer"/>'s content by <paramref name="kind"/>; null where it holds no regular file's content.</summary>
    internal static T? Parse<T>(DeclarationAnswer answer, IDeclarationKind<T> kind) where T : class =>
        answer is DeclarationAnswer.Present { Content: { } content } ? Parse(content, kind) : null;

    /// <summary>The parse of <paramref name="content"/> by <paramref name="kind"/>, kept by its content key where it names no problem.</summary>
    internal static T Parse<T>(DeclarationBytes content, IDeclarationKind<T> kind) where T : class
    {
        var key = new ParseKey(content.Key, kind.GetType());
        if (DeclarationBound.Contents.TryGet<T>(key, out var kept)) return kept;
        var parsed = kind.Parse(content);
        if (!kind.Problem(parsed)) DeclarationBound.Contents.Keep(key, parsed, DeclarationBound.Entry + kind.Weigh(parsed));
        return parsed;
    }

    /// <summary>The view of <paramref name="key"/>, made once a key from the <paramref name="contents"/> its asker holds.</summary>
    internal static TValue View<TKey, TValue>(TKey key, IDeclarationView<TKey, TValue> view, params DeclarationBytes[] contents)
        where TKey : notnull where TValue : class
    {
        var at = new ViewKey(view.GetType(), key);
        if (DeclarationBound.Contents.TryGet<TValue>(at, out var kept)) return kept;
        var value = view.View(key, new DeclarationContents(contents));
        DeclarationBound.Contents.Keep(at, value, DeclarationBound.Entry + view.Weigh(value));
        return value;
    }

    /// <summary>The file at a commit as git answers it by path, while a replacement can be in force or git's bytes did not prove themselves.</summary>
    internal static async Task<DeclarationAnswer> PassedAsync(string root, string commit, string path, int limit, CancellationToken ct)
    {
        var read = await GitObjectReads.ReadAsync(root, GitObjectName.At(commit, path), limit, ct).ConfigureAwait(false);
        if (read is not ({ } answer, var bytes)) return new DeclarationAnswer.Unread(DeclarationUnread.Failed, $"git could not read {path} at {commit} in {root}");
        if (answer.Missing) return DeclarationAnswer.Absent.One;
        if (answer.Type != "blob") return new DeclarationAnswer.Present(null, new DeclarationEntry("", answer.Type, ""));
        if (answer.Size > limit) return TooLarge(path, limit);
        if (answer.Count < answer.Size) return Short(path, answer);
        var content = DeclarationBytes.Of(bytes.AsSpan(answer.Start, answer.Count).ToArray(), null);
        Keep(content);
        return new DeclarationAnswer.Present(content, null);
    }

    private static async Task<(DeclarationAnswer Answer, bool Proven)> BlobAsync(string root, BlobId id, int limit, bool admit, CancellationToken ct)
    {
        if (admit && DeclarationBound.Indexes.TryGet<string>(new BlobKey(id.Hex), out var key)
                  && DeclarationBound.Contents.TryGet<DeclarationBytes>(new ContentKey(key), out var held))
        {
            return (held.Size > limit ? TooLarge(id.Hex, limit) : new DeclarationAnswer.Present(held with { Id = id }, null), true);
        }

        var read = await GitObjectReads.ReadAsync(root, id.Hex, limit, ct).ConfigureAwait(false);
        if (read is not ({ } answer, var bytes)) return (new DeclarationAnswer.Unread(DeclarationUnread.Failed, $"git could not read the blob {id} in {root}"), false);
        if (answer.Missing) return (new DeclarationAnswer.Unread(DeclarationUnread.Missing, $"git holds no blob {id} in {root}"), false);
        if (answer.Type != "blob") return (new DeclarationAnswer.Unread(DeclarationUnread.Failed, $"{id} is no blob"), false);
        if (answer.Size > limit) return (TooLarge(id.Hex, limit), false);
        if (answer.Count < answer.Size) return (Short(id.Hex, answer), false);

        var raw = bytes.AsSpan(answer.Start, answer.Count).ToArray();
        var proven = id.Proves(raw);
        var content = DeclarationBytes.Of(raw, null);
        Keep(content);
        if (proven && admit) DeclarationBound.Indexes.Keep(new BlobKey(id.Hex), content.Key.Hex, DeclarationBound.Entry + DeclarationBound.Text(content.Key.Hex));
        return (new DeclarationAnswer.Present(proven ? content with { Id = id } : content, null), proven);
    }

    // Bytes are a function of their content key, so any read's are kept by it, an id never with them.
    private static void Keep(DeclarationBytes content)
    {
        if (content.Size <= Bound) DeclarationBound.Contents.Keep(content.Key, content with { Id = null }, DeclarationBound.Entry + content.Weight);
    }

    private static DeclarationAnswer.Unread TooLarge(string what, int limit) =>
        new(DeclarationUnread.TooLarge, $"{what} is larger than its reader's {limit} bytes");

    private static DeclarationAnswer.Unread Short(string what, GitObjectAnswer answer) =>
        new(DeclarationUnread.Short, $"{what} read {answer.Count} of its {answer.Size} bytes");
}
