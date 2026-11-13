namespace Daoris.Knowledge;

/// <summary>The person's waiver of one release order, with their reason: the order is kept and shown as waived, and no longer orders a release, breaks a circle or goes stale.</summary>
/// <param name="Deployable">The deployable released second.</param>
/// <param name="On">The deployable it was to be released after.</param>
/// <param name="Reason">Why the person waived it; never empty.</param>
/// <param name="At">When they waived it.</param>
public sealed record DeliveryWaiver(DeliveryRef Deployable, DeliveryRef On, string Reason, DateTimeOffset At);
