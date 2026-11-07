namespace Daoris.Driver;

/// <summary>
/// The home the running program acts for, named once by its entry: the window's and the host's each name theirs as they start,
/// the one they adopted or were started with. A unit that carries no home of its own reads this one, Daoris's own git and the tools'
/// environment a start with no home is handed among them; none reads the variable the process inherited, which in a test's or a
/// tool's process is the shell's, and there names the person's install.
/// </summary>
public static class ProgramHome
{
    /// <summary>The home the program's entry named; null where none was, as in a test's process, whose git is then the system's.</summary>
    public static string? Named { get; set; }
}
