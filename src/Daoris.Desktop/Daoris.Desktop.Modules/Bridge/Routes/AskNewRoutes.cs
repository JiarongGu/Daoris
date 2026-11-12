using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>A new ask as <c>daoris ask "…"</c> presses it; <c>Files</c> are full paths on this machine, read when the ask is made.</summary>
public sealed record AskNewPress(
    string Workspace, string Sentence, string? To = null, IReadOnlyList<string>? Links = null, IReadOnlyList<string>? Files = null,
    string? Review = null, string? ReviewWords = null, string? Kind = null, string? Workflow = null, string? WorkflowWords = null,
    string? Short = null, bool Said = false, IReadOnlyList<AskRequirementPress>? Requirements = null, string? Through = null);

/// <summary>One requirement a new ask carries: the person's words and the check.</summary>
public sealed record AskRequirementPress(string Quote, string Check);

/// <summary>
/// A new ask as the person's press of the pipe (<c>ASK_NEW</c>): <c>daoris ask "…"</c> runs its own path (<c>said</c>) in this Daoris,
/// whose service client holds the person key, so the terminal's press is confirmed in the window, and Ask Daoris's delegate press runs as
/// its access says, never keyless. The page's composer posts to the same door itself.
/// </summary>
public sealed class AskNewRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>An ask made at a workspace, answered by its intake, or published at once as a quest for the receiver it names.</summary>
    [DriverRoute("ASK_NEW", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("ASK_NEW", Workspace = "workspace")]
    [GroundRepository("ASK_NEW", "to", "workspace")]
    [Says("ASK_NEW", "workspace:form", "sentence:words", "to:repository")]
    [Publishes("ASK_NEW", StateScopes.Asks | StateScopes.Quests)]
    private Task<object?> AskNewAsync(AskNewPress press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var ask = new AskNew(press.Workspace.Trim(), press.Sentence, Named(press.To), press.Links, press.Files,
            Named(press.Review), Named(press.ReviewWords), Named(press.Kind), Named(press.Workflow), Named(press.WorkflowWords), Named(press.Short),
            press.Requirements?.Select(each => new Daoris.Contracts.Quests.QuestRequirementWire(each.Quote, each.Check)).ToList(), Named(press.Through));
        return TerminalSaid.PressAsync(_loop, press.Said, (output, _) => AskQuotedPress.Refused(_loop, ask.Carried) is { } refused
            ? AskQuotedPress.Say(output, refused)
            : AskNewCommand.RunAsync(ask,
                new AskReviewWorld(service, () => DriverConfig.Load(_loop.ConfigPath)),
                new AskWorkflowWorld(service, () => DriverConfig.Load(_loop.ConfigPath), _loop.Home), output, cancellationToken));
    }

    private static string? Named(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
