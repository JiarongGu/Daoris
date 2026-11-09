namespace Daoris.Driver;

/// <summary>
/// What a take does with the slot's ignored tops, read once at the new start (the tree-pool design §36.3, 1, §37.3, 1): the outputs
/// moved aside after the switch, the intermediates moved only where the toolchain moved or the slot passed its bound, the tops holding a
/// path tracked at the start, listed again after the switch and sorted entry by entry, the bases kept, and the rules the plan reads,
/// which the clearing sorts the listed-again entries with and reads no other.
/// </summary>
internal sealed record TakePlan(
    IReadOnlyList<string> Outputs, IReadOnlyList<string> Intermediates, IReadOnlyList<string> Relisted, IReadOnlyList<TreeBase> Bases, StoreDependencies? Rules)
{
    internal static readonly TakePlan None = new([], [], [], [], null);

    /// <summary>The set-aside folder the take moves into, the one its bases' trimmed entries join at their release.</summary>
    internal string Aside { get; init; } = "";

    /// <summary>What the plan set aside that a base would have kept, and why, said by the take.</summary>
    internal IReadOnlyList<string> Said { get; init; } = [];
}
