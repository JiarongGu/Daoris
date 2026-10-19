using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The asks as every window holds them, a pushed slice (PUSH1d3, D182): every ask the service answers, closed and done ones and every
/// workspace's included, each as it stands, allow-listed to the fields the page reads, so a tick reads <c>/api/asks</c> again in no
/// window, and an ask a connector wrote reaches every window by one push.
/// </summary>
/// <remarks>
/// <para><b>The quests' shape</b> (<see cref="QuestsStateSlice"/>): a partition per workspace keyed by ask, a file's path replaced by
/// whether it is kept here, and the person's words, which the asks' list never shows, left to the pull.</para>
///
/// <para><b>Its wakes.</b> A change a press declared to the asks or the quests and the service coming up, on the kit's bus; and the
/// change stream's <c>asks</c> family, its <c>quests</c>, since an ask stands done once its quests closed (<c>Asks.Standing</c>), and its
/// <c>sessions</c>, since an ask may be deleted only where nothing stands on its quests (<c>QuestExchange.DeletableAsync</c>).</para>
/// </remarks>
public static class AsksStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "asks";

    /// <summary>
    /// What each ask carries: the fields of the page's <c>Ask</c> type, as the service names them. A field the page comes to read is
    /// pushed once it is named here, which <c>AsksStateSliceTests</c> holds against the page's type.
    /// </summary>
    public static readonly IReadOnlyList<string> Fields =
    [
        "id", "workspace", "sentence", "state", "tier", "asked", "updated", "asker", "note", "links", "attachments", "proposal", "quests",
        "intake", "short", "deletable", "goAheads", "reviewChoices", "reviewProposals", "workflowChoices", "workflowProposals",
    ];

    /// <summary>What the service answers of an ask that is never pushed: every word the person gave on it, which a trace reads.</summary>
    public static readonly IReadOnlyList<string> PulledOnly = ["words", "wordsKeptFrom"];

    /// <summary>The change stream's families that move an ask as the service answers it.</summary>
    public static readonly IReadOnlyList<string> Families = ["asks", "quests", "sessions"];

    /// <summary>What wakes it on the kit's bus: a change declared to the asks or the quests, and the service coming up.</summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && (changed.Scopes.Contains("asks") || changed.Scopes.Contains("quests"))),
        new("DRIVER_READY"),
    ];

    /// <summary>The slice the shell's loop owns, reading <paramref name="loop"/>'s service once it answers.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, async ct => loop.Service is { } service ? Judge(await service.AsksJsonAsync(ct).ConfigureAwait(false)) : null)
        {
            Keyed = true,
            WakesOn = WakesOn,
            Hears = [wake => QuestsStateSlice.HearLedger(loop, Families, wake)],
            // The service away or refusing: the windows keep what was judged, and the next wake reads it again.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>One judgement of what the service answered: a partition per workspace, each ask under its id with the page's fields.</summary>
    public static IReadOnlyList<SliceJudged> Judge(string answered) => QuestsStateSlice.Partitioned(answered, Fields, "asks");
}
