using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// Registers an <see cref="IExecutor"/> with the chat runner: its kind, which names it, its <see cref="ExecutorPhase"/> and the executors it
/// comes after. The runner builds every marked class once, in that order, as it is made, and refuses there a kind declared twice, an
/// unknown or later executor named after, and a cycle.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class ExecutorAttribute(string name, ExecutorPhase phase, params string[] after) : PartAttribute(name, phase, after);
