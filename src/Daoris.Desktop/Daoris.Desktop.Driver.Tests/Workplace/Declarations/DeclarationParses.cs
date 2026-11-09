using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The parses census by declared types (the design's §33, §50): every type a kind's parse or a view reaches holds only immutable
/// concrete types (a string, a primitive, an enum, a compiled <c>Regex</c>, a cloned <c>JsonElement</c>, <c>ImmutableArray&lt;&gt;</c>,
/// <c>FrozenDictionary&lt;,&gt;</c>, <c>FrozenSet&lt;&gt;</c>, a version, or a sealed record of these, each field set only when it is
/// made); an interface or abstract collection, <c>object</c>, an array or a delegate admits a mutable value on any branch and is red.
/// A version holds ids, numbers and strings alone, so no version carries an answer. A kind enters the store frozen, so the census is
/// green at every row.
/// </summary>
internal static class DeclarationParses
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Each kind and view <paramref name="assembly"/> declares.</summary>
    internal static IEnumerable<Type> Declared(Assembly assembly) =>
        assembly.GetTypes().Where(type => type is { IsAbstract: false, IsInterface: false } && Roots(type).Any());

    /// <summary>What a type of <paramref name="kinds"/> reaches that may hold a mutable value, and each version holding more than ids.</summary>
    internal static List<string> Problems(IEnumerable<Type> kinds)
    {
        var problems = new List<string>();
        var seen = new HashSet<Type>();
        foreach (var root in kinds.SelectMany(Roots)) Judge(root, root.Name, problems, seen);
        foreach (var version in seen.Where(type => DeclarationKeepers.Versions.Contains(type.Name)))
        {
            foreach (var field in Hierarchy(version).SelectMany(type => type.GetFields(Fields)).Where(field => !Id(field.FieldType)))
                problems.Add($"{version.Name}.{Name(field)} holds {field.FieldType.Name}, which no version may: ids, numbers and strings alone");
        }

        return problems;
    }

    // A kind's parse, and a view's key and value.
    private static IEnumerable<Type> Roots(Type type) => type.GetInterfaces()
        .Where(each => each.IsGenericType && each.GetGenericTypeDefinition().Name is "IDeclarationKind`1" or "IDeclarationView`2")
        .SelectMany(each => each.GetGenericArguments());

    private static void Judge(Type type, string at, List<string> problems, HashSet<Type> seen)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner) type = inner;
        if (Leaf(type)) return;
        if (type.IsGenericType && type.GetGenericTypeDefinition() is var open
            && (open == typeof(ImmutableArray<>) || open == typeof(FrozenSet<>) || open == typeof(FrozenDictionary<,>)))
        {
            foreach (var argument in type.GetGenericArguments()) Judge(argument, at + "'s element", problems, seen);
            return;
        }

        if (!seen.Add(type) || DeclarationKeepers.Versions.Contains(type.Name)) return;
        if (Record(type) && (type.IsSealed || type.IsAbstract))
        {
            foreach (var field in type.GetFields(Fields))
            {
                if (!field.IsInitOnly) problems.Add($"{at}.{Name(field)} can be set after it is made");
                Judge(field.FieldType, $"{at}.{Name(field)}", problems, seen);
            }

            if (type.IsAbstract)
            {
                foreach (var derived in type.Assembly.GetTypes().Where(each => each.BaseType == type)) Judge(derived, derived.Name, problems, seen);
            }

            return;
        }

        problems.Add($"{at} is {type.Name}, which admits a mutable value");
    }

    private static bool Leaf(Type type) =>
        type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(Regex) || type == typeof(JsonElement);

    private static bool Id(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var each && (each == typeof(string) || each.IsPrimitive || each.IsEnum || DeclarationKeepers.Versions.Contains(each.Name));

    private static bool Record(Type type) =>
        type.GetMethod("<Clone>$") is not null || type.IsValueType && type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic) is not null
        || type.IsAbstract && type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) is not null;

    private static IEnumerable<Type> Hierarchy(Type type)
    {
        for (var each = type; each is not null && each != typeof(object) && each != typeof(ValueType); each = each.BaseType) yield return each;
    }

    private static string Name(FieldInfo field) => field.Name.StartsWith('<') ? field.Name[1..field.Name.IndexOf('>')] : field.Name;
}
