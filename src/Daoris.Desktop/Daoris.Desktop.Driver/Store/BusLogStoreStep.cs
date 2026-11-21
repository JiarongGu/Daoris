namespace Daoris.Driver;

/// <summary>
/// The store's step that makes the bus's log (<see cref="DesktopStoreSteps"/>, <see cref="DesktopBusLogStore"/>): its
/// entries in order and its measures by day. New tables an older build never opens, so the stamp stays where it is.
/// </summary>
/// <remarks>
/// An entry's row is its envelope: times as milliseconds since the epoch, its causes and payload as JSON. No index beside the order: a
/// reader by id or by subject comes with the step that reads one. A measure's row is one day's count, total and most of one measure.
/// </remarks>
internal static class BusLogStoreStep
{
    public static StoreMigration Migration { get; } = new(DesktopStore.BusLogVersion, DesktopStore.PolicyWriter, write => write.Execute(
        """
        CREATE TABLE bus_log (
          seq      INTEGER PRIMARY KEY,
          id       TEXT NOT NULL,
          kind     TEXT NOT NULL,
          subject  TEXT NOT NULL,
          emitter  TEXT NOT NULL,
          answerer TEXT,
          deadline INTEGER,
          effect   TEXT,
          causes   TEXT,
          payload  TEXT,
          at       INTEGER NOT NULL
        );
        CREATE TABLE bus_measures (
          day     TEXT NOT NULL,
          measure TEXT NOT NULL,
          name    TEXT NOT NULL,
          count   INTEGER NOT NULL,
          total   INTEGER NOT NULL,
          most    INTEGER NOT NULL,
          PRIMARY KEY (day, measure, name)
        ) WITHOUT ROWID;
        """));
}
