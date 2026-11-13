using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One semantic model per source tree of a compilation, shared by every census that binds the tree: a model keeps the member bodies
/// it bound, so a body the structure walk bound is not bound again by a declarations census reading the same compilation. A model
/// answers from many threads at once. A compilation let go takes its models with it.
/// </summary>
internal static class SharedModels
{
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<SyntaxTree, SemanticModel>> Models = new();

    /// <summary>The model of <paramref name="tree"/> in <paramref name="compilation"/>, made at its first ask.</summary>
    internal static SemanticModel Of(Compilation compilation, SyntaxTree tree) =>
        Models.GetValue(compilation, _ => new()).GetOrAdd(tree, each => compilation.GetSemanticModel(each));
}
