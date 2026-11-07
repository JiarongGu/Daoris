using Daoris.Driver;

namespace Daoris.Desktop.PieceStart;

/// <summary>
/// The launcher every agent, plugin process and piece starts through: joins the jobs it is named, in order, then starts the harness inside them (<see cref="PieceLauncher.Run"/>).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // The holder key leaves this process first, so no start it builds can copy it.
        Environment.SetEnvironmentVariable("DAORIS_ROLE_KEY", null);
        // The inbox's variables leave with it (the event hook design §6.3).
        Environment.SetEnvironmentVariable("CLAUDE_CODE_MESSAGING_SOCKET", null);
        Environment.SetEnvironmentVariable("CLAUDE_CODE_MESSAGING_TOKEN", null);
        return PieceLauncher.Run(args, Console.Error, ProcessJob.JoinNamed, PieceLauncher.Start);
    }
}
