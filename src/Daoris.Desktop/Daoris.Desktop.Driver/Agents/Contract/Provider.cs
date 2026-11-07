namespace Daoris.Driver;

/// <summary>
/// An agent with one or more doors: one registration of a sealed record of
/// parts. What is a fact about another program is declared data, each fact in one place; every part is required, and a part stated
/// absent (<see cref="Known{T}.Absent"/>) reads as the behaviour for an agent that says nothing.
/// </summary>
/// <param name="Id">The agent's id, unique among providers and never another provider's door's.</param>
/// <param name="Identity">Product, maker, mark, stand-in, and the plugins that declared a provider made from declarations.</param>
/// <param name="Doors">Its doors; at least one, but for an unavailable descriptor.</param>
/// <param name="Program">Its own binary; absent for a provider made from declarations, whose doors are programs of their own.</param>
/// <param name="Accounts">The account pool: its key and facts (see <see cref="AccountsPart"/>).</param>
/// <param name="Tables">Limit, window, sign-in and refused words, and error words.</param>
/// <param name="Usage">How an account's windows are asked between turns.</param>
/// <param name="Lent">What a harness whose <c>accountOf</c> names this provider's id reads of it, one level.</param>
/// <param name="Models">The settings file: the account defaults' aliases and efforts, apart from what a session may carry.</param>
/// <param name="Children">The child vocabulary port.</param>
/// <param name="Rules">How it takes the product's rules; absent where it takes none, so the person's denies reach none of its sessions.</param>
/// <param name="Skills">How it lays out the skills a start is handed; absent where it is handed none.</param>
/// <param name="Conversations">The conversation home port.</param>
/// <param name="Reach">Where it may work; required, never absent, since absent would read as the widest answer.</param>
/// <param name="Vocabulary">The variables, files and wire keys it names, which the census reads.</param>
/// <param name="Unavailable">
/// Known and never started: a name of a disabled plugin kept so turning the plugin off loses none of its wiring, with no
/// door.
/// </param>
public sealed record Provider(
    ProviderId Id,
    ProviderIdentity Identity,
    IReadOnlyList<AgentDoor> Doors,
    Known<ToolSpec> Program,
    Known<AccountsPart> Accounts,
    Known<TablesPart> Tables,
    Known<UsagePart> Usage,
    Known<LentPart> Lent,
    Known<ISettingsFile> Models,
    Known<IChildVocabulary> Children,
    Known<IRuleLanguage> Rules,
    Known<ISkillLayout> Skills,
    Known<IConversationHome> Conversations,
    Reach Reach,
    Known<VocabularyPart> Vocabulary,
    bool Unavailable = false)
{
    /// <summary>The account key: its pool's string, or its id where it states no pool (no walk, no cool-off).</summary>
    public AccountKey KeyOf() => Accounts.IsKnown ? Accounts.Required.Key : new AccountKey(Id.Value);
}

/// <summary>
/// A way into a provider's agent, today an adapter keeping its id: its transport, its identity as its declaration
/// says it, its home (the variable its spawn hands an account's home in, its own home and trust file, each its own), what a harness
/// naming its id reads of it, its postures and abilities, and what a session on it may carry (<see cref="Offers"/>).
/// </summary>
/// <remarks>
/// <see cref="Lent"/> is the door's own account words, which a harness naming this door's id reads; a door that gives its provider its
/// id and is keyed by it (<c>claude-code</c>) has none of its own, since the provider holds what it declares. <see cref="AccountOf"/>
/// is the harness's <c>accountOf</c> verbatim (C3-2.1), null where it declares none; the door's account key is it, else its
/// provider's id. <see cref="Instructions"/> is what the door's program reads of a tree by itself.
/// </remarks>
public sealed record AgentDoor(
    DoorId Id,
    Transport Transport,
    DoorIdentity Identity,
    Known<ToolSpec> Program,
    DoorHome Home,
    LentPart Lent,
    Known<IDoorOffers> Offers,
    PosturesPart Postures,
    Known<ISpawn> Spawn,
    Known<IDialect> Dialect,
    AbilitiesPart Abilities,
    DoorTablesPart Tables,
    AccountKey? AccountOf,
    InstructionsPart Instructions);
