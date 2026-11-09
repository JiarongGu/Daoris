using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// Registers a node kind with <see cref="NodeKit"/>: its id, a <see cref="NodeKinds"/> constant; the editor's group it is offered in; and
/// the kinds it comes after there. A name <c>*/&lt;step&gt;</c> registers Daoris's half of every plugin's step of that name. The kit builds every
/// marked class once, in that order, and refuses there an id declared twice, an unknown or later kind named after, and a cycle.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class NodeKindAttribute(string name, NodeGroup group, params string[] after) : PartAttribute(name, group, after)
{
    /// <summary>Whether the kind is declared before it is offered: not in the palette, refused by a save (<see cref="ArrivingKinds"/>).</summary>
    public bool Arriving { get; set; }
}
