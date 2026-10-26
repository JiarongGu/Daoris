using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkOrderAgentWords : IAgentWorkWords
{
    [ModuleInitializer]
    internal static void Install() => AgentWorkWords.Current = new WorkOrderAgentWords();

    public bool Quotes(IAgentAskWords? words, string text) => AskWordsText.Quotes(words, text);
    public string AskWords(IAgentAskWords? words, string questId) => AskWordsText.Beneath(words, questId);
    public string GoAheads(IAgentAskWords? words) => GoAheadsText.Beneath(words);
    public string Channel(ChannelHeld? held) => ChannelText.Beneath(held);
}
