namespace Daoris.Driver;

/// <summary>
/// Loop's implementation of the open's <see cref="IChoiceReask"/> (the agent-provider design §2.5, row n): the walk asked for a refused model,
/// Roles' for a driven run and Course's pending and walk for a conversation, each composed where its executor is built and handed here as
/// <paramref name="walk"/>. The refusal itself is already kept in the offers store (<see cref="OfferRefusals"/>) when the reading arrives.
/// </summary>
internal sealed class ChoiceReask(Func<ModelRefusal, Task<ActorAsked?>> walk) : IChoiceReask
{
    public Task<ActorAsked?> ReaskAsync(ModelRefusal refusal) => walk(refusal);

    /// <summary>The port over <paramref name="walk"/>, or none where no actor choice was asked and so nothing is judged.</summary>
    public static IChoiceReask? Of(Func<ModelRefusal, Task<ActorAsked?>>? walk) => walk is null ? null : new ChoiceReask(walk);
}
