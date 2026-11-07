using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>SUPERVISOR_DECIDE</c>'s exact form: <c>{ kind, subject, generation, proof, do, choice?, reasons }</c>, every field named once
/// (ignoring case, as the bridge reads names), of its type and bound, and nothing else. <c>subject</c> is the kind's typed reference,
/// <c>do</c> one of the kind's acts unqualified, and <c>choice</c> present exactly where the act chooses an offered label (a question's
/// answer); <c>reasons</c> 1 to 5, each exactly <c>{ says: 1-300, source: 1-200 }</c>.
/// </summary>
public sealed record SupervisorDecideForm(
    SupervisorSubject Subject, string Generation, string Proof, string Do, string? Choice, IReadOnlyList<SupervisorReason> Reasons) : ISupervisorAct
{
    public string Kind => Subject.Kind;

    public byte[] Canonical => SupervisorProof.Canonical(SupervisorRoutes.Decide, Subject, Generation, Do, Choice, Reasons);

    /// <summary>Whether <paramref name="act"/> of <paramref name="kind"/> names one of the subject's offered labels.</summary>
    public static bool Chooses(string kind, string act) => kind == SupervisorKinds.Question && act == SupervisorKinds.Answer;

    /// <summary>The form <paramref name="payload"/> is, or null where it is anything more or less.</summary>
    public static SupervisorDecideForm? Read(JsonElement? payload)
    {
        if (SupervisorActs.Fields(payload, "do", 40, "choice") is not { } read) return null;
        if (!SupervisorKinds.ActsOf(read.Subject.Kind).Contains(SupervisorKinds.Qualified(read.Subject.Kind, read.Chosen), StringComparer.Ordinal)) return null;
        return Chooses(read.Subject.Kind, read.Chosen) == (read.Choice is not null)
            ? new SupervisorDecideForm(read.Subject, read.Generation, read.Proof, read.Chosen, read.Choice, read.Reasons)
            : null;
    }

    /// <summary>The form with the proof the holder's key makes over this connection's <paramref name="nonce"/>, or none where either is not one.</summary>
    public SupervisorDecideForm Proved(string key, string nonce) => this with { Proof = SupervisorProof.Prove(key, nonce, Canonical) ?? "" };

    /// <summary>The form as the verb sends it: its fields in the form's order, <c>choice</c> only where the act takes one.</summary>
    public object Payload() => Choice is null
        ? new { kind = Kind, subject = Subject.Wire(), generation = Generation, proof = Proof, @do = Do, reasons = SupervisorActs.Wire(Reasons) }
        : new { kind = Kind, subject = Subject.Wire(), generation = Generation, proof = Proof, @do = Do, choice = Choice, reasons = SupervisorActs.Wire(Reasons) };
}

/// <summary>
/// <c>SUPERVISOR_LEAVE</c>'s exact form: <c>{ kind, subject, generation, proof, bound, reasons }</c>, held as the decide's is, <c>bound</c>
/// one of <see cref="SupervisorBounds.All"/>. Its proof is over its own route's canonical bytes, so no decide's proof is a leave's.
/// </summary>
public sealed record SupervisorLeaveForm(
    SupervisorSubject Subject, string Generation, string Proof, string Bound, IReadOnlyList<SupervisorReason> Reasons) : ISupervisorAct
{
    public string Kind => Subject.Kind;

    public byte[] Canonical => SupervisorProof.Canonical(SupervisorRoutes.Leave, Subject, Generation, Bound, null, Reasons);

    /// <summary>The form <paramref name="payload"/> is, or null where it is anything more or less, or names a bound the list does not.</summary>
    public static SupervisorLeaveForm? Read(JsonElement? payload) =>
        SupervisorActs.Fields(payload, "bound", 40, choice: null) is { Choice: null } read && SupervisorBounds.Known(read.Chosen)
            ? new SupervisorLeaveForm(read.Subject, read.Generation, read.Proof, read.Chosen, read.Reasons)
            : null;

    public SupervisorLeaveForm Proved(string key, string nonce) => this with { Proof = SupervisorProof.Prove(key, nonce, Canonical) ?? "" };

    public object Payload() =>
        new { kind = Kind, subject = Subject.Wire(), generation = Generation, proof = Proof, bound = Bound, reasons = SupervisorActs.Wire(Reasons) };
}

/// <summary>What the judge reads of either role's act: the generation it names, its proof, and the bytes the proof is over.</summary>
public interface ISupervisorAct
{
    string Kind { get; }
    SupervisorSubject Subject { get; }
    string Generation { get; }
    string Proof { get; }
    byte[] Canonical { get; }
}

/// <summary>The decision door's two forms read alike: every field named once, of its type and bound, and nothing else.</summary>
public static class SupervisorActs
{
    /// <summary>The act <paramref name="route"/> names, read from <paramref name="payload"/>; null for any other route or form.</summary>
    public static ISupervisorAct? Read(string route, JsonElement? payload) => route switch
    {
        SupervisorRoutes.Decide => SupervisorDecideForm.Read(payload),
        SupervisorRoutes.Leave => SupervisorLeaveForm.Read(payload),
        _ => null,
    };

    internal sealed record ActFields(SupervisorSubject Subject, string Generation, string Proof, string Chosen, string? Choice, IReadOnlyList<SupervisorReason> Reasons);

    internal static object[] Wire(IReadOnlyList<SupervisorReason> reasons) => [.. reasons.Select(reason => new { says = reason.Says, source = reason.Source })];

    /// <summary>
    /// The fields both forms share, <paramref name="chosen"/> the act's (<c>do</c> or <c>bound</c>) up to <paramref name="most"/>, and the
    /// optional <paramref name="choice"/> where the form may carry one; the subject read by its kind.
    /// </summary>
    internal static ActFields? Fields(JsonElement? payload, string chosen, int most, string? choice)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in body.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value)) return null;
        }

        string[] names = ["kind", "subject", "generation", "proof", chosen, "reasons"];
        var chose = choice is not null && fields.ContainsKey(choice);
        if (fields.Count != names.Length + (chose ? 1 : 0) || !names.All(fields.ContainsKey)) return null;
        if (Text(fields["kind"], 40) is not { } kind || SupervisorSubject.Read(kind, fields["subject"]) is not { } subject
            || Text(fields["generation"], 128) is not { } generation || Text(fields["proof"], 64) is not { } proof
            || Text(fields[chosen], most) is not { } act || Reasons(fields["reasons"]) is not { } reasons)
        {
            return null;
        }

        var label = chose ? Text(fields[choice!], 80) : null;
        return chose && label is null ? null : new ActFields(subject, generation, proof, act, label, reasons);
    }

    private static List<SupervisorReason>? Reasons(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return null;
        var reasons = new List<SupervisorReason>();
        foreach (var reason in value.EnumerateArray())
        {
            if (reason.ValueKind != JsonValueKind.Object) return null;
            var parts = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in reason.EnumerateObject())
            {
                if (!parts.TryAdd(property.Name, property.Value)) return null;
            }

            if (parts.Count != 2 || !parts.TryGetValue("says", out var says) || !parts.TryGetValue("source", out var source)) return null;
            if (Text(says, QuestionSupervisorDecisions.MaxSays) is not { } said || Text(source, QuestionSupervisorDecisions.MaxSource) is not { } where)
            {
                return null;
            }

            reasons.Add(new SupervisorReason(said, where));
        }

        return reasons.Count is 0 or > QuestionSupervisorDecisions.MostReasons ? null : reasons;
    }

    // A string of 1 to max characters, not blank. Its words are kept as written: the reasons are the owner's to read.
    private static string? Text(JsonElement value, int max) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text.Length <= max
        && text.Trim().Length > 0 ? text : null;
}
