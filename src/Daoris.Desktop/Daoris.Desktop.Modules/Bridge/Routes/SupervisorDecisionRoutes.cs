using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>The supervisor's one decision door: decide and leave, on any kind's subject, and what waits.</summary>
public sealed class SupervisorDecisionRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    // The supervisor's decision: `{ kind, subject, generation, proof, do, choice?, reasons }`, `subject` the kind's typed reference
    // (`{ session, question }`, `{ session, point }`), a role's act, `Role` in the pipe's census. Refused on every door but a terminal's the
    // judge's role method covered (`PipeDoor.Role`), and by the judge where the role's grants do not cover the kind's act (`not-granted`).
    // Kept against the subject as it stands: a question's answer held for the role's window, a course's go-on held, then released through the
    // count's own door. `{ kept, decision?, goesOnAt?, why? }`, refused as data and keeping nothing.
    [DriverRoute("SUPERVISOR_DECIDE", PipeClass.Role, Drain = PipeDrain.Passes)]
    [Publishes("SUPERVISOR_DECIDE", StateScopes.Sessions | StateScopes.Driver, Session = "subject.session")]
    private async Task<object?> SupervisorDecideAsync(SupervisorDecisionPress press, CancellationToken cancellationToken) =>
        await SupervisorDecisionDoor.DecideAsync(_loop, press.Form.Json, cancellationToken).ConfigureAwait(false);

    // The supervisor's leaving a subject to the owner: `{ kind, subject, generation, proof, bound, reasons }`, the role's second act, judged
    // as the decide is and needing no grant. Kept as a decision of state `left`, final at once: nothing reaches the session, and the owner's
    // card says *left to you* with the bound and the reasons. `{ kept, decision?, why? }`.
    [DriverRoute("SUPERVISOR_LEAVE", PipeClass.Role, Drain = PipeDrain.Passes)]
    [Publishes("SUPERVISOR_LEAVE", StateScopes.Sessions | StateScopes.Driver, Session = "subject.session")]
    private async Task<object?> SupervisorLeaveAsync(SupervisorDecisionPress press, CancellationToken cancellationToken) =>
        await SupervisorDecisionDoor.LeaveAsync(_loop, press.Form.Json, cancellationToken).ConfigureAwait(false);

    // What waits for the supervisor's holder: every kind's subjects in a place whose supervisor's live hold covers an act of the kind, each
    // with its generation and the acts covered. Told by event: it reads the service and the question files, so an ask is answered from what
    // is held and the verb asks until an answer computed after its own first ask (`at`) comes. A read naming no key:
    // `{ waiting: [{ kind, subject, repository, generation, acts, … }], at }`.
    [DriverRoute("SUPERVISOR_WAITING", PipeClass.Open)]
    [ByEvent("SUPERVISOR_WAITING", Untold = "the question files, and the verb waits for an answer made after its own ask")]
    private async Task<object?> SupervisorWaitingAsync(NoPayload _, CancellationToken cancellationToken) =>
        new { Waiting = await SupervisorDecisionDoor.WaitingAsync(_loop, cancellationToken).ConfigureAwait(false), At = Moments.Write(DateTimeOffset.UtcNow) };
}
