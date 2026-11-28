namespace Daoris.Driver;

/// <summary>
/// The store's step that makes the tree ledger (<see cref="DesktopStoreSteps"/>, <see cref="TreeLedger"/>, <see cref="TreeSaveRecords"/>):
/// each save of a tree, each move of a copy and each day's reading of a repository's copies. New tables in their one shape, which an
/// older build never opens, so the stamp stays where it is; no older shape is read.
/// </summary>
/// <remarks>
/// A save's row is its record: the kept ref is its id, its files a JSON list of path, hash, bytes and blob. A move's row is one make,
/// reuse, removal, install or restore, with the copy's own bytes before and after (a hard link counted at the store, once). A day's row is
/// the last reading of that day, the drive's free space at its first and last reading kept beside it, and the copies it listed with their
/// own bytes, which the next reading compares against.
/// </remarks>
internal static class TreeLedgerStoreStep
{
    public static StoreMigration Migration { get; } = new(DesktopStore.TreeLedgerVersion, DesktopStore.PolicyWriter, write => write.Execute(
        """
        CREATE TABLE tree_saves (
          id         TEXT PRIMARY KEY,
          workspace  TEXT NOT NULL,
          repository TEXT NOT NULL,
          root       TEXT NOT NULL,
          tree       TEXT NOT NULL,
          branch     TEXT,
          head       TEXT NOT NULL,
          save       TEXT NOT NULL,
          staged     TEXT NOT NULL,
          raw        TEXT NOT NULL,
          work       TEXT NOT NULL,
          files      TEXT NOT NULL,
          bytes      INTEGER NOT NULL,
          took       INTEGER NOT NULL,
          at         TEXT NOT NULL,
          landed     TEXT,
          dropped    TEXT
        ) WITHOUT ROWID;
        CREATE INDEX tree_saves_tree ON tree_saves (tree, at);
        CREATE TABLE tree_moves (
          id         INTEGER PRIMARY KEY AUTOINCREMENT,
          workspace  TEXT NOT NULL,
          repository TEXT NOT NULL,
          day        TEXT NOT NULL,
          at         TEXT NOT NULL,
          move       TEXT NOT NULL,
          tree       TEXT NOT NULL,
          before     INTEGER NOT NULL,
          after      INTEGER NOT NULL,
          ready      INTEGER
        );
        CREATE INDEX tree_moves_day ON tree_moves (day, workspace, repository);
        CREATE TABLE tree_days (
          workspace     TEXT NOT NULL,
          repository    TEXT NOT NULL,
          day           TEXT NOT NULL,
          trees         INTEGER NOT NULL,
          slot_bytes    INTEGER NOT NULL,
          linked_bytes  INTEGER NOT NULL,
          saves_pending INTEGER NOT NULL,
          saves_bytes   INTEGER NOT NULL,
          volume        TEXT,
          free_first    INTEGER,
          free_last     INTEGER,
          first_at      TEXT NOT NULL,
          last_at       TEXT NOT NULL,
          listed        TEXT NOT NULL,
          PRIMARY KEY (workspace, repository, day)
        ) WITHOUT ROWID;
        """));
}
