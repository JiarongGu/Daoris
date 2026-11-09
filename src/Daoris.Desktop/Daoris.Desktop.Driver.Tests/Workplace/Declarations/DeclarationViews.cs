using System.Globalization;
using System.Reflection;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The views and kinds census (the design's §23, §33): a kind's parse and a view's computation are followed through every call they
/// reach in Daoris's own assemblies, an interface or abstract call to every implementation; red where one reaches a file, a process,
/// the clock, the environment, a port, <c>DriverConfig</c> or a home layer's reader, and where a call leaves a string, a char or a
/// formattable value to the ambient culture: its target has an overload taking a comparer, a culture or a format provider (or, for a
/// culture-sensitive comparison of strings, a <c>StringComparison</c>) that the call does not pass. The overloads are the runtime's own.
/// </summary>
internal static class DeclarationViews
{
    private static readonly HashSet<string> FileTypes =
        ["File", "Directory", "FileInfo", "DirectoryInfo", "FileSystemInfo", "FileStream", "RandomAccess", "DriveInfo", "FileSystemWatcher"];

    private static readonly HashSet<string> Layers = ["PersonFiles", "FoundFiles", "FileAsks", "WorkflowStore"];

    // The string comparisons whose default is the ambient culture, not ordinal.
    private static readonly HashSet<string> CultureCompared = ["StartsWith", "EndsWith", "IndexOf", "LastIndexOf", "Compare", "CompareTo"];

    /// <summary>What the parses and views of <paramref name="types"/> reach that a pure function of its contents may not, each with its root.</summary>
    internal static List<string> Problems(IEnumerable<Type> types, params Assembly[] ours)
    {
        var reach = new CallReach(ours);
        var problems = new List<string>();
        var seen = new HashSet<MethodBase>();
        var next = new Stack<(MethodBase Method, string Root)>(types.SelectMany(Roots).Select(method => (method, $"{method.DeclaringType!.Name}.{method.Name}")));
        while (next.TryPop(out var item))
        {
            if (!seen.Add(item.Method)) continue;
            var where = $"{item.Method.DeclaringType?.Name}.{item.Method.Name}";
            var callees = reach.CalleesOf(item.Method);
            foreach (var callee in callees)
            {
                if ((Forbidden(callee) ?? Ambient(callee)) is { } why)
                {
                    problems.Add($"{item.Root} reaches {callee.DeclaringType?.Name}.{callee.Name}: {why}, in {where}");
                    continue;
                }

                foreach (var target in Dispatched(callee, ours)) next.Push((target, item.Root));
            }

            if (Interpolated(callees) is { } formatted)
                problems.Add($"{item.Root} formats {formatted} in an interpolation by the ambient culture, in {where}");
        }

        return [.. problems.Distinct()];
    }

    // An interpolation's handler made with no provider formats each formattable hole by the ambient culture.
    private static string? Interpolated(IReadOnlyList<MethodBase> callees) =>
        callees.Any(callee => callee is ConstructorInfo { DeclaringType.Name: "DefaultInterpolatedStringHandler" } made && made.GetParameters().Length == 2)
            ? callees.OfType<MethodInfo>().Where(callee => callee is { Name: "AppendFormatted", IsGenericMethod: true, DeclaringType.Name: "DefaultInterpolatedStringHandler" })
                .Select(callee => callee.GetGenericArguments()[0]).FirstOrDefault(hole => hole != typeof(string) && typeof(IFormattable).IsAssignableFrom(hole))?.Name
            : null;

    private static IEnumerable<MethodBase> Roots(Type type) => type.GetInterfaces()
        .Where(each => each.IsGenericType && each.GetGenericTypeDefinition().Name is "IDeclarationKind`1" or "IDeclarationView`2")
        .SelectMany(each => type.GetInterfaceMap(each) is var map
            ? map.InterfaceMethods.Zip(map.TargetMethods).Where(pair => pair.First.Name is "Parse" or "View").Select(pair => (MethodBase)pair.Second)
            : []);

    private static IEnumerable<MethodBase> Dispatched(MethodBase callee, Assembly[] ours)
    {
        yield return callee;
        if (callee is not MethodInfo { IsVirtual: true } || callee.DeclaringType is not { } declaring || !ours.Contains(declaring.Assembly)) yield break;
        foreach (var type in ours.SelectMany(assembly => assembly.GetTypes()).Where(type => type is { IsAbstract: false, IsInterface: false }))
        {
            if (declaring.IsInterface)
            {
                foreach (var face in type.GetInterfaces().Where(face => face == declaring || face.IsGenericType && declaring.IsGenericType
                             && face.GetGenericTypeDefinition() == declaring.GetGenericTypeDefinition()))
                {
                    var map = type.GetInterfaceMap(face);
                    for (var at = 0; at < map.InterfaceMethods.Length; at++)
                        if (map.InterfaceMethods[at].Name == callee.Name) yield return map.TargetMethods[at];
                }
            }
            else if (type.IsSubclassOf(declaring) && type.GetMethod(callee.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                         [.. callee.GetParameters().Select(parameter => parameter.ParameterType)]) is { } overriding)
            {
                yield return overriding;
            }
        }
    }

    private static string? Forbidden(MethodBase method)
    {
        var type = method.DeclaringType;
        if (type is null) return null;
        var name = method.Name;
        return type switch
        {
            { Namespace: "System.IO" } when FileTypes.Contains(type.Name) => "a file",
            { Namespace: "System.IO", Name: "StreamReader" or "StreamWriter" } when method.IsConstructor && method.GetParameters() is [{ ParameterType: var first }, ..] && first == typeof(string) => "a file",
            { Name: "AtomicFile" or "ProcessStarts" } or { FullName: "System.Diagnostics.Process" or "System.Diagnostics.ProcessStartInfo" } => "a file or a process",
            { FullName: "System.DateTime" or "System.DateTimeOffset" } when name is "get_Now" or "get_UtcNow" or "get_Today" => "the clock",
            { FullName: "System.Diagnostics.Stopwatch" or "System.TimeProvider" } => "the clock",
            { FullName: "System.Environment" } => "the environment",
            { FullName: "System.Globalization.CultureInfo" } when name.Contains("Current", StringComparison.Ordinal) => "the ambient culture",
            { FullName: "System.TimeZoneInfo" } when name == "get_Local" => "the local time zone",
            _ when type.Namespace?.StartsWith("Daoris", StringComparison.Ordinal) is true && type.Name.EndsWith("Port", StringComparison.Ordinal) => "a port",
            _ when type.Namespace?.StartsWith("Daoris", StringComparison.Ordinal) is true && type.Name == "DriverConfig" => "DriverConfig",
            _ when type.Namespace?.StartsWith("Daoris", StringComparison.Ordinal) is true && Layers.Contains(type.Name) => "a home layer's reader",
            _ => null,
        };
    }

    private static string? Ambient(MethodBase callee)
    {
        if (callee.DeclaringType is not { } type || type.Namespace?.StartsWith("Daoris", StringComparison.Ordinal) is true
            || type.Name == "DefaultInterpolatedStringHandler") return null;
        var operands = callee.GetParameters().Select(parameter => parameter.ParameterType)
            .Concat(callee.IsStatic || callee.IsConstructor ? [] : [type])
            .Concat(callee is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericArguments() : []);
        if (!operands.Any(Formatted)) return null;
        var asked = Definition(callee).GetParameters().Select(parameter => parameter.ParameterType.ToString()).ToList();
        foreach (var overload in type.GetMember(callee.Name, MemberTypes.Method | MemberTypes.Constructor, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                     .OfType<MethodBase>().Where(each => each.GetParameters().Length == asked.Count + 1))
        {
            var offered = overload.GetParameters();
            for (var extra = 0; extra < offered.Length; extra++)
            {
                if (!asked.SequenceEqual(offered.Where((_, at) => at != extra).Select(parameter => parameter.ParameterType.ToString()))) continue;
                var kind = offered[extra].ParameterType;
                if (kind == typeof(IFormatProvider) || kind == typeof(CultureInfo) || kind.IsGenericType && kind.GetGenericTypeDefinition() == typeof(IComparer<>))
                    return $"leaves {offered[extra].ParameterType.Name} to the ambient culture";
                if (kind == typeof(StringComparison) && CultureCompared.Contains(callee.Name) && callee.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)))
                    return "compares by the ambient culture";
            }
        }

        return null;
    }

    private static MethodBase Definition(MethodBase method) => method is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericMethodDefinition() : method;

    private static bool Formatted(Type type) =>
        type == typeof(string) || type == typeof(char) || typeof(IFormattable).IsAssignableFrom(type)
        || type.IsGenericType && type.GetGenericArguments().Any(argument => argument == typeof(string) || argument == typeof(char));
}
