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

    internal static void Hand(ProcessStartInfo info, string input) => s_inputs.AddOrUpdate(info, input);

    /// <summary>The input handed to <paramref name="info"/>'s start; null where it is handed none.</summary>
    internal static string? Of(ProcessStartInfo info) => s_inputs.TryGetValue(info, out var input) ? input : null;
}
