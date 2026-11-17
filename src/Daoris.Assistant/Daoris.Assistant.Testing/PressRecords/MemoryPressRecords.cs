using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>The window's presses in memory, in the order the test made them.</summary>
public sealed class MemoryPressRecords : IPressRecords
{
    private readonly Lock _gate = new();
    private readonly List<PressMade> _made = [];

    public IReadOnlyList<PressMade> Of(string conversation)
    {
        lock (_gate) return [.. _made.Where(each => each.Conversation == conversation)];
    }

    /// <summary>Records <paramref name="press"/> as made now.</summary>
    public void Press(PressMade press)
    {
        lock (_gate) _made.Add(press);
    }
}
