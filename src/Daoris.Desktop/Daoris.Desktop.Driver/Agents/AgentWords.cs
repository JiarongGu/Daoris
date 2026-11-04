using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A refusal the command makes before it acts, in its words, with today's exit: its grammar and its own checks.</summary>
public sealed class AgentWords(string message, int exit = 2) : Exception(message)
{
    public int Exit { get; } = exit;
}
