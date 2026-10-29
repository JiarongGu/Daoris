using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What the session requests ask of Ask Daoris's judges, installed when the driver loads (<see cref="ICourseHelp"/>).</summary>
internal sealed class CourseHelpOperations : ICourseHelp
{
    [ModuleInitializer]
    internal static void Install() => CourseHelp.Current = new CourseHelpOperations();

    public Func<SessionRequest, CancellationToken, Task<HousekeepingAnswer>> Housekeeping(string home, Func<ServiceClient?> service) =>
        new HelpHousekeeping(home, service).AnswerAsync;

    public Func<SessionRequest, CancellationToken, Task<SettingAnswer>> Settings(string home, Func<ServiceClient?> service) =>
        new HelpSettings(home, service).AnswerAsync;
}
