using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// Registers an <see cref="ILookPass"/> with the driver's look: its name, its <see cref="LookPhase"/> and the passes it runs after.
/// The driver finds every marked class at its first look and refuses there a name declared twice, an unknown or later pass named
/// after, and a cycle.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class LookPassAttribute(string name, LookPhase phase, params string[] after) : PartAttribute(name, phase, after);
