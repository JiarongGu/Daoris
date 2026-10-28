namespace Daoris.Driver;

/// <summary>
/// The notes a piece keeps (COAGENT1b, D199 point 8; the co-agent design §2.3 step 11, §4.2, §7.8), each by its code: on the lead its
/// piece's course, on the partner whose session it works for. Notes, never words: none reaches a prompt, reopens a record or resets the
/// answers-alone count, which reads words and the person's press alone (<see cref="AnswersAlone"/>).
/// </summary>
public static class PartnerNotes
{
    /// <summary>On the lead: a piece was asked of an agent, and its slot reserved.</summary>
    public static Noted Asked(string agent) =>
        Noted.Of(NoteCodes.PartnerAsked, $"A piece was asked of {agent}: it works in a clone of its own and changes nothing here.", ("agent", agent));

    /// <summary>On the lead: the partner's record opened and its turn begins.</summary>
    public static Noted Started(string agent, string partner) =>
        Noted.Of(NoteCodes.PartnerStarted, $"Partner {agent} started on the piece in session {partner}.", ("agent", agent), ("session", partner));

    /// <summary>On the lead: the piece's answer was handed, a claim beside a commit to take in.</summary>
    public static Noted Answered(string agent, string partner) =>
        Noted.Of(
            NoteCodes.PartnerAnswered,
            $"Partner {agent} (session {partner}) answered: its work is a commit to take in, and its words are claims to check.",
            ("agent", agent), ("session", partner));

    /// <summary>On the lead: the piece was refused, and why.</summary>
    public static Noted Refused(string why) =>
        Noted.Of(NoteCodes.PartnerRefused, $"The piece was refused: {Said(why)}.", ("why", why));

    /// <summary>On the lead: the turn's end where it was not the partner's own, its work kept.</summary>
    public static Noted? Ended(string? end, string agent) => end switch
    {
        PartnerEnds.Stopped => Noted.Of(NoteCodes.PartnerStopped, $"Partner {agent} was stopped; the work it left is kept.", ("agent", agent)),
        PartnerEnds.OutOfTime => Noted.Of(NoteCodes.PartnerOutOfTime, $"Partner {agent} ran past its minutes; the work it left is kept.", ("agent", agent)),
        PartnerEnds.Limited => Noted.Of(NoteCodes.PartnerLimited, $"Partner {agent}'s account met a limit; the work it left is kept.", ("agent", agent)),
        _ => null,
    };

    /// <summary>On the lead: Daoris cannot know what became of the piece, and it waits for the person.</summary>
    public static Noted Uncertain(string why) =>
        Noted.Of(NoteCodes.PartnerUncertain, $"The piece is uncertain: {Said(why)}. It waits for you.", ("why", why));

    /// <summary>On the lead: the commit changes files outside the piece's paths.</summary>
    public static Noted Beyond(int count) =>
        Noted.Of(NoteCodes.PartnerBeyond, $"The piece's commit changes {count} file{(count == 1 ? "" : "s")} beyond its paths.", ("count", count));

    /// <summary>On the lead: the import refused what the copy held, and the copy is kept for the person.</summary>
    public static Noted Kept(string why) =>
        Noted.Of(NoteCodes.PartnerKept, $"The piece's work was not taken into the repository: {Said(why)}. Its copy is kept for you.", ("why", why));

    /// <summary>On the partner: whose session it works for.</summary>
    public static Noted Working(string lead) =>
        Noted.Of(NoteCodes.PartnerWorking, $"This session is a partner working one piece for session {lead}, in a clone of its own.", ("session", lead));

    /// <summary>A code's reason as the machine's log and the note's text say it; the page words it from the code.</summary>
    public static string Said(string why) => why switch
    {
        PartnerWhy.AskerUnfit => "the session that asked cannot lead a piece",
        PartnerWhy.AskerGone => "the session that asked is not on this machine",
        PartnerWhy.Blank => "the piece is blank",
        PartnerWhy.PathOutside => "a path it names leads outside the repository",
        PartnerWhy.NoCommit => "the session has no commit to grow from",
        PartnerWhy.BaseMissing => "the commit it grows from is not in the repository",
        PartnerWhy.TooMany => "the session already has two pieces open",
        PartnerWhy.NotListed => "the agent named is not one the rule lists",
        PartnerWhy.NoConnector => "the agent hands no connector to its sessions",
        PartnerWhy.NotMeasured => "the agent's confinement as a partner is not measured",
        PartnerWhy.CopyFailed => "its copy could not be made",
        PartnerWhy.NoJob => "its process could not be held in its own job",
        PartnerWhy.StateUnreadable => "its state could not be read",
        PartnerWhy.JobUnknown => "Daoris restarted while it worked",
        PartnerWhy.RefMoved => "its ref stands at another commit",
        PartnerWhy.ImportRefused => "what its copy held could not be imported",
        PartnerWhy.NoWork => "it left no work",
        PartnerWhy.HandCut => "Daoris restarted while its answer was handed",
        _ => why,
    };
}
