namespace Daoris.Driver;

/// <summary>
/// What a try to land a due session came to (LAND2b, design §8): the codes its due entry keeps and the machine log writes.
/// The first eight are the design's; the rest name why an entry closed without a landing, so nothing closes unsaid (D143
/// point 1).
/// </summary>
public static class AutoLandingCode
{
    /// <summary>The branch was made and recorded, and the rule's plugin pushed it where it names one.</summary>
    public const string Landed = "landed";

    /// <summary>
    /// A later done moved the chain's branch on as a fast-forward (LAND2c, D149 point 2): a chain's later step, or a session that
    /// went on after its landing. The rule's plugin pushed it where it names one.
    /// </summary>
    public const string Advanced = "advanced";

    /// <summary>The done made no commits, so there was nothing to land.</summary>
    public const string Nothing = "nothing";

    /// <summary>The quest is done and held (D133's departure, D144's evidence): it lands at the look after the release.</summary>
    public const string Held = "held";

    /// <summary>The tree holds uncommitted work, which a branch would leave behind.</summary>
    public const string Uncommitted = "uncommitted";

    /// <summary>
    /// The pattern names a branch that stands and may not move on (D87, D149 point 2): one Daoris did not make, one moved since
    /// its landing, one checked out, or one the session's work does not grow from.
    /// </summary>
    public const string Exists = "exists";

    /// <summary>
    /// The chain's branch already reads on the line, so its pull request was merged, and an advance would push work no pull
    /// request carries (LAND2c, D149 point 3). It waits: bringing the repository up to date and the person's Accept land it fresh.
    /// </summary>
    public const string Completed = "completed";

    /// <summary>The branch was made and recorded; the rule's plugin cannot land work here, so its push was not tried (D145 point 2).</summary>
    public const string PluginUnready = "plugin-unready";

    /// <summary>The branch was made and recorded; the rule's plugin failed, or answered that it did not push (D100).</summary>
    public const string PluginFailed = "plugin-failed";

    /// <summary>
    /// The branch was made and recorded; its quest was held, taken back or submitted again before the rule's plugin was told, so the
    /// push was not tried (FABRIC-CLOSE1h2, D173's note: a hold stops a landing up to the moment its push begins). Kept open and read
    /// again at every look: once its quest stands unheld, the look hands the branch on, or lands the work that went on past it.
    /// </summary>
    public const string PushHeld = "push-held";

    /// <summary>Its work reached a branch of the person's another way: their press, or a push of their own.</summary>
    public const string Already = "already";

    /// <summary>A newer session went on in its tree, and stands for it now (<c>ReviewableTree</c>'s rule).</summary>
    public const string Superseded = "superseded";

    /// <summary>Its tree was discarded before it landed.</summary>
    public const string Gone = "gone";

    /// <summary>Its quest is no longer done: the person turned a departure down, or the service no longer holds it.</summary>
    public const string Undone = "undone";

    /// <summary>Its repository's rule no longer accepts automatically: the person's press is the way again.</summary>
    public const string Off = "off";

    /// <summary>The landing was refused for another reason, git's or the pattern's, in its own sentence.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// Its work waits for the person's review (REVIEWENV1c, D154 point 7; the review environment design §3.1): the level says
    /// review, and no <i>reviewed</i> on a set-up that holds its tip, and no skip, lets it go yet. Kept once, read again at every
    /// look as a hold is, and landed at the first look after the review.
    /// </summary>
    public const string Unreviewed = "unreviewed";

    /// <summary>
    /// Its work waits for a second opinion (XAGENT1f, D155 point 10; the second-agent design §8.1): the level says one is read
    /// before it lands, and none is settled yet, or the chain's last step here is still to run. Kept once, read again at every
    /// look, and landed at the first look once settled. It comes before <see cref="Unreviewed"/>, as the gate's order does (§7).
    /// </summary>
    public const string Opinion = "opinion";

    /// <summary>
    /// Its work is on the line, or on a branch of the person's, by content already (SQUASHTIDY1f; SQUASHTIDY1's proof, as the
    /// session's head reads it since SQUASHTIDY1b): a squash merge or a cherry-pick carried it, so landing it again would make a
    /// second copy. Refused at every door, and an entry it ends stays closed: the tree's discard is the way.
    /// </summary>
    public const string Carried = "carried";

    /// <summary>Whether a try with this code closes its entry; the rest wait for a change, a release or the person's press.</summary>
    public static bool Closes(string code) =>
        code is not (Held or Uncommitted or Exists or Completed or Refused or Unreviewed or Opinion or PushHeld);

    /// <summary>Whether a try with this code is tried again only once its tree's tip or status moves (design §2).</summary>
    public static bool OnChange(string code) => code is Uncommitted or Exists or Completed or Refused;
}
