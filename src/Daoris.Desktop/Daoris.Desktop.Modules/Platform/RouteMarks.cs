using System.Reflection;
using System.Runtime.CompilerServices;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What a route's marks tell the pipe, read once at start: its class (<see cref="Rule"/>, a whole class or a look and a press
/// told apart by a field), what an update's drain does with it, what it marks <c>owner-set</c>, and why its setting is never
/// <c>already</c>.
/// </summary>
public sealed record RouteMark(string Module, string Route, PipeRule Rule, PipeDrain? Drain, OwnerTarget? Owner, string? NotAlready)
{
    /// <summary>Whether a form of it is <paramref name="class"/>.</summary>
    public bool Takes(params PipeClass[] @class) => @class.Contains(Rule.Class) || @class.Contains(Rule.Otherwise);
}

/// <summary>
/// Reads a module's routes as their marks class them: the driver's off each handler's <see cref="DriverRouteAttribute"/> and the
/// marks beside it, another module's off its class (<see cref="ModuleRouteAttribute"/>, <see cref="WindowModuleAttribute"/>). A
/// route whose marks are incomplete or name a route the member does not answer fails the first use, as a route marked twice does:
/// a person's or a role's press with no drain, a person's or a window's with no owner mark, or a mark for a route nobody answers.
/// </summary>
/// <remarks>It holds no state: the driver's module calls it while its own table is built, before anything here could read that table.</remarks>
public static class RouteMarks
{
    /// <summary>
    /// The marks of <paramref name="routes"/>, each a route <paramref name="member"/> answers with its class and its drain as said,
    /// read off the marks beside them; <paramref name="where"/> names the member in a refusal.
    /// </summary>
    internal static IReadOnlyList<RouteMark> Read(
        string module, MemberInfo member, IReadOnlyList<(string Route, PipeClass Class, PipeDrain? Drain)> routes, string where)
    {
        var classedBy = member.GetCustomAttributes<ClassedByAttribute>().ToList();
        var owners = member.GetCustomAttributes<OwnerAttribute>().ToList();
        var notAlready = member.GetCustomAttributes<NotAlreadyAttribute>().ToList();
        var answered = routes.Select(each => each.Route).ToHashSet(StringComparer.Ordinal);
        if (classedBy.Select(each => each.Route).Concat(owners.Select(each => each.Route)).Concat(notAlready.Select(each => each.Route))
                .FirstOrDefault(route => !answered.Contains(route)) is { } stray)
        {
            throw new InvalidOperationException($"{where} marks `{stray}` for the pipe, and answers no such route.");
        }

        var marks = new List<RouteMark>();
        foreach (var (route, @class, drain) in routes)
        {
            var by = classedBy.Where(each => each.Route == route).ToList();
            if (by.Count > 1) throw new InvalidOperationException($"{where} classes `{route}` by its payload twice.");
            var rule = by is [var form] ? PipeRule.Of(form.Field, form.Class, @class, form.Values) with { Unless = form.Unless } : PipeRule.Whole(@class);
            var mark = new RouteMark(module, route, rule, drain, Owner(owners.Where(each => each.Route == route).ToList(), where, route),
                notAlready.SingleOrDefault(each => each.Route == route)?.Why);
            if (mark.Takes(PipeClass.Person, PipeClass.Role) != drain.HasValue)
            {
                throw new InvalidOperationException(drain.HasValue
                    ? $"{where} says a drain for `{route}`, and no form of it is a person's or a role's press."
                    : $"{where} says no drain for `{route}`, a person's or a role's press: Waits or Passes.");
            }

            if (mark.Takes(PipeClass.Person, PipeClass.Window) != (mark.Owner is not null))
            {
                throw new InvalidOperationException(mark.Owner is not null
                    ? $"{where} marks an owner for `{route}`, and no form of it is a person's or a window's."
                    : $"{where} marks no owner for `{route}`, a person's or a window's route: its fields, a reason or by its plan.");
            }

            if (mark.NotAlready is not null && !mark.Takes(PipeClass.Person))
            {
                throw new InvalidOperationException($"{where} says `{route}` is never already, and it is no person's press.");
            }

            marks.Add(mark);
        }

        return marks;
    }

    /// <summary>A route's owner marks merged, or null where it says none: fields, a closed reason or by its plan, one of the three.</summary>
    private static OwnerTarget? Owner(IReadOnlyList<OwnerAttribute> said, string where, string route)
    {
        if (said.Count == 0) return null;
        var reasons = said.Select(each => each.None).OfType<string>().ToList();
        var target = new OwnerTarget([.. said.SelectMany(each => each.Fields())], reasons.FirstOrDefault(), said.Any(each => each.ByPlan));
        var ways = (target.Fields.Count > 0 ? 1 : 0) + reasons.Count + (target.ByPlan ? 1 : 0);
        if (ways != 1 || reasons.Any(each => !OwnerNone.All.Contains(each)))
        {
            throw new InvalidOperationException($"{where} marks `{route}`'s owner as fields, one closed reason or by its plan: one of them, once.");
        }

        return target;
    }

    /// <summary>
    /// The marks of <paramref name="assembly"/>'s modules but the driver's, read off each module's class, and the modules classed whole
    /// as a window's; a module's name is read as the dispatcher reads it, off the module.
    /// </summary>
    public static (IReadOnlyList<RouteMark> Routes, IReadOnlyList<string> Windows) Modules(Assembly assembly)
    {
        var routes = new List<RouteMark>();
        var windows = new List<string>();
        foreach (var type in assembly.GetTypes().Where(type => type is { IsAbstract: false, IsClass: true } && typeof(IIpcModule).IsAssignableFrom(type)))
        {
            var said = type.GetCustomAttributes<ModuleRouteAttribute>().ToList();
            var window = type.GetCustomAttribute<WindowModuleAttribute>() is not null;
            if (said.Count == 0 && !window) continue;
            // The name is the module's own constant, so an instance built without its constructor answers it.
            var name = ((IIpcModule)RuntimeHelpers.GetUninitializedObject(type)).ModuleName;
            if (window && said.Count > 0) throw new InvalidOperationException($"{type.Name} is a window's whole and classes routes too.");
            if (window) windows.Add(name);
            else routes.AddRange(Read(name, type, [.. said.Select(each => (each.Route, each.Class, each.DrainSaid))], type.Name));
        }

        return (routes, windows);
    }
}
