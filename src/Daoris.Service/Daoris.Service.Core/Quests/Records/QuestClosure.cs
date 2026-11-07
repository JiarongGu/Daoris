namespace Daoris.Knowledge;

/// <summary>
/// A remote's answer to whether every machine of its workspace said it knows a bound quest: the machines it holds an operation or a push from whose last push declared no <c>closure</c>, by id.
/// None undeclared is every machine declared, and only then may a quest of that workspace bind to a closure graph. A remote before it
/// answers nothing, which is never read as all.
/// </summary>
public sealed record QuestClosure(IReadOnlyList<string> Undeclared)
{
    /// <summary>Whether every machine the remote knows of declared <c>closure</c>.</summary>
    public bool All => Undeclared.Count == 0;
}
