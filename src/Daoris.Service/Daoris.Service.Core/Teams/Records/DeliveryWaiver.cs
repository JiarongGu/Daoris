namespace Daoris.Knowledge;

/// <summary>The person's waiver of one release order, with their reason: the order stays, shown as waived, and orders no release, closes no circle and goes stale no more.</summary>
/// <param name="Deployable">The deployable released second.</param>
/// <param name="On">The deployable it would follow.</param>
/// <param name="Reason">The person's reason; never empty.</param>
/// <param name="At">When.</param>
public sealed record DeliveryWaiver(DeliveryRef Deployable, DeliveryRef On, string Reason, DateTimeOffset At);
