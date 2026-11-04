using System.Text.Json.Nodes;
using Daoris.Contracts.Feed;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// A shared host is fed by desktops: records and content arrive attributed to the key that carried them (D47 §§4-6), and the quest
/// operations they push are ordered here (D68). A local host is fed by nobody, so a door with no meaning there does not exist there.
/// </summary>
public sealed class FeedRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Shared) return;

        routes.Map<FeedSessions, FeedResponse>(FeedSessionsAsync);
        routes.Map<SessionsSince, JsonObject>(async (request, http, ct) =>
            Caller(http) is { } caller
                ? WireObject.Of(SessionWire.Page(await Service.Sessions.TeamSinceAsync(request.Since ?? 0, caller, ct: ct)))
                : Refusal.BadRequest("the team's records are read by a keyed caller — its key says which are its own"));
        routes.Map<FeedChannel, FeedResponse>(FeedChannelAsync);
        routes.Map<ChannelSince, JsonObject>(async (request, http, ct) =>
            Caller(http) is { } caller
                ? WireObject.Of(ChannelWire.Page(await Service.Messages.TeamSinceAsync(request.Since ?? 0, caller, ct: ct)))
                : Refusal.BadRequest("the team's messages are read by a keyed caller — its key says which are its own"));
        routes.Map<FeedEntries, FeedResponse>(FeedEntriesAsync);
        routes.Map<FeedCodeMap, FeedResponse>(async (request, http, ct) =>
            string.IsNullOrWhiteSpace(request.Repository)
                ? Refusal.BadRequest("repository is required")
                : FeedAnswers.Of<FeedResponse>(await Service.Service.FeedCodeMapAsync(
                    request.Repository, request.File, request.Map,
                    FeedAnswers.FedFrom(request.Commit, request.CommittedAt, request.Branch, request.Base, http), ct)));
        routes.Map<ReadFeedHeld, FeedHeldResponse>(async (request, _, ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Repository)) return Refusal.BadRequest("repository is required");

            var held = await Service.Service.HeldAsync(request.Repository, ct);
            return new FeedHeldResponse(request.Repository, held.Knowledge, held.CodeMap, held.Registration);
        });

        // The remote's quest doors (D68, sync design §8): what it accepted after a number, in its order, and a push rebased on one, judged
        // quest by quest through the table every machine uses. FABRIC-CLOSE1i: both answer the workspace's handshake, which machines here
        // have not declared they know a bound quest.
        routes.Map<ReadOperations, JsonObject>(async (request, _, ct) =>
            WireObject.Of(QuestWire.Page(await Service.Quests.OperationsSinceAsync(request.Since ?? 0, ct: ct) with
            {
                Closure = await Service.Quests.ClosureAsync(ct),
            })));
        routes.Map<PushOperations, JsonObject>(PushAsync);

        // FABRIC-CLOSE1i2: a machine heard from nothing here is answered `retired: false`, so a 404 stays what a remote before the door answers.
        routes.Map<RetireQuestMachine, JsonObject>(async (request, _, ct) =>
        {
            var retired = await Service.Quests.RetireMachineAsync(request.Machine, DateTimeOffset.UtcNow, ct);
            return WireObject.Of(QuestWire.Retired(retired, await Service.Quests.ClosureAsync(ct)));
        });
    }

    // The key's name is the caller's identity, and what fed records are attributed to; none is a door answered only to keyed callers.
    private static string? Caller(HttpContext http) =>
        http.Items["daoris.principal"] is string caller && !string.IsNullOrWhiteSpace(caller) ? caller : null;

    // The record upserts whole and is never re-judged (D47 §6). There is no transcript field to strip, because the wire has none. Only
    // joined repositories' records are taken (§4). Unreachable without a key while the shared gate stamps every /api caller, and kept: a
    // gate refactor that dropped the stamp must fail here, loudly, not mirror records under an empty name.
    private async Task<Answer<FeedResponse>> FeedSessionsAsync(FeedSessions request, HttpContext http, CancellationToken ct)
    {
        if (Caller(http) is not { } origin) return Refusal.BadRequest("the feed carries its key's identity — this door answers only keyed callers");

        using var body = new StreamReader(http.Request.Body);
        if (SessionWire.ReadFeed(await body.ReadToEndAsync(ct)) is not { } records)
        {
            return Refusal.BadRequest("a feed is `records`, each with its created and updated times");
        }

        return Fed(await new SessionFeed(Service.Service, Service.Sessions).FeedAsync(origin, records, ct));
    }

    // An ask's channel messages (SESSONE2d, D167 point 8), the session records' door in its shape: the words arrive cleaned and are
    // cleaned again on arrival (ChannelFeed).
    private async Task<Answer<FeedResponse>> FeedChannelAsync(FeedChannel request, HttpContext http, CancellationToken ct)
    {
        if (Caller(http) is not { } origin) return Refusal.BadRequest("the feed carries its key's identity — this door answers only keyed callers");

        using var body = new StreamReader(http.Request.Body);
        if (ChannelWire.ReadFeed(await body.ReadToEndAsync(ct)) is not { } messages)
        {
            return Refusal.BadRequest("a channel feed is `messages`, each with its id, ask, time and words");
        }

        return Fed(await new ChannelFeed(Service.Service, Service.Messages).FeedAsync(origin, messages, ct));
    }

    private static Answer<FeedResponse> Fed(SessionFeedOutcome outcome) => outcome.Refusal switch
    {
        SessionFeedRefusal.None => new FeedResponse(outcome.Records, outcome.Message),
        SessionFeedRefusal.NotJoined => Refusal.Conflict(outcome.Message),
        _ => Refusal.BadRequest(outcome.Message),
    };

    // The disclosure judgement and the provenance judgement are both the service's own (FeedAsync), so any future door shares them.
    private async Task<Answer<FeedResponse>> FeedEntriesAsync(FeedEntries request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Repository)) return Refusal.BadRequest("repository is required");

        var entries = new List<KnowledgeEntry>();
        foreach (var entry in request.Entries ?? [])
        {
            if (!Enum.TryParse<EntryKind>(entry.Kind ?? "", ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            {
                return Refusal.BadRequest($"unknown kind '{entry.Kind}' — one of: rule, knowledge, skill, decision, fix, taskoutcome, index");
            }

            entries.Add(new KnowledgeEntry(
                request.Repository, kind, Provenance.Local, entry.Title ?? "", entry.Body ?? "",
                entry.RelativePath ?? "", entry.Anchor,
                Lines: LineSpan.Of(entry.FirstLine, entry.LastLine, entry.Body ?? "")));
        }

        return FeedAnswers.Of<FeedResponse>(await Service.Service.FeedAsync(
            request.Repository, entries,
            FeedAnswers.FedFrom(request.Commit, request.CommittedAt, request.Branch, request.Base, http), ct));
    }

    private async Task<Answer<JsonObject>> PushAsync(PushOperations request, HttpContext http, CancellationToken ct)
    {
        using var body = new StreamReader(http.Request.Body);
        var json = await body.ReadToEndAsync(ct);
        if (QuestWire.ReadPush(json) is not var (@base, pushed)) return Refusal.BadRequest(QuestWire.Shape);

        // The remote re-judges a publish as its own door would (JudgeReceived), and files it by the receiver's registration here: the push
        // names no workspace (SYNC0a).
        var registered = await Service.Service.RegistryAsync(ct: ct);
        var judged = await Service.Quests.ReceiveAsync(
            @base, pushed,
            asked => Service.Exchange.JudgeReceived(asked, registered),
            asked => registered.Named(asked.To)?.InWorkspace ?? Workspaces.Default,
            ct);
        // What the push said of its machine (FABRIC-CLOSE1i), heard whatever was kept of it, since a refused operation's build is still its own.
        await Service.Quests.DeclaredAsync(QuestWire.Declares(json), pushed, DateTimeOffset.UtcNow, ct);
        return WireObject.Of(QuestWire.Pushed(judged with { Closure = await Service.Quests.ClosureAsync(ct) }));
    }
}
