namespace Daoris.Driver;

/// <summary>Daoris's word after a carried or seeded part's handed words: why the conversation goes on there, by the move's reason.</summary>
internal static class CarryWords
{
    /// <summary>The word for a part moved by <paramref name="why"/> (one of <see cref="HandOverWhy"/>) that goes on in <paramref name="carried"/>.</summary>
    public static string Of(string why, CarryPlan.Carried carried) =>
        why == HandOverWhy.Limit && carried is not CarryPlan.Seeded
            ? LimitCarry.Words
            : $"This conversation goes on here because {HandOverWhy.Noted(why)}. Go on with the work where it stopped.";
}
