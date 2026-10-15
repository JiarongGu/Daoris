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
/// its note, past judging here like any refusal), <c>on</c> starts it within the limits. An ask the person wrote themselves (the
/// terminal verb) is theirs: they said yes by typing it, so it goes whatever the rule says.</para>
///
/// <para><b>The limits</b> (design §5): at most two helpers at once per asker, refused in a coded sentence the asker reads past
/// that; and each a slot under the cap, which only waits, since a slot frees.</para>
/// </remarks>
public static class HelperJudge
{
    /// <summary>The most helpers at work at once for one asker.</summary>
    public const int PerAsker = 2;

    /// <param name="state">The ask's state on file, or null where the driver wrote none.</param>
    /// <param name="asker">The asking session's record, or null where it is not on this machine.</param>
    /// <param name="started">How many of this asker's other asks are started now.</param>
    /// <param name="slot">Whether a slot under the cap is free for it.</param>
    public static HelperVerdict Judge(HelperAsk ask, HelperState? state, PriorSession? asker, HelperRule rule, int started, bool slot)
    {
        if (state is { State: HelperStates.Started or HelperStates.Answered or HelperStates.Refused }) return HelperVerdict.Done;
        if (asker is null or { Teammate: true }) return HelperVerdict.Refuse(HelperWhy.AskerGone);
        if (asker.Serves is not null || asker.Opinion is not null || asker.Ask is { Length: > 0 }) return HelperVerdict.Refuse(HelperWhy.AskerUnfit);

        if (ask.AskedBy == HelperAsk.ByAgent)
        {
            if (rule.Mode == HelperRules.Off) return HelperVerdict.Refuse(HelperWhy.Off);
            if (rule.Mode == HelperRules.Ask && state is not { State: HelperStates.Approved }) return HelperVerdict.Wait(null);
        }

        if (started >= PerAsker) return HelperVerdict.Refuse(HelperWhy.TooMany);
        return slot ? HelperVerdict.Go : HelperVerdict.Wait(HelperVerdict.Cap);
    }
}
