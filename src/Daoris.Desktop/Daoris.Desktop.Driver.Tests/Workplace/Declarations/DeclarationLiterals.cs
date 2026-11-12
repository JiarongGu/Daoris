using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A string outside the store that names a declaration: its file under the desktop tree, its line, its member, and what it names.</summary>
internal sealed record LiteralHit(string File, int Line, string Member, string Value);

/// <summary>
/// The literal check (the design's §43, §61): outside the store's folder, red on a string whose path tail is a declaration's name.
/// A value is known whole where it is a constant (a literal, a <c>const</c>, a concatenation or interpolation of constants) or a call of
/// a member of <c>string</c> or <c>Path</c> whose receiver and arguments are all known whole, evaluated by calling that member; and known
/// after its last separator where it is a <c>Path.Combine</c> or <c>Path.Join</c> whose last arguments are known, joined, or a
/// <c>Path.ChangeExtension</c> of such a value with a known extension. A literal fragment of a value that is not constant is judged
/// alone. A constant naming one of the home's five declaration entries, handed to <c>HomeEntry.Named</c> or compared with an entry's
/// <c>Name</c>, is red too. A <c>DeclarationNames</c> member is read-only and never folded.
/// </summary>
internal static class DeclarationLiterals
{
    /// <summary>The declarations' names, compared without regard to case as a Windows volume compares them.</summary>
    internal static readonly ImmutableArray<string> Names =
        ["daoris.json", "daoris.files.json", "daoris.gates.json", "daoris.lanes.json", "driver.json", "files.json", "files-found.json", "files-asks.json"];

    /// <summary>The home's declaration entries, by the name <c>HomeEntry.Named</c> compares by ordinal.</summary>
    internal static readonly ImmutableArray<string> Entries = ["driver", "files", "files-found", "files-asks", "workflows"];

    private const string WorkflowsFolder = "data/workflows";

    private static readonly ImmutableHashSet<string> Members =
        [.. typeof(string).GetMethods().Concat(typeof(Path).GetMethods()).Select(method => method.Name)];

    /// <summary>Every string of <paramref name="compilation"/>'s sources outside the store's folder that names a declaration.</summary>
    internal static List<LiteralHit> Hits(Compilation compilation)
    {
        var trees = DesktopCompilations.Trees(compilation).Where(tree => !tree.FilePath.StartsWith("Daoris.Desktop.Driver/Workplace/Declarations/", StringComparison.Ordinal)).ToList();
        // Only a node with a constant leaf can be known, so the model binds no other: a literal, or a name some source declares const.
        var consts = trees.SelectMany(tree => tree.GetRoot().DescendantNodes()).SelectMany(node => node switch
        {
            FieldDeclarationSyntax field when field.Modifiers.Any(SyntaxKind.ConstKeyword) => field.Declaration.Variables.Select(each => each.Identifier.ValueText),
            LocalDeclarationStatementSyntax local when local.IsConst => local.Declaration.Variables.Select(each => each.Identifier.ValueText),
            _ => [],
        }).ToHashSet(StringComparer.Ordinal);
        return [.. trees.AsParallel().AsOrdered().Select(tree => In(compilation, tree, consts)).ToList().SelectMany(each => each).Distinct()];
    }

    private static List<LiteralHit> In(Compilation compilation, SyntaxTree tree, HashSet<string> consts)
    {
        var hits = new List<LiteralHit>();
        var model = SharedModels.Of(compilation, tree);
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            var named = node switch
            {
                LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal => Judged(literal.Token.ValueText),
                InterpolatedStringTextSyntax fragment => Judged(fragment.TextToken.ValueText),
                BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } sum when sum.Parent is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } && Leafed(sum, consts) =>
                    model.GetConstantValue(sum) is { HasValue: true, Value: string folded } ? Judged(folded) : null,
                InterpolatedStringExpressionSyntax whole => model.GetConstantValue(whole) is { HasValue: true, Value: string folded } ? Judged(folded) : null,
                InvocationExpressionSyntax call when Called(call) is { } name && (name == "Named" || Members.Contains(name) && Leafed(call, consts)) =>
                    Invoked(model.GetOperation(call) as IInvocationOperation),
                BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression } compare when Named(compare.Left) || Named(compare.Right) =>
                    Compared(model.GetOperation(compare) as IBinaryOperation),
                IsPatternExpressionSyntax pattern when Named(pattern.Expression) =>
                    model.GetOperation(pattern) is IIsPatternOperation { Value: var value, Pattern: IConstantPatternOperation { Value: var constant } } && IsEntryName(value) ? Entry(constant) : null,
                _ => null,
            };
            if (named is not null) hits.Add(new(tree.FilePath, DesktopCompilations.Line(node), MemberOf(node), named));
        }

        return hits;
    }

    /// <summary>The name <paramref name="value"/> gives a declaration by its tail, or null.</summary>
    internal static string? Judged(string value)
    {
        var path = value.Replace('\\', '/');
        var tail = path[(path.LastIndexOfAny(['/', ':']) + 1)..];
        if (Names.Contains(tail, StringComparer.OrdinalIgnoreCase)) return tail;
        return path.TrimEnd('/').EndsWith(WorkflowsFolder, StringComparison.OrdinalIgnoreCase) ? WorkflowsFolder : null;
    }

    private static string? Invoked(IInvocationOperation? call)
    {
        if (call is null) return null;
        if (call.TargetMethod is { Name: "Named", ContainingType.Name: "HomeEntry" })
            return call.Arguments.Length == 1 ? Entry(call.Arguments[0].Value) : null;
        if (call.TargetMethod is { Name: "Equals" } && call.Arguments.Concat<IOperation?>([call.Instance]).Any(IsEntryName))
            return call.Arguments.Select(argument => Entry(argument.Value)).FirstOrDefault(entry => entry is not null);
        return Known.Of(call) is { } known ? Judged(known.Value) : null;
    }

    private static string? Compared(IBinaryOperation? compare) =>
        compare is null ? null : IsEntryName(compare.LeftOperand) ? Entry(compare.RightOperand) : IsEntryName(compare.RightOperand) ? Entry(compare.LeftOperand) : null;

    private static bool IsEntryName(IOperation? operation) =>
        Unwrapped(operation) is IPropertyReferenceOperation { Property: { Name: "Name", ContainingType.Name: "HomeEntry" } };

    private static string? Entry(IOperation? operation) =>
        Unwrapped(operation) is { ConstantValue: { HasValue: true, Value: string name } } && Entries.Contains(name) ? $"the home entry {name}" : null;

    private static IOperation? Unwrapped(IOperation? operation) => operation switch
    {
        IConversionOperation conversion => Unwrapped(conversion.Operand),
        IParenthesizedOperation parenthesized => Unwrapped(parenthesized.Operand),
        IArgumentOperation argument => Unwrapped(argument.Value),
        _ => operation,
    };

    private static bool Leafed(SyntaxNode node, HashSet<string> consts) =>
        node.DescendantNodes().Any(each => each is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } or InterpolatedStringExpressionSyntax
            || each is IdentifierNameSyntax name && consts.Contains(name.Identifier.ValueText));

    private static bool Named(ExpressionSyntax side) => side is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Name" } or IdentifierNameSyntax { Identifier.ValueText: "Name" };

    private static string? Called(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    private static string MemberOf(SyntaxNode node)
    {
        var member = node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(each => each is not BaseTypeDeclarationSyntax);
        var type = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "<top>";
        var name = member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            FieldDeclarationSyntax field => string.Join(',', field.Declaration.Variables.Select(variable => variable.Identifier.ValueText)),
            ConstructorDeclarationSyntax => ".ctor",
            _ => member?.Kind().ToString() ?? "<type>",
        };
        return $"{type}.{name}";
    }

    /// <summary>A value of <c>string</c> or <c>Path</c> known whole, or known only after its last separator.</summary>
    private sealed record Known(string Value, bool Whole)
    {
        internal static Known? Of(IOperation? operation)
        {
            var value = Unwrapped(operation);
            if (value is { ConstantValue: { HasValue: true, Value: string constant } }) return new(constant, true);
            if (value is not IInvocationOperation call || call.TargetMethod.ContainingType is not { } type
                || !(type.SpecialType == SpecialType.System_String || type.ToDisplayString() == "System.IO.Path")) return null;
            var arguments = call.Arguments.OrderBy(argument => argument.Parameter?.Ordinal ?? 0).SelectMany(Spread).ToList();
            if (call.TargetMethod.Name is "Combine" or "Join" && type.SpecialType != SpecialType.System_String)
            {
                var trailing = arguments.AsEnumerable().Reverse().Select(Of).TakeWhile(known => known is { Whole: true }).Select(known => known!.Value).Reverse().ToList();
                if (trailing.Count == 0) return null;
                return new(string.Join('/', trailing), trailing.Count == arguments.Count);
            }

            if (call.TargetMethod.Name == "ChangeExtension" && arguments.Count == 2 && Of(arguments[0]) is { } stem && Of(arguments[1]) is { Whole: true } extension)
                return new(Path.ChangeExtension(stem.Value, extension.Value), stem.Whole);
            return Evaluated(call) is { } whole ? new(whole, true) : null;
        }

        private static IEnumerable<IOperation> Spread(IArgumentOperation argument) => argument.Value switch
        {
            IArrayCreationOperation { Initializer: { } initializer } when argument.ArgumentKind != ArgumentKind.Explicit => initializer.ElementValues,
            ICollectionExpressionOperation collection when argument.ArgumentKind != ArgumentKind.Explicit => collection.Elements,
            var value => [value],
        };

        // Calls the member itself over its known arguments, a params span read as its array overload.
        private static string? Evaluated(IInvocationOperation call)
        {
            object? receiver = null;
            if (call.Instance is { } instance && (receiver = ValueOf(instance)) is null) return null;
            var parameters = new List<Type>();
            var values = new List<object?>();
            foreach (var argument in call.Arguments.OrderBy(argument => argument.Parameter?.Ordinal ?? 0))
            {
                if (argument.Parameter is null || Runtime(argument.Parameter.Type) is not { } runtime) return null;
                parameters.Add(runtime);
                if (runtime.IsArray)
                {
                    var elements = Spread(argument).Select(ValueOf).ToList();
                    if (elements.Any(element => element is null)) return null;
                    var array = Array.CreateInstance(runtime.GetElementType()!, elements.Count);
                    for (var at = 0; at < elements.Count; at++) array.SetValue(elements[at], at);
                    values.Add(array);
                }
                else if (ValueOf(argument.Value) is { } known) values.Add(known);
                else return null;
            }

            var owner = call.TargetMethod.ContainingType.SpecialType == SpecialType.System_String ? typeof(string) : typeof(Path);
            try
            {
                return owner.GetMethod(call.TargetMethod.Name, [.. parameters])?.Invoke(receiver, [.. values]) as string;
            }
            catch (System.Reflection.TargetInvocationException)
            {
                return null;
            }
        }

        private static object? ValueOf(IOperation operation) =>
            Unwrapped(operation) is { ConstantValue: { HasValue: true, Value: { } constant } } ? constant : Of(operation) is { Whole: true } known ? known.Value : null;

        private static Type? Runtime(ITypeSymbol type) => type switch
        {
            IArrayTypeSymbol array => Runtime(array.ElementType)?.MakeArrayType(),
            INamedTypeSymbol { Name: "ReadOnlySpan", TypeArguments: [var element] } => Runtime(element)?.MakeArrayType(),
            { SpecialType: SpecialType.System_String } => typeof(string),
            { SpecialType: SpecialType.System_Char } => typeof(char),
            { SpecialType: SpecialType.System_Int32 } => typeof(int),
            { SpecialType: SpecialType.System_Boolean } => typeof(bool),
            { SpecialType: SpecialType.System_Object } => typeof(object),
            INamedTypeSymbol { TypeKind: TypeKind.Enum } named => Type.GetType($"{named.ContainingNamespace.ToDisplayString()}.{named.MetadataName}"),
            _ => null,
        };
    }
}
