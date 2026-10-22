using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// How each agent's accounts are used, as every window holds it (PUSH1e, D182 points 2-4 and 6): <c>ACCOUNTS</c>' answer judged once
/// per wake for every window, its verdicts beside each reading (the curve now, whether a window is over it, each scope's curves and
/// measures), pushed when it moves, so a tick and a focus read <c>ACCOUNTS</c> again in no window, and a probe's result moves every
/// window by one push.
/// </summary>
/// <remarks>
/// <para><b>One builder, two doors.</b> The judge is <see cref="AccountsRoutes.AccountsAnswer"/> itself, so the pull an older page
/// makes and the push cannot drift. It reads files and memory only: <c>harnesses.json</c>, <c>cooling.json</c>, <c>windows.json</c>,
/// the names, the roster's projection held in memory and its walk asked without probing; never an agent asked. Daoris's sessions
/// running on each account are counted from the records the loop's sessions slice already holds (<see cref="Running"/>), never a read
/// of every record of its own; unknown until that slice is judged.</para>
///
/// <para><b>A partition per agent</b>, named by the accounts' owner as the route names it, each its answer for that agent pushed whole:
/// an agent's accounts are few, and one reading moves its scopes' measures beside its own row.</para>
///
/// <para><b>Its wakes</b>, each only asking. A change declared to the roster's scope (<c>harnesses</c>: <i>Read now</i>, an edit of how
/// accounts are used, <i>Try now</i>, a read of the roster again, an account's settings), a harness action's end (a sign-in makes an
/// account), a session's end (a limit may have cooled an account, its door may have said what one has left), the service coming up, and
/// a moved running count (<see cref="HearRunning"/>). A terminal's edit of the files, a door's frame written mid-session, and the curve
/// moving with the clock are read at the period, 15 s, and pushed only where a whole percent or a verdict moved.</para>
///
/// <para><b>Desktop-only.</b> Account names ride the bridge only (D47 §4); nothing here is a credential, a key or who signed in.</para>
/// </remarks>
public static class AccountsStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "accounts";

    /// <summary>
    /// What an agent's partition carries: the fields of the route's agent, as the page's <c>AgentAccounts</c> reads them; <c>pooled</c>,
    /// the fold's unsaid receipts (POOL1a1, the account-pool design §5.2, §6.3), so the page that shows them reads them from the push.
    /// </summary>
    public static readonly IReadOnlyList<string> AgentFields = ["agent", "speaks", "reads", "own", "accounts", "scopes", "pooled"];

    /// <summary>
    /// What each account carries: the fields of the route's account, the page's <c>AccountFacts</c> among them. Never a key, who signed
    /// in, or the agent's own words: the route answers none.
    /// </summary>
    public static readonly IReadOnlyList<string> AccountFields = ["name", "displayName", "cooling", "offered", "said", "week", "running"];

    /// <summary>What the route answers of an agent or an account that is never pushed: nothing yet.</summary>
    public static readonly IReadOnlyList<string> PulledOnly = [];

    /// <summary>What wakes it on the kit's bus. A moved running count wakes it too (<see cref="HearRunning"/>).</summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && changed.Scopes.Contains("harnesses")),
        new("HARNESS_ENDED"),
        new(DriverLoop.EndedType),
        new("DRIVER_READY"),
    ];

    /// <summary>The slice the shell's loop owns, judged from <paramref name="loop"/>'s roster at once, with no service.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(Judge(loop.Harnesses, Running(loop), loop.Harnesses.Clock())))
        {
            WakesOn = WakesOn,
            Hears = [wake => HearRunning(loop, wake)],
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>One judgement: the route's answer, a partition per agent named by it, each the agent's answer as the route writes it.</summary>
    public static IReadOnlyList<SliceJudged> Judge(HarnessRoster roster, IReadOnlyDictionary<string, int>? running, DateTimeOffset now)
    {
        var answer = JsonSerializer.SerializeToNode(AccountsRoutes.AccountsAnswer(roster, running, now), KeepNulls)!.AsObject();
        var judged = new List<SliceJudged>();
        foreach (var agent in answer["agents"]?.AsArray() ?? [])
        {
            if (agent is not JsonObject written || Json.NonEmpty(written, "agent") is not { } name) continue;
            judged.Add(new SliceJudged(name, Picked(written)));
        }

        return judged;
    }

    /// <summary>An agent as pushed: the fields of <see cref="AgentFields"/>, each account the fields of <see cref="AccountFields"/>.</summary>
    private static JsonObject Picked(JsonObject agent)
    {
        var picked = Pick(agent, AgentFields);
        if (picked["accounts"] is JsonArray accounts)
        {
            picked["accounts"] = new JsonArray([.. accounts.Select(account => account is JsonObject one ? (JsonNode)Pick(one, AccountFields) : null)]);
        }

        return picked;
    }

    private static JsonObject Pick(JsonObject from, IReadOnlyList<string> fields)
    {
        var picked = new JsonObject();
        foreach (var field in fields)
        {
            if (from.TryGetPropertyValue(field, out var value)) picked[field] = value?.DeepClone();
        }

        return picked;
    }

    /// <summary>
    /// Daoris's sessions running on each account, from the records the loop's sessions slice holds in memory, counted by the route's own
    /// rule (<see cref="AccountsRoutes.Counted"/>); null where that slice has not judged, which the page says as unknown, never zero.
    /// </summary>
    public static IReadOnlyDictionary<string, int>? Running(DriverLoop loop)
    {
        var snapshot = loop.Slices.Find(SessionsStateSlice.Name)?.Snapshot();
        if (snapshot is not { Status: StateSlice.Ready }) return null;
        var records = snapshot.Partitions
            .SelectMany(partition => partition.Data is JsonObject part ? part.Select(each => each.Value as JsonObject) : [])
            .OfType<JsonObject>();
        return RunningOn(records, AccountsRoutes.Owners(loop.Harnesses));
    }

    /// <summary>The running count over records as the sessions slice holds them, by the route's own rule.</summary>
    public static IReadOnlyDictionary<string, int> RunningOn(IEnumerable<JsonObject> records, Func<string, string> ownerOf) =>
        AccountsRoutes.Counted(
            records.Select(record => (Json.NonEmpty(record, "id"), Json.NonEmpty(record, "adapter"), Json.NonEmpty(record, "profile"), Json.NonEmpty(record, "state"))),
            ownerOf);

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where the loop's sessions slice pushed a move that changed a running count: a session's
    /// start or end on an account. A record's move that counts the same (a working session's every word) wakes nothing. Disposed, it
    /// hears no more.
    /// </summary>
    public static IDisposable HearRunning(DriverLoop loop, Action wake)
    {
        var sessions = loop.Slices.Find(SessionsStateSlice.Name);
        if (sessions is null) return new Hearing(() => { });
        var heard = Counter(() => Running(loop), wake);
        sessions.Pushed += heard;
        return new Hearing(() => sessions.Pushed -= heard);
    }

    /// <summary>What a hearing does at each move: count again, and wake where the counts differ from the last heard.</summary>
    public static Action Counter(Func<IReadOnlyDictionary<string, int>?> count, Action wake)
    {
        string? last = null;
        var gate = new Lock();
        return () =>
        {
            var counted = count();
            var now = counted is null ? "" : string.Join(",", counted.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key}={each.Value}"));
            bool moved;
            lock (gate)
            {
                moved = now != last;
                last = now;
            }

            if (moved) wake();
        };
    }

    /// <summary>The wire's options with every null written, as the bridge answers the route: an absent fact reads as null on both.</summary>
    private static readonly JsonSerializerOptions KeepNulls = new(IpcJson.Options)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private sealed class Hearing(Action stop) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0) stop();
        }
    }
}
