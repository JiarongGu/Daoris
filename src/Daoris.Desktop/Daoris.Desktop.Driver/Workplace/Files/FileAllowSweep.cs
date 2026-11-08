namespace Daoris.Driver;

/// <summary>
/// The start's sweep of the rule file's allows: each decision a supervisor began that the last run did not finish is recorded from what
/// the person's file holds, <c>made</c> where its allow stands under it and <c>lapsed</c> where none does, the ask then waiting again.
/// </summary>
internal static class FileAllowSweep
{
    internal static IReadOnlyList<FileAllowSwept> AtStart(string home, MachineLog? log)
    {
        var swept = FileAllows.Sweep(home);
        foreach (var each in swept) log?.Info(each.Made ? "supervisor.files-made" : "supervisor.files-lapsed", ("decision", each.Decision));
        return swept;
    }
}
