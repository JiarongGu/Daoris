using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A quest's done and its delete as the person's presses of the pipe: the service's own doors for them
/// (<c>POST /api/quests/{id}/done</c> and <c>DELETE /api/quests/{id}</c>), which the quest drawer and <c>daoris quest done|delete</c>
/// reach by HTTP, sent as the person's records. The steward's done and delete run these same routes inside its role's door.
/// </summary>
/// <remarks>A refusal of the service is an answer, <c>{ done: false, refused, message }</c>, in the service's own words.</remarks>
public sealed class QuestEndRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    private const StateScopes Ends = StateScopes.Quests | StateScopes.Sessions | StateScopes.Asks;

    /// <summary>The person's own done of a quest: their words, none of its requirements answered, never an agent's done.</summary>
    [DriverRoute("QUEST_DONE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("QUEST_DONE", Quest = "quest")]
    [GroundQuest("QUEST_DONE", "quest")]
    [Says("QUEST_DONE", "quest:quest")]
    [Publishes("QUEST_DONE", Ends)]
    private async Task<object?> QuestDoneAsync(QuestEndPress press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        if (press.Said)
        {
            var said = await TerminalSaid.RunAsync(output => QuestDoneCommand.RunAsync(new QuestDoneAsk(press.Quest.Trim().TrimStart('#'), press.Note), service, output, cancellationToken))
                .ConfigureAwait(false);
            _loop.Nudge();
            return said;
        }

        return Answer(await service.PersonDoneAsync(press.Quest.Trim(), press.Note, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A quest made by mistake deleted, refused in the service's words for one anything stands on.</summary>
    [DriverRoute("QUEST_DELETE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("QUEST_DELETE", Quest = "quest")]
    [GroundQuest("QUEST_DELETE", "quest")]
    [Says("QUEST_DELETE", "quest:quest")]
    [Publishes("QUEST_DELETE", Ends)]
    private async Task<object?> QuestDeleteAsync(QuestEndPress press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        if (press.Said)
        {
            var said = await TerminalSaid.RunAsync(output => QuestDeleteCommand.RunAsync(press.Quest.Trim().TrimStart('#'), service, output, cancellationToken))
                .ConfigureAwait(false);
            _loop.Nudge();
            return said;
        }

        return Answer(await service.DeleteQuestAsync(press.Quest.Trim(), cancellationToken).ConfigureAwait(false));
    }

    private object Answer((bool Ok, string Message) sent)
    {
        _loop.Nudge();
        return new { Done = sent.Ok, Refused = sent.Ok ? null : StewardWhy.Refused, sent.Message };
    }
}
