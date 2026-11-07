using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>WORK_HELD</c>'s word <c>ask-fix</c>: the window's <i>Ask for a fix…</i> and <c>daoris workflow fix</c> are this one
/// press. It publishes the ask and moves no node; the terminal, which asks for <c>said</c>, is answered the verb's own line.
/// </summary>
internal static class HeldFixPress
{
    public static async Task<object> AnswerAsync(ServiceClient service, NodeRecordFiles records, WorkHeldPress press, CancellationToken ct)
    {
        if (press.Said) return await TerminalSaid.RunAsync(output => WorkflowFixCommand.AskAsync(service, records, press.Unit, press.Node, press.Attempt, output, ct)).ConfigureAwait(false);

        var pressed = await VerifyFixAsks.AskAsync(service, records, press.Unit, press.Node, press.Attempt, ct).ConfigureAwait(false);
        return new
        {
            press.Unit, press.Node, press.Attempt, press.Resolution, pressed.Done, Refused = pressed.Done ? null : "not-asked", pressed.Message,
        };
    }
}
