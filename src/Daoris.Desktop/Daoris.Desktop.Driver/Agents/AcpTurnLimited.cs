namespace Daoris.Driver;

/// <summary>
/// A turn its door ended on its account's limit, the limit's sentence its agent's last message (<see cref="AcpLimitTurn"/>): the run ends
/// there as on a refused prompt, the sentence its failure, so the conclusion cools the account and the line goes on by its limit's rules,
/// never prompted again on that session.
/// </summary>
public sealed class AcpTurnLimited(string words) : DriverException($"the agent's turn ended on its account's limit: {words}")
{
    /// <summary>The sentence, as the agent's message said it.</summary>
    public string Words { get; } = words;
}
