namespace Daoris.Driver;

/// <summary>
/// An agent with one or more doors (AGENTPROVIDER1; the agent-provider design §2.1, §2.3): one registration of a sealed record of
/// parts. What is a fact about another program is declared data, each fact in one place; every part is required, and a part stated
/// absent (<see cref="Known{T}.Absent"/>) reads as today's behaviour for an agent that says nothing (D53). Row a names the parts and
/// fills the ones today's toolchains declare; the parts whose ports a later row declares are <see cref="PortRef"/>s stated absent.
/// </summary>
/// <param name="Id">The agent's id, unique among providers and never another provider's door's.</param>
/// <param name="Identity">Product, maker, mark, stand-in, and the plugins that declared a provider made from declarations.</param>
/// <param name="Doors">Its doors; at least one, but for an unavailable descriptor.</param>
/// <param name="Program">Its own binary; absent for a provider made from declarations, whose doors are programs of their own.</param>
/// <param name="Accounts">The account pool: its key and facts (see <see cref="AccountsPart"/>).</param>
/// <param name="Tables">Limit, window, sign-in and refused words, and error words.</param>
/// <param name="Usage">How an account's windows are asked between turns.</param>
/// <param name="Lent">What a harness whose <c>accountOf</c> names this provider's id reads of it, one level (O4-F1).</param>
/// <param name="Models">The settings file port (a later row).</param>
/// <param name="Children">The child vocabulary port (a later row).</param>
/// <param name="Rules">The rule language port (a later row).</param>
/// <param name="Skills">The skill layout port (a later row).</param>
/// <param name="Conversations">The conversation home port (a later row).</param>
/// <param name="Reach">Where it may work; required, never absent, since absent would read as the widest answer (O1-F12).</param>
/// <param name="Vocabulary">The variables, files and wire keys it names, which the census reads.</param>
/// <param name="Unavailable">
/// Known and never started: a name of a disabled plugin kept so turning the plugin off loses none of its wiring (C1-1.2), with no
/// door.
/// </param>
public sealed record Provider(
    ProviderId Id,
    ProviderIdentity Identity,
    IReadOnlyList<AgentDoor> Doors,
    Known<ProgramFacts> Program,
    Known<AccountsPart> Accounts,
    Known<TablesPart> Tables,
    Known<UsagePart> Usage,
    Known<LentPart> Lent,
    Known<PortRef> Models,
    Known<IChildVocabulary> Children,
    Known<PortRef> Rules,
    Known<PortRef> Skills,
    Known<IConversationHome> Conversations,
    Reach Reach,
    Known<VocabularyPart> Vocabulary,
    bool Unavailable = false)
{
    /// <summary>The account key: its pool's string, or its id where it states no pool (no walk, no cool-off).</summary>
    public AccountKey KeyOf() => Accounts.IsKnown ? Accounts.Required.Key : new AccountKey(Id.Value);
}

/// <summary>
/// A way into a provider's agent, today an adapter keeping its id (the design §2.3): its transport, its identity as its declaration
/// says it, its home (the variable its spawn hands an account's home in, its own home and trust file, each its own), what a harness
/// naming its id reads of it, its postures and abilities. The parts whose ports a later row declares (offers, spawn, dialect) are
/// <see cref="PortRef"/>s stated absent.
/// </summary>
/// <remarks>
/// <see cref="Lent"/> is the door's own account words, which a harness naming this door's id reads; a door that gives its provider its
/// id and is keyed by it (<c>claude-code</c>) has none of its own, since the provider holds what it declares. <see cref="AccountOf"/>
/// is the harness's <c>accountOf</c> verbatim (C3-2.1), null where it declares none; the door's account key is it, else its
/// provider's id.
/// </remarks>
public sealed record AgentDoor(
    DoorId Id,
    Transport Transport,
    DoorIdentity Identity,
    Known<ProgramFacts> Program,
    DoorHome Home,
    LentPart Lent,
    Known<PortRef> Offers,
    PosturesPart Postures,
    Known<ISpawn> Spawn,
    Known<IDialect> Dialect,
    AbilitiesPart Abilities,
    DoorTablesPart Tables,
    AccountKey? AccountOf);
