namespace Daoris.Driver;

/// <summary>
/// A check's facts teed into the bus's log, each kind named and versioned once here: an attempt started, and settled as passed, failed
/// with its gate and red classes, or unknown with its code. Ids, codes, counts and times only; the why and the log stay in the attempt's
/// result. Here until one registry declares every type.
/// </summary>
/// <remarks>A settle names its start's key (<see cref="BusDraft.After"/>), so it is caused by the start this process appended.</remarks>
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

    /// <summary>A check's attempt began: its run the subject, its attempt the effect, its repository, commit and gates' count.</summary>
    public static BusDraft CheckStarted(VerifySend send) => new(CheckStartedKind, send.Run, Emitter)
    {
        Effect = EffectOf(send),
        Key = "check:" + send.Invocation,
        Payload =
        [
            ("invocation", send.Invocation), ("repository", send.Repository), ("commit", send.Commit), ("gates", send.Gates?.Count),
            ("reach", send.Reach is null ? "all" : "paths"),
        ],
    };

    /// <summary>
    /// A check's attempt settled, of the commit class: passed, failed with its gate and its red classes (the first
    /// <see cref="DesktopBusLogStore.MostListed"/>, and their count), or unknown with its code; its wall time is its handling time.
    /// </summary>
    public static BusDraft CheckSettled(VerifySend send, VerifyResult result)
    {
        var red = result.Red ?? [];
        return new(KindOf(result), send.Run, Emitter)
        {
            Effect = EffectOf(send),
            After = ["check:" + send.Invocation],
            Commit = true,
            Took = result.WallSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            Payload =
            [
                ("invocation", send.Invocation), ("commit", result.Commit), ("verdict", result.Verdict), ("code", result.Code), ("failed", result.Failed),
                ("red", red.Count == 0 ? null : (IReadOnlyList<string>)[.. red.Take(DesktopBusLogStore.MostListed).Select(Bounded)]),
                ("redCount", (long)red.Count), ("gates", result.Gates.Count),
            ],
        };
    }

    private static string KindOf(VerifyResult result) => result.Verdict switch
    {
        VerifyVerdicts.Reached => CheckPassedKind,
        VerifyVerdicts.Failed => CheckFailedKind,
        _ => CheckUnknownKind,
    };

    /// <summary>A check attempt's effect: its invocation at its commit, the one run the executor makes of it.</summary>
    private static string EffectOf(VerifySend send) => $"{send.Invocation}|{send.Commit}";

    private static string Bounded(string id) => id.Length <= DesktopBusLogStore.Longest ? id : id[..DesktopBusLogStore.Longest];
}
