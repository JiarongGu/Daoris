namespace Daoris.Driver;

/// <summary>Work orders renders the ask, go-ahead and channel sections from a start's lower inputs.</summary>
public interface IAgentWorkWords
{
    bool Quotes(IAgentAskWords? words, string text);
    string AskWords(IAgentAskWords? words, string questId);
    string GoAheads(IAgentAskWords? words);
    string Channel(ChannelHeld? held);
}

/// <summary>The required instruction operations installed by Work orders when the driver loads.</summary>
public static class AgentWorkWords
{
    private static IAgentWorkWords? _current;
    public static IAgentWorkWords Current
    {
        get => _current ?? throw new InvalidOperationException("no agent work words are installed: Work orders installs them when the driver loads");
        set => _current = value;
    }
}
