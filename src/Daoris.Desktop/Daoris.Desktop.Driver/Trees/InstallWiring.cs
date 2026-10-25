using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The workplace's answer to the port the install declares for a child's git (FOUNDATION1 mod-v, the design §14.3): the prompt variables and
/// the doors <see cref="GitWiring"/> hands. Installed when the driver's assembly loads, before any start can pass the tools' seam, so every
/// host, the loop and every test hand a child exactly what they always did and no construction site hands the install anything.
/// </summary>
internal sealed class WorkingTreeGit : IChildGit
{
    [ModuleInitializer]
    internal static void Install() => ChildGit.Current = new WorkingTreeGit();

    public void Hand(ProcessStartInfo info, string? home) => GitWiring.Hand(info, home);
}
