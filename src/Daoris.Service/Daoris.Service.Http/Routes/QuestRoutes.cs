using Daoris.Contracts.Quests;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The quest doors: the reads, the publish and the respond on both hosts, and a local host's person and driver doors. Each judges
/// through the exchange the connector's tools use, so the doors cannot drift on who may be addressed or what a move requires.
/// </summary>
public sealed class QuestRoutes(HostWiring host) : IRouteModule
{
    private readonly QuestAnswers _answers = new(host);

    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        routes.Map<ListQuests, QuestResponse[]>(ListAsync);
        routes.Map<ReadQuestMoves, QuestMovesResponse>(async (request, _, ct) =>
            QuestAnswers.MovesOf(request.Id.TrimStart('#'), QuestMoves.Read(await Service.Quests.HistoryAsync(request.Id.TrimStart('#'), ct))));
        routes.Map<PublishQuest, QuestActionResponse>(PublishAsync);
        routes.Map<RespondQuest, QuestActionResponse>(RespondAsync);

        // A person's and the driver's doors are a local host's: said or read on their own machine, travelling from there as
        // operations. No connector tool reaches them: only the person says yes, reopens, goes on or brings a take forward, and an
        // agent's result is its respond.
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<AcceptQuest, QuestActionResponse>(async (request, http, ct) =>
            await StateAsync(http, await Service.Exchange.AcceptAsync(request.Id, DateTimeOffset.UtcNow, ct), ct));
        routes.Map<MarkQuestDone, QuestActionResponse>(async (request, http, ct) =>
            await StateAsync(http, await Service.Exchange.PersonDoneAsync(request.Id, request.Note, DateTimeOffset.UtcNow, ct), ct));
        routes.Map<ReopenQuest, QuestActionResponse>(ReopenAsync);
        routes.Map<ContinueQuest, QuestActionResponse>(async (request, http, ct) =>
            await StateAsync(http, await Service.Exchange.ContinueAsync(request.Id, DateTimeOffset.UtcNow, ct), ct));
        routes.Map<ForwardConflict, QuestActionResponse>(ForwardAsync);
        routes.Map<PostQuestEvidence, QuestActionResponse>(EvidenceAsync);
        routes.Map<BindQuest, QuestActionResponse>(async (request, http, ct) => await StateAsync(
            http,
            await Service.Exchange.BindAsync(request.Id, request.Graph, DateTimeOffset.UtcNow, ct, atTake: request.AtTake == true, session: request.Session),
            ct));
    }

    private async Task<Answer<QuestResponse[]>> ListAsync(ListQuests request, HttpContext http, CancellationToken ct)
    {
        var quests = await Service.Quests.ListAsync(request.Repository, request.IncludeClosed ?? false, request.Workspace, ct);
        var deletable = await _answers.DeletableAsync(quests, ct);
        var takes = await Service.Quests.ReadTakesAsync(quests, ct);
        var local = host.MachineLocal(http);
        var here = _answers.HereFor(http);
        return quests.Select(q => QuestAnswers.ToQuest(q, Service.Files, local, deletable.Contains(q.Id), takes.GetValueOrDefault(q.Id), here)).ToArray();
    }

    private async Task<Answer<QuestActionResponse>> PublishAsync(PublishQuest request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.From) || string.IsNullOrWhiteSpace(request.To)
            || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return Refusal.BadRequest("from, to, title and body are all required");
        }

        // A local host is on the machine that has the file, so a file comes with its content and is kept here. A shared host keeps
        // names only: content reaching it is bytes leaving a machine, refused rather than dropped, because dropped looks kept.
        var uploads = new List<QuestUpload>();
        var named = new List<QuestAttachment>();
        foreach (var file in request.Attachments ?? [])
        {
            if (string.IsNullOrWhiteSpace(file.Name)) return Refusal.BadRequest("every attachment needs a name");

            if (host.Mode == ServiceMode.Shared)
            {
                if (file.Content is not null)
                {
                    return Refusal.BadRequest(
                        $"`{file.Name}` arrived with its content, and a shared deployment keeps names, never bytes "
                        + "(D65 §2) — the file stays on the machine that has it, which sends its name and hash.");
                }

                if (file.Sha256 is null || file.Bytes is null)
                {
                    return Refusal.BadRequest(
                        $"`{file.Name}` needs its sha256 and its size — by name is the only way a file reaches a shared deployment.");
                }

                named.Add(new QuestAttachment(file.Name, file.Sha256, file.Bytes.Value));
            }
            else
            {
                if (file.Content is null)
                {
                    return Refusal.BadRequest(
                        $"`{file.Name}` arrived without its content — this host is on the machine that keeps a quest's "
                        + "files, so it takes them whole.");
                }

                uploads.Add(new QuestUpload(file.Name, file.Content));
            }
        }

        // Every field is judged by the exchange with every other door's. An agent's publish, keyless on a host that holds a key,
        // has its review choice judged as a connector's is.
        var outcome = await Service.Exchange.PublishAsync(
            new QuestAsk(request.From, request.To, request.Title, request.Body)
            {
                Links = request.Links ?? [],
                Uploads = uploads,
                Named = named,
                Then = QuestAnswers.StepsOf(request.Then),
                Requirements = QuestAnswers.RequirementsOf(request.Requirements),
                Short = request.Short,
                Weight = request.Weight,
                Paths = request.Paths ?? [],
                Request = request.Request,
                Review = QuestAnswers.ReviewOf(request.Review),
                ByAgent = host.Gate.IsAgent(http),
            },
            DateTimeOffset.UtcNow, ct);

        return outcome.Refusal switch
        {
            QuestPublishRefusal.None => await _answers.MovedAsync(http, outcome.Quest!, outcome.Message, ct),
            // Two circles never joined, or a quest this machine forgot that the remote holds closed: a state, not a malformed ask.
            QuestPublishRefusal.CrossWorkspace or QuestPublishRefusal.Cleared => Refusal.Conflict(outcome.Message),
            _ => Refusal.BadRequest(outcome.Message),
        };
    }

    private async Task<Answer<QuestActionResponse>> RespondAsync(RespondQuest request, HttpContext http, CancellationToken ct)
    {
        // `whileOpen` is an abandon's decline, sent on the person's press: the person's form of an agent's door, which wants the key
        // where the host holds one.
        if (host.Gate.Refused(http, PersonDoors.Respond, form: request.WhileOpen == true) is { } refused) return refused;

        // A number left out arrives as 0 and a half left out blank, each refused by the exchange naming which.
        var answers = (request.Answers ?? []).Select(a => new QuestAnswer(a?.Requirement ?? 0, a?.Met, a?.Departed, a?.Quote)).ToList();
        var outcome = await Service.Exchange.RespondAsync(
            request.Id, request.Action ?? "", request.Reason, DateTimeOffset.UtcNow, ct, on: request.On, answers: answers,
            whileOpen: request.WhileOpen == true);

        return outcome.Refusal switch
        {
            QuestRespondRefusal.None => await _answers.MovedAsync(http, outcome.Quest!, outcome.Message, ct),
            QuestRespondRefusal.NotFound => Refusal.NotFound(outcome.Message),
            // A refused transition is a state: the losing side of a race stands down, as does a step whose source was reopened.
            QuestRespondRefusal.AlreadyTaken or QuestRespondRefusal.Closed or QuestRespondRefusal.SourceReopened => Refusal.Conflict(outcome.Message),
            _ => Refusal.BadRequest(outcome.Message),
        };
    }

    private async Task<Answer<QuestActionResponse>> ReopenAsync(ReopenQuest request, HttpContext http, CancellationToken ct)
    {
        if (request.Submission is not { Machine: { Length: > 0 } machine, Sequence: > 0 and var sequence })
        {
            return Refusal.BadRequest(
                "A reopen names the submission it reopens: `submission` with its `machine` and `sequence`, as the quest answers it.");
        }

        return await StateAsync(
            http, await Service.Exchange.ReopenAsync(request.Id, new QuestOperationRef(machine, sequence), request.Note, DateTimeOffset.UtcNow, ct), ct);
    }

    private async Task<Answer<QuestActionResponse>> ForwardAsync(ForwardConflict request, HttpContext http, CancellationToken ct)
    {
        if (request is not { Machine: { Length: > 0 } machine, Sequence: > 0 and var sequence })
        {
            return Refusal.BadRequest("A bring-forward names the conflict it resolves: its `machine` and `sequence`, as the quest answers it.");
        }

        return await StateAsync(
            http, await Service.Exchange.BringForwardAsync(request.Id, new QuestOperationRef(machine, sequence), DateTimeOffset.UtcNow, ct), ct);
    }

    private async Task<Answer<QuestActionResponse>> EvidenceAsync(PostQuestEvidence request, HttpContext http, CancellationToken ct)
    {
        // A field left out arrives blank and is refused by the exchange naming which; when and where are the operation's.
        var verdict = new QuestEvidenceVerdict(
            request.Commit ?? "", request.How ?? "",
            (request.Items ?? []).Select(i => new QuestEvidenceRead(i?.Requirement ?? 0, i?.Path, i?.Gate, i?.Result ?? "")
            {
                Object = i?.Object, Changed = i?.Changed, Spelled = i?.Spelled,
            }).ToList())
        {
            Session = request.Session,
            Submission = request.Submission is { } asked ? new QuestOperationRef(asked.Machine ?? "", asked.Sequence ?? 0) : null,
        };
        var outcome = await Service.Exchange.EvidenceAsync(request.Id, verdict, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            QuestRespondRefusal.None => await _answers.MovedAsync(http, outcome.Quest!, outcome.Message, ct),
            QuestRespondRefusal.NotFound => Refusal.NotFound(outcome.Message),
            QuestRespondRefusal.NotAwaitingEvidence => Refusal.Conflict(outcome.Message),
            _ => Refusal.BadRequest(outcome.Message),
        };
    }

    // A person's or the driver's move: the quest as it now stands, or why nothing moved, a state in the lock's shape.
    private async Task<Answer<QuestActionResponse>> StateAsync(HttpContext http, QuestRespondOutcome outcome, CancellationToken ct) =>
        outcome.Refusal switch
        {
            QuestRespondRefusal.None => await _answers.MovedAsync(http, outcome.Quest!, outcome.Message, ct),
            QuestRespondRefusal.NotFound => Refusal.NotFound(outcome.Message),
            _ => Refusal.Conflict(outcome.Message),
        };
}
