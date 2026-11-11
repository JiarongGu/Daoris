namespace Daoris.Driver;

/// <summary>
/// A carried Accept stands for the repair its landing asked for and nothing more: the carry-on that commits the uncommitted work or resolves
/// the conflict with the line. The review begun again at a tip that continues the one accepted is accepted again only while its parts and
/// their handbacks are those of the record the person accepted; a new part, a new handback or a send-back lapses it, and the review asks again.
/// </summary>
internal static class CarriedRepairs
{
    /// <summary><paramref name="repair"/> naming the review attempt the person accepted: the first carry's, kept across the carries after it.</summary>
    public static NodeRepair Named(NodeRecord? record, NodeRepair repair) => repair with
    {
        Attempt = record?.Repairs.FirstOrDefault(each => each.Node == repair.Node)?.Attempt
            ?? (record is null ? null : WorkPerformer.StatusOf(record, repair.Node).Acceptance?.Attempt),
    };

    /// <summary>
    /// Whether the work the review at <paramref name="tip"/> reads is still the record the person accepted for <paramref name="repair"/>, its
    /// commits aside: the same parts with the same handbacks. A repair that names no attempt stands; a record that does not read lapses it.
    /// </summary>
    public static bool Stands(string home, RunScope scope, NodeRepair repair, string tip)
    {
        if (repair.Attempt is not { } attempt) return true;
        if (ReviewRecords.Read(home, scope.Run.Binding.Run, repair.Node, attempt) is not { } accepted) return false;
        var present = ReviewWork.Read(home, scope, tip).Parts;
        return accepted.Parts.Select(Key).SequenceEqual(present.Select(Key));
    }

    /// <summary>The words the Accept given again carries: the record accepted and the tip the repair carried it to.</summary>
    public static string Receipt(string home, string run, NodeRepair repair, string tip) =>
        $"carried on from the record you accepted ({ReviewAccepts.Short(repair.Attempt is { } attempt ? ReviewRecords.Read(home, run, repair.Node, attempt)?.Revision : null)}) "
        + $"to {ReviewAccepts.Short(tip)}, to finish the repair its landing asked for";

    private static (string, string?, long?) Key(ReviewPart part) => (part.Quest, part.Machine, part.Sequence);
}
