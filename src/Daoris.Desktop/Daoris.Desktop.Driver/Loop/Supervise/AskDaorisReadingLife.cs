using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// A reading's life around the one walk (<see cref="ISuperviseLife"/>): the range frozen when a reader takes it, its packet composed then
/// under the prompt's nonce and kept beside its report, the answer kept on the report with its reader and how that reader stands to the
/// range's writer, and every end that is no answer closing the range unread with why, logged and never said in the conversation. With no
/// choice ready, a queued range keeps the walk's sentence and waits; a range a reader already took closes unread with it.
/// </summary>
/// <param name="compose">The packet of a taken range under a nonce.</param>
/// <param name="holds">Whether the role that took the range still holds the kind at the generation it took it under.</param>
internal sealed class AskDaorisReadingLife(
    AskDaorisReports reports, Func<AskDaorisReport, string, CancellationToken, Task<AskDaorisPacketText>> compose, Func<SupervisorWaiting, bool> holds,
    MachineLog? log) : ISuperviseLife
{
    private AskDaorisPacketText? _sent;

    public async Task<string?> PromptAsync(SupervisorWaiting waiting, ReviewerChoice choice, CancellationToken ct)
    {
        var (session, range) = (waiting.Subject.Session, waiting.Subject.Id);
        var writer = waiting.Writers.FirstOrDefault();
        var taken = reports.Change(session, range, report => report.State is AskDaorisStates.Queued or AskDaorisStates.Running
            ? report with { State = AskDaorisStates.Running, Waiting = null, Writer = writer, Independence = ReadIndependence.Of(writer, choice.Label), Said = choice.Sentence }
            : null);
        if (taken is null) return null;
        if (_sent is null)
        {
            var sent = await compose(taken, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3)), ct).ConfigureAwait(false);
            reports.KeepPacket(taken, sent.Text);
            reports.Change(session, range, report => report with { Limits = [.. sent.Limits.Distinct()] });
            _sent = sent;
        }

        return SupervisorInstruction.Compose(waiting, _sent.Text);
    }

    public Task<string> SettleAsync(SuperviseRunAsk ask, SuperviseRunEnd end, CancellationToken ct)
    {
        var waiting = ask.Waiting;
        if (end.State == "stopped" || ct.IsCancellationRequested) return Task.FromResult(Close(waiting, "its turn was stopped"));
        if (end.State != "completed") return Task.FromResult(Close(waiting, $"its turn ended {end.State}"));
        var (answer, why) = AskDaorisReport.Read(end.LastWords, _sent?.Calls, _sent?.Words);
        if (answer is null) return Task.FromResult(Close(waiting, why ?? "its block does not read"));
        if (answer.Subject != waiting.Subject) return Task.FromResult(Close(waiting, "its block names another range"));
        if (!holds(waiting)) return Task.FromResult(Close(waiting, "the role that reads Ask Daoris's acts changed while it read"));

        var effective = ask.Choice.Effective!;
        var reader = new SupervisorAgentHolder(waiting.Role, ask.Revision, effective.Agent, effective.Model, effective.Effort, end.Ran, end.Posture, end.Session ?? "")
        {
            Tokens = end.Tokens,
        };
        var state = answer.Leave is null ? AskDaorisStates.Read : AskDaorisStates.Left;
        reports.Change(waiting.Subject.Session, waiting.Subject.Id, report => report.State != AskDaorisStates.Running ? null : report with
        {
            State = state, Closed = DateTimeOffset.UtcNow, Reader = reader, Findings = answer.Findings, More = answer.More, Leave = answer.Leave, Reasons = answer.Reasons,
        });
        log?.Info(
            "ask-daoris.read", ("range", waiting.Subject.Id), ("state", state), ("flags", answer.Findings.Count(each => each.Severity == AskDaorisFinding.Flag).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("findings", answer.Findings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return Task.FromResult(answer.Leave is null ? SupervisorAttemptOutcomes.Decided : SupervisorAttemptOutcomes.Left);
    }

    public void Unavailable(SupervisorWaiting waiting, string sentence)
    {
        var (session, range) = (waiting.Subject.Session, waiting.Subject.Id);
        if (reports.Find(session, range)?.State == AskDaorisStates.Running) Close(waiting, sentence);
        else reports.Change(session, range, report => report.State == AskDaorisStates.Queued && report.Waiting != sentence ? report with { Waiting = sentence } : null);
    }

    /// <summary>The range closed unread with <paramref name="why"/>, logged; the attempt's outcome, failed.</summary>
    public string Close(SupervisorWaiting waiting, string why)
    {
        Unread(reports, waiting.Subject.Session, waiting.Subject.Id, why, log);
        return SupervisorAttemptOutcomes.Failed;
    }

    public static void Unread(AskDaorisReports reports, string session, string range, string why, MachineLog? log)
    {
        var closed = reports.Change(session, range, report => report.State is AskDaorisStates.Queued or AskDaorisStates.Running
            ? report with { State = AskDaorisStates.Unread, Closed = DateTimeOffset.UtcNow, Why = why }
            : null);
        if (closed is not null) log?.Info("ask-daoris.unread", ("range", range), ("why", why));
    }
}
