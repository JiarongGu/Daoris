namespace Daoris.Knowledge;

/// <summary>One step of a chain: whom to ask next, and what.</summary>
/// <param name="To">The repository asked — addressable from the chain's asker, judged when composed.</param>
/// <param name="Title">One line. <c>{parent}</c> becomes the id of the quest this step follows.</param>
/// <param name="Body">Why, and the evidence. <c>{parent}</c> is expanded here too.</param>
public sealed record QuestStep(string To, string Title, string Body)
{
    /// <summary>
    /// The environment this step shows the chain's work in, which makes it a set-up step. Null for every other step.
    /// </summary>
    public string? SetUpIn { get; init; }
}
