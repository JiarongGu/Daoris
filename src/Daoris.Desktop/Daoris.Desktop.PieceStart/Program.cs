using Daoris.Driver;

namespace Daoris.Desktop.PieceStart;

/// <summary>
/// The launcher every agent, plugin process and piece starts through (COAGENT1b, D199's note on the strict start; TERMTRUST1a, D201 point
/// 4): joins the jobs it is named, in order, then starts the harness inside them (<see cref="PieceLauncher.Run"/>).
/// </summary>
public static class Program
{
    public static int Main(string[] args) => PieceLauncher.Run(args, Console.Error, ProcessJob.JoinNamed, PieceLauncher.Start);
}
