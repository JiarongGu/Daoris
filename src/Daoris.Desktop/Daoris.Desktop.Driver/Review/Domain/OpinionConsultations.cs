namespace Daoris.Driver;

/// <summary>A second opinion's consultation record (<see cref="Consultation"/>), assembled with no model and no I/O.</summary>
public static class OpinionConsultations
{
    /// <summary>
    /// A second opinion's consultation, from the host's view of it and the working session's answers: one giver in <c>review</c>
    /// mode, since it reads a candidate and its evidence, with the reviewer's maker and product, what it read, and whether it saw another
    /// answer (a recheck reads the first pass's findings). Its label decides what did not hold: one of the work's own families is
    /// <c>same-maker</c>, said as not independent, and an undeclared maker is <c>maker-not-declared</c>. The decider's line is the
    /// working session's answers: how many findings changed the work. None before it answered.
    /// </summary>
    public static Consultation Of(OpinionView view, OpinionReading? answered)
    {
        string[] notHeld =
        [
            .. view.Label switch
            {
                ReviewerLabels.SameAgent => [Consultations.SameMaker],
                ReviewerLabels.MakerNotDeclared => [Consultations.MakerNotDeclared],
                _ => Array.Empty<string>(),
            },
            .. view.Rechecks is null ? Array.Empty<string>() : [Consultations.SawAnother],
        ];
        var giver = new ConsultationGiver(
            view.Pass, Consultations.ReviewMode, Consultations.ReviewMode, view.Maker ?? "", view.Product ?? "", null,
            view.Read ?? $"{view.Base}..{view.Tip}", view.Rechecks is not null, notHeld);
        var changed = answered is null
            ? null
            : $"{answered.Findings.Count(finding => finding.Counts == OpinionViews.Fixed)} of {answered.Findings.Count} findings changed the work";
        return new Consultation("opinion", view.Id, [giver], changed);
    }
}
