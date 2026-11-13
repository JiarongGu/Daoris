using Daoris.Ports.Processes;

namespace Daoris.Driver;

/// <summary>
/// Git's admission of a start, as a root hands it to the machine (<see cref="StartPolicy"/>): a git's image starts only in a place
/// the git scheduler issued, and any start made in a flow that holds such a place takes the place's class and holds it until it
/// exits. The machine's start funnels ask it through the port and never name git's places, so the machine depends on no git.
/// </summary>
public static class GitStartAdmission
{
    /// <summary>The place a start of <paramref name="program"/> belongs to, or null outside every git place.</summary>
    /// <exception cref="GitStartRefusedException">A git's image with no place of the scheduler's.</exception>
    public static StartPlace? Admit(string program, string purpose)
    {
        GitPlace.Admit(program, Enum.Parse<ProcessPurpose>(purpose));
        return GitPlace.Current is { } place ? new StartPlace(ProcessStarts.ClassOf(place.Whose), place.Bind) : null;
    }
}
