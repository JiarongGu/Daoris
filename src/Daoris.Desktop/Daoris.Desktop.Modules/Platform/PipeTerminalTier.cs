using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A person's press that proves no session's key, judged by the machine's terminal tier (<see cref="TerminalTiers"/>, read at each
/// press): under <c>act</c>, from a process the pipe places outside every agent Daoris runs, it is the person's press already confirmed,
/// run with no card on the delegate's path (<see cref="PipeDelegate.Pressed"/>) and attributed to a terminal. Under <c>ask</c>, and from
/// a process inside an agent's job or one the pipe cannot place, it is asked on the card as before: a session's process that left its
/// key off is still a session's. Each press run under act is kept in the tier's record (<see cref="TerminalActs"/>): what it named,
/// the command it is, its admission and its end.
/// </summary>
internal static class PipeTerminalTier
{
    /// <summary>The covered judgement under act from outside every agent; null where the press is asked as before.</summary>
    public static PipeDelegateJudgement? Judge(InstancePipe pipe, PipeConnection connection)
    {
        if (pipe.TerminalTier?.Invoke() != TerminalTiers.Act) return null;
        return pipe.StandingConfirmation.Caller.Place(connection.Client) == StandingPlace.Outside
            ? new(PipeDelegateTier.Covered, Terminal: true)
            : null;
    }

    /// <summary>A press admitted under act, kept running from now, its command read beside its run and its end once it answers; the windows told at each.</summary>
    public static void Kept(InstancePipe pipe, IpcRequest request, Task<string> answer)
    {
        if (pipe.TerminalActs is not { } acts) return;
        var (scope, target) = HelpPressRecord.Subject(request);
        var kept = acts.Began(request.Module ?? "", request.Type ?? "", scope, target);
        pipe.TellDriverState();
        _ = CommandAsync(pipe, acts, request, kept);
        _ = answer.ContinueWith(done =>
        {
            var (outcome, code) = Outcome(done);
            acts.Ended(kept, outcome, code);
            pipe.TellDriverState();
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// How an answer ended the press: <c>done</c> where it succeeded; <c>unknown</c> where no answer reads or Daoris stopped under it, so
    /// its end is not known; else <c>failed</c>, with its refusal's code.
    /// </summary>
    internal static (string Outcome, string? Code) Outcome(Task<string> done)
    {
        if (!done.IsCompletedSuccessfully) return (TerminalActs.Unknown, null);
        try
        {
            using var read = JsonDocument.Parse(done.Result);
            var root = read.RootElement;
            if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True)
            {
                return root.TryGetProperty("data", out var data) ? Declined(data) ?? (TerminalActs.Done, null) : (TerminalActs.Done, null);
            }

            var code = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
            return code is InstancePipe.Interrupted or InstancePipe.Stopping ? (TerminalActs.Unknown, code) : (TerminalActs.Failed, code);
        }
        catch (JsonException)
        {
            return (TerminalActs.Unknown, null);
        }
    }

    /// <summary>
    /// A press the pipe answered as succeeded whose route says it did not happen: a refusal answered as data (<c>done: false</c>, its
    /// <c>refused</c> code where it names one) or a verb's run that exited non-zero (<c>said.code</c>); null where it did.
    /// </summary>
    private static (string Outcome, string? Code)? Declined(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return null;
        if (data.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.False)
        {
            return (TerminalActs.Failed, data.TryGetProperty("refused", out var refused) && refused.ValueKind == JsonValueKind.String ? refused.GetString() : null);
        }

        return data.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.Object
            && said.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var exit) && exit != 0
                ? (TerminalActs.Failed, null)
                : null;
    }

    /// <summary>The verb's command the press is, as its route's view words it for a card; none where the route has no view or it reads none.</summary>
    private static async Task CommandAsync(InstancePipe pipe, TerminalActs acts, IpcRequest request, string kept)
    {
        if (pipe.Grounds is not { } grounds) return;
        var viewed = await PressViews.ReadAsync(grounds, request.Module ?? "", request.Type ?? "", request.Payload, CancellationToken.None).ConfigureAwait(false);
        if (viewed?.View is not { } view) return;
        acts.Commanded(kept, view.Command);
        pipe.TellDriverState();
    }
}
