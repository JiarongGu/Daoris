namespace Daoris.Assistant.Tests;

/// <summary>The turns' contract on the scripted fake.</summary>
public sealed class ScriptedTurnEventsContractTests : TurnEventsContract
{
    private readonly ScriptedTurnEvents _turns = new();

    protected override ITurnEvents Backend => _turns;

    protected override Task<long> AppendAsync(string conversation, string kind, string text) => Task.FromResult(_turns.Append(conversation, kind, text));

    protected override Task EndTurnAsync(string conversation, TurnOutcome outcome)
    {
        _turns.EndTurn(conversation, outcome);
        return Task.CompletedTask;
    }
}
