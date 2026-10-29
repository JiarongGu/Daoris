namespace Daoris.Driver;

/// <summary>
/// Whether a record resumes, judged against this machine's disk (ANSWER1a, MSG1b), under the name its callers use (STRUCT3h,
/// D166's note): <c>Continuations.Judge</c> for the loop's look, the runner's going-on and the tests, with the tree's step asked
/// of <see cref="ConversationTrees"/>. The judgement is <see cref="Continuations"/>'s, pure, in the Domain.
/// </summary>
public static class ContinuationJudge
{
    extension(Continuations)
    {
        /// <summary>Null where the record resumes; else the first reason it does not, its tree asked of the disk.</summary>
        public static ContinueReason? Judge(
            PriorSession park, string adapter, bool doorResumes, string? profile, HarnessConversation? kept, ContinueReason? account = null) =>
            Continuations.Judge(ConversationTrees.Stands, park, adapter, doorResumes, profile, kept, account);
    }
}
