namespace Daoris.Driver;

/// <summary>A conversation whose turns are visible — what every door's question about a turn asks.</summary>
internal interface ITurnedChat
{
    IChatTurns Turns { get; }

    /// <summary>Let the turns already asked for run, then close the conversation's run (<paramref name="close"/>).</summary>
    Task FinishAsync(Func<Task> close);
}
