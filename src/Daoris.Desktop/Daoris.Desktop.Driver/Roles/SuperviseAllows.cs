using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The verb's reading of the allow kind's three shapes and the files kind's held act: the id the verb is given matches the subject's
/// session (a held act's repository) where it has one, else the subject's own id.
/// </summary>
internal static class SuperviseAllows
{
    public static bool Reads(SupervisorSubject subject) => subject.Kind is SupervisorKinds.Allow or SupervisorKinds.Files;

    /// <summary>Whether <paramref name="id"/>, as the verb is given it, names <paramref name="subject"/>.</summary>
    public static bool Names(SupervisorSubject subject, string id) =>
        subject.Kind == SupervisorKinds.Allow && subject.Session.Length == 0 ? subject.Id == id : subject.Session == id;

    /// <summary>The first column of a listed subject: its session, or a dash where it names none.</summary>
    public static string Who(SupervisorSubject subject) => subject.Session.Length == 0 ? "-" : subject.Session;

    /// <summary>What a listed allow says it is, in Daoris's own words: nothing the asking agent wrote.</summary>
    public static string Says(JsonElement listed, SupervisorSubject subject) => subject switch
    {
        { Kind: SupervisorKinds.Files } => $"allow {subject.Act?.What} {subject.Id} in {subject.Session}, asked at start {subject.Act?.Start}",
        { IsAsk: true } => $"allow the steward's {SupervisePointPrint.Text(listed, "act")} {subject.Id}",
        { IsPress: true } => $"allow the working session's press {SupervisePointPrint.Text(listed, "act")} {subject.Id}",
        { IsProposal: true } => $"allow widening {subject.Id}",
        _ => $"allow permission request {subject.Id}",
    };

    /// <summary>The listed allow or held act whose subject <paramref name="id"/>, <paramref name="subjectId"/> and <paramref name="kind"/> name; null where none waits.</summary>
    public static async Task<SupervisorSubject?> FindAsync(InstanceWorld instance, string id, string subjectId, string kind, CancellationToken ct) =>
        await SessionsSuperviseCommand.WaitingAsync(instance, ct).ConfigureAwait(false) is { } waiting
            ? waiting.Select(SessionsSuperviseCommand.SubjectOf)
                .FirstOrDefault(each => each is not null && each.Kind == kind && each.Id == subjectId && Names(each, id))
            : null;

    /// <summary>The print of a waiting allow or held act: where it is placed, its kind's facts fenced, the line's course, and how to decide it.</summary>
    public static async Task<int> PrintAsync(ISupervisorWorld world, JsonElement listed, SupervisorSubject subject, string id, TextWriter output, CancellationToken ct)
    {
        var generation = SupervisePointPrint.Text(listed, "generation");
        var print = new StringBuilder();
        print.Append(SessionsSuperviseCommand.Header).Append('\n');
        print.Append($"{Says(listed, subject)}\nplace: {(SupervisePointPrint.Text(listed, "repository") is { Length: > 0 } place ? place : "the machine")}\n");
        print.Append(await FactsAsync(world, listed, ct).ConfigureAwait(false));
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

    private static async Task<string> FactsAsync(ISupervisorWorld world, JsonElement listed, CancellationToken ct)
    {
        if (!listed.TryGetProperty("facts", out var facts) || facts.ValueKind != JsonValueKind.Object) return "";
        var parts = facts.TryGetProperty("parts", out var each) && each.ValueKind == JsonValueKind.Array
            ? each.EnumerateArray().Select(part => (Label: SupervisePointPrint.Text(part, "label") ?? "", Text: SupervisePointPrint.Text(part, "text") ?? "")).ToList()
            : [];
        var fence = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3));
        return await SupervisorKindFacts.ComposeAsync(world.Service, world.Home, SupervisePointPrint.Text(facts, "session") ?? "", fence, parts, ct).ConfigureAwait(false);
    }
}
