using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Who changed the terminal's tier, and by which door.</summary>
public static class TerminalTierBy
{
    /// <summary>The person, in Daoris's window.</summary>
    public const string Window = "window";

    /// <summary>The person, confirming a terminal's press on its card.</summary>
    public const string Card = "card";

    /// <summary>Ask Daoris, pressing with no card for its conversation.</summary>
    public const string AskDaoris = "ask-daoris";

    /// <summary>A working session, pressing as its own.</summary>
    public const string Session = "session";

    /// <summary>A terminal, pressing with no card under the tier's act.</summary>
    public const string Terminal = "terminal";

    /// <summary>The verb, with no Daoris running.</summary>
    public const string Alone = "alone";
}

/// <summary>One change of the terminal's tier: the tier it set, when, who made it (<see cref="TerminalTierBy"/>), and the session that pressed it.</summary>
public sealed record TerminalTierChange(string Tier, DateTimeOffset At, string By, string? Session);

/// <summary>
/// One press a terminal made under the tier's act, run with no card: the route, the field that named what it acted on and its value,
/// its admission, and its end (<see cref="TerminalActs"/>'s outcomes).
/// </summary>
public sealed record TerminalPress(string Id, string Module, string Route, string? Scope, string? Target, DateTimeOffset At, string Outcome)
{
    /// <summary>The verb's command the press is, as its card would show it; null where its route has no view.</summary>
    public string? Command { get; init; }

    /// <summary>The refusal's code a failed press answered.</summary>
    public string? Code { get; init; }

    /// <summary>When its answer came, or when a restart found it interrupted.</summary>
    public DateTimeOffset? Ended { get; init; }
}

/// <summary>The record as the window reads it, newest first.</summary>
public sealed record TerminalActsNow(IReadOnlyList<TerminalTierChange> Changes, IReadOnlyList<TerminalPress> Presses);

/// <summary>
/// The record of the terminal's tier (<see cref="TerminalTiers"/>): each change of it, who made it and from which door, and each press a
/// terminal made under <c>act</c> with no card, kept across a restart in <c>data/terminal-acts.json</c>. A press still running when
/// Daoris stopped is read back <see cref="Interrupted"/>: its end is not known. The newest are kept.
/// </summary>
public sealed class TerminalActs
{
    public const int ChangesKept = 20;
    public const int PressesKept = 100;

    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";

    /// <summary>Answered, its end not known: the run broke off, or Daoris stopped under it.</summary>
    public const string Unknown = "unknown";

    /// <summary>Running when Daoris stopped, read back at the next start.</summary>
    public const string Interrupted = "interrupted";

    private readonly string? _path;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly List<TerminalTierChange> _changes = [];
    private readonly List<TerminalPress> _presses = [];

    /// <param name="path">Where it is kept; none keeps it in the run's memory alone.</param>
    public TerminalActs(string? path, TimeProvider? clock = null)
    {
        _path = path;
        _clock = clock ?? TimeProvider.System;
        if (!Restarted()) return;
        lock (_gate) Keep();
    }

    /// <summary>The tier set to <paramref name="tier"/> by <paramref name="by"/>, a session's press naming its session.</summary>
    public void Changed(string tier, string by, string? session)
    {
        lock (_gate)
        {
            _changes.Add(new TerminalTierChange(tier, _clock.GetUtcNow(), by, session));
            if (_changes.Count > ChangesKept) _changes.RemoveAt(0);
            Keep();
        }
    }

    /// <summary>A press admitted under act with no card, running: its id, for <see cref="Ended"/>.</summary>
    public string Began(string module, string route, string? scope, string? target)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        lock (_gate)
        {
            _presses.Add(new TerminalPress(id, module, route, scope, target, _clock.GetUtcNow(), Running));
            if (_presses.Count > PressesKept) _presses.RemoveAt(0);
            Keep();
        }

        return id;
    }

    /// <summary>The verb's command the press is, read beside its run.</summary>
    public void Commanded(string id, string command) => Settle(id, press => press with { Command = command });

    /// <summary>The press's answer came: <paramref name="outcome"/>, with the refusal's code where it failed.</summary>
    public void Ended(string id, string outcome, string? code) =>
        Settle(id, press => press with { Outcome = outcome, Code = code, Ended = _clock.GetUtcNow() });

    /// <summary>The changes and presses, newest first.</summary>
    public TerminalActsNow Now()
    {
        lock (_gate) return new([.. Enumerable.Reverse(_changes)], [.. Enumerable.Reverse(_presses)]);
    }

    private void Settle(string id, Func<TerminalPress, TerminalPress> change)
    {
        lock (_gate)
        {
            var at = _presses.FindIndex(each => each.Id == id);
            if (at < 0) return;
            _presses[at] = change(_presses[at]);
            Keep();
        }
    }

    /// <summary>The record as a restart reads it, a press still running read interrupted now; true where that changed what is kept.</summary>
    private bool Restarted()
    {
        if (_path is null || !File.Exists(_path)) return false;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(_path));
            var root = document.RootElement;
            var interrupted = false;
            foreach (var each in Array(root, "changes"))
            {
                if (Json.Text(each, "tier") is { } tier && Moments.Read(Json.Text(each, "at")) is { } at && Json.Text(each, "by") is { } by)
                {
                    _changes.Add(new TerminalTierChange(tier, at, by, Json.Text(each, "session")));
                }
            }

            foreach (var each in Array(root, "presses"))
            {
                if (Json.Text(each, "id") is not { } id || Json.Text(each, "module") is not { } module || Json.Text(each, "route") is not { } route
                    || Moments.Read(Json.Text(each, "at")) is not { } at || Json.Text(each, "outcome") is not { } outcome)
                {
                    continue;
                }

                var press = new TerminalPress(id, module, route, Json.Text(each, "scope"), Json.Text(each, "target"), at, outcome)
                {
                    Command = Json.Text(each, "command"), Code = Json.Text(each, "code"), Ended = Moments.Read(Json.Text(each, "ended")),
                };
                if (press.Outcome == Running)
                {
                    press = press with { Outcome = Interrupted, Ended = _clock.GetUtcNow() };
                    interrupted = true;
                }

                _presses.Add(press);
            }

            return interrupted;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _changes.Clear();
            _presses.Clear();
            return false;
        }
    }

    private static IEnumerable<JsonElement> Array(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
            : [];

    // Under the lock, oldest first. A write that fails leaves the run's memory as it is; the next change writes it again.
    private void Keep()
    {
        if (_path is null) return;
        try
        {
            JsonFile.Write(_path, writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("changes");
                foreach (var each in _changes)
                {
                    writer.WriteStartObject();
                    writer.WriteString("tier", each.Tier);
                    writer.WriteString("at", Moments.Write(each.At));
                    writer.WriteString("by", each.By);
                    if (each.Session is { } session) writer.WriteString("session", session);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("presses");
                foreach (var each in _presses)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", each.Id);
                    writer.WriteString("module", each.Module);
                    writer.WriteString("route", each.Route);
                    if (each.Scope is { } scope) writer.WriteString("scope", scope);
                    if (each.Target is { } target) writer.WriteString("target", target);
                    if (each.Command is { } command) writer.WriteString("command", command);
                    writer.WriteString("at", Moments.Write(each.At));
                    writer.WriteString("outcome", each.Outcome);
                    if (each.Code is { } code) writer.WriteString("code", code);
                    if (each.Ended is { } ended) writer.WriteString("ended", Moments.Write(ended));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }
}
