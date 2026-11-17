namespace Daoris.Assistant.Tests;

/// <summary>The conversations' contract on the scripted fake.</summary>
public sealed class ScriptedConversationsContractTests : ConversationsContract
{
    private readonly ScriptedConversations _conversations = new();

    protected override IConversations Backend => _conversations;

    protected override Task EndTurnAsync(string conversation)
    {
        _conversations.EndTurn(conversation);
        return Task.CompletedTask;
    }
}
