using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a press of Ask Daoris's does after its Confirm, said in its conversation: an update or a pin waits here for the end of the
/// harness action it started, and a verb that published an ask or a quest has them kept with the conversation
/// (<see cref="HelpPublished"/>) and named in its line. Either may happen before the settled sink registers the press, so an end or an
/// answer that has no press yet is held, and the press takes it as it registers.
/// </summary>
public sealed class PressedEnds(Func<DateTimeOffset>? clock = null)
{
    private const int Held = 64;

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly ConcurrentDictionary<string, Pressed> _waiting = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Pressed> _answering = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, int Exit, string? Problem)> _earlyEnds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _earlyAnswers = new(StringComparer.Ordinal);

    private sealed record Pressed(string Session, string Id, string Command, DateTimeOffset Asked);

    /// <summary>
    /// Takes a confirmed press of an Ask Daoris conversation: waits on the end of the action an update or a pin started, and on the answer
    /// that may name an ask or a quest. What either did already is said now.
    /// </summary>
    public void Wait(PipeConfirmation shown, Func<string, string, string, bool>? tell, Action<string, HelpPublishedEntry>? keep)
    {
        if (shown.Asker.Session is not { } session || shown.View is not { } view || view.Command is not { Length: > 0 } command) return;
        var press = new Pressed(session, shown.Id, command, shown.Asked);
        _answering[shown.Id] = press;
        if (_earlyAnswers.TryRemove(shown.Id, out var text)) Answered(shown.Id, text, tell, keep);

        if (!view.Facts.TryGetValue("agent", out var harness)) return;
        var action = view.Kind.StartsWith("agent-update", StringComparison.Ordinal) ? "update"
            : view.Kind.StartsWith("agent-pin", StringComparison.Ordinal) ? "pin" : null;
        if (action is null) return;
        _waiting[Key(harness, action)] = press;
        if (_earlyEnds.TryRemove(Key(harness, action), out var ended) && ended.At >= shown.Asked) Ended(harness, action, ended.Exit, ended.Problem, tell);
    }

    /// <summary>The end of a harness action, said into the conversation of the press that started it; held where no press is waiting yet.</summary>
    public void Ended(string harness, string action, int exitCode, string? problem, Func<string, string, string, bool>? tell)
    {
        if (!_waiting.TryRemove(Key(harness, action), out var press))
        {
            Hold(_earlyEnds, Key(harness, action), (_clock(), exitCode, problem));
            return;
        }

        var word = problem is null && exitCode == 0
            ? $"Daoris: `{press.Command}` finished."
            : $"Daoris: `{press.Command}` did not finish: {problem ?? $"it ended with exit code {exitCode}"}.";
        tell?.Invoke(press.Session, word, $"press-ended/{press.Id}");
    }

    /// <summary>
    /// The answer a confirmed press's verb gave, as the pipe wrote it: an ask or a quest it names is kept with the conversation and said
    /// in its line. Held where the press is not registered yet.
    /// </summary>
    public void Answered(string id, string answer, Func<string, string, string, bool>? tell, Action<string, HelpPublishedEntry>? keep)
    {
        if (!_answering.TryRemove(id, out var press))
        {
            Hold(_earlyAnswers, id, answer);
            return;
        }

        if (Named(answer) is not var (ask, quest)) return;
        keep?.Invoke(press.Session, new HelpPublishedEntry(_clock(), id, "press", null) { Ask = ask, Quest = quest });
        var made = string.Join(" and ", new[] { ask is null ? null : $"ask `#{ask}`", quest is null ? null : $"quest `#{quest}`" }.OfType<string>());
        tell?.Invoke(press.Session, $"Daoris: `{press.Command}` published {made}.", $"press-published/{id}");
    }

    /// <summary>The ask and the quest a verb's answer names, or none where it names neither.</summary>
    private static (string? Ask, string? Quest)? Named(string answer)
    {
        try
        {
            using var document = JsonDocument.Parse(answer);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
            string? Id(string name) => data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } id ? id : null;
            var (ask, quest) = (Id("ask"), Id("quest"));
            return ask is null && quest is null ? null : (ask, quest);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Hold<T>(ConcurrentDictionary<string, T> held, string key, T value)
    {
        held[key] = value;
        // What no press ever claims goes in time, oldest forgotten first, as a few are enough: the press follows its end by a moment.
        while (held.Count > Held && held.Keys.FirstOrDefault() is { } stale) held.TryRemove(stale, out _);
    }

    private static string Key(string harness, string action) => $"{harness}:{action}";
}
