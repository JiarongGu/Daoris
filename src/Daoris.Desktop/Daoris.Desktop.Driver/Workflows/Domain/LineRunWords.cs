using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// A verification run in the terminal's words (FABRIC-PIPELINE1d; the pipeline design §4.4): what it judged and its verdict on the run's head,
/// then who asked it and each gate's evidence. The page words the same verdicts from its catalogue.
/// </summary>
public static class LineRunWords
{
    /// <summary>The run's subject and verdict, beside its repository and workflow on its head line.</summary>
    public static string Subject(WorkflowRunLine line)
    {
        var at = TextBounds.ShortCommit(line.Commit, 7);
        var of = line.Branch is { } branch ? $"{branch} at {at}" : at;
        return $"verification {line.Run} of {of} · {Verdict(line)}";
    }

    /// <summary>A verdict as its status word: <i>verified</i>, <i>failed: `gate`</i>, <i>not known: why</i>, <i>not now</i>, <i>running</i>, <i>waiting</i>.</summary>
    public static string Verdict(WorkflowRunLine line) => line.Verdict switch
    {
        LineVerdicts.Verified => "verified",
        LineVerdicts.Failed => line.Gate is { } gate ? $"failed: `{gate}`" : "failed",
        LineVerdicts.NotNow => "not now",
        LineVerdicts.Unknown => $"not known: {Why(line.Why)}",
        LineVerdicts.Running => "running its gates",
        LineVerdicts.Waiting => "waiting for its turn",
        _ => line.Verdict,
    };

    /// <summary>Why a check is not known, by <see cref="VerifyResult"/>'s code; any other said as nothing establishing it.</summary>
    public static string Why(string? code) => code switch
    {
        VerifyResult.Changed => "the gates changed since they were accepted",
        VerifyResult.Bound => "it ran past its bound",
        VerifyResult.Stopped => "it was stopped",
        VerifyResult.Busy => "the runner stayed busy",
        _ => "nothing says what became of it",
    };

    /// <summary>The lines under the head: who asked it and when, then each gate with its verdict, exit, wall and log.</summary>
    public static IEnumerable<string> Under(WorkflowRunLine line)
    {
        var who = line.Level == VerifyRuns.Person ? "asked by you" : $"asked by its {line.Level} route";
        yield return $"{who} at {line.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";
        if (line.Line is { } on)
        {
            yield return line.Base is { } from ? $"the batch landed on {on} since {TextBounds.ShortCommit(from, 7)}" : $"the first verification of {on}";
        }

        foreach (var gate in line.Gates)
        {
            var exit = gate.Exit is { } code ? $", exit {code}" : "";
            var wall = gate.WallSeconds is { } seconds ? $", {Math.Round(seconds).ToString(CultureInfo.InvariantCulture)}s" : "";
            var log = gate.Log is { } path ? $" · log {path}" : "";
            yield return $"gate {gate.Name}: {gate.Verdict}{exit}{wall}{log}";
        }
    }
}
