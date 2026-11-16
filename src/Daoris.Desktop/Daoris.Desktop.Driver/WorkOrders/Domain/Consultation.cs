using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One giver of a consultation (D308): its pass and role, its maker, adapter and account, its <c>Mode</c> (<c>independent</c>: from
/// one neutral brief with no sight of another answer; <c>review</c>: reads candidates), the digest of what it read, whether it saw
/// another answer, and which of independence's conditions did not hold.
/// </summary>
/// <param name="NotHeld">Each condition that did not hold, by code: <c>same-maker</c> (another giver of its mode shares its maker),
/// <c>saw-another</c>; empty where all held, and for a review, whose sight of the candidates is its job.</param>
public sealed record ConsultationGiver(
    string Pass, string Role, string Mode, string Maker, string Adapter, string? Account, string Read, bool SawAnother, IReadOnlyList<string> NotHeld);

/// <summary>
/// A consultation as its record stands: what was consulted (<c>Kind</c> and <c>Subject</c>), each giver, and the decider's one line
/// on whether it changed the decision, null until the decider says it. Independence is recorded, never assumed.
/// </summary>
public sealed record Consultation(string Kind, string Subject, IReadOnlyList<ConsultationGiver> Givers, string? Changed = null)
{
    /// <summary>Whether two or more independent givers of different makers answered: the views the record may count as two.</summary>
    public bool Independent =>
        Givers.Where(giver => giver.Mode == Consultations.IndependentMode).Select(giver => giver.Maker).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 2;
}

/// <summary>A brainstorm's consultation record, <c>consultation.json</c> beside its document, assembled with no model and no I/O.</summary>
public static class Consultations
{
    public const int Version = 1;
    public const string IndependentMode = "independent";
    public const string ReviewMode = "review";
    public const string SameMaker = "same-maker";

    /// <summary>
    /// A brainstorm's or design pass's consultation, from its document: each generator that answered is an independent giver that
    /// read the frozen packet alone; the synthesis, or the design pass's comparison, a review that read the packet and the
    /// proposals. Two answered generators of one maker each say <c>same-maker</c>, and the record counts no independence.
    /// </summary>
    public static Consultation Of(BrainstormDocument document, string kind)
    {
        var answered = document.Passes.Where(pass => pass.State == BrainstormPassRecord.Answered).ToList();
        var makers = answered.Where(pass => pass.Role == BrainstormPassRecord.Generate)
            .GroupBy(pass => pass.By.Maker, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var givers = answered.Select(pass => pass.Role == BrainstormPassRecord.Generate
            ? new ConsultationGiver(pass.By.Pass, pass.Role, IndependentMode, pass.By.Maker, pass.By.Adapter, pass.By.Account, document.Packet, false,
                makers[pass.By.Maker] > 1 ? [SameMaker] : [])
            : new ConsultationGiver(pass.By.Pass, pass.Role, ReviewMode, pass.By.Maker, pass.By.Adapter, pass.By.Account, document.Packet, true, []))
            .ToList();
        return new Consultation(kind, document.Id, givers);
    }

    /// <summary>
    /// A second opinion's consultation (D308), from the host's view of it and the working session's answers: one giver in <c>review</c>
    /// mode, since it reads a candidate and its evidence, with the reviewer's maker and product, what it read, and whether it saw another
    /// answer (a recheck reads the first pass's findings). Its label decides what did not hold: one of the work's own families is
    /// <c>same-maker</c>, said as not independent, and an undeclared maker is <c>maker-not-declared</c>. The decider's line is the
    /// working session's answers: how many findings changed the work. None before it answered.
    /// </summary>
    public static Consultation OfOpinion(OpinionView view, OpinionReading? answered)
    {
        string[] notHeld =
        [
            .. view.Label switch
            {
                ReviewerLabels.SameAgent => [SameMaker],
                ReviewerLabels.MakerNotDeclared => [MakerNotDeclared],
                _ => Array.Empty<string>(),
            },
            .. view.Rechecks is null ? Array.Empty<string>() : [SawAnother],
        ];
        var giver = new ConsultationGiver(
            view.Pass, ReviewMode, ReviewMode, view.Maker ?? "", view.Product ?? "", null, view.Read ?? $"{view.Base}..{view.Tip}",
            view.Rechecks is not null, notHeld);
        var changed = answered is null
            ? null
            : $"{answered.Findings.Count(finding => finding.Counts == OpinionViews.Fixed)} of {answered.Findings.Count} findings changed the work";
        return new Consultation("opinion", view.Id, [giver], changed);
    }

    public const string MakerNotDeclared = ReviewerLabels.MakerNotDeclared;
    public const string SawAnother = "saw-another";

    /// <summary>The record's bytes: <c>consultation: 1</c>, then its fields, LF only.</summary>
    public static byte[] Json(Consultation consultation)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("consultation", Version);
            writer.WriteString("kind", consultation.Kind);
            writer.WriteString("subject", consultation.Subject);
            writer.WriteBoolean("independent", consultation.Independent);
            writer.WriteStartArray("givers");
            foreach (var giver in consultation.Givers)
            {
                writer.WriteStartObject();
                writer.WriteString("pass", giver.Pass);
                writer.WriteString("role", giver.Role);
                writer.WriteString("mode", giver.Mode);
                writer.WriteString("maker", giver.Maker);
                // The door it came through, as a brainstorm's members say it: the old seam's word is never a new record's (D232).
                writer.WriteString("door", giver.Adapter);
                if (giver.Account is null) writer.WriteNull("account");
                else writer.WriteString("account", giver.Account);
                writer.WriteString("read", giver.Read);
                writer.WriteBoolean("sawAnother", giver.SawAnother);
                writer.WriteStartArray("notHeld");
                foreach (var code in giver.NotHeld) writer.WriteStringValue(code);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (consultation.Changed is null) writer.WriteNull("changed");
            else writer.WriteString("changed", consultation.Changed);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
    }
}
