using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A quest's departure accept, reopen and go-on, its review verdict and its workflow as the person's presses of the pipe: the verbs
/// <c>daoris quest accept|reopen|go-on|review|workflow</c> run their own path (<c>said</c>) in this Daoris, whose service client holds the
/// person key, so the terminal's press is confirmed in the window and the service is called as the person's, never keyless.
/// </summary>
/// <remarks>
/// A refusal of the service is an answer in its own words, the verb's exit 1; the route itself refuses only a quest the service does not
/// hold, which the verb says first.
/// </remarks>
public sealed class QuestVerdictRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    private const StateScopes Moves = StateScopes.Quests | StateScopes.Sessions | StateScopes.Asks;

    /// <summary>A done's departure from what the person required, accepted: the work it held goes on.</summary>
    [DriverRoute("QUEST_ACCEPT", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("QUEST_ACCEPT", Quest = "quest")]
    [GroundQuest("QUEST_ACCEPT", "quest")]
    [Says("QUEST_ACCEPT", "quest:quest")]
    [Publishes("QUEST_ACCEPT", Moves)]
    private Task<object?> QuestAcceptAsync(QuestEndPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, _) => QuestAcceptCommand.RunAsync(Id(press.Quest), Service(), output, cancellationToken));

    /// <summary>A sealed submission reopened, its agent making it ready again: the person's, with their words where they give any.</summary>
    [DriverRoute("QUEST_REOPEN", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("QUEST_REOPEN", Quest = "quest")]
    [GroundQuest("QUEST_REOPEN", "quest")]
    [Says("QUEST_REOPEN", "quest:quest", "note:words")]
    [Publishes("QUEST_REOPEN", Moves)]
    private Task<object?> QuestReopenAsync(QuestEndPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, _) => QuestReopenCommand.RunAsync(new QuestReopenAsk(Id(press.Quest), GoOn: false, Note(press.Note)), Service(), output, cancellationToken));

    /// <summary>The step a source's reopen held, gone on with the new work.</summary>
    [DriverRoute("QUEST_GO_ON", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("QUEST_GO_ON", Quest = "quest")]
    [GroundQuest("QUEST_GO_ON", "quest")]
    [Says("QUEST_GO_ON", "quest:quest")]
    [Publishes("QUEST_GO_ON", Moves)]
    private Task<object?> QuestGoOnAsync(QuestEndPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, _) => QuestReopenCommand.RunAsync(new QuestReopenAsk(Id(press.Quest), GoOn: true, null), Service(), output, cancellationToken));

    /// <summary>
    /// The person's verdict on a review, or their choice at the chain's gate. A <i>not yet</i>'s words go to the step's session as its next turn,
    /// by <c>sessions say</c>'s own path.
    /// </summary>
    [DriverRoute("QUEST_REVIEW", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("QUEST_REVIEW", Quest = "quest")]
    [GroundQuest("QUEST_REVIEW", "quest")]
    [Says("QUEST_REVIEW", "quest:quest", "verdict:form", "environment:form", "words:words")]
    [Publishes("QUEST_REVIEW", Moves)]
    private Task<object?> QuestReviewAsync(QuestReviewPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, _) =>
        {
            var ask = new QuestReviewAsk(Id(press.Quest), press.Environment is null ? press.Verdict ?? throw Because(press) : QuestReviewCommand.SetUpStep, Note(press.Words))
            {
                SetUpIn = press.Environment,
            };
            var sessions = SessionsVerbs.WorldOf(_context);
            var log = _loop.Log;
            return QuestReviewCommand.RunAsync(ask, new QuestReviewWorld(Service())
            {
                // The rule a set-up's choice is judged against: the service holds none.
                Config = DriverConfig.Load(_loop.ConfigPath),
                Say = (session, words, ct) => SessionsCommand.RunAsync(new SessionsAsk("say") { Ids = [session], Text = words }, sessions, output, ct),
                Log = log is null ? null : line => SessionLog.WriteLanding(log, line),
            }, output, cancellationToken);
        });

    /// <summary>A quest's own kind and workflow, or none, as its page's choice sets it.</summary>
    [DriverRoute("QUEST_WORKFLOW", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("QUEST_WORKFLOW", Quest = "quest")]
    [GroundQuest("QUEST_WORKFLOW", "quest")]
    [Says("QUEST_WORKFLOW", "quest:quest", "kind:form", "workflow:form", "words:words")]
    [Publishes("QUEST_WORKFLOW", Moves)]
    private Task<object?> QuestWorkflowAsync(QuestWorkflowPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, _) => QuestWorkflowCommand.RunAsync(
            new QuestWorkflowAsk(Id(press.Quest), press.Kind, press.Workflow, Note(press.Words)),
            new AskWorkflowWorld(Service(), () => DriverConfig.Load(_loop.ConfigPath), _loop.Home), output, cancellationToken));

    private Task<object?> SaidAsync(bool said, Func<TextWriter, TextWriter, Task<int>> run) => TerminalSaid.PressAsync(_loop, said, run);

    private ServiceClient Service() => _loop.Service ?? throw NotReady();

    private static string Id(string quest) => quest.Trim().TrimStart('#');

    private static string? Note(string? words) => string.IsNullOrWhiteSpace(words) ? null : words.Trim();

    private static Exception Because(QuestReviewPress press) =>
        new DriverException($"a review press names a verdict or an environment for quest `{press.Quest}`, and none was given.");
}
