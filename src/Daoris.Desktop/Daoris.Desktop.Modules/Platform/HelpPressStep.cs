using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The step of a run an Ask Daoris press acted on and the act pressed (an Accept, the pass of an unchecked done, a skip), so the person reads
/// in Notifications which step of which run was pressed for them, not only that a run's workflow was.
/// </summary>
/// <param name="Node">The step's id in the run's graph.</param>
/// <param name="Act">The act pressed on it, as the run's door names it.</param>
public sealed record HelpPressStep(string Node, string Act)
{
    /// <summary>The step a press naming a run carried, or null where it named no step and act.</summary>
    public static HelpPressStep? Of(IpcRequest request) =>
        HelpPressRecord.Named(request, "node") is { } node && HelpPressRecord.Named(request, "act") is { } act ? new(node, act) : null;
}
