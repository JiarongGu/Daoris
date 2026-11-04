using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Daoris.Contracts;

/// <summary>
/// What one <see cref="IRoute{TAnswer}"/> declares, read from its record: the one reader the host's binder, the driver's send
/// and the page's generator share.
/// </summary>
/// <param name="Request">The request record.</param>
/// <param name="Answer">What a success answers.</param>
/// <param name="Method">The HTTP method, upper case.</param>
/// <param name="Path">The path's template.</param>
/// <param name="PathNames">The names of the template's <c>{name}</c> segments, in order.</param>
public sealed partial record RouteDeclaration(Type Request, Type Answer, string Method, string Path, IReadOnlyList<string> PathNames)
{
    private static readonly ConcurrentDictionary<Type, RouteDeclaration> Read = new();

    /// <summary>Whether the record's other values travel in the body rather than the query.</summary>
    public bool TakesBody => Method is not ("GET" or "DELETE");

    /// <summary>The path with each <c>{name}</c> segment filled by <paramref name="value"/>, escaped.</summary>
    public string PathWith(Func<string, string> value) => Segment().Replace(Path, match => Uri.EscapeDataString(value(match.Groups[1].Value)));

    /// <summary>The declaration of <paramref name="request"/>, which implements <see cref="IRoute{TAnswer}"/> once.</summary>
    /// <exception cref="ArgumentException">A type that declares no route, or more than one.</exception>
    public static RouteDeclaration Of(Type request) => Read.GetOrAdd(request, static type =>
    {
        var routes = type.GetInterfaces().Where(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IRoute<>)).ToList();
        if (routes.Count != 1) throw new ArgumentException($"{type.Name} must declare exactly one route, and declares {routes.Count}.", nameof(request));

        var answer = routes[0].GetGenericArguments()[0];
        // Through a generic method, so the static members are read as the compiler dispatches them, whether a record
        // implements them by name or explicitly.
        var (method, path) = ((string, string))typeof(RouteDeclaration)
            .GetMethod(nameof(Members), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type, answer)
            .Invoke(null, null)!;
        return new RouteDeclaration(type, answer, method, path, [.. Segment().Matches(path).Select(match => match.Groups[1].Value)]);
    });

    /// <summary>Every route <paramref name="assembly"/> declares, by its record's name.</summary>
    public static IReadOnlyList<RouteDeclaration> In(Assembly assembly) =>
    [
        .. assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && type.GetInterfaces().Any(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IRoute<>)))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(Of),
    ];

    private static (string, string) Members<TRequest, TAnswer>() where TRequest : IRoute<TAnswer> => (TRequest.Method, TRequest.Path);

    // A template segment's name, without the constraint, default or catch-all marks the framework's templates allow.
    [GeneratedRegex(@"\{\*{0,2}([A-Za-z0-9_]+)[^}]*\}")]
    private static partial Regex Segment();
}
