using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// A declaration's kind: its one parse, of the content's bytes alone, so a parse is a function of its content key and is kept by it.
/// A parse that names a problem is made again from the kept bytes at each ask.
/// </summary>
internal interface IDeclarationKind<T> where T : class
{
    /// <summary>The parse of <paramref name="content"/>, reading nothing but it.</summary>
    T Parse(DeclarationBytes content);

    /// <summary>Whether the parse names a problem, so it is kept for none.</summary>
    bool Problem(T parsed);

    /// <summary>What the parse holds in memory, for its kind's bound.</summary>
    long Weigh(T parsed);
}

/// <summary>
/// A view: a pure function of its key record, made through the contents handle from the content keys that record holds and kept once a
/// key. The record holds every input the view rests on, so no path, clock or setting outside a content key reaches it.
/// </summary>
internal interface IDeclarationView<TKey, TValue> where TKey : notnull where TValue : class
{
    TValue View(TKey key, DeclarationContents contents);

    long Weigh(TValue value);
}

/// <summary>
/// The contents a view is made from: the parse of each content its asker handed the store, by its key. A view asking a key its asker
/// did not hand reads nothing and throws.
/// </summary>
internal sealed class DeclarationContents(IEnumerable<DeclarationBytes> held)
{
    private readonly ImmutableDictionary<ContentKey, DeclarationBytes> _held = held.ToImmutableDictionary(content => content.Key, content => content);

    internal T Parse<T>(ContentKey key, IDeclarationKind<T> kind) where T : class =>
        _held.TryGetValue(key, out var content)
            ? RepositoryDeclarations.Parse(content, kind)
            : throw new InvalidOperationException($"a view asked its contents for {key}, which its key does not hold");
}
