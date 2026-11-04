using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// Registers an <see cref="ITurnStage"/> with the protocol door: its name, its <see cref="TurnPhase"/> and the stages it runs after.
/// A session finds every marked class at its first frame and refuses there a name declared twice, an unknown or later stage named
/// after, and a cycle.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class TurnStageAttribute(string name, TurnPhase phase, params string[] after) : PartAttribute(name, phase, after);
