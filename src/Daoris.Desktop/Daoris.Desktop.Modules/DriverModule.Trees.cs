using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What a session's tree holds and what becomes of it, the page's `bridge/trees.ts` (MOD5): the review's
/// diff (SURF6), the files a composer offers (CONV4d), a file's preview (D111), discard (D51), landing
/// (WSR1) and the hand-off (WSR5b).
/// </summary>
public sealed partial class DriverModule
{
    // What a session actually did (SURF6, design §5). The evidence string says that work
    // happened; this says what it was. Desktop-only by construction, like the console: it is
    // read off a checkout, and only the machine holding one can answer at all.
    /// <summary>
    /// One session's landed work, as a diff (SURF6).
    /// </summary>
    /// <remarks>
    /// <para><b>Read-only, and that is the whole of this route.</b> It runs `git diff` in the tree the
    /// record names and returns what git said. Nothing here writes, merges or removes anything — the
    /// acts that do are the person's and are their own routes, so a surface that only shows the work
    /// cannot accidentally change it.</para>
    ///
    /// <para><b>Unreviewable is INFORMATION, not a failure</b> (D48 §6's class): a record mirrored
    /// from another machine names no tree here, a record made before the base commit was written has
    /// no range, and a tree that has been discarded is gone. None of those is a fault, and each has a
    /// different sentence, so the page can say which.</para>
    /// </remarks>
    [DriverRoute("SESSION_DIFF")]
    private async Task<object?> DiffAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var service = _loop.Service ?? throw NotReady();

        var (tree, baseCommit) = await service.SessionGroundAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to diff. That is what a record looks like when it travelled from the machine "
                + "that did the work.",
                ("session", id));
        }

        if (string.IsNullOrWhiteSpace(baseCommit))
        {
            throw Refusals.Because(
                Refusals.SessionNoBase,
                "this session's record does not say which commit its tree stood at when it began, so "
                + "there is no range to measure. Records made before Daoris started writing that down "
                + "keep their evidence line and cannot gain a diff.",
                ("session", id));
        }

        // A tree brought up to date since (WSR6) no longer holds the commit its session began at: the review measures
        // from where its branch now grows from, or it would show the line's own changes as the session's work.
        var from = await new SessionTrees(_loop.Home).ReviewBaseAsync(tree, baseCommit, cancellationToken);
        var diff = await WorkingTree.DiffAsync(tree, from, cancellationToken);
        if (diff is null)
        {
            throw Refusals.Because(
                Refusals.SessionRangeUnreadable,
                "git could not read that range where the session ran — the tree has moved, been "
                + "discarded, or no longer holds the commit it started from.",
                ("session", id));
        }

        return new
        {
            Session = id,
            diff.Base,
            diff.Truncated,
            Files = diff.Files.Select(file => new
            {
                file.Path,
                file.Status,
                file.Added,
                file.Removed,
                file.Patch,
            }).ToArray(),
        };
    }

    // What a person may `@` in a conversation (CONV4d): the files in the tree the record names.
    // Desktop-only for the diff's reason — it is read off a checkout — and read-only.
    /// <summary>
    /// The files in one session's tree, for the composer's `@` (CONV4d).
    /// </summary>
    /// <remarks>
    /// <b>Unlisted is INFORMATION</b>, in the review's class: a record from another machine names no
    /// tree here, and a tree that is gone or not a repository of its own has nothing git will answer
    /// for. One code for both, because the person's next move is the same — type the path, which both
    /// doors expand as typed (`docs/2026-09-25-message-content-evidence.md`).
    /// </remarks>
    [DriverRoute("SESSION_FILES")]
    private async Task<object?> FilesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        var files = string.IsNullOrWhiteSpace(tree)
            ? null
            : await WorkingTree.FilesAsync(tree, cancellationToken);
        if (files is null)
        {
            throw Refusals.Because(
                Refusals.SessionTreeUnlisted,
                "git cannot list this session's tree here — its record names no tree on this machine, or "
                + "the tree is gone or not a repository of its own. A path typed after @ still reaches the agent.",
                ("session", id));
        }

        return new { Session = id, files.Files, files.Unlisted };
    }

    // A file the conversation or the review named, read for the person to look at in the side bar
    // (PREVIEW1, D111). Desktop-only for the diff's reason, and read-only: there is no editor (D55).
    /// <summary>
    /// One file in a session's tree, for its preview (PREVIEW1, D111): the tree the record names, which for a
    /// conversation in the repository's checkout is the checkout.
    /// </summary>
    /// <remarks>
    /// <para><b>Only inside that tree</b> (<see cref="FilePreview"/>): a path outside it, a path through a link
    /// that leads out of it, and a path under `.git` are each refused as a code of their own.</para>
    ///
    /// <para><b>The answer names the file as the page asked for it</b>, relative to the tree, and never the
    /// tree's own path, which is machine-local material the page has no use for.</para>
    /// </remarks>
    [DriverRoute("SESSION_FILE")]
    private async Task<object?> FileAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var path = PayloadHelper.GetRequiredValue<string>(request.Payload, "path");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        return await PreviewAsync(id, tree, path, cancellationToken, _loop.Log);
    }

    /// <summary>
    /// A file read for its preview in <paramref name="tree"/>, answered as the page receives it or refused as
    /// a code. Public, as <see cref="OptionsAnswer"/> is, so the route's answers are tested without a service.
    /// </summary>
    /// <param name="log">
    /// The machine log a preview that opened is written to (LEFT2, D94), as <c>preview.opened</c>: the session and
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
            { Refusal: FilePreviewRefusal.None, File: { } file } => new
            {
                Session = session, file.Path, file.Size, file.Binary, file.Text, file.Truncated,
            },
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
                "this session's record names no tree on this machine, or the tree is gone, so there is no file here to show.",
                named),
            _ => throw Refusals.Because(
                Refusals.PreviewNotAFile,
                $"`{path}` is not a file in this session's tree now: it was deleted or moved, it is a folder, or it cannot be read.",
                named),
        };
    }

    // Discarding a reviewed session's tree (SURF6b, D51 rule 7): the PERSON's act, since nothing
    // deletes itself, so its own route rather than anything the diff route could do as a side effect.
    // Its sibling, the merge alone, is retired (LEFT2): the work lands through `LAND_SESSION_TREE`,
    // which merges where the repository's rule says merge (D87).
    /// <summary>
    /// Discard a session's tree (SURF6b, D51 rule 7).
    /// </summary>
    /// <remarks>
    /// <para><b>A refusal here is an ANSWER, not an error</b> — the tree holds work nobody merged, or
    /// uncommitted work. Each comes back as `{ done: false, message }` with the sentence the tree layer
    /// wrote, exactly as `START_CHAT` does, because the person's next move is different for each and a
    /// code would flatten them into one.</para>
    ///
    /// <para><b>Discard needs `force` said out loud.</b> The unforced call is what produces the
    /// refusal that names what would be lost, so the page asks, shows that sentence, and only then
    /// sends `force`. Destroying work is never a side effect of tidying (D51 rule 7).</para>
    /// </remarks>
    [DriverRoute("DISCARD_SESSION_TREE")]
    private async Task<object?> DiscardTreeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var force = Flag(request, "force");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to discard.",
                ("session", id));
        }

        // The same home the loop derives for the chat runner and the watch: the directory holding
        // `driver.json`. Derived rather than stored twice, so one answer cannot drift from the other.
        var removal = await new SessionTrees(_loop.Home).RemoveAsync(tree, force, cancellationToken);
        _loop.Nudge();
        return new { Session = id, Done = removal.Removed, removal.Message };
    }

    // How a reviewed session's work lands (WSR1, D87): what a press WOULD do, said before it —
    // merge into the line, or the branch the rule names for this session — and the press, which
    // applies the repository's rule. It is the one door that lands a session's work.
    /// <summary>
    /// A reviewed session's landing (WSR1, D87): the plan before the press, or the press itself.
    /// </summary>
    /// <remarks>
    /// The pattern is expanded from the session's quest and its title, or for a conversation from the
    /// session and what the person first said — this machine's own record, never the session record's.
    /// A refusal is an answer, as the merge door's are.
    /// </remarks>
    [DriverRoute("LANDING")]
    [DriverRoute("LAND_SESSION_TREE")]
    private async Task<object?> LandAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing here to land.",
                ("session", id));
        }

        var questId = await service.SessionQuestAsync(id, cancellationToken);
        // Named for the chain's first quest (WSR5): a chain lands from its last step.
        var subject = await LandingRules.SubjectAsync(
            id, questId, quest => service.FindQuestAsync(quest, cancellationToken),
            _loop.Events.Openings([id]).GetValueOrDefault(id));
        // A rule's plugin says its lines on the console under its name, as a hook's do (D64 §4, D100).
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line)));

        if (request.Type == "LANDING")
        {
            var plan = await trees.PlanAsync(tree, subject, cancellationToken);
            return new { Session = id, plan.Form, plan.Target, plan.Source, plan.Plugin, plan.Problem };
        }

        var landed = await trees.LandAsync(tree, subject, cancellationToken);
        // Kept where the conversation is kept, so the landing and the plugin's word outlast the press (D100).
        if (landed.Landed) _loop.Events.Keep(id, LandingRules.Note(landed), line => _loop.Output.Append(id, line));
        _loop.Nudge();
        return new
        {
            Session = id, Done = landed.Landed, landed.Message, landed.Branch,
            Plugin = landed.Plugin is { } said
                ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed }
                : null,
        };
    }

    // A branch a landing made, handed to a landing plugin afterwards (WSR5b): what a press would do,
    // said before it, and the press — `daoris-driver trees hand` is the terminal's door (D50).
    /// <summary>
    /// A session's landed branch handed to a landing plugin after its landing (WSR5b): the plan before the
    /// press, or the press. The branch is the one this session's landing made and recorded; a session whose
    /// landing made none answers no branch, which is information, not a failure.
    /// </summary>
    /// <remarks>
    /// A refusal is an answer, as a landing's is. The checkout is the registry's, since the tree may have been
    /// tidied away; a repository with no checkout here is answered in the same sentence form.
    /// </remarks>
    [DriverRoute("HANDOFF_PLAN")]
    [DriverRoute("HANDOFF")]
    private async Task<object?> HandOffAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var named = Optional(request, "plugin");
        var service = _loop.Service ?? throw NotReady();
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line)));

        // The session's newest landing: a session lands once, and a second press is refused while its branch stands.
        var entry = trees.Recorded.Find(id).FirstOrDefault(each => string.Equals(each.Session, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return request.Type == "HANDOFF_PLAN"
                ? new { Session = id, Branch = (string?)null }
                : (object)new { Session = id, Done = false, Message = SessionTrees.NotLanded(id), Branch = (string?)null };
        }

        var root = await CheckoutOfAsync(service, entry.Repository, cancellationToken);
        if (root is null)
        {
            var none = $"`{entry.Repository}` has no checkout on this machine, so there is no `{entry.Branch}` here to hand on.";
            return request.Type == "HANDOFF_PLAN"
                ? new { Session = id, Branch = (string?)entry.Branch, entry.Repository, Plugin = (string?)null, Problem = (string?)none, entry.PullRequest, Commits = 0 }
                : (object)new { Session = id, Done = false, Message = none, Branch = (string?)entry.Branch };
        }

        if (request.Type == "HANDOFF_PLAN")
        {
            var plan = await trees.HandPlanAsync(root, entry, named, cancellationToken);
            return new { Session = id, Branch = (string?)plan.Branch, plan.Repository, plan.Plugin, plan.Problem, plan.PullRequest, plan.Commits };
        }

        var handed = await HandAsync(trees, root, entry, named, cancellationToken);
        return new
        {
            Session = id, Done = handed.Handed, handed.Message, Branch = (string?)handed.Branch,
            Plugin = handed.Plugin is { } said
                ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed }
                : null,
        };
    }

    /// <summary>A repository's checkout here, from the registry — or null where it has none on this machine.</summary>
    private static async Task<string?> CheckoutOfAsync(ServiceClient service, string repository, CancellationToken cancellationToken)
    {
        var root = (await service.RegistryAsync(cancellationToken))
            .FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        return string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ? null : root;
    }

    /// <summary>
    /// The hand-off's press, which the review's button and Ask Daoris's card share (WSR5b): the plugin spoken
    /// to, its word kept where the landing's own note is (D100), and the loop told to look again.
    /// </summary>
    private async Task<TreeHand> HandAsync(SessionTrees trees, string root, LandedBranch entry, string? plugin, CancellationToken cancellationToken)
    {
        var handed = await trees.HandAsync(root, entry, plugin, cancellationToken);
        if (handed.Plugin is not null) _loop.Events.Keep(entry.Session, LandingRules.HandNote(handed), line => _loop.Output.Append(entry.Session, line));
        _loop.Nudge();
        return handed;
    }
}
