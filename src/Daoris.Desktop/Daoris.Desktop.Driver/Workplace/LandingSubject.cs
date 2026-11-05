using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The session a press lands: its id, and its quest and that quest's title where it has one.</summary>
public sealed record LandingSubject(string Session, string? Quest, string? Title)
{
    /// <summary>
    /// The quest's name where it is not its title (SESSUX1j, LANDNAME1): its short title, which the branch is named for. Null
    /// where the name is the title, or there is no quest: the title names the branch.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The branch name the person entered (LANDNAME1a, D145): it replaces the rule's whole name, the pattern's <c>{slug}</c> and all,
    /// and only where the rule makes a branch. Blank or null keeps the rule's. The automatic landing takes none.
    /// </summary>
    public string? Entered { get; init; }

    /// <summary>The entered name trimmed, or null where none was entered.</summary>
    public string? EnteredName => string.IsNullOrWhiteSpace(Entered) ? null : Entered.Trim();

    /// <summary>
    /// The quest the session itself served (FABRIC-UNITLINK1, D173's CLOSELAND1 note), whatever <see cref="Quest"/> names it for: the
    /// landing's record keeps it on the landing or its advance. Set where a door names the session's landing
    /// (<see cref="SessionTrees.OwnSubjectAsync"/>, an entry's parts); null for a conversation, and where no door set it.
    /// </summary>
    public string? Served { get; init; }

    /// <summary>
    /// That quest's current submission as the door read it (FABRIC-CLOSE1h13), by its machine and sequence, which the record keeps as
    /// what the landing was made on; null for a done and a conversation.
    /// </summary>
    public QuestSubmissionRef? Submission { get; init; }

    /// <summary>The sessions a combined Accept brings into this landing (LINESTOGETHER1d), which its record or advance lists beside it.</summary>
    public IReadOnlyList<LandedWork> Brought { get; init; } = [];

    /// <summary>
    /// The branch an entry's Accept lands this part on (ACCEPTALL1, D167's 2026-10-15 amendment point 1): the entry's one branch,
    /// named once for its first part, so its parts land together. Null names it by the rule for this subject, as before; a name the
    /// person entered still comes first, and a tree on a branch of its own still lands as that branch (TREEFOLLOW1).
    /// </summary>
    public string? EntryBranch { get; init; }

    /// <summary>
    /// Whether the press joins the entry's branch (ACCEPTALL1b, ACCEPTALL1f): it lands on <see cref="EntryBranch"/>, replaying a part
    /// that ran alongside onto its tip. False where no entry names a branch, or the person entered a name, which lands as entered.
    /// </summary>
    public bool Joins => EntryBranch is not null && EnteredName is null;

    /// <summary>
    /// The request's body (PROCESS1b-i), where a ticket's key is searched after the title: the quest's, null where it is blank or
    /// there is no quest. A conversation's words are its opening, its <see cref="Title"/>.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>The request's links, searched last for a ticket's key (PROCESS1b-i), each as given; null where it carries none.</summary>
    public IReadOnlyList<string>? Links { get; init; }

    /// <summary>
    /// This subject, named for a part's own quest and title (TREEFOLLOWNAME1): a part on a branch of its own lands as that branch, so
    /// its landing record and its plugin's pull request carry its own words, not the entry's first part's. The session, the entry's
    /// branch and a name entered stay as they are.
    /// </summary>
    public LandingSubject NamedFor(LandingSubject own) =>
        this with { Quest = own.Quest, Title = own.Title, Name = own.Name, Body = own.Body, Links = own.Links };

    /// <summary>The branch pattern's <c>{slug}</c>: the quest's name in the words git takes, its title where it has no other.</summary>
    public string Slug => BranchPatterns.Slug(Name ?? Title);

    /// <summary>
    /// Whether this is a conversation's subject, its title its opening (<see cref="LandingRules.SubjectAsync"/>): no quest, no name, and
    /// the person's first words as its title (ACCEPTFOOT2, D164's 2026-10-16 note).
    /// </summary>
    public bool NamedByOpening => Quest is null && Name is null && Title is not null;

    /// <summary>
    /// This subject named for its work where it is a conversation's (ACCEPTFOOT2): its name the oldest commit subject its work holds
    /// beyond the line, as PROCESS1b-ii names a squash with no title, or empty where none was read, so its branch is never a slug of
    /// the person's first words. The opening stays its title, for the record and the squash, and is searched for a ticket's key after
    /// that subject. Any other subject is as it was.
    /// </summary>
    public LandingSubject ForWork(string? oldest) =>
        NamedByOpening ? this with { Name = string.IsNullOrWhiteSpace(oldest) ? "" : oldest.Trim() } : this;

    /// <summary>
    /// The title a squash's subject takes (ACCEPTFOOT2b): a quest's own title, but for a conversation, whose title is its opening, the
    /// oldest commit's subject <see cref="ForWork"/> named it by, or null where none was read, so the squash's own fallback takes the
    /// commits' first subject.
    /// </summary>
    public string? SquashTitle => Quest is null && Title is not null ? (string.IsNullOrWhiteSpace(Name) ? null : Name) : Title;

    /// <summary>
    /// The subject a quest is landed as: its title for the record, its name for the branch where they differ, and its body and
    /// links for a ticket's key to be found in. Blank and empty are carried as none, so a quest without them is the subject it was.
    /// </summary>
    public static LandingSubject Of(string session, QuestView quest) => new(session, quest.Id, quest.Title)
    {
        Name = quest.Name == quest.Title ? null : quest.Name,
        Body = string.IsNullOrWhiteSpace(quest.Body) ? null : quest.Body,
        Links = quest.Links.Count > 0 ? quest.Links : null,
    };
}
