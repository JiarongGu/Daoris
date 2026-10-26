namespace Daoris.Driver;

/// <summary>
/// The agent's refusal of a choice at an open (D205 §3.3, MODELROLE1c2): the category and the value asked, and how,
/// <see cref="AgentOffers.NotOffered"/> where the open's list lacks it, <see cref="AgentOffers.RefusedCode"/> where the set was refused
/// or answered with another value the list offers.
/// </summary>
public sealed record ModelRefusal(string Category, string Value, string Code);
