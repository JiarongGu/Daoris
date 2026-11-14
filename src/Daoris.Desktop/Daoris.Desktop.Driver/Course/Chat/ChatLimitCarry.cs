namespace Daoris.Driver;

/// <summary>
/// A conversation's limit move onto another account of its own agent that carries its own conversation there: planned from the door's
/// declaration and the context the part left, its files copied once that part ended, and its new part's record opened as a resume of that
/// conversation, the refused turns' words and the moved ones its prompt with Daoris's word after them, no brief. Where the door refuses the
/// resume before any prompt, the same part goes on in the brief, the copy taken out of that account's home and the refusal said.
/// </summary>
/// <remarks>
/// Ask Daoris's room, a move to another agent and a host whose open moves no word are briefed, and plan nothing here.
/// </remarks>
internal sealed class ChatLimitCarry(
    IExecutorWiring wiring, AgentDoor door, PriorSession prior, HarnessSelection selection, CarryPlan.Carried carried,
    IReadOnlyList<SessionEvent> refused, IReadOnlyList<ChatMessage> unkept, int before, ConversationMove move, MoveFence fence,
    TaskCompletionSource<Task> read)
{
    private ChatContext context => wiring.Context;

    private IChatRunning runner => wiring;

    // What the carried run is handed, which the brief's run on the same part takes again where the resume is refused.
    private (string To, ChatAgent Agent, DriverConfig Config, ChatPlace Place, Func<string, string, Task>? OnEnded, IReadOnlyList<MovedWord> Moved)? _ran;

    /// <summary>The carry as planned: whole, or compacted first.</summary>
    public CarryPlan.Carried Plan => carried;

    /// <summary>Whether the new part runs as the carried resume; false where the move's fence holds it, so nothing is handed there.</summary>
    public bool Resumed { get; private set; }

    /// <summary>
    /// How the move takes the conversation, asked once the part it leaves has ended so its files are whole: carried, its copy made in the
    /// new account's home, or briefed by the carry's code.
    /// </summary>
    public static CarryPlan Planned(ChatContext context, AgentDoor door, PriorSession prior, HarnessSelection selection, string tree)
    {
        var registry = context.Harnesses.Adapters;
        var carry = LimitCarry.Declared(registry, door.Id);
        string? from;
        try
        {
            from = prior.Profile is { Length: > 0 } left ? HarnessSettings.ProfileHome(context.Home, registry.OwnerOf(door.Id).Value, left) : null;
        }
        catch (DriverException)
        {
            from = null;
        }

        var to = selection.Profile is { Length: > 0 } ? selection.ProfileHome : null;
        var high = context.Events.After(prior.Session, 0).Events.Where(e => e.Kind == SessionEventKind.Usage && e.Used is not null).MaxBy(e => e.Used);
        var plan = LimitCarry.Plan(carry, context.Conversations.Read(prior.Session)?.Conversation, from, to, high?.Used, high?.Size);
        return from is null || to is null ? plan : LimitCarry.Copied(plan, carry, from, to, tree);
    }

    /// <summary>The carry's code where the move asked to carry and is briefed instead; null where it carries or asked nothing.</summary>
    public static string? NotCarried(CarryPlan? plan) => (plan as CarryPlan.Briefed)?.Why;

    public void Bound(string to) => context.Conversations.Keep(to, door.Id.Value, carried.Conversation);

    /// <summary>
    /// The new part's run, its record open: the move's fence first, then its record moved to working and its run handed the carried
    /// conversation to resume. A fence that holds takes the copy back out and runs the part with nothing handed, its words waiting there.
    /// </summary>
    public async Task<ChatStart> RunAsync(
        string to, string message, ChatAgent agent, DriverConfig config, ChatPlace place, Func<string, string, Task>? onEnded,
        IReadOnlyList<MovedWord> moved, CancellationToken ct)
    {
        _ran = (to, agent, config, place, onEnded, moved);
        if (await fence.AskAsync(to, moved).ConfigureAwait(false) is not null)
        {
            Withdraw();
            return await runner.RunAsync("chat", to, message, agent, selection, config, place, onEnded, ct).ConfigureAwait(false);
        }

        var words = await MovedWords.OnAsync(context.Service, context.Home, prior.Session, to, moved, ct).ConfigureAwait(false);
        PriorSession? record;
        try
        {
            // A going-on's run finds its record working, marked as its opener's: the open left it queued.
            context.Processes.MarkOpening(to);
            await context.Service.AdvanceAsync(to, "starting", ct: ct).ConfigureAwait(false);
            await context.Service.AdvanceAsync(to, "working", transcript: DaorisHome.Entry(context.Home, HomeEntry.Sessions, $"{to}.log"), ct: ct)
                .ConfigureAwait(false);
            record = await context.Service.RecordAsync(to, ct).ConfigureAwait(false) ?? throw new DriverException($"the record of `{to}` could not be read.");
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
        {
            Withdraw();
            await FailedAsync(to, error.Message).ConfigureAwait(false);
            return new ChatStart(null, error.Message);
        }

        var ask = new ResumeAsk(
            carried.Conversation, words?.Words ?? [], CarriedNotes.From(prior.Session, prior.Adapter ?? agent.Name, prior.Profile, HandOverWhy.Limit, carried),
            LimitCarry.Words, word => ChatFiles.Kept(context.Home, to, word.Files))
        {
            Resent = [.. refused.Select(word => new ChatMessage(word.Text ?? "", ChatFiles.Kept(context.Home, prior.Session, word.Files ?? []))), .. unkept],
        };
        Resumed = true;
        // Said before the run, so a refusal's brief, which follows its run, is always said after it.
        context.Note(prior.Session, CarriedNotes.On(to, agent.Name, selection.Profile, HandOverWhy.Limit, carried.Tokens));
        context.Service.AccountSaid(Continuations.Moved(
            to, prior.Session, prior.Adapter ?? agent.Name, agent.Name, prior.Profile, selection.Profile, HandOverWhy.Limit, briefed: false,
            carry: carried is CarryPlan.Compacted ? "compacted" : "whole"));
        var started = await runner.RunAsync("chat", to, message, agent, selection, config, place, onEnded, ct, new GoingOn(record, ask) { Carried = this })
            .ConfigureAwait(false);
        if (started.SessionId is null) Withdraw();
        return started;
    }

    /// <summary>The words are on the wire: each handed word's <c>sent</c> by its transfer, then their take off the new part, then the move's reading.</summary>
    public void Went(ResumeAsk ask)
    {
        if (_ran is not { } ran) return;
        List<string> transfers = [.. ran.Moved.Where(word => ask.Ids.Contains(word.Id, StringComparer.Ordinal)).Select(word => word.Transfer)];
        move.Wire(transfers);
        read.TrySetResult(Task.Run(async () =>
        {
            try
            {
                if (ask.Ids.Count > 0) await context.Service.TakenAsync(ran.To, ask.Ids, by: null).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException or ObjectDisposedException)
            {
                context.Note(ran.To, $"— your words could not be taken off its record: {error.Message}");
            }

            await move.ReadAsync().ConfigureAwait(false);
        }));
    }

    /// <summary>
    /// The door refused the carried resume before any prompt, and its run let go: the copy taken out of the new account's home, and the same
    /// part, its record working, opened afresh and handed Daoris's brief, the refused turns' words and the moved ones, the refusal said.
    /// </summary>
    public async Task<ChatStart> FallBackAsync()
    {
        if (_ran is not { } ran) return new ChatStart(null, "");
        Withdraw();
        var service = context.Service;
        var brief = await ContextBriefs.ComposeAsync(
                service, context.Events, context.Home, prior.Session, HandOverWhy.Limit, conversation: true, ct: CancellationToken.None, before: before)
            .ConfigureAwait(false);
        var started = await runner.RunAsync(
                "chat", ran.To, $"conversation `{ran.To}` goes on, handed Daoris's brief.", ran.Agent, selection, ran.Config, ran.Place, ran.OnEnded, CancellationToken.None, working: true)
            .ConfigureAwait(false);
        if (started.SessionId is null) return started;

        var found = await MovedWords.OnAsync(service, context.Home, prior.Session, ran.To, ran.Moved, CancellationToken.None).ConfigureAwait(false);
        var moved = found is not null && context.Holds(ran.To) ? await found.AttemptedAsync(service).ConfigureAwait(false) : found;
        List<SaidWordView> handed = [.. moved?.Words ?? []];
        List<string> transfers = moved is null ? [] : [.. ran.Moved.Where(word => moved.Ids.Contains(word.Id, StringComparer.Ordinal)).Select(word => word.Transfer)];
        var high = RotatedOpening.ContextOf(null, context.Events, prior.Session);
        var ranOn = prior.Adapter ?? ran.Agent.Name;
        var message = new ChatMessage(
            string.Join("\n\n", [.. refused.Select(word => word.Text), .. handed.Select(word => word.Text), .. unkept.Select(word => word.Text)]),
            [.. refused.SelectMany(word => ChatFiles.Kept(context.Home, prior.Session, word.Files ?? [])), .. moved?.Files ?? [], .. unkept.SelectMany(word => word.Files)])
        {
            Brief = brief,
            TakenOver = HandOverNotes.MovedFrom(prior.Session, ranOn, prior.Profile, HandOverWhy.Limit, high, brief.Text.Length),
            Delivered = () =>
            {
                move.Wire(transfers);
                read.TrySetResult(Task.Run(async () =>
                {
                    if (moved is { Words.Count: > 0 } taking) await taking.TakeAsync(service).ConfigureAwait(false);
                    await move.ReadAsync().ConfigureAwait(false);
                }));
            },
        };

        var said = await wiring.Of<ChatHandOver>().SayHandedOver(ran.To, message, LimitMove.Shown(refused, handed, unkept)).ConfigureAwait(false);
        context.Note(prior.Session, HandOverNotes.MovedOn(ran.To, ran.Agent.Name, selection.Profile, HandOverWhy.Limit, high, brief.Text.Length));
        context.Note(ran.To, CarriedNotes.NotCarried(LimitCarry.Refused));
        service.AccountSaid(Continuations.Moved(
            ran.To, prior.Session, ranOn, ran.Agent.Name, prior.Profile, selection.Profile, HandOverWhy.Limit, briefed: true, carry: LimitCarry.Refused));
        if (!said) context.Note(ran.To, HandOverNotes.Unsaid());
        return started;
    }

    public void Withdraw()
    {
        if (selection.ProfileHome is { Length: > 0 } home && prior.Tree is { Length: > 0 } tree)
        {
            LimitCarry.Withdrawn(LimitCarry.Declared(context.Harnesses.Adapters, door.Id), home, tree, carried.Conversation);
        }
    }

    private async Task FailedAsync(string to, string why)
    {
        try
        {
            await context.Service.AdvanceAsync(to, "failed", Observation.Failure(why), ct: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException)
        {
            // The record says nothing more; the next look reads it as it now is.
        }
    }
}
