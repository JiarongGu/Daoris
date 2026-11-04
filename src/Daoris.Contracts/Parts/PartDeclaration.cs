namespace Daoris.Contracts.Parts;

/// <summary>One part as its mark declares it, and the class that carries the mark.</summary>
public sealed record PartDeclaration(string Name, Enum Phase, IReadOnlyList<string> After, Type Type)
{
    /// <summary>The declaration a class's mark makes.</summary>
    public static PartDeclaration Of(PartAttribute mark, Type type) => new(mark.Name, mark.Phase, mark.After, type);
}
