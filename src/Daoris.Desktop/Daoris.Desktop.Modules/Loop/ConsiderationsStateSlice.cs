using System.Text.Json;
using System.Text.Json.Nodes;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What the last look said and the page draws as standing facts (FOUNDATION1s, D182): why each open quest is sitting, the starts held for
/// the agent's trust, and the starts held on accounts. Judged by the shell's loop from what the look kept and pushed when one moves, so a
/// tick writes none of it into a window and a window opening reads its snapshot.
/// </summary>
/// <remarks>
/// <para><b>Three partitions</b>, each a list pushed whole: <c>considered</c> (<see cref="DriverLoop.Considered"/>), <c>untrusted</c>
/// (the trust holds as facts, D73: machine-local paths, so over this bridge only) and <c>waits</c> (<see cref="DriverLoop.TickWait"/>).
/// Nothing is judged before the first look.</para>
///
/// <para><b>Its wakes</b> are the loop's: each look wakes it where it kept them, and a trust press that wrote its grant takes that hold
/// off and wakes it, so the page shows the grant at once rather than a look later.</para>
/// </remarks>
public static class ConsiderationsStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "considerations";

    /// <summary>Its partitions, in the order they are judged.</summary>
    public static readonly IReadOnlyList<string> Partitions = ["considered", "untrusted", "waits"];

    /// <summary>The slice the shell's loop owns, judging what <paramref name="loop"/>'s last look kept.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, _ => Task.FromResult(Judge(loop)));

    /// <summary>One judgement: null before the first look; after it, the three lists as the tick used to hand them to the page.</summary>
    public static IReadOnlyList<SliceJudged>? Judge(DriverLoop loop)
    {
        if (loop.Considered is not { } considered) return null;
        return
        [
            new("considered", Written(considered)),
            new("untrusted", Written(loop.Trust.Latest.Select(hold => new
            {
                hold.Folder,
                hold.TrustFile,
                hold.Quest,
                hold.Ask,
                // Left out but where its file is the own sign-in's (POOL1a3, D197 point 3).
                Own = hold.Own ? true : (bool?)null,
            }))),
            new("waits", Written(loop.Look.Waits.Select(DriverLoop.TickWait))),
        ];
    }

    private static JsonArray Written<T>(IEnumerable<T> rows) =>
        new([.. rows.Select(row => JsonSerializer.SerializeToNode(row, IpcJson.Options))]);
}
