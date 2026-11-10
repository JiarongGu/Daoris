using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What a session's review and a file's preview become on the wire, and the refusals that stand for a review with nothing to show.
/// Public so each shape is tested without a service.
/// </summary>
public static class TreesReviewWires
{
    /// <summary>The landing plan's count of uncommitted paths in the session's own tree: 0 where clean or not asked.</summary>
    public static int UncommittedCount(int? held) => held ?? 0;

    /// <summary>
    /// Where a review's files are read from: the session's own tree, its landed branch once the tree cannot be read, or a folder's
    /// manifests, which name no commit and offer nothing of git's.
    /// </summary>
    public static class ReviewSource
    {
        public const string Tree = "tree";
        public const string Branch = "branch";
        public const string Place = "place";
    }

    /// <summary>
    /// What a review becomes on the wire: the files and the range they are measured from, where they are read from,
    /// and — for a landed session — where its work landed, where that branch stands, and whether the
    /// review reads as landed, which is what the page's acts follow.
    /// </summary>
    /// <remarks>Never a machine path: the checkout the branch is read in is the registry's, and the page has no use for it.</remarks>
    public static object ReviewAnswer(
        string session, TreeDiff? diff, string source, LandedReview? landed, bool treeGone, TreeDiff? uncommitted = null,
        LineBehind? behind = null, string? treeBranch = null, LandOffer? offer = null) => new
    {
        Session = session,
        Base = diff?.Base ?? "",
        diff?.Truncated,
        Files = (diff?.Files ?? []).Select(file => new
        {
            file.Path,
            file.Status,
            file.Added,
            file.Removed,
            file.Patch,
        }).ToArray(),
        Source = source,
        // The session's own tree's uncommitted work, apart from the committed list: null where the
        // source is not that tree or nothing is uncommitted. A shape added, so an answer without it reads as it did.
        Uncommitted = source == ReviewSource.Tree && uncommitted is { Files.Count: > 0 }
            ? new
            {
                Files = uncommitted.Files.Select(file => new { file.Path, file.Status, file.Added, file.Removed, file.Patch }).ToArray(),
                Truncated = uncommitted.Truncated is not null,
            }
            : null,
        // How the session's own tree stands against the latest line known here: the line's name, the commits it has that
        // the tree does not, and the tree's beyond it. Null where the source is not that tree or the line could not be read; a shape added.
        Behind = source == ReviewSource.Tree && behind is not null ? new { behind.Line, behind.Commits, behind.Ahead } : null,
        // The branch the read tree's HEAD stands on, which the page's head names over the record's guess: the
        // agent may have switched it or deleted its `daoris/` branch and detached. Null where detached, and where the
        // source is a landed branch, whose tree is not read.
        TreeBranch = source == ReviewSource.Tree ? treeBranch : null,
        // What the driver would land of the tree: null where it offers none, the
        // work not beyond the line or landed, and on a landed branch's review; a shape added.
        Offer = source == ReviewSource.Tree && offer is not null ? new { offer.Branch, offer.Tree, offer.Commits, offer.Uncommitted } : null,
        Landed = landed is null ? null : new
        {
            landed.Entry.Branch,
            landed.Entry.Repository,
            landed.Entry.Line,
            LandedAt = landed.Entry.LandedAt == DateTimeOffset.MinValue
                ? null
                : Moments.Write(landed.Entry.LandedAt),
            landed.Entry.Plugin,
            landed.Entry.Pushed,
            landed.Entry.PullRequest,
            // Who accepted it: `person`, `auto`, or null for a landing that kept none.
            landed.Entry.AcceptedBy,
            landed.State,
            AsLanded = landed.ReadsAsLanded(treeGone),
            Reads = landed.Reads is { } reads ? new { reads.Kind, reads.Where, reads.Files, reads.Detail } : null,
            Removed = landed.Entry.RemovedAs is { } kind
                ? new
                {
                    Kind = kind,
                    Where = landed.Entry.RemovedOn,
                    At = Moments.Write(landed.Entry.GoneAt),
                }
                : null,
            // A branch the person withdrew, with when, their reason and the recovery ref its commits stay
            // on; `removed` stays the clean-up's alone, and a trace with no reason says none.
            Withdrawn = landed.Entry is { GoneAs: LandedGone.Withdrawn, GoneAt: { } withdrawnAt }
                ? new { At = Moments.Write(withdrawnAt), Why = landed.Entry.GoneWhy, landed.Entry.KeptOn }
                : null,
            landed.Detail,
        },
    };

    /// <summary>
    /// Why a record with no tree here has nothing to diff: what a record looks like when it travelled from the machine that did the work. The
    /// route says instead which session's tree it is where the id given is a tree's name (<see cref="TreeNamedRefusals"/>).
    /// </summary>
    public const string NoTreeToDiff = "this session's record names no working tree on this machine, so there is nothing here to diff. That is "
        + "what a record looks like when it travelled from the machine that did the work.";

    /// <summary>Why a review with no landing to read instead has nothing to show, as the refusal the page renders — each its own code.</summary>
    public static ShenoraException Unreviewable(string session, string? tree, string? baseCommit)
    {
        if (string.IsNullOrWhiteSpace(tree)) return Refusals.Because(Refusals.SessionNotReviewable, NoTreeToDiff, ("session", session));

        if (TreeFolders.TreeGone(tree))
        {
            return Refusals.Because(
                Refusals.SessionTreeGone,
                "this session's tree is gone from this machine — a clean-up after its landing, or a discard, removed it — so "
                + "there is nothing here to diff, and no tree to land or discard.",
                ("session", session));
        }

        if (string.IsNullOrWhiteSpace(baseCommit))
        {
            return Refusals.Because(
                Refusals.SessionNoBase,
                "this session's record does not say which commit its tree stood at when it began, so "
                + "there is no range to measure. Records made before Daoris started writing that down "
                + "keep their evidence line and cannot gain a diff.",
                ("session", session));
        }

        return Refusals.Because(
            Refusals.SessionRangeUnreadable,
            "git could not read that range where the session ran — the tree has moved, been "
            + "discarded, or no longer holds the commit it started from.",
            ("session", session));
    }

    /// <summary>
    /// A file read for its preview from a session's landed branch, in the repository's checkout
    /// <paramref name="root"/>, answered as the page receives it or refused as a code.
    /// </summary>
    /// <param name="tree">The session's tree as its record names it, gone now: a path the page sends inside it is the same path on the branch.</param>
    /// <param name="log">As <see cref="PreviewAsync"/>'s: the session and the file relative to the repository, never the branch's name, which carries a title's words.</param>
    public static async Task<object> LandedPreviewAsync(
        string session, string? root, LandedBranch landing, string? tree, string path, CancellationToken cancellationToken, MachineLog? log = null)
    {
        var read = await FilePreview.ReadLandedAsync(root, landing, path, tree, cancellationToken);
        if (read is { Refusal: FilePreviewRefusal.None, File: { } opened })
        {
            log?.Info("preview.opened", ("session", session), ("path", opened.Path));
        }

        return FileAnswer(session, path, read);
    }

    /// <summary>A file read for its preview in <paramref name="tree"/>, answered as the page receives it or refused as a code.</summary>
    /// <param name="log">
    /// The machine log a preview that opened is written to, as <c>preview.opened</c>: the session and
    /// the file relative to the tree, never the tree's own path and never the file's words. Null writes none.
    /// </param>
    public static async Task<object> PreviewAsync(
        string session, string? tree, string path, CancellationToken cancellationToken, MachineLog? log = null)
    {
        var read = string.IsNullOrWhiteSpace(tree)
            ? FilePreviewResult.Refused(FilePreviewRefusal.NoTree)
            : await FilePreview.ReadAsync(tree, path, cancellationToken);
        if (read is { Refusal: FilePreviewRefusal.None, File: { } opened })
        {
            log?.Info("preview.opened", ("session", session), ("path", opened.Path));
        }

        return FileAnswer(session, path, read);
    }

    /// <summary>What a preview's reading becomes on the wire: the file, or its refusal, each with its own code.</summary>
    public static object FileAnswer(string session, string path, FilePreviewResult read)
    {
        (string, string)[] named = [("session", session), ("path", path)];
        return read switch
        {
            // The landed branch it is read from, once the tree is gone; null for the file on disk.
            { Refusal: FilePreviewRefusal.None, File: { } file } => new
            {
                Session = session, file.Path, file.Size, file.Binary, file.Text, file.Truncated, file.Branch,
            },
            { Refusal: FilePreviewRefusal.NotOnBranch } => throw Refusals.Because(
                Refusals.PreviewNotOnBranch,
                $"`{path}` is not a file on `{read.Branch}`, the branch this session's work landed on: it was deleted or moved "
                + "there, or it is a folder or a link.",
                [.. named, ("branch", read.Branch ?? "")]),
            { Refusal: FilePreviewRefusal.Unfetched } => throw Refusals.Because(
                Refusals.PreviewUnfetched,
                $"`{path}` is a file on `{read.Branch}`, but this clone left its contents on the remote, which did not give them when asked.",
                [.. named, ("branch", read.Branch ?? "")]),
            { Refusal: FilePreviewRefusal.Outside } => throw Refusals.Because(
                Refusals.PreviewOutsideTree,
                $"`{path}` is outside this session's tree, so the preview does not read it.",
                named),
            { Refusal: FilePreviewRefusal.LinkLeaves } => throw Refusals.Because(
                Refusals.PreviewLinkLeavesTree,
                $"`{path}` goes through a link that leads out of this session's tree, so the preview does not read it.",
                named),
            { Refusal: FilePreviewRefusal.GitFolder } => throw Refusals.Because(
                Refusals.PreviewGitFolder,
                $"`{path}` is inside `.git`, which is git's own folder rather than the session's work.",
                named),
            { Refusal: FilePreviewRefusal.NoTree } => throw Refusals.Because(
                Refusals.PreviewNoTree,
                "this session's record names no tree on this machine, or the tree is gone and no branch its landing made stands "
                + "here to read instead, so there is no file here to show.",
                named),
            _ => throw Refusals.Because(
                Refusals.PreviewNotAFile,
                $"`{path}` is not a file in this session's tree now: it was deleted or moved, it is a folder, or it cannot be read.",
                named),
        };
    }
}
