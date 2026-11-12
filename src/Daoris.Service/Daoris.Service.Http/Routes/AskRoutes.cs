using System.Globalization;
using Daoris.Contracts.Asks;
using Daoris.Contracts.Quests;
using static Daoris.Knowledge.Http.ChannelRoutes;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The ask doors, a local host's alone: the intake is this machine's, like a quest file's bytes, and a shared deployment has no door
/// here. A sentence entered at a workspace is answered by the tier this machine has, the declarations with no harness, a named receiver
/// at once; the ask's channel is what its sessions and the person said; and the person's choices, go-aheads, close and delete are theirs.
/// </summary>
public sealed class AskRoutes(HostWiring host) : IRouteModule
{
    private const string FilesRefused = "every attachment needs its name and its content — an ask is made on the machine that has the file";

    private readonly AskAnswers _answers = new(host);

    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<ListAsks, AskResponse[]>(async (request, http, ct) =>
            (await Service.Asks.ListAsync(request.Workspace, request.IncludeClosed ?? false, ct))
            .Select(a => AskAnswers.ToAsk(a, Service.Files, host.MachineLocal(http))).ToArray());
        routes.Map<PostAsk, AskActionResponse>(AskAsync);
        routes.Map<ChooseAskReview, AskActionResponse>(async (request, http, ct) =>
            _answers.Moved(http, await Service.Asks.ChooseReviewAsync(request.Id, request.Choice, request.Words, DateTimeOffset.UtcNow, ct)));
        routes.Map<ChooseAskThrough, AskActionResponse>(async (request, http, ct) =>
            _answers.Moved(http, await Service.Asks.ChooseThroughAsync(request.Id, request.Words, DateTimeOffset.UtcNow, ct)));
        routes.Map<ChooseAskWorkflow, AskActionResponse>(async (request, http, ct) =>
            _answers.Moved(http, await Service.Asks.ChooseWorkflowAsync(request.Id, request.Kind, request.Workflow, request.Words, DateTimeOffset.UtcNow, ct)));
        routes.Map<ReadAsk, AskResponse>(async (request, http, ct) =>
            await Service.Asks.FindAsync(request.Id, ct) is { } ask
                ? AskAnswers.ToAsk(ask, Service.Files, host.MachineLocal(http))
                : Refusal.NotFound($"No ask `#{request.Id.TrimStart('#')}`."));
        routes.Map<ReadAskChannel, ChannelResponse>(ReadChannelAsync);
        routes.Map<ListElsewhereAsks, ElsewhereAsksResponse>(async (_, _, ct) =>
            new ElsewhereAsksResponse([.. (await Service.Channel.ElsewhereAsync(ct)).Select(a =>
                new ElsewhereAskResponse(a.Id, a.Workspace, a.Origins, a.Latest, a.Messages, a.Unread))]));
        routes.Map<PostAskChannelMessage, ChannelPostResponse>(PostChannelAsync);
        routes.Map<AcknowledgeAskChannel, ChannelAckResponse>(AcknowledgeAsync);
        routes.Map<MarkAskChannelRead, ChannelResponse>(async (request, _, ct) =>
        {
            var reader = string.IsNullOrWhiteSpace(request.Entry) ? ChannelDesk.PersonEntry : request.Entry.Trim();
            return await Service.Channel.MarkReadAsync(request.Id, reader, request.Through, ct) is { } read
                ? ToChannel(request.Id, reader, read)
                : NoAsk(request.Id);
        });
        routes.Map<PublishAsk, AskActionResponse>(PublishAsync);
        routes.Map<CloseAsk, AskActionResponse>(async (request, http, ct) =>
            _answers.Moved(http, await Service.Asks.CloseAsync(request.Id, request.Reason ?? "", DateTimeOffset.UtcNow, ct)));
        routes.Map<AnswerGoAhead, AskActionResponse>(AnswerGoAheadAsync);
        routes.Map<DeleteAsk, DeletedResponse>(DeleteAsync);
    }

    private static Refusal NoAsk(string id) =>
        Refusal.NotFound($"No ask `#{id.TrimStart('#')}`.", ChannelDesk.Spell(ChannelRefusal.NotFound));

    private static List<QuestUpload>? UploadsOf(IReadOnlyList<QuestAttachmentRequest>? files)
    {
        var uploads = new List<QuestUpload>();
        foreach (var file in files ?? [])
        {
            if (string.IsNullOrWhiteSpace(file.Name) || file.Content is null) return null;

            uploads.Add(new QuestUpload(file.Name, file.Content));
        }

        return uploads;
    }

    private async Task<Answer<AskActionResponse>> AskAsync(PostAsk request, HttpContext http, CancellationToken ct)
    {
        if (UploadsOf(request.Attachments) is not { } uploads) return Refusal.BadRequest(FilesRefused);

        var outcome = await Service.Asks.AskAsync(
            new AskRequest(request.Workspace ?? "", request.Sentence ?? "")
            {
                Links = request.Links ?? [],
                Uploads = uploads,
                To = request.To,
                // The person's choices from the composer, judged by the desk.
                Review = request.Review,
                ReviewWords = request.ReviewWords,
                Kind = request.Kind,
                Workflow = request.Workflow,
                WorkflowWords = request.WorkflowWords,
                Short = request.Short,
                Paths = request.Paths ?? [],
                Requirements = QuestAnswers.RequirementsOf(request.Requirements),
                Through = request.Through,
            },
            DateTimeOffset.UtcNow, ct);
        return _answers.Moved(http, outcome);
    }

    // A read moves no mark. An ask held elsewhere reads from the messages the sync mirrored, read-only.
    private async Task<Answer<ChannelResponse>> ReadChannelAsync(ReadAskChannel request, HttpContext http, CancellationToken ct)
    {
        DateTimeOffset? since = null;
        if (!string.IsNullOrEmpty(request.Since))
        {
            if (!DateTimeOffset.TryParse(request.Since, CultureInfo.InvariantCulture, out var after)) return Refusal.BadRequest("`since` is a moment, ISO 8601.");

            since = after;
        }

        var reader = string.IsNullOrWhiteSpace(request.Entry) ? ChannelDesk.PersonEntry : request.Entry.Trim();
        return await Service.Channel.ReadAsync(request.Id, reader, since, moveMark: false, ct) is { } read
            ? ToChannel(request.Id, reader, read)
            : NoAsk(request.Id);
    }

    private async Task<Answer<ChannelPostResponse>> PostChannelAsync(PostAskChannelMessage request, HttpContext http, CancellationToken ct)
    {
        var posted = await Service.Channel.PostAsPersonAsync(
            request.Id, request.Text, request.To, request.Re, DateTimeOffset.UtcNow, ct, urgent: request.Urgent ?? true);
        if (posted.Refusal == ChannelRefusal.None) return new ChannelPostResponse(ToChannelItem(posted.Message!));

        var code = ChannelDesk.Spell(posted.Refusal);
        return posted.Refusal switch
        {
            ChannelRefusal.NotFound => Refusal.NotFound(code, code),
            _ => Refusal.BadRequest(code, code),
        };
    }

    private async Task<Answer<ChannelAckResponse>> AcknowledgeAsync(AcknowledgeAskChannel request, HttpContext http, CancellationToken ct)
    {
        if (!ChannelAcks.Known(request.State) || string.IsNullOrWhiteSpace(request.Entry))
        {
            return Refusal.BadRequest("an acknowledgment names its entry and a state: taking, released, sent, taken, read or partly", "bad-state");
        }

        var changed = await Service.Channel.AcknowledgeAsync(
            request.Id, request.Entry.Trim(), string.IsNullOrWhiteSpace(request.Session) ? null : request.Session.Trim(), request.State!,
            [.. (request.Messages ?? []).OfType<string>()], DateTimeOffset.UtcNow, ct);
        return changed is null ? NoAsk(request.Id) : new ChannelAckResponse(changed);
    }

    private async Task<Answer<AskActionResponse>> PublishAsync(PublishAsk request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.To)) return Refusal.BadRequest("to is required — the repository this ask becomes a quest for");
        if (UploadsOf(request.Attachments) is not { } uploads) return Refusal.BadRequest(FilesRefused);

        // A person's publish names a receiver and nothing else; an intake's carries its own words. Anything beyond `to` makes a
        // draft — words, links, files or a chain alone included.
        var drafted = request.Title is not null || request.Body is not null || request.Links is { Count: > 0 }
            || uploads.Count > 0 || request.Then is { Count: > 0 } || request.Requirements is { Count: > 0 } || request.Short is not null
            || request.Review is not null || request.ReviewProposal is not null || request.WorkflowProposal is not null
            || request.Paths is { Count: > 0 };
        var draft = drafted
            ? new AskDraft(request.Title, request.Body)
            {
                Links = request.Links ?? [],
                Uploads = uploads,
                Then = QuestAnswers.StepsOf(request.Then),
                Requirements = QuestAnswers.RequirementsOf(request.Requirements),
                Short = request.Short,
                Review = QuestAnswers.ReviewOf(request.Review),
                ReviewProposal = request.ReviewProposal is { } proposal ? new ReviewProposed(proposal.Choice, proposal.Reason) : null,
                // The exchange checks a kind proposal's reason quotes the ask; the desk keeps it.
                WorkflowProposal = request.WorkflowProposal is { } kind ? new WorkflowProposed(kind.Kind, kind.Reason) : null,
                // Added to the ask's own by the desk.
                Paths = request.Paths ?? [],
            }
            : null;

        // Keyless where the host holds a person key, an agent's publish onto the ask, crediting the session it names, if any;
        // with the key and no session, the person's choice of receiver.
        return _answers.Moved(
            http,
            await Service.Asks.PublishAsync(request.Id, request.To, DateTimeOffset.UtcNow, ct, draft, request.Session, byAgent: host.Gate.IsAgent(http)));
    }

    // No connector tool answers a go-ahead, since the production acts stay the person's. The answer that leaves none of a parked
    // session's go-aheads waiting is that park's answer, and the sentence says which parks go on and which still wait.
    private async Task<Answer<AskActionResponse>> AnswerGoAheadAsync(AnswerGoAhead request, HttpContext http, CancellationToken ct)
    {
        if (!int.TryParse(request.Number, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
        {
            return Refusal.NotFound($"`{request.Number}` is no go-ahead's number — they are numbered from 1 on their ask.");
        }

        if (request.Answer is not ("approved" or "refused"))
        {
            return Refusal.BadRequest("answer is `approved` or `refused` — the person's yes or no to the act.");
        }

        var now = DateTimeOffset.UtcNow;
        var outcome = await Service.Asks.AnswerGoAheadAsync(request.Id, n, request.Answer == "approved", request.Words, now, ct);
        var parks = request.GoesOn == false ? GoAheadParks.None : await Service.Ledger.GoOnWithGoAheadsAsync(outcome, now, ct);
        return outcome.Refusal switch
        {
            GoAheadAnswerRefusal.None => new AskActionResponse(
                AskAnswers.ToAsk(outcome.Ask!, Service.Files, host.MachineLocal(http)), outcome.Message + parks.Said, null),
            GoAheadAnswerRefusal.NotFound or GoAheadAnswerRefusal.NoGoAhead => Refusal.NotFound(outcome.Message),
            _ => Refusal.BadRequest(outcome.Message),
        };
    }

    private async Task<Answer<DeletedResponse>> DeleteAsync(DeleteAsk request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Asks.DeleteAsync(request.Id, DateTimeOffset.UtcNow, ct);
        switch (outcome.Refusal)
        {
            case AskRefusal.None:
                // The ask's channel goes with it: the same words asked again are the same ask, and must not find the old channel.
                await Service.Channel.ForgetAsync(request.Id, ct);
                return new DeletedResponse(request.Id.TrimStart('#'), outcome.Message);
            case AskRefusal.NotFound:
                return Refusal.NotFound(outcome.Message);
            default:
                return Refusal.Conflict(outcome.Message);
        }
    }
}
