namespace Daoris.Knowledge;

/// <summary>What an operation does to a memory (the memory coordinator design §3).</summary>
public static class MemoryOpKind
{
    public const string Write = "write";
    public const string Relate = "relate";
    public const string End = "end";
    public const string Confirm = "confirm";
    public const string Propose = "propose";

    /// <summary>A write that ends the entries it replaces, in one change once approved.</summary>
    public const string Supersede = "supersede";

    internal static readonly string[] All = [Write, Relate, End, Confirm, Propose, Supersede];

    /// <summary>Whether a released row of <paramref name="kind"/> awaits its approver: it ends an entry.</summary>
    internal static bool Ends(string kind) => kind is End or Supersede;
}
