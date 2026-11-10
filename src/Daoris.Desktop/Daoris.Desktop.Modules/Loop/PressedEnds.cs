using System.Collections.Concurrent;

namespace Daoris.Desktop;

/// <summary>
/// An update or a pin Ask Daoris pressed, whose end is said in its conversation: the press waits here from its Confirm until the
/// harness action it started ends. One at a time, as the action's one slot is.
/// </summary>
public sealed class PressedEnds
{
    private readonly ConcurrentDictionary<string, (string Session, string Id, string Command)> _waiting = new(StringComparer.Ordinal);

    /// <summary>Waits on the end of a confirmed press whose view is an agent's update or pin; any other press is none of this.</summary>
    public void Wait(PipeConfirmation shown)
    {
        if (shown.Asker.Session is not { } session || shown.View is not { } view || view.Command is not { Length: > 0 } command) return;
        if (!view.Facts.TryGetValue("agent", out var harness)) return;
        var action = view.Kind.StartsWith("agent-update", StringComparison.Ordinal) ? "update"
            : view.Kind.StartsWith("agent-pin", StringComparison.Ordinal) ? "pin" : null;
        if (action is not null) _waiting[Key(harness, action)] = (session, shown.Id, command);
    }

    /// <summary>The end of a harness action, said into the conversation of the press that started it, if one did.</summary>
    public void Ended(string harness, string action, int exitCode, string? problem, Func<string, string, string, bool>? tell)
    {
        if (!_waiting.TryRemove(Key(harness, action), out var press)) return;
        var word = problem is null && exitCode == 0
            ? $"Daoris: `{press.Command}` finished."
            : $"Daoris: `{press.Command}` did not finish: {problem ?? $"it ended with exit code {exitCode}"}.";
        tell?.Invoke(press.Session, word, $"press-ended/{press.Id}");
    }

    private static string Key(string harness, string action) => $"{harness}:{action}";
}
