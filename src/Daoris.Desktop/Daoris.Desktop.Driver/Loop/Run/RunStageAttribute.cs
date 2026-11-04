using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// Registers an <see cref="IRunStage"/> with the driver's run of a driven session: its name, its <see cref="RunPhase"/> and the stages
/// it runs after. The driver finds every marked class at its first run and refuses there a name declared twice, an unknown or later
/// stage named after, and a cycle.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class RunStageAttribute(string name, RunPhase phase, params string[] after) : PartAttribute(name, phase, after);
