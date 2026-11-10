using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// An ask's workflow and its review choice as the person's presses of the pipe: <c>daoris ask workflow|review</c> (and
/// <c>--set-workflow</c>, <c>--set-review</c>) run their own path (<c>said</c>) in this Daoris, whose service client holds the person key,
/// so the terminal's press is confirmed in the window and the service is called as the person's, never keyless.
/// </summary>
public sealed class AskChoiceRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    private const StateScopes Moves = StateScopes.Asks | StateScopes.Quests;

    /// <summary>An ask's kind and workflow, or none, which every chain the ask publishes whose work has not started follows.</summary>
    [DriverRoute("ASK_WORKFLOW", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("ASK_WORKFLOW", Ask = "ask")]
    [GroundAsk("ASK_WORKFLOW", "ask")]
    [Says("ASK_WORKFLOW", "ask:ask", "kind:form", "workflow:form", "words:words")]
    [Publishes("ASK_WORKFLOW", Moves)]
    private Task<object?> AskWorkflowAsync(AskWorkflowPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, _) => AskWorkflowCommand.RunAsync(
            new AskWorkflowAsk(Id(press.Ask), press.Kind, press.Workflow, Note(press.Words)),
            new AskWorkflowWorld(Service(), () => DriverConfig.Load(_loop.ConfigPath), _loop.Home), output, cancellationToken));

    /// <summary>Whether an ask's work is reviewed before it lands, and where: <c>on</c>, <c>off</c> or an environment its workspace declares.</summary>
    [DriverRoute("ASK_REVIEW", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("ASK_REVIEW", Ask = "ask")]
    [GroundAsk("ASK_REVIEW", "ask")]
    [Says("ASK_REVIEW", "ask:ask", "choice:form", "words:words")]
    [Publishes("ASK_REVIEW", Moves)]
    private Task<object?> AskReviewAsync(AskReviewPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, _) => AskReviewCommand.RunAsync(
            new AskReviewAsk(Id(press.Ask), press.Choice.Trim(), Note(press.Words)),
            new AskReviewWorld(Service(), () => DriverConfig.Load(_loop.ConfigPath)), output, cancellationToken));

    private Task<object?> SaidAsync(bool said, Func<TextWriter, TextWriter, Task<int>> run) => TerminalSaid.PressAsync(_loop, said, run);

    private ServiceClient Service() => _loop.Service ?? throw NotReady();

    private static string Id(string ask) => ask.Trim().TrimStart('#');

    private static string? Note(string? words) => string.IsNullOrWhiteSpace(words) ? null : words.Trim();
}
