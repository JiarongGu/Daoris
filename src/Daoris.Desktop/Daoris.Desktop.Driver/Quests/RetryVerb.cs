using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver retry</c> (RETRY1, SESSUX1b; FOUNDATION1 mod-iii): the work orders' own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the judgement of a quest's <i>Try again</i> (<see cref="QuestRetry"/>). The planner's verdict on
/// each quest, over a fresh snapshot of the service this machine's driver watches, is handed it by Loop (<paramref name="readVerdicts"/>).
/// </summary>
/// <param name="readVerdicts">The planner's verdicts, read over the choices.</param>
public sealed class RetryVerb(Func<DriverConfig, CancellationToken, Task<IReadOnlyList<Consideration>>> readVerdicts) : ChoiceVerb
{
    public override string Word => "retry";

    /// <summary>
    /// <c>retry</c> (RETRY1, SESSUX1b): with <c>--session</c> the person's stop released, with <c>--at</c> a mark the person gives, both
    /// alone; else the quest's *Try again*, judged from the planner's verdict (<see cref="QuestRetry.Decide"/>) on both paths.
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var named = Named(args, "retry");
        var quest = named.StartsWith('#') ? named[1..] : named;

        int Released(string session)
        {
            Say($"daoris: quest `#{quest}` is released from your stop of session `{session}`.");
            Say("  The driver takes it up again at its next look: a taken quest is carried on in the tree that session");
            Say("  worked in, an open one is planned again. A later stop holds it again.");
            Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
            return 0;
        }

        int Marked(int mark, bool counted, int strikes)
        {
            Say($"daoris: quest `#{quest}` may be started again.");
            Say($"  Counting from {mark} failure(s){(counted ? ", as this machine's records count them" : "")} — what already");
            Say($"  happened is still in the records, and {(strikes == 0 ? "no" : strikes.ToString(CultureInfo.InvariantCulture))} more failure(s) will park it again.");
            Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
            return 0;
        }

        var sessionAt = IndexOf(args, "--session");
        if (sessionAt != -1)
        {
            var session = args.ElementAtOrDefault(sessionAt + 1)?.Trim();
            if (string.IsNullOrEmpty(session) || session.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ChoiceWords("`--session` needs the session your stop ended — the quest's *Sitting* names it: "
                    + "`daoris driver retry <quest> --session <id>`.");
            }

            if (args.Contains("--at"))
            {
                throw new ChoiceWords("a retry either releases your stop (`--session <id>`) or counts the strikes from a mark "
                    + "(`--at <n>`) — a stop is not a strike, so the two never go together.");
            }

            var key = FindName(before.Released.Keys, quest) ?? quest;
            return new("retry", null, null, _ =>
            {
                StandingChoices.Write(path, config => config.WithReleased(key, session));
                return Task.FromResult<object?>(null);
            }, _ => null, _ => Released(session));
        }

        var at = IndexOf(args, "--at");
        if (at != -1)
        {
            var mark = args.ElementAtOrDefault(at + 1) is { } given ? Number(given) : double.NaN;
            if (!IsWhole(mark) || mark < 0 || mark > int.MaxValue) throw new ChoiceWords("`driver retry --at` needs a whole number of failures to count from.");
            var count = (int)mark;
            return new("retry", null, null, _ =>
            {
                StandingChoices.Write(path, config => config.WithForgiven(quest, count));
                return Task.FromResult<object?>(null);
            }, _ => null, _ => Marked(count, false, before.Strikes));
        }

        // While Daoris runs, the quest's *Try again* (RETRY_QUEST): the instance judges from its planner's verdict and answers what it
        // did. Alone, the same operation judges from a fresh verdict over this machine's service.
        return new("retry", ChoiceRoutes.Retry, new JsonObject { ["quest"] = quest }, async ct =>
        {
            IReadOnlyList<Consideration> verdicts;
            try
            {
                verdicts = await readVerdicts(DriverConfig.Load(path), ct).ConfigureAwait(false);
            }
            catch (Exception unread) when (unread is HttpRequestException or TaskCanceledException or DriverException or InvalidOperationException
                                               && unread is not DriverConfigUnreadableException && !ct.IsCancellationRequested)
            {
                var mark = before.ForgivenAt(quest);
                throw new ChoiceWords($"cannot count the failures of `#{quest}`: this machine's session records could not be read — "
                    + $"{unread.Message.TrimEnd('.')}.\n  Nothing was written. Give the count yourself: `daoris driver retry {quest} --at <n>`, with n the "
                    + $"failures its *Sitting* names{(mark > 0 ? $" plus {mark}, the mark its last retry left" : "")}.\n"
                    + "  The quest's page's *Try again* counts them for you.");
            }

            var decision = QuestRetry.Decide(quest, verdicts);
            var now = StandingChoices.Write(path, decision.Change);
            return new Retried(decision.Did, decision.Session, now.ForgivenAt(quest), now.Strikes);
        }, answer =>
        {
            var retried = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("retried", out var said) && said.ValueKind == JsonValueKind.Object
                ? said
                : default;
            var did = retried.ValueKind == JsonValueKind.Object ? Json.Text(retried, "did") : null;
            var session = retried.ValueKind == JsonValueKind.Object ? Json.Text(retried, "session") : null;
            var mark = 0;
            if (answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("forgiven", out var forgiven) && forgiven.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in forgiven.EnumerateObject())
                {
                    if (string.Equals(entry.Name, quest, StringComparison.OrdinalIgnoreCase) && entry.Value.ValueKind == JsonValueKind.Number)
                    {
                        mark = entry.Value.GetInt32();
                        break;
                    }
                }
            }

            var strikes = answer.ValueKind == JsonValueKind.Object && Json.Number(answer, "strikes") is { } counted ? (int)counted : before.Strikes;
            return new Retried(did ?? RetryDecision.Marked, session, mark, strikes);
        }, fields => fields is Retried { Did: RetryDecision.Released, Session: { } session } ? Released(session)
            : fields is Retried marked ? Marked(marked.Mark, true, marked.Strikes) : Marked(0, true, before.Strikes));
    }

    /// <summary>What *Try again* did, as the words read it: from the operation alone, or from the route's answer.</summary>
    private sealed record Retried(string Did, string? Session, int Mark, int Strikes);
}
