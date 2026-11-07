using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>The performer's rules for its tries: pure, so the tables hold them without a process.</summary>
public static class LandTries
{
    /// <summary>
    /// Whether a run's work is worth a try now: its first, and a hold's at every pass; a refusal only once its tree's tip or status moved
    /// since it, so a pass never repeats one that nothing changed; and a try that closed, only at a tip other than the one it was kept at.
    /// </summary>
    public static bool ShouldTry(LandEntry entry, string? tip, string status) => entry.Last switch
    {
        null => true,
        { Code: var code } last when LandingCode.OnChange(code) =>
            !string.Equals(last.Tip, tip, StringComparison.Ordinal) || !string.Equals(last.Status, status, StringComparison.Ordinal),
        // A hold, a review and a second opinion wait on the person or another agent, not on the tree, so each is read again at
        // every look.
        { Code: LandingCode.Held or LandingCode.Unreviewed or LandingCode.Opinion } => true,
        // A `land` is started once a node and tip, so a try that closed at its tip says nothing of a later one.
        { Tip: { } at } when !string.Equals(at, tip, StringComparison.Ordinal) => true,
        _ => false,
    };

    public static bool Landed(LandEntry entry, string? tip) =>
        entry.Last is { Code: LandingCode.Landed or LandingCode.Advanced, Tip: { } at } && string.Equals(at, tip, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A tree's <c>git status --porcelain</c> as a fact a try is kept with: <c>clean</c>, or a fingerprint of what git listed,
    /// so a later look can tell whether it moved without keeping a path.
    /// </summary>
    public static string Fingerprint(string porcelain)
    {
        var lines = porcelain.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()).Where(line => line.Length > 0).ToList();
        if (lines.Count == 0) return "clean";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)));
        return "sha256:" + Convert.ToHexStringLower(hash)[..16];
    }

    /// <summary>
    /// What a landing came to, by its code (design §8). The push is the plugin's whether the branch was made or moved on, so a
    /// plugin that could not or did not push is kept as that first (LAND2c).
    /// </summary>
    public static string CodeOf(TreeLanding landed)
    {
        if (!landed.Landed) return landed.Refusal ?? LandingCode.Refused;
        return landed.AdvancedFrom is not null ? LandingCode.Advanced : LandingCode.Landed;
    }
}

/// <summary>
/// What the conversation's record keeps of each try it is told of (LAND2b, design §2, D100): a note whose lead-in is a code
/// (LANG1a), so the page words it in the reader's language, and the landing's own sentence beneath it as a part of its own.
/// </summary>
public static class LandTryNotes
{
    /// <summary>Whether a try with this code is said in the conversation; the closings without a landing stay the due list's and the log's.</summary>
    public static bool Says(string code) => code is LandingCode.Landed or LandingCode.Advanced or LandingCode.Nothing
        or LandingCode.Held or LandingCode.Uncommitted or LandingCode.Exists or LandingCode.Completed or LandingCode.Refused
        or LandingCode.Unreviewed or LandingCode.Opinion or LandingCode.CarriedOn or LandingCode.CarryBound;

    /// <summary>
    /// The note for one try: its lead-in by its code, then the landing's sentence where there was a landing to say it.
    /// </summary>
    /// <param name="landed">What the landing said, or null where the try never reached one (a hold, uncommitted work, no commits).</param>
    /// <param name="uncommitted">How many paths were uncommitted, where that held it.</param>
    /// <param name="branch">The branch the pattern names, where a refusal names one and the landing did not.</param>
    /// <param name="rebaseTold">
    /// A refusal whose replay onto the line conflicted, and whose session Daoris told to rebase in its own tree (LANDREBASE1): its own
    /// lead-in, <c>landing.rebase</c>. A refusal it could not tell keeps <c>landing.refused</c>.
    /// </param>
    /// <param name="carry">
    /// The person's Accept the try lands or carries on (LANDBACK1 part 1, <see cref="CarriedAccepts"/>): <see cref="LandCarry.Uncommitted"/> or
    /// <see cref="LandCarry.Conflicted"/> for a try that carried its session on, and anything else for a landing that the kept yes
    /// accepted, which says the person's press accepted it and not the rule's switch.
    /// </param>
    public static SessionEvent Of(
        string code, TreeLanding? landed, int? uncommitted = null, string? branch = null, bool rebaseTold = false,
        string? carry = null)
    {
        var named = landed?.Branch ?? branch ?? "";
        var lead = code switch
        {
            LandingCode.CarriedOn when carry == LandCarry.Conflicted => Noted.Of(ChatNoteCodes.LandingCarriedConflict,
                "accepted: replaying its work onto the line conflicts, so its session was carried on to rebase in its own tree, as follows. "
                + "It lands when its tree is clean."),
            LandingCode.CarriedOn => Noted.Of(ChatNoteCodes.LandingCarriedDirty,
                $"accepted: its tree has {uncommitted ?? 0} uncommitted path(s), so its session was carried on to commit them by its repository's "
                + "conventions, or restore the ones it does not want and say which. It lands when its tree is clean.",
                ("paths", uncommitted ?? 0)),
            LandingCode.CarryBound => Noted.Of(ChatNoteCodes.LandingCarryBound,
                $"accepted, but its session was carried on {LandCarry.Bound} times and its tree is still not clean or its work still conflicts, "
                + "so nothing was landed. It waits for your review: Accept again once it has committed, or decide the rest is not wanted."),
            // The person's yes, kept on it while its session cleaned the tree, landed it: said as theirs (LANDBACK1 part 1).
            LandingCode.Landed or LandingCode.Advanced when carry is not null => Noted.Of(ChatNoteCodes.LandingAcceptedCarried,
                $"accepted by your press once its tree was clean: its work is on `{named}`.", ("branch", named)),
            // An advance is an acceptance too (LAND2c): the work is on the chain's branch, and the sentence beneath says it moved.
            LandingCode.Landed or LandingCode.Advanced => Noted.Of(ChatNoteCodes.LandingAccepted,
                $"{LandingNotes.AutoAccepted} its work is on `{named}`.", ("branch", named)),
            LandingCode.Nothing => Noted.Of(ChatNoteCodes.LandingNothing, "its quest was done and its work made no commits, so nothing was landed."),
            LandingCode.Held => Noted.Of(ChatNoteCodes.LandingHeld,
                "its quest is done and held for your yes, so its work lands at the look after you release it."),
            LandingCode.Uncommitted => Noted.Of(ChatNoteCodes.LandingUncommitted,
                $"not accepted automatically: its tree has {uncommitted ?? 0} uncommitted path(s), which a branch would leave behind. "
                + "Commit them in the tree or tell the session, then Accept lands it.",
                ("paths", uncommitted ?? 0)),
            LandingCode.Exists => Noted.Of(ChatNoteCodes.LandingExists,
                $"not accepted automatically: `{named}` is already a branch, and Daoris does not move it. It waits for your review.",
                ("branch", named)),
            // The review's gate held it (REVIEWENV1c): its own code, the gate's sentence beneath (REVIEWENV1c2).
            LandingCode.Unreviewed => Noted.Of(ChatNoteCodes.LandingUnreviewed,
                "not accepted automatically: its work waits for your review before it lands, as follows. It lands at the first look "
                + "after you review it or skip the review."),
            // The second opinion's gate held it (XAGENT1f): its own code since the page words it (XAGENT1g), as the review's wait
            // has since REVIEWENV1c2; the gate's sentence beneath says what waits.
            LandingCode.Opinion => Noted.Of(ChatNoteCodes.LandingOpinion,
                "not accepted automatically: its work waits for a second opinion before it lands, as follows. It lands at the first "
                + "look once the opinion is settled, or you go on without one."),
            LandingCode.Refused when rebaseTold => Noted.Of(ChatNoteCodes.LandingRebase,
                "not accepted automatically: replaying its work onto the line conflicts, so its session was asked to rebase in its own tree, "
                + "as follows. It lands at the first look after its tree changes."),
            _ => Noted.Of(ChatNoteCodes.LandingRefused, "not accepted automatically: the landing was refused, as follows. It waits for your review."),
        };

        // The landing's own sentence beneath, as written: it carries git's and the plugin's words beside Daoris's.
        var said = landed is { Message.Length: > 0 } ? lead.Then(" ", Noted.Said(landed.Message, NoteBy.Program)) : lead;
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }
}

/// <summary>The catalogue of <see cref="LandingLine"/> lines, which the owner of what they say keeps: each builder writes the fields its line gives it and nothing else.</summary>
public static class LandingLines
{
    extension(LandingLine)
    {
        /// <summary>One try: whose, where, its code, and what it carried or what held it.</summary>
        public static LandingLine Auto(
            string session, string repository, string workspace, string code, int? commits, int? uncommitted) =>
            new("landing.auto",
            [
                ("session", session), ("repository", repository), ("workspace", workspace), ("code", code), ("commits", commits),
                ("uncommitted", uncommitted),
            ]);
    }
}
