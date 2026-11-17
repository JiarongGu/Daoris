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
    /// not: it is no go-ahead a role this holder holds may answer (answered since, asked of no live session, a sign-in, a high-risk or marked
    /// place, or no held role is granted the act).
    /// </summary>
    public static Answer? Of(IReadOnlyList<JsonElement> waiting, string ask, int number, bool approved, string? words, out string? refusal)
    {
        refusal = null;
        var named = ask.Trim().TrimStart('#');
        foreach (var listed in waiting)
        {
            if (SessionsSuperviseCommand.SubjectOf(listed) is not { Kind: SupervisorKinds.GoAhead, GoAheadParts: { } parts } subject) continue;
            if (!string.Equals(parts.Ask.TrimStart('#'), named, StringComparison.Ordinal) || parts.Number != number) continue;
            var generation = listed.TryGetProperty("generation", out var held) && held.ValueKind == JsonValueKind.String ? held.GetString() : null;
            if (generation is not { Length: > 0 }) continue;
            var said = string.IsNullOrWhiteSpace(words) ? "answered at the terminal, with no words" : words.Trim();
            return new Answer(subject.Session, new SupervisorDraft(
                SupervisorKinds.GoAhead, subject.Id, generation, SupervisorKinds.Answer, approved ? SupervisorDoorActs.Approved : SupervisorDoorActs.Refused,
                [new SupervisorReason(said, Source)]));
        }

        refusal = $"go-ahead {number} on ask `#{named}` waits for no role this terminal holds: it is answered, asked of no live session, "
            + "a sign-in, at a high-risk or marked place, or no role it holds is granted `goahead.answer`; it stays the owner's. Nothing was kept.";
        return null;
    }

    /// <summary>
    /// The verb in a role's terminal: the go-ahead found among what waits for the holder and answered as the role's decision, or refused with
    /// why. Exit 0 kept, 1 refused, 2 where no Daoris answers (a role answers only through it).
    /// </summary>
    public static async Task<int> RunAsync(
        ISupervisorWorld world, string ask, int number, bool approved, string? words, TextWriter output, CancellationToken ct = default)
    {
        if (world.Instance is not { } instance || await SessionsSuperviseCommand.WaitingAsync(instance, ct).ConfigureAwait(false) is not { } waiting)
        {
            await output.WriteLineAsync($"ask go-ahead: Daoris is not running here, and a role answers only through it ({SessionsSuperviseCommand.NoInstance}).")
                .ConfigureAwait(false);
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
