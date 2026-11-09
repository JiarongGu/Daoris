namespace Daoris.Driver;

/// <summary>
/// The person's permissions on one copy of a machine-local input into a tree: a copy a <c>deny</c> matches is not made, one an <c>ask</c>
/// holds is not made and is kept as an ask for a supervisor's allow at a later start, and one they let through closes the ask it had.
/// </summary>
internal static class TreeCopyPermit
{
    internal static FileVerdict Judge(string home, string repository, FileRules rules, string path)
    {
        var act = FileActs.Copy(path);
        var verdict = rules.Judge(act);
        if (verdict == FileVerdict.Held) FileAsks.Note(home, new FileAsk(repository, Moments.Write(DateTimeOffset.UtcNow), FileAsk.Copy, act) { Person = rules.NamesPerson(act) });
        else FileAsks.Close(home, repository, FileAsk.Copy, act);
        return verdict;
    }
}
