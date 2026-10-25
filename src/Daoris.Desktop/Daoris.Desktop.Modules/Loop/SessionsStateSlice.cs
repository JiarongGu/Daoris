using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The session records as every window holds them, the push path's third slice (PUSH1d2, D182): every record the service answers,
/// closed ones and every workspace's included, allow-listed to the fields the page reads, so a tick reads <c>/api/sessions</c> again in
/// no window, and a record a connector wrote reaches the windows with no desktop dispatch.
/// </summary>
/// <remarks>
/// <para><b>One entry, its variants read from it.</b> The page holds the widest list (every repository, closed included, every
/// workspace) and each view's list is derived from it; D182's PUSH1d2 note gives the measure that settled it.</para>
///
/// <para><b>A partition per workspace, keyed by session</b> (<see cref="StateSlice.Keyed"/>): a record's move pushes that record alone,
/// a delta on the number the window holds, never its workspace's history again; a record deleted is named among the keys gone.</para>
///
/// <para><b>Its own read.</b> The service's answer of every record (<see cref="ServiceClient.SessionRecordsJsonAsync"/>), not the look's
/// snapshot, which holds only the open ones; null before the service answers, which keeps the slice not judged.</para>
///
/// <para><b>Its wakes.</b> A change a press or the driver declared to the sessions (the driver's own record moves among them), a
/// conversation's end and the service coming up, on the kit's bus; and the change stream's <c>sessions</c> family, heard where the
/// watch tells the loop its moves (<see cref="DriverLoop.HeardMove"/>), which is how a connector's write arrives.</para>
///
/// <para><b>Desktop-only.</b> A record's tree is a machine path, which only this machine's windows read (D47, D182 point 2); the
/// transcript, the words waiting and the rest the page does not read stay the pull's.</para>
/// </remarks>
public static class SessionsStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "sessions";

    /// <summary>
    /// What each record carries: the fields of the page's <c>Session</c> type, as the service names them. A field the page comes to
    /// read is pushed once it is named here, which <c>SessionsStateSliceTests</c> holds against the page's type.
    /// </summary>
    public static readonly IReadOnlyList<string> Fields =
    [
        "id", "quest", "repository", "adapter", "state", "kind", "note", "evidence", "created", "updated", "workspace", "harnessVersion",
        "profile", "tree", "baseCommit", "ask", "opinion", "serves", "from", "fromWhy", "fromBy", "answer", "access", "noteParts", "piece",
        // An account's limit made its failure: every list words it limited (LIMITRESUME1a, D206 point 1).
        "limit",
        // D205 §9.3: the machine-local provenance, absent on a mirror and a sync.
        "actor", "choice", "asked", "override", "ran",
        // FOUNDATION1c (D212): the work its marks read as, judged in .NET and answered on loopback, beside the kind; absent on a record a
        // host answers none for, and then the page reads today's predicates.
        "work",
    ];

    /// <summary>
    /// What the service answers of a record that is never pushed: the transcript's path, the words waiting, and what no view reads
    /// from the list (whether its quest was taken by its connector, a stop's cause, its delete, its line of work, its mode, and why
    /// its take's bind lapsed, which the driver's end line reads, CLOSETAKE1; and the downloaded versions its starts were handed,
    /// which the downloads' rule reads, KEEP1b; and the quest a chat took, which the run's reader reads, CHATFLOW1a).
    /// </summary>
    public static readonly IReadOnlyList<string> PulledOnly =
        ["transcript", "took", "interrupted", "deletable", "said", "entry", "helpMode", "tookBind", "resolved", "tookQuest"];

    /// <summary>
    /// What wakes it on the kit's bus: a change declared to the sessions, a conversation's end and the service coming up. The
    /// change stream's family wakes it too (<see cref="HearLedger"/>).
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && changed.Scopes.Contains("sessions")),
        new(DriverLoop.EndedType),
        new("DRIVER_READY"),
    ];

    /// <summary>The slice the shell's loop owns, reading <paramref name="loop"/>'s service once it answers.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, async ct => loop.Service is { } service ? Judge(await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false)) : null)
        {
            Keyed = true,
            WakesOn = WakesOn,
            Hears = [wake => HearLedger(loop, wake)],
            // The service away or refusing: the windows keep what was judged, and the next wake reads it again.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>
    /// One judgement of what the service answered: a partition per workspace, named by it, each record under its id with the fields
    /// the page reads. A record with no id is no record; one with no workspace is the default's, as the service names it.
    /// </summary>
    public static IReadOnlyList<SliceJudged> Judge(string answered)
    {
        var records = JsonNode.Parse(answered) as JsonArray ?? throw new JsonException("the service answered the sessions as no list");
        var parts = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in records)
        {
            if (node is not JsonObject record || Text(record, "id") is not { } id) continue;
            var workspace = Text(record, "workspace") ?? "default";
            var picked = new JsonObject();
            foreach (var field in Fields)
            {
                if (record.TryGetPropertyValue(field, out var value)) picked[field] = value?.DeepClone();
            }

            if (!parts.TryGetValue(workspace, out var part)) parts[workspace] = part = new JsonObject();
            part[id] = picked;
        }

        return [.. parts.Select(each => new SliceJudged(each.Key, each.Value))];
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where the loop hears the change stream name the <c>sessions</c> family: a record
    /// written by any door, a connector's among them. Another family, or another topic, wakes nothing. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearLedger(DriverLoop loop, Action wake)
    {
        void Heard(string topic, string subject)
        {
            if (topic == ChangeTopics.Ledger
                && subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(Name, StringComparer.Ordinal))
            {
                wake();
            }
        }

        loop.MoveHeard += Heard;
        return new Hearing(() => loop.MoveHeard -= Heard);
    }

    private static string? Text(JsonObject record, string field) =>
        record[field] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private sealed class Hearing(Action stop) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0) stop();
        }
    }
}
