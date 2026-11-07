namespace Daoris.Driver;

/// <summary>
/// The attempts one pass may call a plugin's work process about: a new start's send, and each attempt a reconciliation asks
/// after or delivers again, take one each from the same count, so <see cref="NodeRuntime.SendsPerPass"/> bounds the whole pass, not its
/// starts alone. What is left over waits for the next pass.
/// </summary>
public sealed class WorkSends(int left)
{
    /// <summary>No bound: a caller outside a pass (a test, a tool) that asks after every attempt due.</summary>
    public static WorkSends Unbounded => new(int.MaxValue);

    /// <summary>The calls still allowed in this pass.</summary>
    public int Left { get; private set; } = left;

    /// <summary>Takes one call, or says none is left.</summary>
    public bool Take()
    {
        if (Left <= 0) return false;
        Left--;
        return true;
    }
}
