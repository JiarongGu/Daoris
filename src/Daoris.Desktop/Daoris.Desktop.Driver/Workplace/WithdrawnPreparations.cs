namespace Daoris.Driver;

/// <summary>
/// A delivery whose landing made nothing: every entry a preparation its landing withdrew, as a refusal such as a replay's conflict ends
/// one. Only its unit's next landing would go on from it, and one that never comes would leave it unresolved, holding every going back
/// to an older build (<see cref="InstallRollback"/>), so the reconciler's look settles it withdrawn. A preparation the look itself reads
/// withdrawn after a stop is not one: its landing was cut, not refused, and it goes on for its unit's next landing.
/// </summary>
public static class WithdrawnPreparations
{
    /// <summary>What the look says, and the delivery's settlement records, of one it settles.</summary>
    public const string Said = "nothing of it stands beyond a preparation its landing withdrew, so it is settled withdrawn";

    /// <summary>Whether <paramref name="phases"/> hold entries, each a preparation ended withdrawn.</summary>
    public static bool Only(IReadOnlyList<DeliveryPhase> phases) =>
        phases.Count > 0 && phases.All(each => each.Phase == DeliveryPhases.Prepared && each.Outcome == DeliveryOutcomes.Withdrawn);
}
