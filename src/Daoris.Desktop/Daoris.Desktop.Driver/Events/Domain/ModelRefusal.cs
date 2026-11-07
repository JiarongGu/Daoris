namespace Daoris.Driver;

/// <summary>
/// The agent's refusal of a choice at an open: the category and the value asked, and how,
/// <see cref="AgentOffers.NotOffered"/> where the open's list lacks it, <see cref="AgentOffers.RefusedCode"/> where the set is refused
/// or answered with another value the list offers.
/// </summary>
public sealed record ModelRefusal(string Category, string Value, string Code);
