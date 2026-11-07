using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>The supervisor's course on a referred point (CONTEXTDRIVE1j), the role's third act.</summary>
public sealed class SupervisorCourseRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    // The supervisor's course on a point a person's `refer` rule holds a line at: `{ id, point, generation, proof, do, reasons }`, the
    // role's third act, judged as the answer is. A `go-on` is held for the role's window, then goes through the count's own door between
    // turns; a `hold` leaves the point to the owner at once. `{ kept, decision?, goesOnAt?, why? }`, refused as data.
    [DriverRoute("SUPERVISOR_COURSE", PipeClass.Role, Drain = PipeDrain.Passes)]
    [Publishes("SUPERVISOR_COURSE", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SupervisorCourseAsync(SupervisorActPress press, CancellationToken cancellationToken) =>
        await SupervisorCourseDoor.CourseAsync(_loop, press.Form.Json, cancellationToken).ConfigureAwait(false);
}
