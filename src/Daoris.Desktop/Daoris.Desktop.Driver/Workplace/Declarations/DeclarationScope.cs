namespace Daoris.Driver;

/// <summary>Whose ask a git start runs inside: the store's own reads, or the door's.</summary>
internal enum DeclarationAsker
{
    /// <summary>The store reading: a listing, a blob by id, a tracked list, and its passing through to git by path while a replacement can stand.</summary>
    Store,

    /// <summary>The door (<see cref="GitTreeReads"/>), which asks git for every path but the declarations it asks the store for.</summary>
    Door,
}

/// <summary>
/// The scope every member of the store and of the door works inside, an async local each sets on entry, so a start made after any
/// <c>await</c> of the ask carries it and the process gate's tally can say whose it is. The innermost member's scope is the one recorded.
/// </summary>
internal static class DeclarationScope
{
    private static readonly AsyncLocal<DeclarationAsker?> Asking = new();

    /// <summary>The innermost asker of the running flow; null outside the store and the door.</summary>
    internal static DeclarationAsker? Current => Asking.Value;

    /// <summary>Enters <paramref name="asker"/>'s scope until the answer is disposed.</summary>
    internal static Entered Enter(DeclarationAsker asker)
    {
        var outer = Asking.Value;
        Asking.Value = asker;
        return new Entered(outer);
    }

    /// <summary>The scope entered, restoring the outer one when disposed.</summary>
    internal readonly struct Entered(DeclarationAsker? outer) : IDisposable
    {
        public void Dispose() => Asking.Value = outer;
    }
}
