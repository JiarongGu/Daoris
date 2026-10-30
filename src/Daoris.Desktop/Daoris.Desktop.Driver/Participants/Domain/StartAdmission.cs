namespace Daoris.Driver;

/// <summary>
/// The start layer's table (FOUNDATION1q, D212; the foundation design §2.3): may this work start, for this asker, keyed by the work. The
/// one answer to <i>may this session ask for a co-worker?</i>, which the driver's judges and the person's doors ask (survey §1.3 row 6);
/// the service's desk and connector keep their own guard, held to the same table (<c>fixtures/helper-askers.json</c>). Whether a record is
/// held on this machine is each door's own question, asked before this.
/// </summary>
public static class StartAdmission
{
    /// <summary>The asker serves another session: a helper, or a partner.</summary>
    public const string Helper = "helper";

    /// <summary>The asker reads another session's work for a second opinion.</summary>
    public const string Reviewer = "reviewer";

    /// <summary>The asker is an ask's intake, which does no work in a repository.</summary>
    public const string Intake = "intake";

    /// <summary>The asker is Ask Daoris's conversation (D203 point 6; the design's §7 row 9).</summary>
    public const string AskDaoris = "ask-daoris";

    /// <summary>Whether a session of <paramref name="work"/> may ask for a co-worker: a working session's alone, driven or a conversation.</summary>
    public static bool AsksCoWorker(string? work) => work is ParticipantWork.Build or ParticipantWork.Design or ParticipantWork.Chat;

    /// <summary>
    /// Why a session may not ask for a helper, or null where its work may. The refusal names the mark the doors named it by before,
    /// <c>serves</c>, then <c>opinion</c>, then the work.
    /// </summary>
    /// <remarks>
    /// An empty <c>serves</c> or <c>opinion</c> is no mark to the work's rule, and every door refused it before: it stays refused, so no
    /// door admits more than it did.
    /// </remarks>
    public static string? HelperAsker(WorkMarks marks)
    {
        var work = ParticipantWork.Of(marks with { Mirror = false });
        if (AsksCoWorker(work) && marks.Serves is null && marks.Opinion is null) return null;
        return marks.Serves is not null ? Helper : marks.Opinion is not null ? Reviewer : work == ParticipantWork.Intake ? Intake : AskDaoris;
    }

    /// <summary>
    /// Whether a session may lead a partner's piece: its work asks for a co-worker, it works in a tree of its own, and it carries no mark
    /// a door refused before (an empty <c>serves</c> or <c>opinion</c>, or Ask Daoris's mode).
    /// </summary>
    public static bool LeadsPiece(WorkMarks marks, string? helpMode, string? tree) =>
        HelperAsker(marks) is null && helpMode is not { Length: > 0 } && tree is { Length: > 0 };
}
