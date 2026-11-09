using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A reference to a banned symbol: the file under the desktop tree, its line, and the banned symbol's documentation id.</summary>
internal sealed record BannedUse(string File, int Line, string Symbol);

/// <summary>
/// The list of banned symbols and the walk the boundary trusts in place of the analyzer's silence: every reference in a compilation's
/// sources to a listed symbol (a member's use, a construction, a conversion through a listed operator, a member of a listed type),
/// found from the syntax trees, so no quieting of the build's error hides one. A name spelled as a listed symbol's that does not bind is
/// an edge the walk cannot follow, said as a use of <c>?</c> and its name.
/// </summary>
internal static class BannedNames
{
    /// <summary>The one list, in the driver's folder, every shipping project's build reads.</summary>
    internal static string ListFile => Path.Combine(DesktopCompilations.Desktop, "Daoris.Desktop.Driver", "BannedSymbols.txt");

    /// <summary>The documentation id of each line of a list: what stands before its message.</summary>
    internal static ImmutableArray<string> Listed(string text) =>
        [.. text.Split('\n').Select(line => line.Split(';')[0].Trim()).Where(id => id.Length > 0 && !id.StartsWith("//", StringComparison.Ordinal))];

    /// <summary>The ids the one list bans.</summary>
    internal static ImmutableArray<string> Listed() => Listed(File.ReadAllText(ListFile));

    /// <summary>Each of <paramref name="ids"/> that names no symbol of <paramref name="compilation"/>.</summary>
    internal static IEnumerable<string> Unresolved(Compilation compilation, IEnumerable<string> ids) =>
        ids.Where(id => DocumentationCommentId.GetSymbolsForDeclarationId(id, compilation).IsEmpty);

    /// <summary>Every reference in <paramref name="compilation"/>'s sources to a symbol <paramref name="ids"/> lists, in order of file and line.</summary>
    internal static List<BannedUse> Uses(Compilation compilation, IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0) return [];
        var banned = new HashSet<ISymbol>(ids.SelectMany(id => DocumentationCommentId.GetSymbolsForDeclarationId(id, compilation)), SymbolEqualityComparer.Default);
        var spelled = banned.Select(Spelling).ToHashSet(StringComparer.Ordinal);
        var unspelled = banned.Any(symbol => symbol is IMethodSymbol { MethodKind: MethodKind.Conversion or MethodKind.UserDefinedOperator });
        var uses = new List<BannedUse>();
        foreach (var tree in DesktopCompilations.Trees(compilation))
        {
            var text = tree.GetText().ToString();
            if (!unspelled && !spelled.Any(name => text.Contains(name, StringComparison.Ordinal))) continue;
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case SimpleNameSyntax name:
                        var info = model.GetSymbolInfo(name);
                        if (info.Symbol is null && info.CandidateSymbols.IsEmpty && spelled.Contains(name.Identifier.ValueText))
                            uses.Add(new(tree.FilePath, DesktopCompilations.Line(name), "?" + name.Identifier.ValueText));
                        Add(info.Symbol ?? info.CandidateSymbols.FirstOrDefault(), node);
                        break;
                    case BaseObjectCreationExpressionSyntax creation:
                        Add(model.GetSymbolInfo(creation).Symbol, node);
                        break;
                }

                if (unspelled && node is ExpressionSyntax expression && model.GetConversion(expression) is { IsUserDefined: true, MethodSymbol: { } conversion })
                    Add(conversion, node);
            }
        }

        return [.. uses.Distinct().OrderBy(use => use.File, StringComparer.Ordinal).ThenBy(use => use.Line)];

        void Add(ISymbol? symbol, SyntaxNode at)
        {
            if (Banned(symbol, banned) is { } hit) uses.Add(new(at.SyntaxTree.FilePath, DesktopCompilations.Line(at), DocumentationCommentId.CreateDeclarationId(hit)!));
        }
    }

    /// <summary>The listed symbol <paramref name="symbol"/> is, or is a member of, or null.</summary>
    private static ISymbol? Banned(ISymbol? symbol, HashSet<ISymbol> banned)
    {
        for (var each = (symbol as IMethodSymbol)?.ReducedFrom ?? symbol?.OriginalDefinition; each is not null; each = each.ContainingSymbol)
        {
            if (banned.Contains(each.OriginalDefinition)) return each.OriginalDefinition;
        }

        return null;
    }

    private static string Spelling(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor => constructor.ContainingType.Name,
        _ => symbol.Name,
    };
}
