using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>SUPERVISOR_COURSE</c>'s exact form: <c>{ id, point, generation,
/// proof, do, reasons }</c>, held as <c>SUPERVISOR_ANSWER</c>'s is, <c>do</c> one of <see cref="SupervisorCourseActs.All"/>. Its proof is
/// over its own route's canonical bytes, so no answer's or defer's proof is a course act's.
/// </summary>
public sealed record SupervisorCourseForm(
    string Id, string Point, string Generation, string Proof, string Do, IReadOnlyList<SupervisorReason> Reasons) : ISupervisorAct
{
    public byte[] Canonical => SupervisorProof.Canonical(SupervisorRoutes.Course, Id, Point, Generation, Do, Reasons);

    /// <summary>The form <paramref name="payload"/> is, or null where it is anything more or less, or names an act the course does not take.</summary>
    public static SupervisorCourseForm? Read(JsonElement? payload) =>
        SupervisorActs.Fields(payload, "do", 20, "point") is { } read && SupervisorCourseActs.All.Contains(read.Chosen, StringComparer.Ordinal)
            ? new SupervisorCourseForm(read.Id, read.Question, read.Generation, read.Proof, read.Chosen, read.Reasons)
            : null;

    public object Payload() => new
    {
        id = Id, point = Point, generation = Generation, proof = Proof, @do = Do,
        reasons = Reasons.Select(reason => new { says = reason.Says, source = reason.Source }).ToArray(),
    };
}
