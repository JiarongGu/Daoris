namespace Daoris.Driver;

/// <summary>
/// The store's step that makes a role's held decisions and their windows, which a restart reads back (<see cref="DesktopStoreSteps"/>).
/// A new table an older build never opens: such a build's start closes the decisions' files as before, and this build reads a row whose
/// file a closing line reached as closed.
/// </summary>
internal static class HeldDecisionsStoreStep
{
    public static StoreMigration Migration { get; } = new(DesktopStore.HeldDecisionsVersion, DesktopStore.PolicyWriter, write => write.Execute(
        """
        CREATE TABLE held_decisions (
          id          TEXT NOT NULL PRIMARY KEY,
          session     TEXT NOT NULL,
          kind        TEXT NOT NULL,
          repository  TEXT NOT NULL,
          generation  TEXT NOT NULL,
          goes_on_at  TEXT NOT NULL,
          kept        TEXT NOT NULL
        );
        """));
}
