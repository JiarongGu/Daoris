using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The verb's reading of the allow kind's three shapes: a held request names a session and a request, a widening only a proposal and a
/// steward's act only an ask, so the id the verb is given matches the session where there is one and the subject's own id otherwise.
/// </summary>
internal static class SuperviseAllows
{
    /// <summary>Whether <paramref name="id"/>, as the verb is given it, names <paramref name="subject"/>.</summary>
    public static bool Names(SupervisorSubject subject, string id) =>
        subject.Kind == SupervisorKinds.Allow && subject.Session.Length == 0 ? subject.Id == id : subject.Session == id;

    /// <summary>The first column of a listed subject: its session, or a dash where it names none.</summary>
    public static string Who(SupervisorSubject subject) => subject.Session.Length == 0 ? "-" : subject.Session;

    /// <summary>What a listed allow says it is, in Daoris's own words: nothing the asking agent wrote.</summary>
    public static string Says(JsonElement listed, SupervisorSubject subject) => subject switch
    {
        { IsAsk: true } => $"allow the steward's {SupervisePointPrint.Text(listed, "act")} {subject.Id}",
        { IsProposal: true } => $"allow widening {subject.Id}",
        _ => $"allow permission request {subject.Id}",
    };

    /// <summary>The listed allow whose subject <paramref name="id"/> and <paramref name="subjectId"/> name; null where none waits.</summary>
    public static async Task<SupervisorSubject?> FindAsync(InstanceWorld instance, string id, string subjectId, CancellationToken ct) =>
        await SessionsSuperviseCommand.WaitingAsync(instance, ct).ConfigureAwait(false) is { } waiting
            ? waiting.Select(SessionsSuperviseCommand.SubjectOf)
                .FirstOrDefault(each => each is { Kind: SupervisorKinds.Allow } && each.Id == subjectId && Names(each, id))
            : null;

    /// <summary>The print of a waiting allow: where it is placed, the acts the holder may make, and how to decide it.</summary>
    public static async Task<int> PrintAsync(JsonElement listed, SupervisorSubject subject, string id, TextWriter output)
    {
        var generation = SupervisePointPrint.Text(listed, "generation");
        var print = new StringBuilder();
        print.Append(SessionsSuperviseCommand.Header).Append('\n');
        print.Append($"{Says(listed, subject)}\nplace: {(SupervisePointPrint.Text(listed, "repository") is { Length: > 0 } place ? place : "the machine")}\n");
        print.Append($"generation: {generation}\n");
        var acts = listed.TryGetProperty("acts", out var named) && named.ValueKind == JsonValueKind.Array
            ? string.Join("|", named.EnumerateArray().Select(each => each.GetString()))
            : "<act>";
        print.Append($"answer: daoris session supervise {id} --kind {subject.Kind} --subject {subject.Id} --generation {generation} --do {acts} "
            + "--reason \"…\" --source \"<where>\"\n");
        print.Append(SessionsSuperviseCommand.LeaveLine);
        await output.WriteAsync(print.ToString()).ConfigureAwait(false);
        return 0;
    }
}
