using System.Diagnostics;

namespace Daoris.Adapters.Machine;

/// <summary>
/// Whatever owns the processes the host starts, as the root composes it: the one management that joins each to the job of its purpose,
/// so it ends with the application however that ends, and that ends a process and what it started.
/// </summary>
/// <param name="Take">Takes a process just started, before its caller holds it, and the purpose its specification named; true where it joined a job.</param>
/// <param name="End">Ends a process and everything it started: why the kill is refused, or null where it is made.</param>
public sealed record ProcessOwner(Func<Process, string, bool> Take, Func<Process, string?> End);
