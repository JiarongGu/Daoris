namespace Daoris.Driver;

/// <summary>
/// The one door a request to integrate a repository's line comes through (INTEGRATE1e part e1; the integration design §4.6, D215): who
/// asked, under the rule and policy as they stand, for which branch at which frozen tip. Built and tested, and called from no production
/// path yet: the owner's card (row h), the steward's act (row i) and the <c>integrate</c> node's start (row n) are its callers to come. Its
/// requests are kept in the desktop store's integration family (part e2, <see cref="IntegrationStore"/>).
/// </summary>
/// <remarks>
/// <para><b>A unit's request comes only from that unit's own landing.</b> So no request passes a session's stopped <c>handed</c>, a required
/// opinion or a review. This part builds two of the door's routes. <see cref="IntegrationRoutes.Recorded"/> takes an existing recorded
/// landing, made by the owner's press or a done, only on its record, its exact tip, its gate proof read as the record keeps it, and no
/// double. <see cref="IntegrationRoutes.Records"/> takes a records branch, no unit's and no run's, and refuses a session's branch and a
/// landing's there. The landing's own route, read from its record's provenance (<see cref="LandedIntegrate"/>, §17.4), is row n's.</para>
///
/// <para><b>It reads, and writes nothing through a tree.</b> Each git it asks is a read of a ref or of ancestry in the repository's
/// checkout, so a landing whose tree LAND3's tidy took needs no tree. The steps that write through a tree (the form, the replay) pass
/// <see cref="TreeBinding.RefusalAsync"/> first, in the parts that build them.</para>
///
/// <para><b>The gate proof is read, never judged again.</b> The review, the second opinion and the workflow version that let the landing go,
/// and who accepted it, are copied from the record as it holds them for its tip: the Accept it records is done. Only a record that does not
/// say who accepted that tip, one from before that was kept, holds no proof to read.</para>
/// </remarks>
public sealed class IntegrationRequests(IIntegrationRecords records, IIntegrationRequestStore store, Func<DriverConfig> config)
{
    /// <summary>
    /// Admit <paramref name="ask"/>, or refuse it in a sentence that says why and that nothing was requested. An admitted request is kept in
    /// the store, where none is open for its branch.
    /// </summary>
    public async Task<IntegrationAdmission> AdmitAsync(IntegrationAsk ask, CancellationToken ct = default)
    {
        var settings = config();
        var landing = LandingRules.Effective(settings, ask.Repository, ask.Workspace);
        if (ask.Route == IntegrationRoutes.Node || ask.Requester.Door == IntegrationDoors.Node) return await NodeAsync(ask, landing, settings, ct).ConfigureAwait(false);
        if (landing.Rule is not { Form: LandingForm.Branch, Integrate: { } integrate })
        {
            return Refuse(IntegrationRefusals.NoIntegrate, NoIntegrateSaid(ask.Repository));
        }

        var policy = integrate.Resolved();
        if (DoorRefusal(ask, policy) is { } closed) return Refuse(IntegrationRefusals.Door, closed);
        return ask.Route switch
        {
            IntegrationRoutes.Recorded => await RecordedAsync(ask, landing, _ => (policy, null), IntegrationRoutes.Recorded, settings, ct).ConfigureAwait(false),
            IntegrationRoutes.Records => await RecordsAsync(ask, landing, policy, settings, ct).ConfigureAwait(false),
            _ => Refuse(IntegrationRefusals.Door, $"`{ask.Route}` is no route this door knows: nothing was requested."),
        };
    }

    private static string NoIntegrateSaid(string repository) =>
        $"`{repository}`'s landing rule carries no `integrate`, so nothing was requested: its landed branches stay the result, as always.";

    /// <summary>
    /// The unit's own landing (INTEGRATE1n part n2; §17.4, F40, C30): the <c>integrate</c> node's start, by Daoris, for the unit and the tip it
    /// froze. The landing's record decides whether its acceptance was the request, by the provenance it keeps for that tip and who accepted it;
    /// then it is admitted as a recorded landing is, on its record, exact tip, gate proof and no double.
    /// </summary>
    private async Task<IntegrationAdmission> NodeAsync(IntegrationAsk ask, Landing landing, DriverConfig settings, CancellationToken ct)
    {
        if (ask.Route != IntegrationRoutes.Node)
        {
            return Refuse(IntegrationRefusals.Door, "the `integrate` node asks only through its own route: nothing was requested.");
        }

        if (ask.Requester != IntegrationRequester.NodeStart)
        {
            return Refuse(IntegrationRefusals.Door, "the `integrate` node's route is Daoris's own: nothing was requested.");
        }

        if (ask.Unit is not { Length: > 0 } || ask.Tip is not { Length: > 0 })
        {
            return Refuse(IntegrationRefusals.Door, "a node's ask names its unit and the tip it froze, and this one does not: nothing was requested.");
        }

        return await RecordedAsync(ask, landing, entry => NodeJudgement(ask, entry, landing), IntegrationRoutes.Node, settings, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the acceptance at <paramref name="entry"/>'s tip was the request (§17.4), by the provenance its newest advance, else its own,
    /// keeps and who accepted it, and the policy the request then freezes: the request the acceptance stood under (the node's own where the
    /// version's step asks), the rest the rule's. Nothing is read from git, and a record that says neither is no request.
    /// </summary>
    internal static (LandingIntegrate? Policy, IntegrationAdmission? Refused) NodeJudgement(IntegrationAsk ask, LandedBranch entry, Landing landing)
    {
        var (by, provenance) = entry.Advances.Count > 0 ? (entry.Advances[^1].AcceptedBy, entry.Advances[^1].Integrate) : (entry.AcceptedBy, entry.Integrate);
        if (by is not (AcceptedBy.Person or AcceptedBy.Auto) || provenance is null || !LandingIntegrate.Requests.Contains(provenance.Request))
        {
            return (null, Refuse(IntegrationRefusals.NoRequest,
                $"`{ask.Branch}`'s landing record does not say it was accepted where `integrate` stood, or what it asked, so its landing is no request: "
                + "it waits for the owner's card, and under `request: done` for the come-up sweep. Nothing was requested."));
        }

        // A rule's step needs the rule to carry it still; a version's own step is the unit's process whatever the rule says (§4.1).
        var rule = landing.Rule is { Form: LandingForm.Branch, Integrate: { } integrate } ? integrate : null;
        if (provenance.Source != LandedIntegrate.Version && rule is null) return (null, Refuse(IntegrationRefusals.NoIntegrate, NoIntegrateSaid(ask.Repository)));
        var request = provenance.Request;
        if (request == LandingIntegrate.Steward)
        {
            return (null, Refuse(IntegrationRefusals.AwaitsSteward,
                $"`{ask.Branch}` was accepted under `request: steward`, and under `request: steward` no landing is the request: it waits for the steward's "
                + "`request` act, or the owner's card. Nothing was requested."));
        }

        if (request == LandingIntegrate.Owner && by != AcceptedBy.Person)
        {
            return (null, Refuse(IntegrationRefusals.AwaitsOwner,
                $"`{ask.Branch}` was landed automatically under `request: owner`, so its landing is no request: it waits for the owner's card. "
                + "Nothing was requested."));
        }

        var resolved = (rule ?? new LandingIntegrate()).Resolved();
        return (resolved with { Request = ask.Request ?? request, Conflicts = ask.Conflicts ?? resolved.Conflicts }, null);
    }

    /// <summary>
    /// The come-up sweep (§4.6): under <c>request: done</c>, every standing landing of <paramref name="repository"/> asked through the
    /// recorded-landing door once, by Daoris, in the order they landed; what each answered comes back. Under any other <c>request</c>, or
    /// where the rule carries no <c>integrate</c>, it asks nothing. Run once at each come-up and once when the rule is set, by callers to come.
    /// </summary>
    public async Task<IReadOnlyList<IntegrationAdmission>> SweepAsync(string repository, string? workspace, string root, CancellationToken ct = default)
    {
        var landing = LandingRules.Effective(config(), repository, workspace);
        if (landing.Rule is not { Form: LandingForm.Branch, Integrate: { } integrate } || integrate.Resolved().Request != LandingIntegrate.Done) return [];
        var answers = new List<IntegrationAdmission>();
        var standing = records.Landings()
            .Where(entry => entry.GoneAt is null && string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Branch)
            .Distinct(StringComparer.Ordinal);
        foreach (var branch in standing)
        {
            answers.Add(await AdmitAsync(new IntegrationAsk(repository, workspace, root, branch, IntegrationRoutes.Recorded, IntegrationRequester.Sweep), ct)
                .ConfigureAwait(false));
        }

        return answers;
    }

    /// <summary>
    /// Why <paramref name="ask"/>'s requester may not ask under <paramref name="policy"/>, or null: the owner's card under every
    /// <c>request</c>, the steward's act under <c>steward</c> naming its act and generation, Daoris's sweep under <c>done</c> and for a
    /// recorded landing alone.
    /// </summary>
    private static string? DoorRefusal(IntegrationAsk ask, LandingIntegrate policy)
    {
        var requester = ask.Requester;
        return requester.Kind switch
        {
            IntegrationRequester.Person => null,
            IntegrationRequester.Steward when policy.Request != LandingIntegrate.Steward =>
                $"the steward requests `{ask.Repository}`'s work only where its rule says `request: steward`, and it says `request: {policy.Request}`: "
                + "nothing was requested.",
            IntegrationRequester.Steward when string.IsNullOrWhiteSpace(requester.Act) || requester.Generation is null =>
                "a steward's request names its act and its appointment's generation, and this one does not: nothing was requested.",
            IntegrationRequester.Steward => null,
            IntegrationRequester.Daoris when ask.Route != IntegrationRoutes.Recorded =>
                $"Daoris requests no records branch: `{ask.Branch}` is requested by the owner's card, or by the steward's act where the rule says "
                + "`request: steward`. Nothing was requested.",
            IntegrationRequester.Daoris when policy.Request != LandingIntegrate.Done =>
                $"the come-up sweep requests `{ask.Repository}`'s landings only where its rule says `request: done`, and it says "
                + $"`request: {policy.Request}`: nothing was requested.",
            IntegrationRequester.Daoris => null,
            _ => $"`{requester.Kind}` is no requester this door knows: nothing was requested.",
        };
    }

    /// <summary>
    /// The recorded-landing door (§4.6, F32 and C27): a standing entry of the machine's landings, bound to the unit, session and quest it
    /// records; its exact tip, the entry's or its newest advance's, at which the branch must stand; its gate proof as it recorded it; and no
    /// request open for it, nor the line holding it.
    /// </summary>
    private async Task<IntegrationAdmission> RecordedAsync(
        IntegrationAsk ask, Landing landing, Func<LandedBranch, (LandingIntegrate? Policy, IntegrationAdmission? Refused)> judge, string route,
        DriverConfig settings, CancellationToken ct)
    {
        var named = records.Landings().Where(entry => SessionBranches.SameBranch(entry.Repository, entry.Branch, ask.Repository, ask.Branch)).ToList();
        if (named.LastOrDefault(entry => entry.GoneAt is null) is not { } entry)
        {
            return Refuse(IntegrationRefusals.NoRecord, named.Count > 0
                ? $"`{ask.Branch}`'s landing on this machine is a trace: its branch went, or is no longer the landing's, so nothing was requested."
                : $"`{ask.Branch}` is no landing this machine recorded in `{ask.Repository}`, so it is not requested as one: a unit's work comes only "
                  + "through its own landing or its recorded landing, and a branch no landing made is a records branch. Nothing was requested.");
        }

        // The node's unit is the node record's own (a landed branch's, or a run's), which the landing's session units never name: the entry
        // is found by its branch and its tip, and the unit is the key the request is kept under.
        if (route != IntegrationRoutes.Node && ask.Unit is { } unit && !entry.Units.Contains(unit, StringComparer.Ordinal))
        {
            return Refuse(IntegrationRefusals.NotUnit,
                $"`{ask.Branch}`'s landing carries no unit `{unit}`, so it is not that unit's to request: nothing was requested.");
        }

        // The route's own judgement of the landing, before git is asked: the node's reads the acceptance's provenance (§17.4).
        var (judged, unjudged) = judge(entry);
        if (unjudged is not null) return unjudged;
        var policy = judged!;
        var tip = entry.Tip;
        if (ask.Tip is { } asked && !SameCommit(asked, tip))
        {
            return Refuse(IntegrationRefusals.Moved,
                $"the landing record holds `{ask.Branch}` at {Short(tip)}, not {Short(asked)}: nothing was requested, and a revision is its unit's "
                + "own next landing.");
        }

        if (ProofOf(entry) is not { } proof)
        {
            return Refuse(IntegrationRefusals.NoProof,
                $"`{ask.Branch}`'s record does not say who accepted it at {Short(tip)}, a landing recorded before Daoris kept that, so it holds no "
                + "gate proof to read: nothing was requested.");
        }

        if (OpenRefusal(ask.Repository, ask.Branch, tip) is { } open) return open;
        if (await StandsRefusalAsync(ask, tip, moved: stands =>
                $"`{ask.Branch}` moved since its landing: it stands at {Short(stands)}, and its record holds {Short(tip)}. Nothing was requested: "
                + "a commit made after its landing is its unit's own next landing, requested by that.", ct).ConfigureAwait(false) is { } unstood)
        {
            return unstood;
        }

        var (line, unlined) = await LineHoldsAsync(ask, tip, policy, settings, ct).ConfigureAwait(false);
        if (unlined is not null) return unlined;

        var work = entry.Work()[^1];
        var request = new IntegrationRequest(NewId(), ask.Repository, entry.Workspace, ask.Branch, tip, route, line!,
            ask.Requester, policy, DateTimeOffset.UtcNow)
        {
            Unit = ask.Unit ?? work.Unit,
            Session = work.Session,
            Quest = work.Quest ?? entry.Quest,
            LandedAt = entry.LandedAt,
            Proof = proof,
            RuleSource = landing.Source,
        };
        return Kept(request, $"`{ask.Branch}` at {Short(tip)} is requested for `{line}` from its landing's record, {By(ask.Requester)}.");
    }

    /// <summary>
    /// The records-branch door (§4.6, D156): a branch no unit's and no run's, requested by the owner's card or the steward's act, at the
    /// commit it stands at, carrying no workflow gate. A session's branch, by its name or its record, and a branch any landing of the
    /// repository recorded, standing or a trace, are refused here: a unit comes only through its own landing or its recorded landing.
    /// </summary>
    private async Task<IntegrationAdmission> RecordsAsync(
        IntegrationAsk ask, Landing landing, LandingIntegrate policy, DriverConfig settings, CancellationToken ct)
    {
        if (ask.Branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal)
            || records.SessionBranches().Any(entry => SessionBranches.SameBranch(entry.Repository, entry.Branch, ask.Repository, ask.Branch)))
        {
            return Refuse(IntegrationRefusals.SessionBranch,
                $"`{ask.Branch}` is a session's branch: a session's work is requested only through its own landing or its recorded landing, never as "
                + "a records branch, so nothing was requested.");
        }

        var landed = records.Landings().Where(entry => SessionBranches.SameBranch(entry.Repository, entry.Branch, ask.Repository, ask.Branch)).ToList();
        if (landed.Count > 0)
        {
            return Refuse(IntegrationRefusals.LandingBranch, landed.Any(entry => entry.GoneAt is null)
                ? $"`{ask.Branch}` is a landing this machine recorded: it is requested as that landing, from its record, never as a records branch. "
                  + "Nothing was requested."
                : $"`{ask.Branch}` was a landing this machine recorded, and its record no longer holds it, so it is not requested as a records branch "
                  + "either: nothing was requested.");
        }

        // The line as set, asked before git; the checkout's guess is asked with the line's commit below, and judged again there.
        var set = policy.Line ?? CanonicalLine.Choose(settings, ask.Repository, ask.Workspace, guess: null).Branch;
        if (set is not null && string.Equals(set, ask.Branch, StringComparison.Ordinal)) return IsLine(ask.Branch);

        if (OpenRefusal(ask.Repository, ask.Branch, ask.Tip) is { } open) return open;
        var (stands, unread) = await StandingAsync(ask, ct).ConfigureAwait(false);
        if (unread is not null) return unread;
        if (ask.Tip is { } asked && !SameCommit(asked, stands!))
        {
            return Refuse(IntegrationRefusals.Moved,
                $"`{ask.Branch}` stands at {Short(stands!)}, not {Short(asked)}: nothing was requested; ask again at the commit it stands at.");
        }

        var (line, unlined) = await LineHoldsAsync(ask, stands!, policy, settings, ct).ConfigureAwait(false);
        if (string.Equals(line, ask.Branch, StringComparison.Ordinal)) return IsLine(ask.Branch);
        if (unlined is not null) return unlined;

        var request = new IntegrationRequest(NewId(), ask.Repository, RemoteTarget.Workspace(ask.Workspace), ask.Branch, stands!,
            IntegrationRoutes.Records, line!, ask.Requester, policy, DateTimeOffset.UtcNow)
        {
            RuleSource = landing.Source,
        };
        return Kept(request,
            $"`{ask.Branch}` at {Short(stands!)} is requested for `{line}` as a records branch, {By(ask.Requester)}: it carries no workflow gate.");
    }

    /// <summary>
    /// The gate proof the record keeps for its tip: its newest advance's where it moved on (LAND2c), else its landing's own; null where that
    /// acceptance's record does not say who accepted it.
    /// </summary>
    private static IntegrationProof? ProofOf(LandedBranch entry)
    {
        if (entry.Advances.Count > 0)
        {
            var advance = entry.Advances[^1];
            return advance.AcceptedBy is { } by ? new IntegrationProof(by) { Review = advance.Review, Opinion = advance.Opinion, Workflow = advance.Workflow } : null;
        }

        return entry.AcceptedBy is { } accepted
            ? new IntegrationProof(accepted) { Review = entry.Review, Opinion = entry.Opinion, Workflow = entry.Workflow }
            : null;
    }

    /// <summary>No double: the request already open for the branch, at <paramref name="tip"/> or another, refuses; null where none is.</summary>
    private IntegrationAdmission? OpenRefusal(string repository, string branch, string? tip) =>
        store.OpenFor(repository, branch) is { } open ? Opened(open, branch, tip) : null;

    private static IntegrationAdmission Opened(IntegrationRequest open, string branch, string? tip) =>
        Refuse(IntegrationRefusals.Open,
            $"a request is already open for `{branch}` at {Short(open.Tip)}, made {By(open.Requester)}: nothing more was requested."
            + (tip is not null && !SameCommit(open.Tip, tip) ? " This revision waits for its outcome." : ""));

    /// <summary>The branch at <paramref name="tip"/> in the checkout, or the refusal: moved, gone on git's own word, or unread.</summary>
    private static async Task<IntegrationAdmission?> StandsRefusalAsync(IntegrationAsk ask, string tip, Func<string, string> moved, CancellationToken ct)
    {
        var (stands, unread) = await StandingAsync(ask, ct).ConfigureAwait(false);
        if (unread is not null) return unread;
        return SameCommit(stands!, tip) ? null : Refuse(IntegrationRefusals.Moved, moved(stands!));
    }

    /// <summary>
    /// The commit the branch stands at in the checkout, or the refusal: gone only on git's own <i>missing</i>, any other failed read unread
    /// (<c>git-ref-absence</c>).
    /// </summary>
    private static async Task<(string? Stands, IntegrationAdmission? Refused)> StandingAsync(IntegrationAsk ask, CancellationToken ct)
    {
        var reference = $"refs/heads/{ask.Branch}";
        if (await TreeRefs.CommitOfAsync(ask.Root, reference, ct).ConfigureAwait(false) is { } stands) return (stands, null);
        var (missing, said) = await ContentHolds.MissingAsync(ask.Root, reference, ct).ConfigureAwait(false);
        return (null, missing
            ? Refuse(IntegrationRefusals.Gone, ask.Route == IntegrationRoutes.Recorded
                ? $"`{ask.Branch}` is no branch in `{ask.Repository}` now, though its landing's record stands: nothing was requested."
                : $"`{ask.Branch}` is no branch in `{ask.Repository}`: nothing was requested.")
            : Refuse(IntegrationRefusals.Unread, $"git could not read `{ask.Branch}` in `{ask.Repository}` ({said}), so nothing was requested; ask again."));
    }

    /// <summary>
    /// The line the request integrates into, the rule's own or the repository's, read once at its commit, and whether it holds
    /// <paramref name="tip"/> by ancestry: held refuses, as does a line with no branch here or an answer git could not give.
    /// </summary>
    private static async Task<(string? Line, IntegrationAdmission? Refused)> LineHoldsAsync(
        IntegrationAsk ask, string tip, LandingIntegrate policy, DriverConfig settings, CancellationToken ct)
    {
        var line = policy.Line ?? (await CanonicalLine.ResolveAsync(ask.Root, ask.Repository, ask.Workspace, settings, ct).ConfigureAwait(false)).Branch;
        if (line is null)
        {
            return (null, Refuse(IntegrationRefusals.NoLine,
                $"`{ask.Repository}` has no line to integrate into here (no line is set and git names none), so nothing was requested."));
        }

        if (await TreeRefs.CommitOfAsync(ask.Root, $"refs/heads/{line}", ct).ConfigureAwait(false) is not { } at)
        {
            return (line, Refuse(IntegrationRefusals.NoLine,
                $"the line `{line}` has no branch in `{ask.Repository}`'s checkout, so nothing was requested."));
        }

        var (code, _, err) = await WorkingTree.GitAsync(ask.Root, ["merge-base", "--is-ancestor", tip, at], ct).ConfigureAwait(false);
        return code switch
        {
            0 => (line, Refuse(IntegrationRefusals.Held, $"the line `{line}` already holds `{ask.Branch}` at {Short(tip)}: nothing was requested.")),
            1 => (line, null),
            _ => (line, Refuse(IntegrationRefusals.Unread,
                $"git could not say whether the line `{line}` holds `{ask.Branch}` ({ContentHolds.Said(err)}), so nothing was requested; ask again.")),
        };
    }

    /// <summary>Kept where none is open for its branch, judged by the store itself, so two doors at once keep one.</summary>
    private IntegrationAdmission Kept(IntegrationRequest request, string message) =>
        store.Add(request) is { } open ? Opened(open, request.Branch, request.Tip) : new IntegrationAdmission(true, message) { Request = request };

    private static IntegrationAdmission Refuse(string refusal, string message) => new(false, message) { Refusal = refusal };

    private static IntegrationAdmission IsLine(string branch) =>
        Refuse(IntegrationRefusals.IsLine, $"`{branch}` is the line itself: there is nothing to request.");

    /// <summary>Who asked, in words: the person's card, Daoris's come-up sweep, or the steward's act by its id.</summary>
    private static string By(IntegrationRequester requester) => requester.Kind switch
    {
        IntegrationRequester.Person => "by the person's card",
        IntegrationRequester.Daoris when requester.Door == IntegrationDoors.Node => "by the unit's `integrate` step",
        IntegrationRequester.Daoris => "by Daoris's come-up sweep",
        IntegrationRequester.Steward => $"by the steward's act `{requester.Act}`",
        _ => $"by `{requester.Kind}`",
    };

    private static bool SameCommit(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);

    private static string NewId() => $"request-{Guid.NewGuid():N}";
}
