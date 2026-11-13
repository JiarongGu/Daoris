using Daoris.Ports.Ambient;
using Daoris.Ports.Git;
using Daoris.Ports.Processes;
using Daoris.Ports.Repositories;

namespace Daoris.Driver;

/// <summary>
/// The one place the driver reads the machine's ports its root chose: the places a start takes, the admission each start is asked
/// for, the seal a kept key is kept under, and the git every git is asked of. The static start funnels, the git runners and the key
/// file ask here, so the root's choice is read once for the machine's whole family; a process whose root chose none has nothing real behind them, and a read throws.
/// </summary>
internal static class MachinePorts
{
    private static PortSet Chosen => AmbientPorts.Read();

    /// <summary>The machine's places for starts.</summary>
    internal static IMachineAdmission Admission =>
        Chosen.Admission ?? throw new InvalidOperationException("the root handed no machine admission, so this process places no start.");

    /// <summary>
    /// The application's admission of a start of <paramref name="program"/> for <paramref name="purpose"/>: the place it belongs to,
    /// or null; a refusal is thrown as the admission threw it.
    /// </summary>
    internal static StartPlace? Admit(string program, ProcessPurpose purpose) =>
        (Chosen.StartPolicy ?? throw new InvalidOperationException("the root handed no admission of starts, so this process starts nothing."))(
            program, purpose.ToString());

    internal static IProcessLiveness Liveness =>
        Chosen.Liveness ?? throw new InvalidOperationException("the root handed no liveness, so this process tells no live holder.");

    /// <summary>The seal a kept key is kept under.</summary>
    internal static SecretSeal Seal => Chosen.Seal ?? throw new InvalidOperationException("the root handed no seal, so this process keeps no key.");

    /// <summary>Git, which the static git funnel (<see cref="GitRuns"/>) runs every git through.</summary>
    /// <summary>The repository's operations the families ask, which the root hands (<see cref="GitRepository.Shared"/>, or a test flow's own).</summary>
    internal static IGitRepository Repository => Chosen.Repository ?? throw new InvalidOperationException("the root handed no repository, so this process asks git no question of one.");

    internal static IGit Git => Chosen.Git ?? throw new InvalidOperationException("the root handed no git, so this process runs no git.");
}
