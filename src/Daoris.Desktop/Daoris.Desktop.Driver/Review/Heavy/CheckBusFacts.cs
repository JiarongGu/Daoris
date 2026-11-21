namespace Daoris.Driver;

/// <summary>
/// A check's facts teed into the bus's log, their types the registry's (<see cref="BusTypes"/>): an attempt started, and settled as passed,
/// failed with its gate and red classes, or unknown with its code. Ids, codes, counts and times only; the why and the log stay in the
/// attempt's result.
/// </summary>
/// <remarks>
/// A settle names its start's key (<see cref="BusDraft.After"/>), so it is caused by the start this process appended. Every start is
/// settled before its effect starts again: by its verdict, by <see cref="BusTypes.CheckRequeued"/> where a busy runner sends it back for a
/// later offer, or by <see cref="BusTypes.CheckEnded"/> where it ends with no result (the driver closing, its result unkept), whose restart
/// sends the same attempt again.
/// </remarks>
public static class CheckBusFacts
{
    /// <summary>The driver closing cut the attempt off.</summary>
    public const string Closing = "closing";

    /// <summary>The attempt's result could not be written.</summary>
    public const string Unkept = "unkept";

    /// <summary>A check's attempt began: its run the subject, its attempt the effect, its repository, commit and gates' count.</summary>
    public static BusDraft CheckStarted(VerifySend send) => new(BusTypes.CheckStarted, send.Run)
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
    /// <see cref="BusShapes.MostListed"/>, and their count), or unknown with its code; its wall time is its handling time. Red
    /// classes its gate did not name leave both null: absent evidence stays unknown, never zero.
    /// </summary>
    public static BusDraft CheckSettled(VerifySend send, VerifyResult result)
    {
        var red = result.Red;
        return new(TypeOf(result), send.Run)
        {
            Effect = EffectOf(send),
            After = [StartKey(send)],
            Commit = true,
            Took = result.WallSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            Payload =
            [
                ("invocation", send.Invocation), ("commit", result.Commit), ("verdict", result.Verdict), ("code", result.Code), ("failed", result.Failed),
                ("red", red is not { Count: > 0 } ? null : (IReadOnlyList<string>)[.. red.Take(BusShapes.MostListed).Select(RedId)]),
                ("redCount", red is null ? null : (long)red.Count), ("gates", result.Gates.Count),
            ],
        };
    }

    /// <summary>A check's attempt sent back by its busy runner, of the commit class: its start settled, its offers so far counted.</summary>
    public static BusDraft CheckRequeued(VerifySend send, int offers) => new(BusTypes.CheckRequeued, send.Run)
    {
        Effect = EffectOf(send),
        After = [StartKey(send)],
        Commit = true,
        Payload = [("invocation", send.Invocation), ("commit", send.Commit), ("code", VerifyResult.Busy), ("offers", offers)],
    };

    /// <summary>A check's attempt ended with no result, of the commit class: its start settled, <paramref name="code"/> saying why.</summary>
    public static BusDraft CheckEnded(VerifySend send, string code) => new(BusTypes.CheckEnded, send.Run)
    {
        Effect = EffectOf(send),
        After = [StartKey(send)],
        Commit = true,
        Payload = [("invocation", send.Invocation), ("commit", send.Commit), ("code", code)],
    };

    /// <summary>The key a start is named by, so its settle is caused by it.</summary>
    private static string StartKey(VerifySend send) => "check:" + send.Invocation;

    private static BusType TypeOf(VerifyResult result) => result.Verdict switch
    {
        VerifyVerdicts.Reached => BusTypes.CheckPassed,
        VerifyVerdicts.Failed => BusTypes.CheckFailed,
        _ => BusTypes.CheckUnknown,
    };

    /// <summary>A check attempt's effect: its invocation at its commit, the one run the executor makes of it.</summary>
    private static string EffectOf(VerifySend send) => $"{send.Invocation}|{send.Commit}";

    /// <summary>A red class as its suite and its unit's last name (a test file's name, never its path), bounded.</summary>
    private static string RedId(string id)
    {
        var bar = id.IndexOf('|', StringComparison.Ordinal);
        var unit = id[(bar + 1)..];
        var named = (bar < 0 ? "" : id[..(bar + 1)]) + unit[(unit.LastIndexOfAny(['/', '\\']) + 1)..];
        if (named.Length <= BusShapes.Longest) return named;
        return named[..(char.IsHighSurrogate(named[BusShapes.Longest - 1]) ? BusShapes.Longest - 1 : BusShapes.Longest)];
    }
}
