namespace Daoris.Driver;

/// <summary>Why a held question waits, and whose move it is.</summary>
internal static class HeldQuestionSaid
{
    /// <summary>
    /// A host before evidence answers no cause, and its one hold was a departure. The check names a commit where no session here
    /// ended on the question, since a done no driven end read must be read at a commit the person names. The code beside the
    /// sentence is which of them it is, so the page words it.
    /// </summary>
    internal static (SittingFact Said, string Reason) Of(Snapshot snapshot, QuestView question)
    {
        var yes = $"once you accept the done as it stands, your yes: `daoris quest accept {question.Id}`.";
        var atCommit = !snapshot.LastRun.ContainsKey(question.Id);
        var named = atCommit ? " --commit <sha>" : "";
        SittingFact Fact(string code) => new(code) { Quest = question.Id, Repository = question.To };
        return question.Hold switch
        {
            EvidenceCodes.Unread when snapshot.Active.FirstOrDefault(s => string.Equals(s.Quest, question.Id, StringComparison.OrdinalIgnoreCase))
                is { } running =>
                (Fact(SittingFact.UnreadRunning) with { Session = running.Id },
                    $"waits on `#{question.Id}`, which `{question.To}` closed done: its evidence is read when session `{running.Id}` "
                    + "ends — it resumes, in the same tree, once that evidence is found."),
            EvidenceCodes.Unread =>
                (Fact(atCommit ? SittingFact.UnreadAtCommit : SittingFact.Unread),
                    $"waits on `#{question.Id}`, which `{question.To}` closed done, and its evidence is not read yet — it resumes, in "
                    + $"the same tree, once the evidence is found, `daoris quest check {question.Id}{named}`, or {yes}"),
            EvidenceCodes.MissingHold =>
                (Fact(SittingFact.Missing),
                    $"waits on `#{question.Id}`, which `{question.To}` closed done without the evidence it names — it resumes, in the "
                    + $"same tree, once a later commit holds it, `daoris quest check {question.Id} --commit <sha>`, or {yes}"),
            // A set-up step waits for the person's review, which a yes does not give.
            EvidenceCodes.Unreviewed =>
                (Fact(SittingFact.Unreviewed) with { Environment = question.SetUpIn },
                    $"waits on `#{question.Id}`, which `{question.To}` closed done: it waits for your review"
                    + (question.SetUpIn is { } environment ? $" in `{environment}`" : "")
                    + $" — it resumes, in the same tree, once you say it is reviewed, `daoris quest review {question.Id} reviewed`, "
                    + $"or skip the review, `daoris quest review {question.Id} skip`."),
            _ =>
                (Fact(SittingFact.Departed),
                    $"waits on `#{question.Id}`, which `{question.To}` closed done departing from what you required — it "
                    + $"resumes, in the same tree, once you accept that, your yes: `daoris quest accept {question.Id}`."),
        };
    }
}
