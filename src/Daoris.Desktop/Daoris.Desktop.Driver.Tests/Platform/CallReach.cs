using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which methods a method can call, read from the built assemblies' IL: a call, a virtual call, a constructor, a delegate made
/// from a method, and an async or iterator method's state machine, followed through Daoris's own assemblies alone.
/// </summary>
/// <remarks>
/// What it cannot see it does not guess: a call through an interface or an abstract method stops at that declaration, and a
/// delegate is followed where it is made, not where it is invoked. So a reach it finds is real, and one hidden behind an interface
/// is missed; the census that reads it says so.
/// </remarks>
internal sealed class CallReach(params Assembly[] ours)
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private readonly HashSet<Assembly> _ours = [.. ours];
    private readonly Dictionary<(Module, int), List<MethodBase>> _callees = [];

    /// <summary>Every method and constructor <paramref name="type"/> declares, its nested types' (its lambdas and state machines) among them.</summary>
    public static IEnumerable<MethodBase> Declarations(Type type) =>
        type.GetMethods(Declared).Cast<MethodBase>()
            .Concat(type.GetConstructors(Declared))
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(Declarations));

    /// <summary>The methods reached from <paramref name="roots"/>, each once, the roots among them.</summary>
    public IReadOnlyList<MethodBase> From(IEnumerable<MethodBase> roots)
    {
        var seen = new HashSet<(Module, int)>();
        var reached = new List<MethodBase>();
        var next = new Stack<MethodBase>(roots);
        while (next.TryPop(out var method))
        {
            if (!seen.Add((method.Module, method.MetadataToken))) continue;
            reached.Add(method);
            if (!_ours.Contains(method.Module.Assembly)) continue;
            foreach (var callee in Callees(method)) next.Push(callee);
        }

        return reached;
    }

    /// <summary>What <paramref name="method"/> itself calls, read once.</summary>
    public IReadOnlyList<MethodBase> CalleesOf(MethodBase method) => _ours.Contains(method.Module.Assembly) ? Callees(method) : [];

    private List<MethodBase> Callees(MethodBase method)
    {
        var key = (method.Module, method.MetadataToken);
        if (_callees.TryGetValue(key, out var known)) return known;
        var found = new List<MethodBase>();
        _callees[key] = found;
        if (method.GetCustomAttribute<StateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext", Declared) is { } moveNext) found.Add(moveNext);

        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException or BadImageFormatException)
        {
            il = null;
        }

        if (il is null) return found;
        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericArguments() : null;
        for (var at = 0; at < il.Length;)
        {
            var value = il[at] == 0xFE && at + 1 < il.Length ? (short)(0xFE00 | il[at + 1]) : il[at];
            at += value > 0xFF || value < 0 ? 2 : 1;
            if (!Codes.TryGetValue(value, out var code)) break;
            if (code.OperandType == OperandType.InlineMethod && at + 4 <= il.Length)
            {
                try
                {
                    if (method.Module.ResolveMethod(BitConverter.ToInt32(il, at), typeArguments, methodArguments) is { } callee) found.Add(callee);
                }
                catch (Exception error) when (error is ArgumentException or TypeLoadException or FileNotFoundException or FileLoadException
                                                  or MissingMethodException or BadImageFormatException)
                {
                    // A member this build cannot resolve is one it cannot reach.
                }
            }

            at += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, at),
                _ => 4,
            };
        }

        return found;
    }
}
