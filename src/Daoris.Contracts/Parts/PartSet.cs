namespace Daoris.Contracts.Parts;

/// <summary>A root's parts, built and in the order it runs them (<see cref="Parts.Build"/>).</summary>
public sealed class PartSet<TPart>
    where TPart : class
{
    private readonly IReadOnlyList<(PartDeclaration Declared, TPart Part)> _parts;

    internal PartSet(IReadOnlyList<(PartDeclaration, TPart)> parts) => _parts = parts;

    /// <summary>The declarations, in the order the root runs them.</summary>
    public IReadOnlyList<PartDeclaration> Order => [.. _parts.Select(each => each.Declared)];

    /// <summary>The parts of <paramref name="phase"/>, in order: what the root runs at that phase's anchor; with their names, <see cref="NamedIn"/>.</summary>
    public IEnumerable<TPart> In(Enum phase) => _parts.Where(each => each.Declared.Phase.Equals(phase)).Select(each => each.Part);

    public IEnumerable<(string Name, TPart Part)> NamedIn(Enum phase) =>
        _parts.Where(each => each.Declared.Phase.Equals(phase)).Select(each => (each.Declared.Name, each.Part));

    /// <summary>The one built part of type <typeparamref name="T"/>: for a root that also calls a module's other doors.</summary>
    public T Of<T>()
        where T : TPart => _parts.Select(each => each.Part).OfType<T>().Single();
}
