using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a child is handed for its git, as the install declares it and the workplace answers (FOUNDATION1 mod-v, the design §14.3: the lower
/// declares a port, the higher implements it): the prompt variables every start takes and, for a start with a home, the doors installed
/// beside it (GITAUTH1a and GITAUTH1b). <see cref="Tools.Hand(ProcessStartInfo, ToolsRead, string)"/> hands a start the tools' environment
/// and asks the port for the git's, so every start the source scan holds still passes one seam.
/// </summary>
public interface IChildGit
{
    /// <summary>Hand a start the prompt variables and, where <paramref name="home"/> is not null, the doors installed beside it, with the home they act for.</summary>
    void Hand(ProcessStartInfo info, string? home);
}

/// <summary>
/// The git wiring in force in this process: the workplace's, installed when the driver's assembly loads (<c>Trees/InstallWiring.cs</c>), so
/// every host and test that starts a child hands it the git's variables as it always has. A test that wants another sets it and puts it back.
/// </summary>
public static class ChildGit
{
    private static IChildGit? _current;

    /// <summary>The git wiring in force.</summary>
    /// <exception cref="InvalidOperationException">None was installed: the assembly's own initializer has not run.</exception>
    public static IChildGit Current
    {
        get => _current ?? throw new InvalidOperationException("no git wiring is installed: the workplace installs it when the driver loads");
        set => _current = value;
    }
}
