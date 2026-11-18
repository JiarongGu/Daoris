---
name: compatible-builds
applies_when: changing what a store keeps (a table's columns, what a row means, a file's keys, a stored name or value), or tempted to keep a reader, a migration or a guard so that a record an earlier build wrote still works
enforces: no build reads a record shape an earlier build wrote; a store changes in place to the one current shape, and a store an older build opens after a purge is not supported
---

# Compatible builds — none: every store has the one current shape

**No build reads a record in a shape an earlier build wrote. A change to what a store keeps changes the
shape, the writer, the reader and the tests together, and leaves nothing behind that accepts the old
shape.** The owner decided it (D323, NOCOMPAT1): a home's history is purged (CLEANSTART1, D322) and every
repository keeps its own records, so there is nothing written by an earlier build left to read. A store an
older build opens after a purge is not supported: the older build does what it was built to do over a shape
it does not know, and nothing here is kept to make that work.

## What goes with a change of shape

- **The old reader.** A column, key, file, stored name or value, event-log line or lock field that only an
  earlier build wrote is read by no code. The new shape is the only one a reader names; a value outside it
  is refused as unknown or read as absent, never mapped to a former spelling.
- **The migration.** A store's schema step that converts old rows, a one-off conversion of a home's files,
  an importer of an older file or folder, a receipt of what was folded: none is written. A change of shape
  edits the store's one creating statement, and a store whose shape moved is purged, not converted.
- **The shim and its allowance.** A census that permits a place to name a former spelling lists no such
  place; a test that lays an older record and asserts it still reads is deleted with its reader. A comment
  that says "an older build wrote" or "kept for records already written" is a reader this rule removes.
- **The doctrine's rule 4 is met without a wait.** Where it lets an older reader stay "until a condition
  ends", the condition is met here for every reader: one install, one owner, a purged history.

## What stays

- **A guard that refuses a store a newer build wrote.** A build that finds a version stamp above its own
  refuses to write (`NewerIndexException`, `StoreNewerException`): it reads no older shape, it declines a
  newer one. It is not a reader of an earlier build's record, and it stays where it exists. A new stamp is
  added only for a store that has one already; no stamp is added to keep rows safe from older builds.
- **A shape that is still written.** A field a current writer still produces, even where it is often
  absent, is the current shape. If you are unsure that a shape is still written, find its writer: where no
  writer remains, it is an old record and goes; where one does, it is current and stays.
- **A reader of what a purge keeps.** A purge takes history, not configuration: saved workflows and their
  versions, a hand-written file (`daoris.json`, `driver.json`), a repository's own lock and hooks. An older
  shape there is still in front of this build, so its reader stays, naming the condition that ends it (no
  saved instance spells it, counted where it can be), or the read refuses with a sentence saying what to do.
  It never crashes and never ignores the shape in silence.
- **A refusal of a store that was not purged.** A home older than this build's shape, never purged, is
  refused at open, naming the home and what to do (purge it, or start fresh), not failed at its first query.
- **Wire fields that an older peer may omit,** while the peer and this build run together over a wire (a
  page and its host in one install, a service and its client), are not records. The rule governs what a
  store keeps, not the two ends of a request that are one release.

## How to apply

- **Change the shape in place.** Rename the column, drop the key, replace the value; update the one
  creating statement, the writer, the reader and the tests in the same change. Do not add a second reading
  "for now".
- **Do not guard against older builds.** No nullable column kept so an older statement still lands, no
  separate table kept so an older build cannot reach a row, no version stamp bumped to refuse an older
  build's writes. Say in the change's note that older builds over a purged store are not supported, once.
- **Find the writer before you delete a reader.** The test that a shape is old is that nothing writes it
  now. A reader of a shape a current writer produces is a current reader.
- **A test lays only the current shape.** A fixture that lays an older record to prove it still reads is
  the reader's own test and leaves with it.
