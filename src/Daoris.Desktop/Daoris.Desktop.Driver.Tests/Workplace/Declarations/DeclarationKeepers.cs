using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the keepers census reads (the design's §23, §47): an answer is a value a member of the store returns, a kind's parse or a
/// view's value (a parse, a view, bytes), and every value made from one, followed through locals, parameters, fields, returns and
/// calls until nothing changes. A version is no answer: a content key, a blob id, a declaration's version, the store's revisions and
/// an effective stamp stop the flow. Kept is an answer written into a static field, an instance field outside its constructor (no
/// clearance in a type that asks the store), an element of a collection a field holds, a <c>??=</c> target or a <c>Lazy&lt;&gt;</c>.
/// The store's own folder is its keeper by design and is not read.
/// </summary>
internal static class DeclarationKeepers
{
    /// <summary>The version types, by name: what a field may hold across asks.</summary>
    internal static readonly ImmutableHashSet<string> Versions = ["ContentKey", "BlobId", "DeclarationVersion", "StoreRevisions", "Effective"];

    private static readonly string[] Hints = ["RepositoryDeclarations", "DeclarationContents", "IDeclarationKind", "IDeclarationView", "Kind.One"];

    /// <summary>Each field of <paramref name="compilation"/>'s sources outside the store that keeps an answer, as <c>File Field</c>.</summary>
    internal static SortedSet<string> Kept(Compilation compilation)
    {
        var trees = DesktopCompilations.Trees(compilation)
            .Where(tree => !tree.FilePath.StartsWith("Daoris.Desktop.Driver/Workplace/Declarations/", StringComparison.Ordinal)).ToList();
        var flow = new Flow(compilation);
        var named = new HashSet<string>(Hints, StringComparer.Ordinal);
        int before;
        do
        {
            before = flow.Count;
            foreach (var tree in trees.Where(tree => named.Any(name => tree.GetText().ToString().Contains(name, StringComparison.Ordinal))))
                flow.Read(tree);
            named.UnionWith(flow.Names);
        }
        while (flow.Count != before);

        return flow.Keepers;
    }

    private sealed class Flow(Compilation compilation)
    {
        private readonly HashSet<ISymbol> _answers = new(SymbolEqualityComparer.Default);

        internal int Count => _answers.Count;

        internal IEnumerable<string> Names => _answers.Select(symbol => symbol.Name).Where(name => name.Length > 2);

        internal SortedSet<string> Keepers { get; } = new(StringComparer.Ordinal);

        internal void Read(SyntaxTree tree)
        {
            var model = SharedModels.Of(compilation, tree);
            foreach (var body in tree.GetRoot().DescendantNodes().Where(node => node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax
                         or ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax or IndexerDeclarationSyntax }
                         or EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } or PropertyDeclarationSyntax }))
            {
                if (model.GetOperation(body) is not { } root) continue;
                foreach (var operation in root.DescendantsAndSelf()) Visit(operation, model, body);
            }
        }

        private void Visit(IOperation operation, SemanticModel model, SyntaxNode body)
        {
            switch (operation)
            {
                case IAssignmentOperation assignment when Answer(assignment.Value):
                    Mark(Target(assignment.Target));
                    Keep(assignment.Target, assignment is ICoalesceAssignmentOperation, model, body);
                    break;
                case IFieldInitializerOperation initializer when Answer(initializer.Value):
                    foreach (var field in initializer.InitializedFields)
                    {
                        Mark(field);
                        if (field.IsStatic || AsksTheStore(body)) Keepers.Add($"{body.SyntaxTree.FilePath} {field.Name}");
                    }

                    break;
                case IVariableDeclaratorOperation { Initializer.Value: var value } declarator when Answer(value):
                    _answers.Add(declarator.Symbol);
                    break;
                case IReturnOperation { ReturnedValue: { } value } when Answer(value) && model.GetEnclosingSymbol(operation.Syntax.SpanStart) is IMethodSymbol method:
                    _answers.Add(method.OriginalDefinition);
                    break;
                case IInvocationOperation call:
                    foreach (var argument in call.Arguments.Where(argument => argument.Parameter is not null && Answer(argument.Value)))
                        _answers.Add(argument.Parameter!.OriginalDefinition);
                    if (Held(call.Instance) is { } holder && call.Arguments.Any(argument => Answer(argument.Value))) Add(holder, operation);
                    break;
                case IForEachLoopOperation loop when Answer(loop.Collection):
                    foreach (var local in loop.Locals) _answers.Add(local);
                    break;
                case IIsPatternOperation pattern when Answer(pattern.Value):
                    foreach (var declared in pattern.Pattern.DescendantsAndSelf().OfType<IDeclarationPatternOperation>())
                        Mark(declared.DeclaredSymbol);
                    break;
                case IObjectCreationOperation { Type: INamedTypeSymbol { Name: "Lazy" } } lazy when lazy.Arguments.Any(argument => Answer(argument.Value)):
                    Add($"Lazy:{DesktopCompilations.Line(lazy.Syntax)}", operation);
                    break;
            }
        }

        private void Keep(IOperation target, bool coalesce, SemanticModel model, SyntaxNode body)
        {
            if (target is IPropertyReferenceOperation { Property.IsIndexer: true } or IArrayElementReferenceOperation && Held(target) is { } element)
            {
                Add(element, target);
                return;
            }

            var field = target switch
            {
                IFieldReferenceOperation reference => (ISymbol)reference.Field,
                IPropertyReferenceOperation { Property: { IsIndexer: false } property } when property.DeclaringSyntaxReferences.Length > 0 => property,
                _ => null,
            };
            if (field is null) return;
            var constructing = model.GetEnclosingSymbol(target.Syntax.SpanStart) is IMethodSymbol { MethodKind: MethodKind.Constructor };
            if (coalesce || field.IsStatic || !constructing || AsksTheStore(body)) Add(field.Name, target);
        }

        private static bool AsksTheStore(SyntaxNode body) =>
            body.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is { } type && Hints.Any(hint => type.ToString().Contains(hint, StringComparison.Ordinal));

        private void Add(string field, IOperation at) => Keepers.Add($"{at.Syntax.SyntaxTree.FilePath} {field}");

        private static string? Held(IOperation? operation) => operation switch
        {
            IFieldReferenceOperation field => field.Field.Name,
            IPropertyReferenceOperation { Property.IsIndexer: true } indexer => Held(indexer.Instance),
            IArrayElementReferenceOperation element => Held(element.ArrayReference),
            IPropertyReferenceOperation { Property: { IsIndexer: false } property } when property.DeclaringSyntaxReferences.Length > 0 => property.Name,
            IConversionOperation conversion => Held(conversion.Operand),
            _ => null,
        };

        private void Mark(ISymbol? symbol)
        {
            if (symbol is not null) _answers.Add(symbol.OriginalDefinition);
        }

        private static ISymbol? Target(IOperation target) => target switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFieldReferenceOperation field => field.Field,
            IPropertyReferenceOperation { Property.IsIndexer: false } property => property.Property,
            _ => null,
        };

        private bool Answer(IOperation? operation)
        {
            if (operation is null || Version(operation.Type)) return false;
            return operation switch
            {
                IInvocationOperation call => Store(call.TargetMethod) || _answers.Contains(call.TargetMethod.OriginalDefinition) || Answer(call.Instance)
                    || call.Arguments.Any(argument => Answer(argument.Value)),
                ILocalReferenceOperation local => _answers.Contains(local.Local),
                IParameterReferenceOperation parameter => _answers.Contains(parameter.Parameter.OriginalDefinition),
                IFieldReferenceOperation field => _answers.Contains(field.Field.OriginalDefinition) || Answer(field.Instance),
                IPropertyReferenceOperation property => _answers.Contains(property.Property.OriginalDefinition) || Answer(property.Instance),
                IAnonymousFunctionOperation function => function.Body.Descendants().Any(Answer),
                _ => operation.ChildOperations.Any(Answer),
            };
        }

        private static bool Version(ITypeSymbol? type) => type is INamedTypeSymbol named
            && (named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T ? Version(named.TypeArguments[0]) : Versions.Contains(named.Name));

        private static bool Store(IMethodSymbol method) =>
            method.ContainingType is { } type && (type.Name is "RepositoryDeclarations" or "DeclarationContents"
                || type is { TypeKind: TypeKind.Interface, Name: "IDeclarationKind" or "IDeclarationView" }
                || method.Name is "Parse" or "View" && type.AllInterfaces.Any(each => each.Name is "IDeclarationKind" or "IDeclarationView"));
    }
}
