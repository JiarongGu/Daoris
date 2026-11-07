namespace Daoris.Knowledge;

/// <param name="Accepted">Every operation now numbered — including any the remote already held.</param>
/// <param name="Behind">Quests something else reached first: fetch, rebase, and push again.</param>
/// <param name="Refused">Quests whose operations do not apply, or whose publish the exchange refused.</param>
public sealed record QuestPush(
    IReadOnlyList<QuestAcceptance> Accepted, IReadOnlyList<string> Behind, IReadOnlyList<QuestPushRefusal> Refused)
{
    /// <summary>The remote's handshake after this push; null from a remote before it.</summary>
    public QuestClosure? Closure { get; init; }
}
