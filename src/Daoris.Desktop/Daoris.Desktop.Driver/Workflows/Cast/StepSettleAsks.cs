using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>A working session's settle as the connector's <c>step_settle</c> wrote it: its id, the session, the settle and the epoch it names.</summary>
public sealed record StepSettleKept(string Id, string Session, StepSettleAsk Ask, int? Epoch);

/// <summary>
/// The working session's settles of the steps handed to it, two files per session under the home's sessions folder:
/// <c>&lt;session&gt;.step-settle.json</c>, read as the service's <c>StepSettleBox</c> writes it, and <c>&lt;session&gt;.step-settle-state.json</c>,
/// the driver's alone, naming the settle it answered by its id. Each settle is taken once, at the settle door as the agent the session's start
/// ran (<see cref="SettleBy.OfChannel"/> for a session's channel, never the person), and what it came to is said to the session as its next
/// turn where it is refused. The file is the contract; the two sides share no code, and each side's tests hold the same shapes.
/// </summary>
public static class StepSettleAsks
{
    public const string AskSuffix = ".step-settle.json";

    public const string StateSuffix = ".step-settle-state.json";

    /// <summary>The states the driver writes: <c>kept</c> on the run's record, or <c>refused</c> with the door's code.</summary>
    public const string Kept = "kept", Refused = "refused";

    private static string PathOf(string home, string session, string suffix) => DaorisHome.Entry(home, HomeEntry.Sessions, session + suffix);

    /// <summary>The settles on file the driver has not answered, oldest first; one that does not read is not a settle.</summary>
    public static IReadOnlyList<StepSettleKept> Waiting(string home) =>
        [.. HandOverFiles.Leaves(DaorisHome.Entry(home, HomeEntry.Sessions), "*" + AskSuffix)
            .Select(leaf => leaf[..^AskSuffix.Length])
            .Where(SessionEventText.IsLocalId)
            .Select(session => Read(home, session))
            .OfType<StepSettleKept>()
            .Where(kept => AnsweredId(home, kept.Session) != kept.Id)];

    /// <summary>The session's settle, or null for none or a file that does not read.</summary>
    public static StepSettleKept? Read(string home, string session)
    {
        try
        {
            var path = PathOf(home, session, AskSuffix);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "id") is not { Length: > 0 } id || Text(root, "run") is not { Length: > 0 } run
                || Text(root, "node") is not { Length: > 0 } node)
            {
                return null;
            }

            var evidence = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("evidence", out var given) && given.ValueKind == JsonValueKind.Object)
            {
                foreach (var each in given.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.String)) evidence[each.Name] = each.Value.GetString()!;
            }

            int? epoch = root.TryGetProperty("epoch", out var held) && held.ValueKind == JsonValueKind.Number && held.TryGetInt32(out var number) ? number : null;
            return new StepSettleKept(id, session, new StepSettleAsk(run, node, Text(root, "outcome"), Text(root, "words"), evidence), epoch);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The id of the settle the driver answered for <paramref name="session"/>, or null.</summary>
    public static string? AnsweredId(string home, string session)
    {
        try
        {
            var path = PathOf(home, session, StateSuffix);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            return Text(document.RootElement, "id");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Takes each settle waiting: settled at the settle door as the session's agent (<paramref name="agentOf"/>, the agent its start ran; the
    /// unnamed <c>agent</c> where none is known), its state written, and a refusal said to the session through <paramref name="say"/> so it is
    /// its next turn. Returns what each came to.
    /// </summary>
    public static async Task<IReadOnlyList<NodeWorded>> TakeAsync(
        NodeRecordFiles records, Func<string, string?> agentOf, Func<string, string, string, CancellationToken, Task<SayAnswer>>? say, Func<DateTimeOffset> clock,
        CancellationToken ct, Func<string, IReadOnlyCollection<string>?>? makers = null, string? only = null)
    {
        var done = new List<NodeWorded>();
        await Taking.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await TakeEachAsync(records, agentOf, say, clock, ct, makers, only, done).ConfigureAwait(false);
        }
        finally
        {
            Taking.Release();
        }

        return done;
    }

    /// <summary>One take at a time: a box listed again under the lock is one an earlier take answered, so a second take of it is a no-op that says nothing.</summary>
    private static readonly SemaphoreSlim Taking = new(1, 1);

    private static async Task TakeEachAsync(
        NodeRecordFiles records, Func<string, string?> agentOf, Func<string, string, string, CancellationToken, Task<SayAnswer>>? say, Func<DateTimeOffset> clock,
        CancellationToken ct, Func<string, IReadOnlyCollection<string>?>? makers, string? only, List<NodeWorded> done)
    {
        foreach (var kept in Waiting(records.Home).Where(each => only is null || string.Equals(each.Session, only, StringComparison.OrdinalIgnoreCase)))
        {
            var by = SettleBy.OfChannel(TerminalTierBy.Session, appointed: false, kept.Epoch, agentOf(kept.Session), kept.Session)!;
            var worded = StepSettles.Settle(records, kept.Ask, by, clock, makers?.Invoke(kept.Ask.Run.Trim().TrimStart('#')));
            if (worded.Refused == SettleDoor.OwnWork && worded.Detail?.StartsWith(SettleDoor.NotYet, StringComparison.Ordinal) == true)
            {
                // The makers are not read (a restart): the ask stays open, so the settle box's sweep takes it again, and the session is told once.
                if (say is not null) await say(kept.Session, StepSettles.Said(worded), $"step-settle:{kept.Id}:not-yet", ct).ConfigureAwait(false);
                continue;
            }

            done.Add(worded);
            JsonFile.Write(PathOf(records.Home, kept.Session, StateSuffix), writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("id", kept.Id);
                writer.WriteString("state", worded.Done ? Kept : Refused);
                if (worded.Refused is { } code) writer.WriteString("code", code);
                writer.WriteString("said", StepSettles.Said(worded));
                writer.WriteString("at", Moments.Write(clock()));
                writer.WriteEndObject();
            });
            if (!worded.Done && say is not null) await say(kept.Session, StepSettles.Said(worded), $"step-settle:{kept.Id}", ct).ConfigureAwait(false);
        }
    }
}
