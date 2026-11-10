using System.Globalization;
using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Asks;
using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The ask doors a local host serves, sent as their records and read as their answers: the asks, one ask, the ask's channel, and the
/// person's choices, go-aheads, publish, close and delete. A refusal is an answer in the service's words, and a host older than a
/// door says so.
/// </summary>
public static class AskDoorViews
{
    /// <summary>This machine's asks that are not closed, newest first — what the loop finds intakes in.</summary>
    public static async Task<IReadOnlyList<AskView>> AsksAsync(this ServiceClient service, CancellationToken ct = default) =>
        [.. (await service.SendAsync(new ListAsks(), ct).ConfigureAwait(false)).Answered().Select(View)];

    /// <summary>Every ask this machine holds, closed ones included — what a delete Ask Daoris proposes is judged against.</summary>
    public static async Task<IReadOnlyList<AskView>> EveryAskAsync(this ServiceClient service, CancellationToken ct = default) =>
        [.. (await service.SendAsync(new ListAsks(IncludeClosed: true), ct).ConfigureAwait(false)).Answered().Select(View)];

    /// <summary>Every ask as the service wrote it, closed ones included: the text the pushed state judges.</summary>
    public static async Task<string> AsksJsonAsync(this ServiceClient service, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new ListAsks(IncludeClosed: true), ct).ConfigureAwait(false);
        sent.Answered();
        return sent.Reply.Payload;
    }

    /// <summary>One ask as it stands — how an intake's end is observed. Null when the service has none.</summary>
    /// <exception cref="DriverException">The service would not say.</exception>
    public static async Task<AskView?> FindAskAsync(this ServiceClient service, string id, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new ReadAsk(id.TrimStart('#')), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;

        return sent.Answer is { } ask
            ? View(ask)
            : throw new DriverException($"the service would not say what became of ask `#{id}`: {sent.Reply.Payload}");
    }

    /// <summary>One ask as the service writes it — the reader the doors use, for the tests that hold its fields.</summary>
    internal static AskView ReadAskJson(string json) =>
        View((AskResponse)JsonSerializer.Deserialize(json, ContractJson.Options.GetTypeInfo(typeof(AskResponse)))!);

    /// <summary>
    /// An ask's channel as one entry reads it, which moves no mark: what the look hands a running part, and a starting part's instruction
    /// carries. No entry is the person's own (the service's default), and <paramref name="since"/> the page's tail of a long channel. Null
    /// where the host holds no such ask, or is one without the channel, which answers the route not found as well.
    /// </summary>
    /// <exception cref="DriverException">The host answered something other than the channel.</exception>
    public static async Task<ChannelView?> ChannelAsync(
        this ServiceClient service, string ask, string? entry, CancellationToken ct = default, DateTimeOffset? since = null)
    {
        var sent = await service.SendAsync(
            new ReadAskChannel(ask.TrimStart('#'), entry, since is { } after ? Moments.Write(after) : null), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;

        return sent.Answer is { } channel
            ? ChannelView.Of(channel)
            : throw new DriverException($"the service would not read ask `#{ask}`'s channel: {sent.Reply.Payload}");
    }

    /// <summary>
    /// The asks the mirrored channel messages name that this machine does not hold, newest message first. Empty where the host is one
    /// without the door, which answers the route not found.
    /// </summary>
    /// <exception cref="DriverException">The host answered something other than the list.</exception>
    public static async Task<IReadOnlyList<ElsewhereAskAnswer>> ElsewhereAsksAsync(this ServiceClient service, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new ListElsewhereAsks(), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return [];

        return sent.Answer is { Asks: { } listed }
            ? [.. listed.Where(row => row.Id is { Length: > 0 } && row.Latest != default)
                .Select(row => new ElsewhereAskAnswer(row.Id, row.Workspace ?? "", row.Origins ?? [], row.Latest, row.Messages, row.Unread))]
            : throw new DriverException($"the service would not list the asks held elsewhere: {sent.Reply.Payload}");
    }

    /// <summary>
    /// An entry's acknowledgment of messages on an ask's channel by their ids: a part's claim (<c>taking</c>), one part's only, its
    /// letting go (<c>released</c>), a start's packet (<c>taken</c>, <c>partly</c>), the write (<c>sent</c>) and the door's word
    /// (<c>taken</c>). Answers the ids it changed: for a claim, those this part won. A host without the door, or without the ask,
    /// changes none, so nothing is claimed and the messages wait on the channel for the agent's own read.
    /// </summary>
    /// <exception cref="DriverException">The host refused the acknowledgment.</exception>
    public static async Task<IReadOnlyList<string>> ChannelAckAsync(
        this ServiceClient service, string ask, string entry, string session, string state, IReadOnlyList<string> messages, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new AcknowledgeAskChannel(ask.TrimStart('#'), entry, session, state, messages), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return [];

        return sent.Answer is { } acknowledged
            ? acknowledged.Acknowledged ?? []
            : throw new DriverException($"the service would not keep `{entry}`'s {state} of ask `#{ask}`'s channel: {sent.Reply.Payload}");
    }

    /// <summary>
    /// Move the person's own read mark on an ask's channel, through <paramref name="through"/> or the newest item, and answer the
    /// channel as it then reads. Null where the host holds no such ask, or is one without the channel.
    /// </summary>
    /// <exception cref="DriverException">The host refused the move.</exception>
    public static async Task<ChannelView?> ChannelMarkAsync(
        this ServiceClient service, string ask, DateTimeOffset? through = null, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new MarkAskChannelRead(ask.TrimStart('#'), Through: through), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;

        return sent.Answer is { } channel
            ? ChannelView.Of(channel)
            : throw new DriverException($"the service would not move the person's mark on ask `#{ask}`'s channel: {sent.Reply.Payload}");
    }

    /// <summary>
    /// The person's post on an ask's channel: their words, to everyone or one of the ask's repositories, answering a message or not. A
    /// refusal is an answer carrying the service's code (<c>empty</c>, <c>too-long</c>, <c>bad-to</c>, <c>bad-re</c>, <c>not-found</c>),
    /// which each door words its own way.
    /// </summary>
    /// <param name="urgent">
    /// Their level: false posts it ordinary, true urgent; null sends none, and the service keeps the person's default, urgent.
    /// </param>
    /// <exception cref="DriverException">The host answered neither the post nor a code.</exception>
    public static async Task<ChannelPosted> ChannelPostAsync(
        this ServiceClient service, string ask, string text, string? to, string? re, CancellationToken ct = default, bool? urgent = null)
    {
        var sent = await service.SendAsync(new PostAskChannelMessage(ask.TrimStart('#'), text, to, re, urgent), ct).ConfigureAwait(false);
        if (sent.Answer is { } posted)
        {
            return ChannelView.ItemOf(posted.Message) is { } item
                ? new ChannelPosted(true, item, null)
                : throw new DriverException($"the service answered something other than a posted message on ask `#{ask}`'s channel: {sent.Reply.Payload}");
        }

        return CodeOf(sent.Reply.Payload) is { Length: > 0 } code
            ? new ChannelPosted(false, null, code)
            : throw new DriverException($"the service would not take the post on ask `#{ask}`'s channel: {sent.Reply.Payload}");
    }

    /// <summary>
    /// Make an ask at a workspace — the sentence, its links, and files read from this machine. The service answers with what became of
    /// it; a refusal is an answer, not an exception.
    /// </summary>
    /// <param name="review">
    /// The person's review choice for its work, as the composer sends it: <c>off</c>, <c>on</c> or an environment's name; null sends
    /// none, and each repository's rule decides.
    /// </param>
    /// <param name="reviewWords">Their words with the choice, sent only with one.</param>
    /// <param name="workflow">
    /// The person's kind and workflow for its work, as the composer sends them; null sends none, and each repository's choice decides.
    /// </param>
    /// <param name="shortTitle">The ask's short title, at most 40 characters; null sends none.</param>
    public static async Task<AskAnswer> AskAsync(
        this ServiceClient service, string workspace, string sentence, IReadOnlyList<string> links,
        IReadOnlyList<(string Name, byte[] Content)> files, string? to, CancellationToken ct = default,
        string? review = null, string? reviewWords = null, AskWorkflowComposed? workflow = null, string? shortTitle = null)
    {
        var chosen = workflow is { } composed && (composed.Kind ?? composed.Workflow) is not null ? workflow : null;
        return await service.AnsweredAsync(
            new PostAsk(
                workspace, sentence, links, [.. files.Select(file => new QuestAttachmentRequest(file.Name, file.Content, null, null))], to,
                Review: review,
                ReviewWords: review is not null && !string.IsNullOrWhiteSpace(reviewWords) ? reviewWords.Trim() : null,
                Kind: chosen?.Kind, Workflow: chosen?.Workflow,
                WorkflowWords: chosen is not null && !string.IsNullOrWhiteSpace(chosen.Words) ? chosen.Words.Trim() : null,
                Short: string.IsNullOrWhiteSpace(shortTitle) ? null : shortTitle.Trim()),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The person's review choice on an ask, or its intake's proposal applied, the door the ask's page presses. The latest stands. The
    /// service's sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    public static Task<AskAnswer> ChooseAskReviewAsync(
        this ServiceClient service, string ask, string choice, string? words, CancellationToken ct = default) =>
        service.AnsweredAsync(new ChooseAskReview(ask.TrimStart('#'), choice, string.IsNullOrWhiteSpace(words) ? null : words.Trim()), ct);

    /// <summary>
    /// The person's kind and workflow on an ask, a person's door; neither clears the choice. The latest stands. The service's sentence
    /// comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    public static Task<AskAnswer> ChooseAskWorkflowAsync(
        this ServiceClient service, string ask, string? kind, string? workflow, string? words, CancellationToken ct = default) =>
        service.AnsweredAsync(new ChooseAskWorkflow(ask.TrimStart('#'), kind, workflow, string.IsNullOrWhiteSpace(words) ? null : words.Trim()), ct);

    /// <summary>An ask turned into a quest for <paramref name="to"/>, as <see cref="AskPublishRequest"/> sends it.</summary>
    public static Task<AskAnswer> PublishAskAsync(
        this ServiceClient service, string id, string to, string? session = null, IReadOnlyList<QuestRequirementWire>? requirements = null,
        CancellationToken ct = default) =>
        service.AnsweredAsync(AskPublishRequest.Of(id, to, session, requirements ?? []), ct);

    /// <summary>A person closes their own ask, with the reason.</summary>
    public static Task<AskAnswer> CloseAskAsync(this ServiceClient service, string id, string reason, CancellationToken ct = default) =>
        service.AnsweredAsync(new CloseAsk(id.TrimStart('#'), reason), ct);

    /// <summary>
    /// The person's yes or no to a go-ahead a session asked on their ask, with their words where they give any: the terminal's door.
    /// The service's sentence comes back verbatim, a refusal (no such go-ahead) included.
    /// </summary>
    /// <param name="goesOn">
    /// Whether the answer that leaves none of a parked session's go-aheads waiting is that park's answer, so the session goes on with
    /// it, as the ask's page has it. False by default, for a caller that answers the park itself, which leaves the park to it.
    /// </param>
    public static async Task<(bool Ok, string Message)> AnswerGoAheadAsync(
        this ServiceClient service, string ask, int number, bool approved, string? words, CancellationToken ct = default, bool goesOn = false)
    {
        var sent = await service.SendAsync(
            new AnswerGoAhead(
                ask.Trim().TrimStart('#'), number.ToString(CultureInfo.InvariantCulture), approved ? "approved" : "refused",
                string.IsNullOrWhiteSpace(words) ? null : words, goesOn),
            ct).ConfigureAwait(false);
        if (sent.Answer is { } answered) return (true, answered.Message ?? "");
        return QuestViews.Error(sent) is { } error
            ? (false, error)
            : (false, $"the service at {service.BaseUrl} has no go-ahead door ({sent.Reply.Status}) — is it older than this driver?");
    }

    /// <summary>
    /// A person deletes an ask made by mistake, with every quest asked by it. The service's sentence comes back verbatim, and a
    /// refusal is an answer too.
    /// </summary>
    public static async Task<(bool Ok, string Message)> DeleteAskAsync(this ServiceClient service, string id, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new DeleteAsk(id.TrimStart('#')), ct).ConfigureAwait(false);
        if (sent.Answer is { } deleted) return (true, deleted.Message ?? "");
        return QuestViews.Error(sent) is { } error
            ? (false, error)
            : (false, $"the service at {service.BaseUrl} has no delete door ({sent.Reply.Status}) — is it older than this driver?");
    }

    private static async Task<AskAnswer> AnsweredAsync(this ServiceClient service, IRoute<AskActionResponse> route, CancellationToken ct)
    {
        var sent = await service.SendAsync(route, ct).ConfigureAwait(false);
        if (sent.Answer is { } done) return new AskAnswer(true, done.Message ?? "", done.Ask?.Id, done.Quest?.Id);
        return QuestViews.Error(sent) is { } error
            ? new AskAnswer(false, error, null, null)
            : new AskAnswer(false, $"the service at {service.BaseUrl} has no ask door ({sent.Reply.Status}) — is it older than this driver?", null, null);
    }

    internal static string? CodeOf(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload, ContractJson.Options.GetTypeInfo(typeof(ErrorResponse))) is ErrorResponse refusal ? refusal.Code : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// An ask as the doors answer it. A word, a go-ahead, a choice or a proposal that is not whole is passed over, as the service passes
    /// over what it cannot read, never a failed read of the ask; a part the host left out reads as none, or null where it must not be
    /// read as the person having said nothing.
    /// </summary>
    internal static AskView View(AskResponse ask)
    {
        var proposal = (ask.Proposal ?? []).Where(match => match.Repository is { Length: > 0 }).ToList();
        return new(ask.Id ?? "", ask.Workspace ?? "", ask.Sentence ?? "", ask.State ?? "", ask.Tier ?? "")
        {
            Asker = ask.Asker,
            Note = ask.Note,
            Intake = ask.Intake,
            Links = ask.Links ?? [],
            Attachments = [.. (ask.Attachments ?? []).Select(file => new QuestFileView(file.Name ?? "", file.Sha256 ?? "", file.Bytes, file.Path))],
            Quests = ask.Quests ?? [],
            Proposed = [.. proposal.Select(match => match.Repository)],
            // A proposal whose evidence is its own name is one the sentence names, and the intake is told so.
            Named = [.. proposal.Where(match => Asks.IsNamed(match.Repository, match.Matched ?? [])).Select(match => match.Repository)],
            Deletable = ask.Deletable,
            // The person's words, which every session on the ask is handed.
            Words = ask.Words is null
                ? null
                : [.. ask.Words.Where(word => word.Kind is { Length: > 0 } && word.Text is not null && word.At != default)
                    .Select(word => new AskWordView(word.Kind, word.Text, word.At, word.Session, word.Quest))],
            WordsKeptFrom = ask.WordsKeptFrom,
            GoAheads = ask.GoAheads is null ? null : [.. ask.GoAheads.Select(GoAhead).OfType<GoAheadView>()],
            ReviewChoices = [.. (ask.ReviewChoices ?? []).Where(chosen => chosen.Choice is { Length: > 0 } && chosen.At != default)
                .Select(chosen => new AskReviewChoiceView(chosen.Choice, chosen.At, chosen.Words))],
            ReviewProposals = [.. (ask.ReviewProposals ?? [])
                .Where(proposed => proposed.Choice is { Length: > 0 } && proposed.Reason is { Length: > 0 } && proposed.At != default)
                .Select(proposed => new AskReviewProposalView(proposed.Choice, proposed.Reason, proposed.At, proposed.Session, proposed.Quest))],
            WorkflowChoices = [.. (ask.WorkflowChoices ?? []).Where(chosen => chosen.At != default)
                .Select(chosen => new AskWorkflowChoiceView(chosen.Kind, chosen.Workflow, chosen.At, chosen.Words))],
            WorkflowProposals = [.. (ask.WorkflowProposals ?? [])
                .Where(proposed => proposed.Kind is { Length: > 0 } && proposed.Reason is { Length: > 0 } && proposed.At != default)
                .Select(proposed => new AskWorkflowProposalView(proposed.Kind, proposed.Reason, proposed.At, proposed.Session, proposed.Quest))],
        };
    }

    private static GoAheadView? GoAhead(GoAheadResponse goAhead) =>
        goAhead.Kind is not { Length: > 0 } || goAhead.On is not { Length: > 0 } || goAhead.Act is not { Length: > 0 }
            ? null
            : new GoAheadView(
                goAhead.Number, goAhead.Kind, goAhead.On, goAhead.Act,
                [.. (goAhead.Asked ?? []).Where(request => request.Session is { Length: > 0 } && request.At != default)
                    .Select(request => new GoAheadRequestView(request.Session, request.Quest, request.At, request.Why ?? ""))])
            {
                Answer = goAhead.Answer is { } answer && answer.At != default ? new GoAheadAnswerView(answer.Approved, answer.Words, answer.At) : null,
                Near = goAhead.Near,
            };
}
