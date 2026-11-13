using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// What a git's start is handed on its input, kept beside the start for as long as the start lives: two object reads start the same
/// command, <c>cat-file --batch</c>, so a count tells one question from another by it.
/// </summary>
internal static class GitStartInputs
{
    private static readonly ConditionalWeakTable<ProcessStartInfo, string> s_inputs = new();

    private static readonly AsyncLocal<string?> s_placed = new();

    internal static void Hand(ProcessStartInfo info, string input) => s_inputs.AddOrUpdate(info, input);

    /// <summary>The input of the start the flow is placed for, which a stand-in of the machine's host hands the start it makes (<see cref="Hand"/>); null outside one.</summary>
    internal static string? Placed => s_placed.Value;

    /// <summary>Until disposed, the start this flow makes is the one of <paramref name="input"/>.</summary>
    internal static IDisposable Placing(string input)
    {
        var before = s_placed.Value;
        s_placed.Value = input;
        return new Restore(() => s_placed.Value = before);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    /// <summary>The input handed to <paramref name="info"/>'s start; null where it is handed none.</summary>
    internal static string? Of(ProcessStartInfo info) => s_inputs.TryGetValue(info, out var input) ? input : null;
}
