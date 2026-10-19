using System.Text.Json;
using System.Text.Json.Nodes;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The driver's state as every window holds it, the push path's first slice (PUSH1a, D182): its standing choices and holds, what
/// runs, and the shell's own standing, judged by the shell's loop from what <c>STATE</c> answers and pushed when it changes, so a
/// tick reads none of it again in any window.
/// </summary>
/// <remarks>
/// <para><b>One builder, two doors.</b> The projection is <c>STATE</c>'s own answer (<see cref="DriverRoutes.State"/>) with an
/// allow-list per partition, so the pull and the push cannot drift: a field <c>STATE</c> comes to answer is pushed only once it is
/// named here, and <c>DriverStateSliceTests</c> fails a field named in neither list.</para>
///
/// <para><b>Three partitions</b>, by how often each moves: <c>choices</c> (what `driver.json` says, a press or a terminal's edit
/// moves it), <c>running</c> (what runs and what waits for review, each look and each start or end moves it) and <c>shell</c>
/// (whether the service is up, and the home this shell established). A session's start pushes <c>running</c> alone.</para>
///
/// <para><b>Desktop-only.</b> The home is a machine path, which only this machine's windows read (D47, D182 point 2); the file the
/// choices are read from, the cap, the agent, the poll and the released stops are the pull's alone, read by no view.</para>
/// </remarks>
public static class DriverStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "driver";

    /// <summary>Each partition, and the fields of <c>STATE</c>'s answer it carries: the allow-list.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Partitions = new Dictionary<string, IReadOnlyList<string>>
    {
        ["shell"] = ["ready", "home", "homeNotice", "homeAccount", "hostNotice"],
        ["choices"] =
        [
            "drivable", "holds", "trees", "notify", "strikes", "coolOff", "forgiven", "intakeAdapter", "helperAdapter",
            "lines", "workspaceLines", "landings", "workspaceLandings", "readAcross", "workspaceReadAcross", "writeAcross", "standing",
            "languages", "workspaceLanguages", "languageTable", "reviews", "workspaceReviews", "opinions", "workspaceOpinions",
            "helpers", "workspaceHelpers", "commits", "workspaceCommits",
        ],
        ["running"] = ["running", "drivingBrowser", "inReview"],
    };

    /// <summary>
    /// What <c>STATE</c> answers that is never pushed: a press's own answer (what *Try again*, a review's put and *Show it again*
    /// said), and what no view reads (the file's path, the cap, the agent, the poll, the released stops).
    /// </summary>
    public static readonly IReadOnlyList<string> PulledOnly =
        ["retried", "reviewed", "shownAgain", "configPath", "cap", "adapter", "pollSeconds", "released"];

    /// <summary>
    /// What wakes it on the kit's bus: a change a press or the driver declared to its state, its sessions (what runs) or its agents
    /// (the families an opinion rule names); a conversation's end; and the service coming up. The loop's look wakes it too.
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed
            && changed.Scopes.Any(scope => scope is "driver" or "sessions" or "harnesses")),
        new("SESSION_ENDED"),
        new("DRIVER_READY"),
    ];

    /// <summary>The slice the shell's loop owns, judging <paramref name="loop"/>'s state.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(Judge(loop)))
        {
            WakesOn = WakesOn,
            // The file unreadable: the windows keep what was judged, and the page's own reads say the driver's refusal.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>One judgement: <c>STATE</c>'s answer as the bridge writes it, each partition its allow-listed fields.</summary>
    public static IReadOnlyList<SliceJudged> Judge(DriverLoop loop)
    {
        var state = JsonSerializer.SerializeToNode(DriverRoutes.State(loop), IpcJson.Options)!.AsObject();
        return [.. Partitions.Select(partition => new SliceJudged(partition.Key, Pick(state, partition.Value)))];
    }

    private static JsonObject Pick(JsonObject state, IReadOnlyList<string> fields)
    {
        var picked = new JsonObject();
        foreach (var field in fields)
        {
            if (state.TryGetPropertyValue(field, out var value)) picked[field] = value?.DeepClone();
        }

        return picked;
    }
}
