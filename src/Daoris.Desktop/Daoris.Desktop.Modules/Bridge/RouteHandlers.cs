using System.Reflection;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What one route's handler takes and answers, for the page's generated types: its payload record, or null where it takes
/// <see cref="NoPayload"/>, and its answer's record, or null where it answers an <see cref="object"/>.
/// </summary>
public sealed record RouteShape(string Route, Type? Payload, Type? Answer);

/// <summary>
/// How a marked handler is called. It takes its payload record, <c>M(TPayload)</c>, which <see cref="RoutePayload"/> reads off the
/// request; it answers at once, or later with a <see cref="CancellationToken"/> and a
/// <see cref="Task{TResult}"/>. What the method returns is the route's answer.
/// </summary>
internal static class RouteHandlers
{
    /// <summary>The call of <paramref name="method"/> on an instance of <paramref name="handler"/>; a method of no such shape fails the module's first use.</summary>
    public static Func<object, IpcRequest, CancellationToken, Task<object?>> Of(Type handler, MethodInfo method)
    {
        var (payload, answer, later) = Parts(handler, method);
        var build = typeof(RouteHandlers).GetMethod(later ? nameof(Later) : nameof(Now), BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Func<object, IpcRequest, CancellationToken, Task<object?>>)build.MakeGenericMethod(handler, payload, answer).Invoke(null, [method])!;
    }

    /// <summary>What <paramref name="method"/> takes and answers, as the page is told it.</summary>
    public static RouteShape ShapeOf(string route, Type handler, MethodInfo method)
    {
        var (payload, answer, _) = Parts(handler, method);
        return new RouteShape(route, payload == typeof(NoPayload) ? null : payload, answer == typeof(object) ? null : answer);
    }

    private static (Type Payload, Type Answer, bool Later) Parts(Type handler, MethodInfo method)
    {
        var parameters = method.GetParameters();
        var later = parameters is [_, { ParameterType: var token }] && token == typeof(CancellationToken);
        var answer = !later ? method.ReturnType
            : method.ReturnType is { IsGenericType: true } task && task.GetGenericTypeDefinition() == typeof(Task<>) ? task.GetGenericArguments()[0] : null;
        if (parameters.Length != (later ? 2 : 1) || answer is null || answer == typeof(void))
        {
            throw new InvalidOperationException(
                $"{handler.Name}.{method.Name} is no handler: it takes its payload record, and a token where it answers later as a task.");
        }

        return (parameters[0].ParameterType, answer, later);
    }

    // A handler that answers at once throws at once, which the router awaits the same way as a fault.
    private static Func<object, IpcRequest, CancellationToken, Task<object?>> Now<T, TPayload, TAnswer>(MethodInfo method)
    {
        var answer = method.CreateDelegate<Func<T, TPayload, TAnswer>>();
        var read = Read<TPayload>();
        return (handler, request, _) => Task.FromResult<object?>(answer((T)handler, read(request)));
    }

    private static Func<object, IpcRequest, CancellationToken, Task<object?>> Later<T, TPayload, TAnswer>(MethodInfo method)
    {
        var answer = method.CreateDelegate<Func<T, TPayload, CancellationToken, Task<TAnswer>>>();
        var read = Read<TPayload>();
        // The payload is read inside the task, so a refused field faults it as a read inside the handler's own body did.
        return async (handler, request, cancellationToken) => await answer((T)handler, read(request), cancellationToken);
    }

    private static Func<IpcRequest, TPayload> Read<TPayload>() => RoutePayload.Reader<TPayload>();
}
