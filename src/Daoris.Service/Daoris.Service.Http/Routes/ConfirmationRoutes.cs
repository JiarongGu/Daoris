using Daoris.Contracts.Confirmations;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The window's confirmation of what an asker asked (PERSONDOOR1b, D156 point 4; ONEWAY1n-i, the oneway design §4 M4). A terminal cannot be
/// told from a session, so a keyless call to the person's or the driver's door is refused, and its asker asks here with the exact request.
/// It waits with no clock; the window lists it and the person answers with the key, and at the Confirm the host makes the request as asked.
/// A local host's alone: a shared host has no door that is the person's alone (§6). <see cref="PersonConfirmations"/> holds the rules.
/// </summary>
public sealed class ConfirmationRoutes(HostWiring host) : IRouteModule
{
    private PersonConfirmations Confirmations => host.Confirmations;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<ListConfirmations, ConfirmationResponse[]>((_, _, _) =>
            Task.FromResult<Answer<ConfirmationResponse[]>>(Confirmations.Waiting().Select(PersonConfirmations.Wire).ToArray()));

        // NOTIFY2j (D196 point 4, the notification design §2.5): what waits after `since`, answered as soon as it moves, one past its time
        // at its time, or at `wait` seconds (30 at most) as it is; any other cursor at once. The desktop's host holds one and tells every
        // window, so no window polls.
        routes.Map<WaitConfirmations, ConfirmationsWaitingResponse>(async (request, _, ct) =>
        {
            using var ends = CancellationTokenSource.CreateLinkedTokenSource(ct, host.Stopping);
            var (next, waiting) = await Confirmations.WaitAsync(request.Since, TimeSpan.FromSeconds(Math.Clamp(request.Wait ?? 0, 0, 30)), ends.Token);
            return new ConfirmationsWaitingResponse(next, [.. waiting.Select(PersonConfirmations.Wire)]);
        });

        // An id an earlier start held reads `restarted`: what waited ended with that start (§4 M4).
        routes.Map<ReadConfirmation, ConfirmationResponse>((request, _, _) => Task.FromResult(
            Confirmations.Find(request.Id) is { } found
                ? PersonConfirmations.Wire(found)
                : Refused<ConfirmationResponse>(Confirmations.Missing(request.Id))));
        routes.Map<AskConfirmation, ConfirmationAskedResponse>(AskAsync);

        // The person's answers, each behind the key (the gate's Person class), and its asker's withdrawal by its token, each while it
        // waits; a state that refuses it is the lock's own shape. The Confirm makes the request, and is answered how its door took it.
        routes.Map<ConfirmConfirmation, ConfirmationActionResponse>(async (request, _, _) => Acted(await Confirmations.ConfirmAsync(request.Id)));
        routes.Map<RefuseConfirmation, ConfirmationActionResponse>((request, _, _) => Task.FromResult(Acted(Confirmations.Refuse(request.Id))));
        routes.Map<WithdrawConfirmation, ConfirmationActionResponse>((request, _, _) =>
            Task.FromResult(Acted(Confirmations.Withdraw(request.Id, request.Token))));
    }

    // The ask is read here rather than bound, so it is read no further than its limit: a JSON body, which a page on another origin sends
    // only with CORS's leave (design §4.2), parsed by the host's own options.
    private async Task<Answer<ConfirmationAskedResponse>> AskAsync(AskConfirmation request, HttpContext http, CancellationToken ct)
    {
        var (body, unread) = await PersonConfirmations.ReadAskAsync(http, ct);
        if (unread is { } refusal) return refusal;

        var (asked, token, refused, message) = Confirmations.Ask(body!.Method, body.Path, body.Body, body.Session, AskerProcess.Of(http.Connection));
        return refused switch
        {
            ConfirmationAskRefusal.None => new ConfirmationAskedResponse(PersonConfirmations.Wire(asked!), message, token!),
            // A host handed no key refused nothing, so there is nothing to confirm: a state, the lock's own shape.
            ConfirmationAskRefusal.NoKey => Refusal.Conflict(message),
            // Its asker's places taken (PERSONDOOR1j): the gate's refusal, in its shape and with its log line, asking nothing.
            ConfirmationAskRefusal.Full => host.Gate.Refusal(http, PersonDoors.ConfirmationsFull, message),
            ConfirmationAskRefusal.Bound => host.Gate.Refusal(http, PersonDoors.AskerFull, message),
            _ => Refusal.BadRequest(message),
        };
    }

    private static Answer<ConfirmationActionResponse> Acted((Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message) outcome) =>
        outcome.Refusal == ConfirmationAnswerRefusal.None
            ? new ConfirmationActionResponse(PersonConfirmations.Wire(outcome.Confirmation!), outcome.Message)
            : Refused<ConfirmationActionResponse>(outcome);

    private static Answer<T> Refused<T>((Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message) outcome) => outcome.Refusal switch
    {
        ConfirmationAnswerRefusal.NotFound => Refusal.NotFound(outcome.Message),
        ConfirmationAnswerRefusal.Restarted => Refusal.NotFound(outcome.Message, PersonConfirmations.Restarted),
        // Made, and its door refused or failed it: what moved is its door's own sentence.
        ConfirmationAnswerRefusal.NotMade => Refusal.Conflict(outcome.Message, PersonConfirmations.NotMade),
        ConfirmationAnswerRefusal.NotAsker => Answer<T>.Else(
            StatusCodes.Status403Forbidden, new ErrorResponse(outcome.Message, PersonConfirmations.NotAsker)),
        _ => Refusal.Conflict(outcome.Message),
    };
}
