namespace Daoris.Driver;

/// <summary>
/// One look over the repositories (<see cref="TreeContext.OneLook"/>): git's guess at each checkout's line, kept by the flow that opened it
/// until it is disposed. The flow's slot is an async local, which an awaited method that set it does not hand back to its caller, so a
/// start whose stages are separate methods takes the look into the next stage with <see cref="Reenter"/>, and git's guess stays asked once.
/// </summary>
internal sealed class TreeLook(AsyncLocal<Dictionary<string, Task<string?>>?>? slot, Dictionary<string, Task<string?>>? asked) : IDisposable
{
    /// <summary>The look of a flow already in one: it keeps nothing and ends nothing.</summary>
    public static TreeLook Nested { get; } = new(null, null);

    /// <summary>Until disposed, this flow shares the look's answers, where it holds no look of its own.</summary>
    public TreeLook Reenter()
    {
        if (slot is null || slot.Value is not null) return Nested;
        slot.Value = asked;
        return this;
    }

    public void Dispose()
    {
        if (slot is not null) slot.Value = null;
    }
}
