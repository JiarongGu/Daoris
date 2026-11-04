using System.Reflection;

namespace Daoris.Contracts.Parts;

/// <summary>
/// Finds a seam's parts in an assembly by their marks, and builds them, in the order their root runs them. A root orders its parts
/// once and builds them with its own wiring, so a part needs no edit to the root and the root needs none to take a new part.
/// </summary>
public static class Parts
{
    /// <summary>Every class of <paramref name="assembly"/> marked with <typeparamref name="TAttribute"/>, ordered by <see cref="PartOrder.Of"/>.</summary>
    /// <exception cref="PartOrderException">The marks cannot be ordered.</exception>
    public static IReadOnlyList<PartDeclaration> Declared<TAttribute>(Assembly assembly)
        where TAttribute : PartAttribute =>
        PartOrder.Of(assembly.GetTypes()
            .Select(type => (Type: type, Mark: type.GetCustomAttribute<TAttribute>(inherit: false)))
            .Where(each => each.Mark is not null)
            .Select(each => PartDeclaration.Of(each.Mark!, each.Type)));

    /// <summary>Each part of <paramref name="order"/> built by <paramref name="make"/>, kept in that order.</summary>
    /// <exception cref="PartOrderException">
    /// A part that is no <typeparamref name="TPart"/>, or one <paramref name="make"/> could not build: its constructor asks for wiring
    /// its root does not hand.
    /// </exception>
    public static PartSet<TPart> Build<TPart>(IReadOnlyList<PartDeclaration> order, Func<Type, TPart> make)
        where TPart : class
    {
        var built = new List<(PartDeclaration, TPart)>(order.Count);
        foreach (var part in order)
        {
            if (!typeof(TPart).IsAssignableFrom(part.Type))
            {
                throw new PartOrderException($"the part {part.Name} ({part.Type.Name}) is no {typeof(TPart).Name}");
            }

            try
            {
                built.Add((part, make(part.Type)));
            }
            catch (Exception error) when (error is MissingMethodException or TargetInvocationException or ArgumentException
                                              or InvalidCastException or MemberAccessException)
            {
                throw new PartOrderException(
                    $"the part {part.Name} ({part.Type.Name}) could not be built from its root's wiring: {error.Message}", error);
            }
        }

        return new PartSet<TPart>(built);
    }
}
