namespace Daoris.Driver;

/// <summary>How a unit's <c>land</c> is reached by a delivery, one of these words.</summary>
public static class DeliveryReaches
{
    /// <summary>By its own delivery, delivered.</summary>
    public const string Delivered = "delivered";

    /// <summary>Brought into the head's delivery by the one Accept that carried several sessions: it had no delivery of its own.</summary>
    public const string BroughtInto = "brought-into";

    /// <summary>Bound as a member to an operation still open: its head's Accept delivers it, and it starts no delivery of its own.</summary>
    public const string Bound = "bound";
}

/// <summary>What reached a unit's <c>land</c>, or binds it: how, the delivery (null where no delivery of this record did), and its words.</summary>
public sealed record DeliveryReached(string Unit, string How, string? Delivery, string Said);

/// <summary>
/// The reach of a unit's <c>land</c>: one Accept carrying several sessions is
/// one delivery, the head's unit's. Each member is reached as brought into it once the operation that brought them together has landed, read
/// from that operation, the inclusion record of every form; while the operation is open the member is bound to it and starts no delivery of
/// its own, so there is no second fence.
/// </summary>
public static class DeliveryReach
{
    /// <summary>How <paramref name="unit"/>'s <c>land</c> is reached or bound now; null where nothing has reached or binds it.</summary>
    public static DeliveryReached? Of(string home, DeliveryStore store, string unit)
    {
        if (store.OfUnit(unit).FirstOrDefault(each => each.Settled == DeliverySettlements.Delivered) is { } own)
        {
            return new(unit, DeliveryReaches.Delivered, own.Key,
                $"`{unit}` was delivered by its own delivery {own.Key[..12]} ({own.Intent.Method} of {Short(own.Intent.Tip)})");
        }

        IReadOnlyList<TogetherOperation> operations;
        try
        {
            operations = new TogetherOperations(home).All();
        }
        catch (IOException error)
        {
            return new(unit, DeliveryReaches.Bound, null,
                $"whether `{unit}` is being brought together with other sessions could not be read ({error.Message}), so it starts no delivery now");
        }

        foreach (var operation in operations.Where(each => each.Members.Any(member => Is(member, unit))).OrderByDescending(each => each.Since))
        {
            if (operation.IsOpen)
            {
                return new(unit, DeliveryReaches.Bound, null,
                    $"`{unit}` is being brought together with another session's work (operation {operation.Id}, {operation.Phase}): its head's Accept "
                    + "delivers it, so it starts no delivery of its own");
            }

            if (operation.Phase != TogetherPhase.Landed || operation.Attempt is not { } attempt) continue;
            var member = operation.Members.First(each => Is(each, unit));
            var head = store.At(operation.Repository, attempt.Pinned).FirstOrDefault(each => each.Settled == DeliverySettlements.Delivered);
            var into = attempt.Form == TogetherForms.Merge ? $"the line `{attempt.Target}`" : $"`{attempt.Target}`";
            return new(unit, DeliveryReaches.BroughtInto, head?.Key,
                $"`{unit}`'s work at {Short(member.Tip)} was brought into {into} by operation {operation.Id}"
                + (head is null ? "" : $", in delivery {head.Key[..12]} of `{head.Intent.Unit}`"));
        }

        return null;
    }

    private static bool Is(TogetherMember member, string unit) => string.Equals(member.Unit, unit, StringComparison.Ordinal);

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);
}
