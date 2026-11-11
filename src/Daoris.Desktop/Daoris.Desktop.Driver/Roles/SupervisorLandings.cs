using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The supervisor's landing decisions: a <c>review</c> step's Accept or a <c>human</c> approval's Approve or Reject, decided by the holder
/// the admission admits for the run's repository and held for the role's window. At its moment the release asks the admission and the
/// step again, then keeps the decision as the node's word on the run's record, <c>released</c>, which the pass applies through the node's own
/// door. The decision is live while released; a restart empties memory, so the word the record keeps outlives it and the pass drops it.
/// Kept in the <see cref="SupervisorBook"/> under the work session's lock and written beside that session's other decisions.
/// </summary>
internal sealed class SupervisorLandings(SupervisorBook book, IDecisionAdmission admission, MachineLog? log) : ILandingWords
{
    /// <summary>Keep <paramref name="landing"/>, or refuse it keeping nothing; <paramref name="refused"/> is what the step itself already refuses.</summary>
    public Task<SupervisorKept> KeepAsync(SupervisorLanding landing, string repository, string? refused, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(landing.Session, () =>
        {
            var subject = landing.Subject;
            if (book.Landings.Values.FirstOrDefault(each => each.Subject == subject) is { } decided)
            {
                if (refused is null && decided.Do == landing.Do && decided.Generation == landing.Generation
                    && decided.State is SupervisorState.Held or SupervisorState.Left)
                {
                    return Task.FromResult(new SupervisorKept(true, decided.Id, decided.State == SupervisorState.Held ? decided.GoesOnAt : null, null));
                }

                refused ??= SupervisorStates.OwnersAgain(decided.State) ? SupervisorWhy.Overruled : SupervisorWhy.Answered;
            }

            refused ??= book.ClosedWhy(subject);
            var left = landing.Do == SupervisorLandingActs.Hold;
            var admitted = admission.Admit(SupervisorKinds.Landing, new RolePlace(Repository: repository), left ? null : landing.Do);
            refused ??= admitted.Why ?? (admitted.Generation != landing.Generation ? SupervisorWhy.RoleChanged : null);
            refused ??= SupervisorKeeping.Refused(landing.Reasons, left || LandingActs.Contains(landing.Do), null);
            if (refused is not null)
            {
                log?.Info("supervisor.refused", ("why", refused));
                return Task.FromResult(new SupervisorKept(false, null, null, refused));
            }

            var id = Guid.NewGuid().ToString("N")[..12];
            var goesOnAt = left ? now : now + QuestionSupervisorDecisions.WindowOf(admitted);
            var decision = new SupervisorLandingDecision
            {
                Id = id, Session = landing.Session, Run = landing.Run, Node = landing.Node, Attempt = landing.Attempt, Repository = repository, Do = landing.Do,
                Generation = landing.Generation, Words = string.Join(" ", landing.Reasons.Select(reason => reason.Says)), GoesOnAt = goesOnAt,
                State = left ? SupervisorState.Left : SupervisorState.Held, Revision = landing.Revision,
            };
            Directory.CreateDirectory(book.Files.FolderOf(landing.Session));
            AtomicFile.WriteText(book.Files.FileOf(landing.Session, id), Json(id, landing, repository, now, goesOnAt));
            Append(decision, decision.State, now, why: null);
            book.Landings[id] = decision;
            log?.Info(left ? "supervisor.left" : "supervisor.landing", ("session", landing.Session), ("run", landing.Run), ("node", landing.Node), ("do", landing.Do));
            return Task.FromResult(new SupervisorKept(true, id, left ? null : goesOnAt, null));
        }, ct);

    /// <summary>
    /// Each held decision whose moment came, released under its session's lock: the admission asked again, then the step by
    /// <paramref name="line"/>, and only then the word kept on the run's record. A person's word already waiting there supersedes it.
    /// Returns the states the moved decisions reached.
    /// </summary>
    public async Task<IReadOnlyList<SupervisorState>> ReleaseAsync(LandingRelease line, DateTimeOffset now, CancellationToken ct = default)
    {
        var moved = new List<SupervisorState>();
        foreach (var due in book.Landings.Values.Where(each => each.State == SupervisorState.Held && each.GoesOnAt <= now).ToList())
        {
            var reached = await book.UnderAsync(due.Session, async () =>
            {
                if (due.State != SupervisorState.Held) return (SupervisorState?)null;
                var handed = new SupervisorLandingDue(due.Id, due.Session, due.Run, due.Node, due.Attempt, due.Repository, due.Do, due.Generation, due.Words)
                {
                    Revision = due.Revision,
                };
                var admitted = admission.Admit(SupervisorKinds.Landing, new RolePlace(Repository: due.Repository), due.Do);
                var why = admitted.Why is null && admitted.Generation == due.Generation
                    ? await line.Why(handed).ConfigureAwait(false)
                    : SupervisorWhy.RoleChanged;
                // Live before its word is on the record: a pass between the two would otherwise drop the word as stale, and the accept with it.
                if (why is null)
                {
                    Append(due, SupervisorState.Released, now, why: null);
                    why = line.Word(handed);
                }

                var to = why is null ? SupervisorState.Released : why == SupervisorWhy.Worded ? SupervisorState.Superseded : SupervisorState.Stale;
                if (to != SupervisorState.Released)
                {
                    Append(due, to, now, to == SupervisorState.Stale ? why : null);
                    Close(due, to);
                }
                log?.Info(to == SupervisorState.Released ? "supervisor.released" : $"supervisor.{SupervisorStates.Spell(to)}", ("session", due.Session), ("run", due.Run), ("node", due.Node));
                return to;
            }, ct).ConfigureAwait(false);
            if (reached is { } state) moved.Add(state);
        }

        return moved;
    }

    /// <summary>The owner's <i>Overrule</i> of a held decision: the step stays theirs. Null where <paramref name="decision"/> is no landing decision of the session.</summary>
    public Task<SupervisorState?> OverruleAsync(string session, string decision, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(session, () =>
        {
            if (!book.Landings.TryGetValue(decision, out var held) || held.Session != session) return Task.FromResult<SupervisorState?>(null);
            if (held.State == SupervisorState.Held)
            {
                Append(held, SupervisorState.Overruled, now, why: null);
                Close(held, SupervisorState.Overruled);
            }

            return Task.FromResult<SupervisorState?>(held.State);
        }, ct);

    /// <summary>Whether <paramref name="subject"/> waits for the holder: no decision on it, and it is not closed to the person.</summary>
    public bool Waits(SupervisorSubject subject) =>
        !book.Landings.Values.Any(each => each.Subject == subject) && !book.Closed.ContainsKey(subject);

    /// <summary>The soonest moment a held decision is due.</summary>
    public DateTimeOffset? NextGoesOn() =>
        book.Landings.Values.Where(each => each.State == SupervisorState.Held).Select(each => (DateTimeOffset?)each.GoesOnAt).Min();

    public bool Live(string decision) => book.Landings.TryGetValue(decision, out var held) && held.State == SupervisorState.Released;

    public void Settled(string decision, SupervisorState to, string? why)
    {
        if (!book.Landings.TryGetValue(decision, out var held) || held.State != SupervisorState.Released) return;
        Append(held, to, DateTimeOffset.UtcNow, why);
        if (to != SupervisorState.WentOn) Close(held, to);
        log?.Info($"supervisor.{SupervisorStates.Spell(to)}", ("session", held.Session), ("run", held.Run), ("node", held.Node));
    }

    /// <summary>A session's landing decisions as the page reads them (<see cref="SupervisorLandingView"/>), memory's state over the file's.</summary>
    public IReadOnlyList<SupervisorLandingView> LandingsOf(string session)
    {
        if (!SessionEventText.IsLocalId(session) || !Directory.Exists(book.Files.FolderOf(session))) return [];
        var views = new List<SupervisorLandingView>();
        foreach (var file in Directory.EnumerateFiles(book.Files.FolderOf(session), "*.json"))
        {
            if (SupervisorLandingView.Read(book.Files, session, Path.GetFileNameWithoutExtension(file)) is not { } read) continue;
            views.Add(book.Landings.TryGetValue(read.Id, out var live) ? read.Over(live) : read);
        }

        return [.. views.OrderBy(view => view.KeptAt).ThenBy(view => view.Id, StringComparer.Ordinal)];
    }

    /// <summary>Whether a decision's word stands applied on its run's record, which the start's sweep reads before it closes the decision.</summary>
    public Func<SupervisorLandingView, bool>? Landed { get; set; }

    public bool Applied(SupervisorLandingView view) => Landed?.Invoke(view) == true;

    private static readonly string[] LandingActs = [SupervisorLandingActs.Accept, SupervisorLandingActs.Approve, SupervisorLandingActs.Reject];

    // A closed subject is the person's, as any decision's that did not go on.
    private void Close(SupervisorLandingDecision decision, SupervisorState to)
    {
        if (SupervisorStates.OwnersAgain(to)) book.Closed[decision.Subject] = to;
    }

    private void Append(SupervisorLandingDecision decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        (decision.State, decision.StateAt, decision.Why) = (to, at, why);
        SupervisorDecisionFiles.TryAppend(book.Files.StatesOf(decision.Session, decision.Id), SupervisorDecisionFiles.StateLine(to, at, why, null));
    }

    // The decision's file: its holder, the step, the act, the reasons, and when it goes on.
    private static string Json(string id, SupervisorLanding landing, string repository, DateTimeOffset now, DateTimeOffset goesOnAt) => new JsonObject
    {
        ["id"] = id,
        ["kind"] = SupervisorKinds.Landing,
        ["subject"] = new JsonObject { ["session"] = landing.Session, ["landing"] = landing.Subject.Id },
        ["session"] = landing.Session,
        ["holder"] = SupervisorHolders.Node(landing.Holder),
        ["generation"] = landing.Generation,
        ["landing"] = new JsonObject
        {
            ["run"] = landing.Run, ["node"] = landing.Node, ["attempt"] = landing.Attempt, ["repository"] = repository, ["do"] = landing.Do,
            ["revision"] = landing.Revision,
        },
        ["bound"] = landing.Do == SupervisorLandingActs.Hold ? landing.Bound : null,
        ["reasons"] = new JsonArray([.. landing.Reasons.Select(reason => (JsonNode)new JsonObject { ["says"] = reason.Says, ["source"] = reason.Source })]),
        ["keptAt"] = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["goesOnAt"] = goesOnAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
