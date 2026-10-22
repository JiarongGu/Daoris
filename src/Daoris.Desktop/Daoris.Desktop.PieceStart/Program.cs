using Daoris.Driver;

namespace Daoris.Desktop.PieceStart;

/// <summary>
/// The partner's launcher (COAGENT1b, D199's note on the strict start), started by the driver for a partner alone: joins the piece's
/// job, then starts the harness inside it (<see cref="PieceLauncher.Run"/>).
/// </summary>
public static class Program
{
    public static int Main(string[] args) => PieceLauncher.Run(args, Console.Error, ProcessJob.JoinNamed, PieceLauncher.Start);
}
