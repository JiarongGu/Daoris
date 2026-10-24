using Daoris.Driver;

namespace Daoris.Desktop.GitDoor;

/// <summary>
/// Daoris's git doors (GITAUTH1b, the git-auth design §4.2 and §4.3), started by git alone: as <c>daoris-git-ssh</c>, git's ssh
/// (<see cref="GitDoors.Ssh"/>); as <c>git-remote-daoris</c>, the remote helper for a <c>daoris::</c> address
/// (<see cref="GitDoors.Helper"/>). The file's own name says which, so the publish lays one program out under both names.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // SUPERVISOR1a-ii (D208 §2.2): the holder key leaves this process first, so no start it builds can copy it.
        Environment.SetEnvironmentVariable("DAORIS_ROLE_KEY", null);
        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
        var folder = Environment.CurrentDirectory;
        if (name.StartsWith("git-remote-", StringComparison.OrdinalIgnoreCase))
        {
            using var input = Console.OpenStandardInput();
            using var output = Console.OpenStandardOutput();
            return GitDoors.Helper(args, Environment.GetEnvironmentVariable, input, output, Console.Error);
        }

        return GitDoors.Ssh(args, Environment.GetEnvironmentVariable, Console.Error, folder);
    }
}
