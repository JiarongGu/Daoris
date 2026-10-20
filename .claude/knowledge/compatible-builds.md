---
name: compatible-builds
applies_when: changing what a store keeps where a build already shipped may still open it (a table's columns, what a row means, who may write it), or choosing how a newer build keeps its rows from an older one
enforces: a mark binds only the builds that read it; to keep stored rows safe from builds already built, put them in a shape those builds cannot see, a new table, rather than marking them where those builds still read and write; a version stamp guards only the builds from the stamp on
---

# Compatible builds — a mark binds only the builds that read it

**An older build sharing a store does what it was built to do, and it was built before your change. A column,
a flag or a version it never reads changes nothing it does. To keep rows safe from builds already shipped,
put them where those builds cannot look: a new table. This is the boundary beside the rule that a stored
shape is never changed in place (`.claude/agents/branch-worker.md`): there the old shape keeps being read
the old way, and here the new rows keep out of the old builds' reach.**

## Why

A take-bind's contract was fixed twice before it held against the builds already built (D173):

- **FABRIC-CLOSE1o** gave a take-bind an owner, a publication and an expiry, as four nullable columns
  added to `quest_take_binds`. An older build over the migrated store kept working: its upsert of
  `(id, graph, at)` landed, and its take's `DELETE … RETURNING graph` read the row as before. That was the
  point of nullable columns, and it was also the gap. The older build's take still bound by the quest's id
  alone, whoever took and however late. Its naming replaced the graph of a row this build owned and left
  the owner in place, so the owner's take bound the older build's graph.
- **FABRIC-CLOSE1w** moved the owned binds into `quest_owned_take_binds`, every column required, a table
  no older build knows. An older build's upsert cannot reach an owned bind, and its take finds none. A
  row in the old table is now an older build's whatever it names, consumed as owning nothing and
  discarded at the open. Its note rejects the other two ways. Detecting an older upsert by an owner that
  disagrees with its graph catches the collision, but an older build's take still binds by the id. A store
  version, like the knowledge index's `user_version` (`NewerIndexException`), binds only this build and
  later ones, and the builds to guard against were CLOSE1l to CLOSE1v, none of which reads it.

## How to apply

- **Name the builds you are guarding against, and ask what each one reads.** Builds before your change
  read what they were built to read. A new column, a flag or a version number is invisible to them. They
  read and write around it, and an upsert that names only its own columns keeps what you added and
  replaces what it owns.
- **Rows an older build must not reach go in a new table.** Make every column required, since no build
  writing there leaves one out. Read the old table's rows as the older build's, owning nothing, and say
  what the newer build does with them at a take and at an open.
- **Nullable columns are for rows an older build may keep writing.** They keep its statements landing,
  as CLOSE1o's did. That makes the store compatible. It does not keep the older build's rows from
  meaning what they meant before.
- **A version stamp guards the builds that read it, from the stamp on.** The quest store has kept one since
  CLOSETAKE1 (QUESTSTOREVER1): `quest_store_version`, `QuestStore.StoreVersion`, never lowered, in its own
  table because the knowledge index sharing the file owns `user_version`. A store stamped ahead still opens
  and refuses a take's bind, saying it was written by a newer Daoris. So a later change to the take-bind
  contract can bump the stamp instead of hiding its rows. Builds from before the stamp still read none of
  it, and against them only a shape they cannot see works.
- **Say what is left.** An older build still acts on its own rows as it always did: CLOSE1w's note keeps
  that its take of a quest this build named takes it unbound and closes by its done. Write the remainder
  into the decision's note, so nobody reads the boundary as wider than it is.
- **Hold it with the older build's own statements.** `QuestTakeBindTests` runs an older build's upsert and
  take statements over an owned bind and over an unarmed store. A test that runs only this build's code
  never sees the boundary.
