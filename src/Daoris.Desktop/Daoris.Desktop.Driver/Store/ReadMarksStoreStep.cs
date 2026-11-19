namespace Daoris.Driver;

/// <summary>
/// The store's step that makes each conversation's read mark on this machine (<see cref="DesktopStoreSteps"/>, <see cref="ReadMarks"/>).
/// A new table in its one shape, which an older build never opens: such a build opens a conversation at its tail, as before.
/// </summary>
internal static class ReadMarksStoreStep
{
    public static StoreMigration Migration { get; } = new(DesktopStore.ReadMarksVersion, DesktopStore.PolicyWriter, write => write.Execute(
        """
        CREATE TABLE read_marks (
          conversation TEXT NOT NULL PRIMARY KEY,
          part         TEXT NOT NULL,
          seq          INTEGER NOT NULL,
          at           TEXT NOT NULL,
          read         TEXT NOT NULL,
          steps        INTEGER
        );
        """));
}
