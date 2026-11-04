using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// What the loop hears once its service is handed over (<see cref="DriverLoop.ComeUpAsync"/>): the records' moves and the person's
/// words told to the windows, its conversations' moves, and the receipts of places' applies a stop left uncertain.
/// </summary>
/// <remarks>
/// <para>The service holds its registry the moment it answers at all, so the service handed over is the whole of being ready, and
/// <c>DRIVER_READY</c> is said once, the moment the routes' <i>still coming up</i> stops: a page opened on a screen that reads the
/// service asks again on it rather than on a tick, which can wait on a remote's sync or on another driver's lease.</para>
/// <para>A chat needs the service that holds its record, so its runner is handed over with it. Two things only the loop does for its
/// conversations: a chat the runner takes up by itself announces its end, since no route that started it is there to hear it; and the
/// words kept on an ended chat while no shell ran are taken up in the background, so no look waits on them.</para>
/// </remarks>
internal static class LoopComeUp
{
    /// <summary>The service's moves and the person's words, told to the windows as they move.</summary>
    public static void HearService(DriverLoop loop, IEventBus eventBus, ServiceClient service)
    {
        // Words held while a session winds up are tried the moment its record moves here, and what a word said now would do is told
        // again as a record moves or a later session of its quest opens.
        service.Moved += loop.Words.OnMoved;
        service.Opened += loop.Words.OnOpened;
        // Every move of a record here tells the windows `sessions` for it, the move back of words withdrawn before the run began among
        // them, so Ask Daoris's list is not read again every 1.5 s while words wait, and a page that read `working` is told it is not.
        service.Moved += moved => StatePublisher.Publish(eventBus, StateScopes.Sessions, moved.Session);
        service.ActorKept += session => StatePublisher.Publish(eventBus, StateScopes.Sessions, session);
        // A follow that sent a registration tells the windows `registry`, so a repository set up from its page reads adopted with no
        // reload. No route declares the look's.
        service.RegistryFollowed += followed =>
        {
            if (followed.Sent) StatePublisher.Publish(eventBus, StateScopes.Registry);
        };
        loop.Words.Reached += (session, reach) =>
            _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", ConversationRoutes.QueueAnswer(loop, session, reach));

        // What a person told a driven session, waiting for its turn to end, told as the queue is, and `Listening` false once it stops
        // taking any, so the page takes its box away. One that closed leaves the session winding up, which only its record answers
        // for, so that is told once it has.
        loop.Processes.HeldChanged += (session, queue) =>
        {
            var reach = queue.Taking ? loop.Words.ReachHere(session) : null;
            _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", new
            {
                Session = session,
                // Each with the id it is taken back or reworded by.
                Queued = queue.Queued.Select(ChatMessage.FromAgent).Select(ConversationRoutes.Said).ToArray(),
                queue.Taking,
                // An open inbox always has a turn in hand; a closed one tells Idle.
                Listening = queue.Taking,
                Reach = ConversationRoutes.ReachOf(reach),
            });
            if (reach is null) loop.Words.Tell(session);
        };
    }

    /// <summary>The conversations' moves, told to the windows, and an Ask Daoris conversation going on writing its room as an open does.</summary>
    public static void HearChat(DriverLoop loop, IEventBus eventBus, ChatRunner chat)
    {
        // Where a conversation's turns stand, as it moves: each change is the whole state, so a missed one costs nothing, and what
        // waits is in no record until it is sent.
        chat.QueueChanged += (session, queue) =>
        {
            var reach = loop.Words.ReachHere(session);
            _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", new
            {
                Session = session,
                // The words, the names of their files (never where the files are kept) and each one's id.
                Queued = queue.Queued.Select(ConversationRoutes.Said).ToArray(),
                queue.Taking,
                // Whether what waits, waits for the door to open rather than for a turn.
                queue.Opening,
                // When its last turn ended here: the chat's last move, which its record never says. Machine-local.
                LastTurn = queue.LastTurnEnded,
                Reach = ConversationRoutes.ReachOf(reach),
            });
            if (reach is null) loop.Words.Tell(session);
        };
        chat.TakenUpEnded += (session, state) => _ = DriverLoop.Ended(eventBus, session, state);
        chat.Answered += item => _ = loop.AnsweredAsync(item);
        // A hand-over's or a curve move's take is over: the words held while it was taken go to the conversation that continues it,
        // and what a word said to the old one now would do is told again.
        chat.TakeEnded += loop.Words.OnTakeEnded;
        chat.DescribeHelp = ct => loop.DescribeHelp is { } describe ? describe(ct) : Task.FromResult<HelpMachine?>(null);
    }

    /// <summary>
    /// A place's apply that a stop left uncertain, read by its postcondition, asking and writing nothing but the receipt: what it found
    /// is the receipt's, and a press of the same plan goes on from there.
    /// </summary>
    public static async Task SettlePlacesAsync(DriverLoop loop, ServiceClient service, CancellationToken ct)
    {
        try
        {
            var config = DriverConfig.Load(loop.ConfigPath);
            var doors = new PlaceApplyDoors(
                new PlaceWorld(service), new SetupWorld(service, loop.Home), loop.Home, PluginCatalog.Load(loop.Home, AdapterSet.Built().Names),
                DriverContext.DoorOf(loop, config), () => DriverConfig.Load(loop.ConfigPath),
                _ => throw new InvalidOperationException("settling writes no configuration"), DateOnly.FromDateTime(DateTime.Now));
            await PlaceApplies.SettleAllAsync(doors, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Unread, and left as it was: the receipt keeps what it said, and a press of its plan reads again.
        }
    }
}
