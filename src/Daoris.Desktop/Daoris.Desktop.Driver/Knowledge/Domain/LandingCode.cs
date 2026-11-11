namespace Daoris.Driver;

/// <summary>
/// What a try to land a due session came to: the codes its due entry keeps and the machine log writes.
/// Some codes name why an entry closed without a landing, so nothing closes unsaid.
/// </summary>
public static class LandingCode
{
    /// <summary>The branch is made and recorded, and the rule's plugin pushed it where it names one.</summary>
    public const string Landed = "landed";

    /// <summary>
    /// A later done moved the chain's branch on as a fast-forward: a chain's later step, or a session that
    /// went on after its landing. The rule's plugin pushed it where it names one.
    /// </summary>
    public const string Advanced = "advanced";

    /// <summary>The done made no commits, so there is nothing to land.</summary>
    public const string Nothing = "nothing";

    /// <summary>The quest is done and held: it lands at the look after the release.</summary>
    public const string Held = "held";

    /// <summary>The tree holds uncommitted work, which a branch would leave behind.</summary>
    public const string Uncommitted = "uncommitted";

    /// <summary>
    /// The pattern names a branch that stands and may not move on: one Daoris did not make, one moved since
    /// its landing, one checked out, or one the session's work does not grow from.
    /// </summary>
    public const string Exists = "exists";

    /// <summary>
    /// The chain's branch already reads on the line, so its pull request is merged, and an advance would push work no pull
    /// request carries. It waits: bringing the repository up to date and the person's Accept land it fresh.
    /// </summary>
    public const string Completed = "completed";

    /// <summary>Its work reached a branch of the person's another way: their press, or a push of their own.</summary>
    public const string Already = "already";

    /// <summary>A newer session went on in its tree, and stands for it now (<c>ReviewableTree</c>'s rule).</summary>
    public const string Superseded = "superseded";

    /// <summary>Its tree is discarded before it landed.</summary>
    public const string Gone = "gone";

    /// <summary>Its quest is no longer done: the person turned a departure down, or the service no longer holds it.</summary>
    public const string Undone = "undone";

    /// <summary>Its repository's rule no longer accepts automatically: the person's press is the way again.</summary>
    public const string Off = "off";

    /// <summary>The landing is refused for another reason, git's or the pattern's, in its own sentence.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// The work's head moved past the tip its <c>land</c> is judged at, so nothing lands what nobody accepted: tried again once the tree
    /// moves, and judged again at its new tip, as the return and the apply refuse a record that moved.
    /// </summary>
    public const string Moved = "moved";

    /// <summary>
    /// Its work waits for the person's review: the level says
    /// review, and no <i>reviewed</i> on a set-up that holds its tip, and no skip, lets it go yet. Kept once, read again at every
    /// look as a hold is, and landed at the first look after the review.
    /// </summary>
    public const string Unreviewed = "unreviewed";

    /// <summary>
    /// Its work waits for a second opinion: the level says one is read
    /// before it lands, and none is settled yet, or the chain's last step here is still to run. Kept once, read again at every
    /// look, and landed at the first look once settled. It comes before <see cref="Unreviewed"/>, as the gate's order does.
    /// </summary>
    public const string Opinion = "opinion";

    /// <summary>
    /// Its work is on the line, or on a branch of the person's, by content already, as the
    /// session's head reads it: a squash merge or a cherry-pick carried it, so landing it again would make a
    /// second copy. Refused at every door, and an entry it ends stays closed: the tree's discard is the way.
    /// </summary>
    public const string Carried = "carried";

    /// <summary>
    /// The person's Accept is pressed on a session whose tree holds uncommitted work or whose replay conflicts, and the session is
    /// carried on to clear it (<see cref="CarriedAccepts"/>): the yes stays on it, and its entry stays open and is
    /// judged again once the carry-on's turn ends, whatever its tree's tip and status then are.
    /// </summary>
    public const string CarriedOn = "carried-on";

    /// <summary>
    /// Carried on twice for one Accept (<see cref="LandCarry.Bound"/>), and its tree is still not clean or its work still conflicts: the
    /// yes is taken back, and the session waits in Review's ready group with the reason. Never read as landed.
    /// </summary>
    public const string CarryBound = "carry-bound";

    /// <summary>The repository's own checkout holds uncommitted work: tried again once its status differs, and no review waits.</summary>
    public const string RootDirty = "root-dirty";

    /// <summary>The line already holds every commit of its branch: nothing to merge, so the entry closes and nothing waits on the person.</summary>
    public const string LineHolds = "line-holds";

    /// <summary>The merge waits in its line's integration behind another, and is tried again at every look: no review waits.</summary>
    public const string Queued = "queued";

    /// <summary>Whether a try with this code closes its entry; the rest wait for a change, a release or the person's press.</summary>
    public static bool Closes(string code) =>
        code is not (Held or Uncommitted or Exists or Completed or Refused or Moved or Unreviewed or Opinion or CarriedOn or RootDirty or Queued);

    /// <summary>Whether a try with this code is tried again only once its tree's tip or status moves.</summary>
    public static bool OnChange(string code) => code is Uncommitted or Exists or Completed or Refused or Moved or CarriedOn;

    /// <summary>Whether a try with this code is tried again only once the checkout's status differs from the kept one.</summary>
    public static bool WaitsOnRoot(string code) => code is RootDirty;
}
