using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What a session's <c>system_plan</c> asks of the plugins, installed when the driver loads (<see cref="ICourseSystems"/>).</summary>
internal sealed class CourseSystemsOperations : ICourseSystems
{
    [ModuleInitializer]
    internal static void Install() => CourseSystems.Current = new CourseSystemsOperations();

    public Func<SessionRequest, CancellationToken, Task<SystemPlanAnswer>> Plan(string home, Func<ServiceClient?> service) =>
        new SystemPlanning(home, service).AnswerAsync;
}
