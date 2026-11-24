using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris ask go-ahead</c> in a role's terminal: admitted by the role the terminal's holder holds, never by its being a terminal. The
/// go-ahead is looked up among what waits for the holder (<c>SUPERVISOR_WAITING</c>, which lists only a go-ahead a role granted
/// <c>goahead.answer</c> may answer at that place: <see cref="GoAheadAuthority"/>), and its answer is the role's decision, held for the owner's
/// overrule as <c>sessions supervise</c> keeps it; a go-ahead no listed role may answer is refused with why, and nothing is kept.
/// </summary>
public static class GoAheadRoleDoor
{
    /// <summary>What the answer is signed as in each reason's source.</summary>
    public const string Source = "daoris ask go-ahead";

    /// <summary>A go-ahead's answer as the role's decision: the session whose subject it is, and the draft that decides it.</summary>
    public sealed record Answer(string Session, SupervisorDraft Draft);

    /// <summary>
    /// The role's answer to go-ahead <paramref name="number"/> on <paramref name="ask"/> among what waits for the holder, or null with why
    /// not: it is no go-ahead a role this holder holds may answer (answered since, asked of no live session, a sign-in, an act the risk policy
    /// classes the owner's, or no held role is granted the act).
    /// </summary>
    public static Answer? Of(IReadOnlyList<JsonElement> waiting, string ask, int number, bool approved, string? words, out string? refusal)
    {
        refusal = null;
        var named = ask.Trim().TrimStart('#');
        var numbers = new List<int>();
        string? otherCase = null, question = null;
        foreach (var listed in waiting)
        {
            if (SessionsSuperviseCommand.SubjectOf(listed) is not { GoAheadParts: { } parts } subject)
            {
                if (SessionsSuperviseCommand.SubjectOf(listed) is { Kind: SupervisorKinds.Question } asked && asked.Id.TrimStart('#').Split('/')[0] == named) question = named;
                continue;
            }

            if (subject.Kind != SupervisorKinds.GoAhead) continue;
            var listedAsk = parts.Ask.TrimStart('#');
            if (!string.Equals(listedAsk, named, StringComparison.Ordinal))
            {
                if (string.Equals(listedAsk, named, StringComparison.OrdinalIgnoreCase)) otherCase = listedAsk;
                continue;
            }

            if (parts.Number != number)
            {
                numbers.Add(parts.Number);
                continue;
            }

            var generation = listed.TryGetProperty("generation", out var held) && held.ValueKind == JsonValueKind.String ? held.GetString() : null;
            if (generation is not { Length: > 0 })
            {
                refusal = $"go-ahead {number} on ask `#{named}` waits for this holder but is listed with no generation to pin an answer to, so none can be kept: "
                    + "ask again in a moment. Nothing was kept.";
                return null;
            }

            var said = string.IsNullOrWhiteSpace(words) ? "answered at the terminal, with no words" : words.Trim();
            return new Answer(subject.Session, new SupervisorDraft(
                SupervisorKinds.GoAhead, subject.Id, generation, SupervisorKinds.Answer, approved ? SupervisorDoorActs.Approved : SupervisorDoorActs.Refused,
                [new SupervisorReason(said, Source)]));
        }

        refusal = numbers.Count > 0
            ? $"ask `#{named}` has go-ahead {string.Join(", ", numbers.Order())} waiting for this holder, not {number}. Nothing was kept."
            : otherCase is not null
            ? $"no ask `#{named}` waits for this holder; an ask is named as written, and `#{otherCase}` does. Nothing was kept."
            : question is not null
            ? $"`#{named}` waits for this holder as a question, not a go-ahead: it is answered through `sessions supervise`. Nothing was kept."
            : $"go-ahead {number} on ask `#{named}` is not among what waits for the roles this terminal holds: it is answered already, it is a sign-in, "
            + "the risk policy classes its act the owner's (a high-risk place, a production-releases mark or a row), no role this terminal holds is granted `goahead.answer`, or no such go-ahead exists; it stays the owner's. Nothing was kept.";
        return null;
    }

    /// <summary>Why no answer came to what waits for the holder: no Daoris answers here, or it is running and has not answered within <paramref name="within"/>.</summary>
    public static string NoAnswer(bool pending, TimeSpan within) =>
        pending
            ? $"Daoris is running here but has not yet read what waits for this holder (within {within.TotalSeconds:0} seconds): ask again in a moment."
            : $"Daoris is not running here, and a role answers only through it ({SessionsSuperviseCommand.NoInstance}).";

    /// <summary>
    /// The verb in a role's terminal: the go-ahead found among what waits for the holder and answered as the role's decision, or refused with
    /// why. Exit 0 kept, 1 refused, 2 where no Daoris answers (a role answers only through it).
    /// </summary>
    public static async Task<int> RunAsync(
        ISupervisorWorld world, string ask, int number, bool approved, string? words, TextWriter output, CancellationToken ct = default)
    {
        if (world.Instance is not { } instance)
        {
            await output.WriteLineAsync($"ask go-ahead: {NoAnswer(pending: false, TimeSpan.Zero)}").ConfigureAwait(false);
            return 2;
        }

        var (waiting, pending) = await SessionsSuperviseCommand.WaitingReadAsync(instance, ct).ConfigureAwait(false);
        if (waiting is null)
        {
            await output.WriteLineAsync($"ask go-ahead: {NoAnswer(pending, instance.Within)}").ConfigureAwait(false);
            return 2;
        }

        if (Of(waiting, ask, number, approved, words, out var refusal) is not { } answer)
        {
            await output.WriteLineAsync($"ask go-ahead: {refusal}").ConfigureAwait(false);
            return 1;
        }

        return await SessionsSuperviseCommand.AnswerAsync(world, instance, answer.Session, answer.Draft, output, ct).ConfigureAwait(false);
    }

    /// <summary>Who answered, as the role's door says it: the roles the holder holds and its holder, a terminal.</summary>
    public static string By(IReadOnlyList<string> roles) =>
        roles.Count == 0 ? "a role held by this terminal" : $"role {string.Join(", ", roles.Select(role => $"`{role}`"))}, held by this terminal";
}
