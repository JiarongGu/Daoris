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
    ///
    /// <para><b>A landed session reads as landed</b> (REVIEW2, D113). Where this machine's landing record holds
    /// the session's landing, the answer says where its work landed — the branch, when, the pull request a
    /// plugin opened — and where that branch stands now. The tree is read first while it is here, since it
    /// holds what the session did and anything it did after its landing; once it is gone, the changes are
    /// read from the landed branch in the repository's own checkout, from where its work grew from, and a
    /// branch gone since is said with whether its work reads on the line. Every read there is of refs and
    /// objects: the person's checkout is never touched.</para>
    /// </remarks>
    [DriverRoute("SESSION_DIFF")]
    private async Task<object?> DiffAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var service = _loop.Service ?? throw NotReady();

        var (tree, baseCommit) = await service.SessionGroundAsync(id, cancellationToken);
        var trees = new SessionTrees(_loop.Home);
        // This machine's landing of the session, standing or a trace (D113): where its work went.
        var landing = trees.Recorded.Landing(id);
        var gone = SessionTrees.TreeGone(tree);

        if (!gone && !string.IsNullOrWhiteSpace(baseCommit))
        {
            // A tree brought up to date since (WSR6) no longer holds the commit its session began at: the review measures
            // from where its branch now grows from, or it would show the line's own changes as the session's work.
            var from = await trees.ReviewBaseAsync(tree!, baseCommit, cancellationToken);
            var diff = await WorkingTree.DiffAsync(tree!, from, cancellationToken);
            if (diff is not null)
            {
                var standing = landing is null
                    ? null
                    : await trees.LandedReviewAsync(await CheckoutOfAsync(service, landing.Repository, cancellationToken), landing, changes: false, cancellationToken);
                return ReviewAnswer(id, diff, ReviewSource.Tree, standing, treeGone: false);
            }
        }

        if (landing is not null)
        {
            var review = await trees.LandedReviewAsync(
                await CheckoutOfAsync(service, landing.Repository, cancellationToken), landing, changes: true, cancellationToken);
            return ReviewAnswer(id, review.Changes, ReviewSource.Branch, review, treeGone: gone);
        }

        throw Unreviewable(id, tree, baseCommit);
    }

    /// <summary>Where a review's files are read from (REVIEW2): the session's own tree, or its landed branch once the tree cannot be read.</summary>
    public static class ReviewSource
    {
        public const string Tree = "tree";
        public const string Branch = "branch";
    }

    /// <summary>
    /// What a review becomes on the wire: the files and the range they are measured from, where they are read from,
    /// and — for a landed session (REVIEW2, D113) — where its work landed, where that branch stands, and whether the
    /// review reads as landed, which is what the page's acts follow. Public, as <see cref="FileAnswer"/> is, so the
    /// shape is tested without a service.
    /// </summary>
    /// <remarks>Never a machine path: the checkout the branch was read in is the registry's, and the page has no use for it.</remarks>
    public static object ReviewAnswer(string session, WorkingTree.TreeDiff? diff, string source, LandedReview? landed, bool treeGone) => new
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
        Landed = landed is null ? null : new
        {
            landed.Entry.Branch,
            landed.Entry.Repository,
            landed.Entry.Line,
            LandedAt = landed.Entry.LandedAt == DateTimeOffset.MinValue
                ? null
                : landed.Entry.LandedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            landed.Entry.Plugin,
            landed.Entry.Pushed,
            landed.Entry.PullRequest,
            // Who accepted it (LAND2b, D145 point 4): `person`, `auto`, or null for a landing from before it was kept.
            landed.Entry.AcceptedBy,
            landed.State,
            AsLanded = landed.ReadsAsLanded(treeGone),
            Reads = landed.Reads is { } reads ? new { reads.Kind, reads.Where, reads.Files, reads.Detail } : null,
            Removed = landed.Entry.RemovedAs is { } kind
                ? new
                {
                    Kind = kind,
                    Where = landed.Entry.RemovedOn,
                    At = landed.Entry.GoneAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                }
                : null,
            landed.Detail,
        },
    };

    /// <summary>Why a review with no landing to read instead has nothing to show, as the refusal the page renders — each its own code.</summary>
    public static Shenora.Core.Ipc.ShenoraException Unreviewable(string session, string? tree, string? baseCommit)
    {
        if (string.IsNullOrWhiteSpace(tree))
        {
            return Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to diff. That is what a record looks like when it travelled from the machine "
                + "that did the work.",
                ("session", session));
        }

        if (SessionTrees.TreeGone(tree))
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
    ///
    /// <para><b>Once the tree is gone, a landed session's file is read from its landed branch</b> (REVIEW2, D113):
    /// a finished session is usually a landed one, and a tidy takes its tree, so its preview had nothing to read.
    /// The answer names the branch, so the page says the file is as that branch holds it. The no-tree answer is
    /// left for a session with neither its tree nor the landing's branch here.</para>
    /// </remarks>
    [DriverRoute("SESSION_FILE")]
    private async Task<object?> FileAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var path = PayloadHelper.GetRequiredValue<string>(request.Payload, "path");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (!SessionTrees.TreeGone(tree) || new SessionTrees(_loop.Home).Recorded.Landing(id) is not { } landing)
        {
            return await PreviewAsync(id, tree, path, cancellationToken, _loop.Log);
        }

        var root = await CheckoutOfAsync(service, landing.Repository, cancellationToken);
        return await LandedPreviewAsync(id, root, landing, tree, path, cancellationToken, _loop.Log);
    }

    /// <summary>
    /// A file read for its preview from a session's landed branch (REVIEW2, D113), in the repository's checkout
    /// <paramref name="root"/>, answered as the page receives it or refused as a code. Public, as <see cref="PreviewAsync"/> is.
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
            // The landed branch it was read from, once the tree is gone (REVIEW2); null for the file on disk.
            { Refusal: FilePreviewRefusal.None, File: { } file } => new
            {
                Session = session, file.Path, file.Size, file.Binary, file.Text, file.Truncated, file.Branch,
            },
            { Refusal: FilePreviewRefusal.NotOnBranch } => throw Refusals.Because(
                Refusals.PreviewNotOnBranch,
                $"`{path}` is not a file on `{read.Branch}`, the branch this session's work landed on: it was deleted or moved "
                + "there, or it is a folder or a link.",
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
        // A rule's plugin says its lines on the console under its name, as a hook's do (D64 §4, D100); what the
        // landing did with it goes to the machine log and the loop's record of its health (PLUGUI1d).
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line), log: _loop.Log, health: _loop.Health));

        // The landing gate (XAGENT1f, REVIEWENV1c): the second opinion, then the review. What holds the press says so before it,
        // and the press refuses it.
        var gate = await trees.GateAsync(tree, questId, new ServiceReviewWorld(service), id, cancellationToken);

        if (request.Type == "LANDING")
        {
            // How it lands, by the process the gate read (WORKFLOW1f): a named workflow's landing step, or the rule.
            var plan = await trees.PlanAsync(tree, subject, cancellationToken, gate.Process);
            return new
            {
                Session = id, plan.Form, plan.Target, plan.Source, plan.Plugin, plan.Problem, Review = Waits(gate.Review), Opinion = Opinion(gate.Opinion),
            };
        }

        // The press answers what it showed beside Accept (the second-agent design §8.2–§8.3): a dispute or commits nobody read,
        // drawn with the gate's token, which the press sends back; a gate that moved since answers nothing.
        gate = await new OpinionPresses(_loop.Home, service).PressedAsync(id, gate, Optional(request, "answers"), tree, questId, cancellationToken);

        // The sessions in use, asked when the rule's tidy reaches the other session branches the work holds (LAND3).
        var landed = await trees.LandAsync(tree, subject, cancellationToken, async token => await InUseAsync(service, token), gate: gate);
        // Kept where the conversation is kept, so the landing and the plugin's word outlast the press (D100).
        if (landed.Landed) _loop.Events.Keep(id, LandingRules.Note(landed), line => _loop.Output.Append(id, line));
        if (_loop.Log is { } log && LandingGateWords.Held(id, trees.Owner(tree), gate, landed, ReviewDoors.Screen) is { } held)
        {
            SessionLog.WriteLanding(log, held);
        }

        _loop.Nudge();
        return new
        {
            Session = id, Done = landed.Landed, landed.Message, landed.Branch,
            Plugin = landed.Plugin is { } said
                ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed }
                : null,
            Review = Waits(gate.Review),
            Opinion = Opinion(gate.Opinion),
        };
    }

    /// <summary>
    /// What the second opinion's gate says beside a landing's plan or press (XAGENT1f, the second-agent design §8.1, §8.5): its
    /// state, whether it holds, whether the rule requires one, the reviewer and its label, the findings and the disputes counted,
    /// the commits unread, why none could be had, and the sentence; and the token a press sends back to answer what it showed.
    /// Null where the level asks no opinion, so a landing no rule touches answers as it did. The page draws it (XAGENT1g).
    /// </summary>
    public static object? Opinion(OpinionGateState gate) => gate.State == OpinionGateStates.None
        ? null
        : new
        {
            gate.State,
            Holds = !gate.LetsGo,
            gate.Required,
            Opinion = gate.Opinion?.Id,
            Reviewer = gate.Opinion?.Adapter,
            gate.Opinion?.Product,
            gate.Opinion?.Maker,
            gate.Opinion?.Label,
            Reviewing = gate.Opinion?.Session,
            Findings = gate.Opinion?.Findings?.Count,
            Disputes = gate.Disputes?.Count,
            gate.Since,
            gate.Code,
            Until = gate.Until?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            gate.Later,
            Person = gate.Person?.Said,
            Answers = gate.PressAnswers ? gate.Token : null,
            Says = gate.Says,
        };

    // The second opinion's presses (XAGENT1f, the second-agent design §8.5, §9): the screen's half of `daoris-driver opinion`,
    // each the driver's own press (OpinionPresses), so both doors say and keep the same. A refusal is an answer, as a landing's is.
    /// <summary>
    /// The second opinion's gate for a session's work, and the person's presses on it (XAGENT1f): <c>OPINION_GATE</c> reads it;
    /// <c>ASK_OPINION</c> asks a pass the next look starts (<i>Ask now</i>, <i>Try again</i>, <i>Ask again</i>, <i>Ask the same
    /// agent, fresh</i> with <c>sameAgent</c>, or a struck quest's <i>Ask another agent for help</i> with <c>occasion: failure</c>);
    /// <c>OPINION_ANYWAY</c> and <c>OPINION_MYSELF</c> keep the person's answer with their words; <c>STOP_OPINION</c> stops a
    /// reviewer reading.
    /// </summary>
    /// <remarks>
    /// Never Ask Daoris's (§9, D110): asking spends an account at the person's choice, and going on, or reading it themselves, is a
    /// judgement no agent has made. Each answers the gate as it then stands, in <see cref="Opinion"/>'s shape.
    /// </remarks>
    [DriverRoute("OPINION_GATE")]
    [DriverRoute("ASK_OPINION")]
    [DriverRoute("OPINION_ANYWAY")]
    [DriverRoute("OPINION_MYSELF")]
    [DriverRoute("STOP_OPINION")]
    private async Task<object?> OpinionPressAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var presses = new OpinionPresses(_loop.Home, service);
        if (request.Type == "STOP_OPINION")
        {
            var opinion = PayloadHelper.GetRequiredValue<string>(request.Payload, "opinion");
            var stopped = await presses.StopAsync(opinion, _loop.Processes, cancellationToken);
            _loop.Nudge();
            return new { Opinion = opinion, Done = stopped.Done, stopped.Message };
        }

        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var words = Optional(request, "words");
        var pressed = request.Type switch
        {
            "ASK_OPINION" => await presses.AskAsync(
                id, Optional(request, "reviewer"), Flag(request, "sameAgent"), words,
                Optional(request, "occasion") == "failure" ? "failure" : "asked", cancellationToken),
            "OPINION_ANYWAY" => await presses.AnywayAsync(id, words, ReviewDoors.Screen, cancellationToken),
            "OPINION_MYSELF" => await presses.MyselfAsync(id, words, ReviewDoors.Screen, cancellationToken),
            _ => await presses.GateAsync(id, cancellationToken),
        };
        if (request.Type != "OPINION_GATE") _loop.Nudge();
        return new
        {
            Session = id, Done = pressed.Done, pressed.Message,
            Opinion = pressed.Gate is { } gate ? Opinion(gate.Opinion) : null,
            // What the review's *Second opinion* draws (XAGENT1g, design §9): the findings with their answers, beside the state.
            Detail = pressed.Gate is { } read ? OpinionDetail(read.Opinion) : null,
        };
    }

    /// <summary>
    /// The second opinion as the review draws it (XAGENT1g; the second-agent design §9): the reviewer, the candidate and whether it
    /// covers what would land, each finding with its weight, place, consequence, reproduction, sureness and proposal beside the
    /// working session's answer and how the driver read it, the recheck's word on it, what it read and could not, the tier, and
    /// the person's answer at the gate. Null where the level asks no opinion.
    /// </summary>
    /// <remarks>
    /// Read from the gate the driver judged, never from the host again: the findings are the reviewer's claims (§6.6), and what
    /// counts of an answer (a fix's commit checked, an unanswered finding) is the driver's reading. The tier is <c>person</c> where
    /// the person read it themselves, <c>none</c> where no agent could read it, and <c>agent</c> otherwise (§10).
    /// </remarks>
    public static object? OpinionDetail(OpinionGateState gate)
    {
        if (gate.State == OpinionGateStates.None) return null;
        var first = gate.Opinion;
        var disputed = gate.Disputes?.First ?? [];
        var raised = gate.Disputes?.Raised ?? [];
        return new
        {
            Tier = gate.State == OpinionGateStates.Myself ? "person" : first is null ? "none" : "agent",
            // Whether what was read covers what would land (§8.2): commits since it was read say it does not.
            Covers = gate.Since is null ? (bool?)null : gate.Since == 0,
            gate.Passes,
            gate.ToPerson,
            gate.Answered,
            Person = gate.Person is { } word
                ? new { word.Said, At = word.At.ToString("O", System.Globalization.CultureInfo.InvariantCulture), word.Words, word.Door }
                : null,
            First = first is null ? null : Pass(first, finding => new
            {
                Answer = first.AnswerTo(finding.Number) is { } answer
                    ? new { answer.Said, answer.Commit, answer.Evidence, answer.Why }
                    : null,
                // How the driver read the answer as the turn ended (§6.4): what counts, and why it does not count as said.
                Counts = gate.Answers?.Findings.FirstOrDefault(row => row.Finding == finding.Number) is { } row
                    ? new { row.Counts, row.Why, row.Fix }
                    : null,
                Rechecked = gate.Recheck?.Rechecked.GetValueOrDefault(finding.Number),
                Disputed = disputed.Contains(finding.Number),
            }),
            Recheck = gate.Recheck is { } recheck ? Pass(recheck, finding => new
            {
                Answer = (object?)null,
                Counts = (object?)null,
                Rechecked = (string?)null,
                Disputed = raised.Contains(finding.Number),
            }) : null,
        };

        static object Pass<T>(OpinionView view, Func<OpinionFindingView, T> beside) => new
        {
            view.Id, view.Occasion, view.Pass, view.State, view.Why, view.Base, view.Tip, Commits = view.Commits.Count, view.Minutes,
            Reviewer = view.Adapter, view.Product, view.Maker, view.Label, Reviewing = view.Session, view.HandedTo, view.Read, view.Limits,
            Findings = view.Findings?.Select(finding => new
            {
                finding.Number, finding.Weight, finding.Where, finding.Claim, finding.Consequence, finding.Reproduce, finding.Sure,
                finding.Proposal, Beside = beside(finding),
            }).ToArray(),
        };
    }

    /// <summary>
    /// What waits on the person at the second opinion's gate on this machine (XAGENT1g; the second-agent design §9): each work owed
    /// an opinion whose gate holds for the person's answer, oldest first, with the gate as <see cref="Opinion"/> says it. *What needs
    /// you* lists them.
    /// </summary>
    /// <remarks>
    /// <para><b>What waits on the person</b> (<see cref="WaitsOnPerson"/>): a dispute, a required opinion none could be had for, and
    /// commits nobody read where the work lands by itself (D145), since there no press of theirs is coming to answer them.</para>
    ///
    /// <para>Read from the opinions owed (<see cref="OpinionDues"/>), each gate judged as every door judges it: nothing is asked, and
    /// a work whose gate could not be read is left out rather than said wrongly.</para>
    /// </remarks>
    [DriverRoute("OPINION_WAITS")]
    private async Task<object?> OpinionWaitsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var presses = new OpinionPresses(_loop.Home, service);
        var config = DriverConfig.Load(_loop.ConfigPath);
        var waits = new List<object>();
        foreach (var due in new OpinionDues(_loop.Home).Open().OrderBy(due => due.At).DistinctBy(due => due.Session, StringComparer.OrdinalIgnoreCase))
        {
            var pressed = await presses.GateAsync(due.Session, cancellationToken).ConfigureAwait(false);
            if (pressed.Gate?.Opinion is not { } gate) continue;
            // Whether it lands by itself, by the process the gate read (WORKFLOW1f): a named workflow's landing step, or the rule.
            var auto = (pressed.Gate.Process?.Landing ?? LandingRules.Choose(config, due.Repository, due.Workspace)).Rule.AutoAccept;
            if (!WaitsOnPerson(gate, auto)) continue;
            waits.Add(new
            {
                due.Session, due.Quest, due.Repository, due.Workspace,
                Since = due.At.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                Auto = auto,
                Opinion = Opinion(gate),
            });
        }

        return new { Waits = waits };
    }

    /// <summary>
    /// Whether the second opinion's gate waits on the person's answer (the second-agent design §9's *What needs you*): a dispute, a
    /// required opinion none could be had for, or commits nobody read where the work lands by itself (<paramref name="auto"/>).
    /// </summary>
    public static bool WaitsOnPerson(OpinionGateState gate, bool auto) => gate.State switch
    {
        OpinionGateStates.Disputed => true,
        OpinionGateStates.Unavailable => gate.Required,
        OpinionGateStates.CommitsSince => auto,
        _ => false,
    };

    /// <summary>
    /// What the review's gate says beside a landing's plan or press (REVIEWENV1c, design §3.1): its state, the environment and the
    /// sentence while it holds the work, so the page can say <i>Waits for your review in <c>environment</c></i> where <i>Accept…</i>
    /// would be (REVIEWENV1g); null where nothing waits, so a landing no review touches answers as it did.
    /// </summary>
    public static object? Waits(ReviewGateState review) => review.LetsGo
        ? null
        : new { review.State, review.Decision.Environment, review.Decision.Level, Quest = review.Decision.SetUpStep?.Id, Says = review.Says };

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
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line), log: _loop.Log, health: _loop.Health));

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
