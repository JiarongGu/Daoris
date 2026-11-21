namespace Daoris.Driver;

/// <summary>
/// A check's facts teed into the bus's log, each kind named and versioned once here: an attempt started, and settled as passed, failed
/// with its gate and red classes, or unknown with its code. Ids, codes, counts and times only; the why and the log stay in the attempt's
/// result. Here until one registry declares every type.
/// </summary>
/// <remarks>
/// A settle names its start's key (<see cref="BusDraft.After"/>), so it is caused by the start this process appended. Every start is
/// settled before its effect starts again: by its verdict, by <see cref="CheckRequeuedKind"/> where a busy runner sends it back for a
/// later offer, or by <see cref="CheckEndedKind"/> where it ends with no result (the driver closing, its result unkept), whose restart
/// sends the same attempt again.
/// </remarks>
public static class CheckBusFacts
{
    /// <summary>The emitter of the check facts.</summary>
    public const string Emitter = "driver/checks";

    /// <summary>A check's attempt began, both its slots held.</summary>
    public const string CheckStartedKind = "check.started/1";

    /// <summary>A check's attempt passed.</summary>
    public const string CheckPassedKind = "check.passed/1";

    /// <summary>A check's attempt failed, naming its gate and its red classes.</summary>
    public const string CheckFailedKind = "check.failed/1";

    /// <summary>A check's attempt ended unknown (its code says why: bound, stopped, busy, unrun), never a failure: the check's give-up.</summary>
    public const string CheckUnknownKind = "check.unknown/1";

    /// <summary>A check's attempt sent back to the queue, its runner busy with its own run: this try settled, a later offer starting it again.</summary>
    public const string CheckRequeuedKind = "check.requeued/1";

    /// <summary>A check's attempt ended with no result (its code says why: <see cref="Closing"/>, <see cref="Unkept"/>); its restart sends it again.</summary>
    public const string CheckEndedKind = "check.ended/1";

    /// <summary>The driver closing cut the attempt off.</summary>
    public const string Closing = "closing";

    /// <summary>The attempt's result could not be written.</summary>
    public const string Unkept = "unkept";

    /// <summary>A check's attempt began: its run the subject, its attempt the effect, its repository, commit and gates' count.</summary>
    public static BusDraft CheckStarted(VerifySend send) => new(CheckStartedKind, send.Run, Emitter)
    {
        Effect = EffectOf(send),
        Key = StartKey(send),
        Payload =
        [
            ("invocation", send.Invocation), ("repository", send.Repository), ("commit", send.Commit), ("gates", send.Gates?.Count),
            ("reach", send.Reach is null ? "all" : "paths"),
        ],
    };

    /// <summary>
    /// A check's attempt settled, of the commit class: passed, failed with its gate and its red classes (the first
    /// <see cref="DesktopBusLogStore.MostListed"/>, and their count), or unknown with its code; its wall time is its handling time. Red
    /// classes its gate did not name leave both null: absent evidence stays unknown, never zero.
    /// </summary>
    public static BusDraft CheckSettled(VerifySend send, VerifyResult result)
    {
        var red = result.Red;
        return new(KindOf(result), send.Run, Emitter)
        {
            Effect = EffectOf(send),
            After = [StartKey(send)],
            Commit = true,
            Took = result.WallSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            Payload =
            [
                ("invocation", send.Invocation), ("commit", result.Commit), ("verdict", result.Verdict), ("code", result.Code), ("failed", result.Failed),
                ("red", red is not { Count: > 0 } ? null : (IReadOnlyList<string>)[.. red.Take(DesktopBusLogStore.MostListed).Select(RedId)]),
                ("redCount", red is null ? null : (long)red.Count), ("gates", result.Gates.Count),
            ],
        };
    }

    /// <summary>A check's attempt sent back by its busy runner, of the commit class: its start settled, its offers so far counted.</summary>
    public static BusDraft CheckRequeued(VerifySend send, int offers) => new(CheckRequeuedKind, send.Run, Emitter)
    {
        Effect = EffectOf(send),
        After = [StartKey(send)],
        Commit = true,
        Payload = [("invocation", send.Invocation), ("commit", send.Commit), ("code", VerifyResult.Busy), ("offers", offers)],
    };

    /// <summary>A check's attempt ended with no result, of the commit class: its start settled, <paramref name="code"/> saying why.</summary>
    public static BusDraft CheckEnded(VerifySend send, string code) => new(CheckEndedKind, send.Run, Emitter)
    {
        Effect = EffectOf(send),
        After = [StartKey(send)],
        Commit = true,
        Payload = [("invocation", send.Invocation), ("commit", send.Commit), ("code", code)],
    };

    /// <summary>The key a start is named by, so its settle is caused by it.</summary>
    private static string StartKey(VerifySend send) => "check:" + send.Invocation;

    private static string KindOf(VerifyResult result) => result.Verdict switch
    {
        VerifyVerdicts.Reached => CheckPassedKind,
        VerifyVerdicts.Failed => CheckFailedKind,
        _ => CheckUnknownKind,
    };

    /// <summary>A check attempt's effect: its invocation at its commit, the one run the executor makes of it.</summary>
    private static string EffectOf(VerifySend send) => $"{send.Invocation}|{send.Commit}";

    /// <summary>A red class as its suite and its unit's last name (a test file's name, never its path), bounded.</summary>
    private static string RedId(string id)
    {
        var bar = id.IndexOf('|', StringComparison.Ordinal);
        var unit = id[(bar + 1)..];
        var named = (bar < 0 ? "" : id[..(bar + 1)]) + unit[(unit.LastIndexOfAny(['/', '\\']) + 1)..];
        if (named.Length <= DesktopBusLogStore.Longest) return named;
        return named[..(char.IsHighSurrogate(named[DesktopBusLogStore.Longest - 1]) ? DesktopBusLogStore.Longest - 1 : DesktopBusLogStore.Longest)];
    }
}
