namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The test classes that change this process's <c>PATH</c> to put a shim on it, run one at a time.
/// </summary>
/// <remarks>
/// 🔴 <c>PATH</c> is the process's, not a test's. Two classes each prepended a folder and restored the
/// saved value when done: run in parallel, one restored <c>PATH</c> while the other's shim still had to
/// be found on it, and a passing test failed as "not found" under a loaded full run (seen during the
/// merges of 2026-09-30). Every other test only ever reads a <c>PATH</c> that still holds all it had,
/// so only these need to be kept apart from each other.
///
/// Since the Process half runs its classes several at once (PROCPAR1), these run alone, after the rest, as
/// <see cref="Alone"/>'s do: a child any class beside them started meanwhile would inherit the shim's <c>PATH</c>.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessPath
{
    public const string Name = "the process PATH";
}
