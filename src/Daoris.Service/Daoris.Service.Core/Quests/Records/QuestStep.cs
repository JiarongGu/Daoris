namespace Daoris.Knowledge;

/// <summary>One step of a chain: whom to ask next, and what (D65 §4).</summary>
/// <param name="To">The repository asked — addressable from the chain's asker, judged when composed.</param>
/// <param name="Title">One line. <c>{parent}</c> becomes the id of the quest this step follows.</param>
/// <param name="Body">Why, and the evidence. <c>{parent}</c> is expanded here too.</param>
public sealed record QuestStep(string To, string Title, string Body)
{
    /// <summary>
    /// The environment this step shows the chain's work in, which makes it a set-up step (REVIEWENV1b, D154 point 4; the
    /// design's §2.1 calls it the step's <c>review</c>, a word the chain's choice already has). Null for every other step.
    /// </summary>
    public string? SetUpIn { get; init; }
}
