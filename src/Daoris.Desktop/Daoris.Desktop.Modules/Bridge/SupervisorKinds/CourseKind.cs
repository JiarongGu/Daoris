using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The course kind: a point a refer holds a line at by its answers alone or a quest's failed sessions, released by a go-on.</summary>
internal sealed class CourseKind : ReferredPointKind
{
    public override string Kind => SupervisorKinds.Course;
}
