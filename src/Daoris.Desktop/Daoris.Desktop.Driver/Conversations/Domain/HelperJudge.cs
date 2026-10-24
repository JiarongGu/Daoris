namespace Daoris.Driver;

/// <summary>What the driver judges of one ask: it goes, it waits (and why), it is refused (with a code), or it is past judging.</summary>
public sealed record HelperVerdict(string Kind, string? Code = null)
{
    public const string GoKind = "go";
    public const string WaitKind = "wait";
    public const string RefuseKind = "refuse";
    public const string DoneKind = "done";

    /// <summary>Why a waiting ask waits where it is no yes the person owes: every slot under the cap is taken.</summary>
    public const string Cap = "cap";

    /// <summary>
    /// Why a waiting ask waits for its asker (COAGENT1a, D199 point 5): a turn of the asker's is in flight, and its work in progress
    /// is captured only at a turn's boundary.
    /// </summary>
    public const string Turn = "turn";

    public static HelperVerdict Go { get; } = new(GoKind);

    /// <summary>The ask is past its start (started, answered or refused): the look reads it elsewhere.</summary>
    public static HelperVerdict Done { get; } = new(DoneKind);

    /// <summary>Wait: for the person's yes where <paramref name="code"/> is null, else for what the code names.</summary>
    public static HelperVerdict Wait(string? code) => new(WaitKind, code);

    public static HelperVerdict Refuse(string code) => new(RefuseKind, code);
}

/// <summary>
/// The driver's judgement of each ask for a helper (MIXAGENT2a-ii, D163 point 2; the mixed agents design §5): what only the driver
/// can judge, since only it reads <c>driver.json</c>, and again what the connector judged, since an ask on file may be old.
/// </summary>
/// <remarks>
/// <para><b>The person's rule is for an agent's ask.</b> <c>off</c> refuses it, <c>ask</c> holds it <c>waiting</c> until the
/// person approves (MIXAGENT2b's press) or says no (HELPERNO1's, a refusal <see cref="HelperWhy.Declined"/> the bridge writes with
/// its note, past judging here like any refusal), <c>on</c> starts it within the limits. Where nothing is set, the default is
/// <c>on</c> for the agents Daoris set up and signed in on this machine (HELPERDEFAULT1), and an ask naming another is refused
/// <see cref="HelperWhy.NotSetUp"/>, as one with none set up is <see cref="HelperWhy.NoneSetUp"/>. An ask the person wrote themselves (the
/// terminal verb) is theirs: they said yes by typing it, so it goes whatever the rule says.</para>
///
/// <para><b>The limits</b> (design §5): at most two helpers at once per asker, refused in a coded sentence the asker reads past
/// that; and each a slot under the cap, which only waits, since a slot frees.</para>
///
/// <para><b>The asker's turn boundary</b> (COAGENT1a, D199 point 5; the co-agent design §2.5): the helper's copy carries the
/// asker's uncommitted work, captured with no turn of the asker's in flight, so an ask whose asker is mid-turn waits
/// (<see cref="HelperVerdict.Turn"/>) and starts at the look after that turn ends. An urgent ask waits the same: its answer still
/// reaches the asker at its next step where it is working again by then.</para>
/// </remarks>
public static class HelperJudge
{
    /// <summary>The most helpers at work at once for one asker.</summary>
    public const int PerAsker = 2;

    /// <param name="state">The ask's state on file, or null where the driver wrote none.</param>
    /// <param name="asker">The asking session's record, or null where it is not on this machine.</param>
    /// <param name="started">How many of this asker's other asks are started now.</param>
    /// <param name="slot">Whether a slot under the cap is free for it.</param>
    /// <param name="between">Whether the asker has no turn in flight: its record is not running, or its newest turn has ended.</param>
    public static HelperVerdict Judge(HelperAsk ask, HelperState? state, PriorSession? asker, HelperRule rule, int started, bool slot, bool between = true)
    {
        if (state is { State: HelperStates.Started or HelperStates.Answered or HelperStates.Refused }) return HelperVerdict.Done;
        if (asker is null or { Teammate: true }) return HelperVerdict.Refuse(HelperWhy.AskerGone);
        if (asker.Serves is not null || asker.Opinion is not null || asker.Ask is { Length: > 0 }) return HelperVerdict.Refuse(HelperWhy.AskerUnfit);

        if (ask.AskedBy == HelperAsk.ByAgent)
        {
            if (rule.Mode == HelperRules.Off) return HelperVerdict.Refuse(HelperWhy.Off);
            // HELPERDEFAULT1 (D163's 2026-10-24 note): the default asks Daoris's own agents alone, those set up and signed in here.
            if (rule.SetUp && ask.Agent is { Length: > 0 } named
                           && !rule.Agents.Any(agent => string.Equals(agent, named, StringComparison.OrdinalIgnoreCase)))
            {
                return HelperVerdict.Refuse(HelperWhy.NotSetUp);
            }

            if (rule.SetUp && rule.Agents.Count == 0) return HelperVerdict.Refuse(HelperWhy.NoneSetUp);
            // A cooling ask already ran once (LIMITRESUME1h): the yes it started under holds for its run again.
            if (rule.Mode == HelperRules.Ask && state is not { State: HelperStates.Approved or HelperStates.Cooling }) return HelperVerdict.Wait(null);
        }

        if (started >= PerAsker) return HelperVerdict.Refuse(HelperWhy.TooMany);
        if (!between) return HelperVerdict.Wait(HelperVerdict.Turn);
        return slot ? HelperVerdict.Go : HelperVerdict.Wait(HelperVerdict.Cap);
    }
}
