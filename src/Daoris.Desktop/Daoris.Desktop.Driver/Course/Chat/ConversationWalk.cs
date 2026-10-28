namespace Daoris.Driver;

/// <summary>
/// What a new conversation's walk over its actor's order answered (MODELROLE1d, D205 §4.1): the agent, the account and the entry it took,
/// and each entry it passed before with its code; or, where none can run, the wait's sentence the press is refused in (§4.5).
/// </summary>
internal sealed record ConversationWalk(
    ISessionAdapter? Adapter, HarnessSelection? Selection, ActorEffective? Chosen, IReadOnlyList<BuildPass> Passes, string? Refusal);
