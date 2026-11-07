---
name: rooms-and-classes
applies_when: declaring a repository's rooms or the classes of its ignored files, or proposing them for a repository that has none
enforces: a room declares what its builds read and write, so rooms whose builds meet are claimed together; an ignored path is carried only by its class, and a tracked path is never declared
---
<!-- daoris: core/core/knowledge/rooms-and-classes.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# Rooms and classes — where a session works in place, and what its ignored files are

**A room is a lane that one session at a time claims and works in, in place in the repository's checkout.
Its declaration names its build units and everything its builds read or write outside them, so rooms
whose builds meet are claimed together. The classes say what each ignored path is, so a tree carries only
what is safe to carry. Declare both from the repository's own build files, and check the declaration
before anyone relies on it.**

## Why

Two sessions in one checkout are safe only where their builds share nothing. A build that compiles another
room's half-done sources, or writes into the folder another room runs, gives each session a result that
is the other's: it passes or fails for someone else's reasons, and nothing says so. The same silence
meets ignored files carried from one tree to another. An intermediate is valid only at the path its tool
made it, and an output run without building looks fresh when it is not. A declaration that says what
crosses lets those meetings be found before they happen, and one that says nothing carries nothing,
which is never wrong, only slower.

## How to apply

**Rooms, in the lanes file.** A lane that carries `builds` is a room. Lanes that are one package, one
lockfile or one build name the same `room` and keep their own names. A laneless group that names a `room`
joins it, as the documents and the shared settings every build reads may. A lane or group that is neither
is in no room, and its paths stay on the fallback: a tree of their own.

- `units`: each build unit whose folder holds the room's sources, one file each. A project file
  (`*.csproj`, `*.fsproj`); a `package.json` that builds; a crate's `Cargo.toml`; a package's
  `pyproject.toml`; a `go.mod`; a module's Gradle or Maven file; a `CMakeLists.txt` that defines targets.
  A solution or a workspace root that only names its members is no unit: its members are.
- `reads`: what the builds read outside their units' folders. A reference to another unit (a project
  reference, a path dependency, a workspace member); shared build settings (an imported props file, a
  central package file, a toolchain or version file); a source compiled in by link; a generator's sources.
- `writes`: what the builds or generators write outside their folders. Generated sources placed in
  another folder, a build's output placed in another's (a web build in a host's static folder), and a
  tracked generated file.
- Paths are repository-relative with `/`; a folder ends in `/`; `*` stays in a folder and `**` crosses.

**Classes, in the gates file's `trees`.**

- `dependencies`: each folder a package manager installs from a lockfile, readable at any path, with
  its `key` (the tracked lockfile and the manager's own configuration, such as an `.npmrc`) and the
  `toolchain` command whose output keys it with them.
- `intermediates`: what the build tool checks itself and makes again when stale, valid only at its
  path: `obj/`, `target/`, `.gradle/`, a CMake build tree, a virtual environment whose launchers name it.
- `outputs`: what a runner uses without building first, or what is cheap to make again: `bin/`, `dist/`,
  a folder a host serves, a generated index. The class belongs to the runner, not the folder: a folder a
  script runs without building is an output even where a tool checks it.
- `prepare`: the commands that warm a tree, each one the declaration's safe list already holds.
- Never declare a path git tracks: it is git's. A tracked entry point under `bin/` means naming the
  outputs folder by folder. Machine-local inputs are declared once, as local inputs, never here.

**Proposing them.** Read the build files, never the folder names: the units, their references and the
settings they import. Write each entry with its reason, as a change on the repository's own branch for the
person to accept where it lands; for another repository, send the request to whoever works there. Then
check it. Where the coordinating tool runs, its room check reads the lanes file, the gates file and git's
tracked files; it refuses overlapping rooms, a unit or a lockfile split across rooms, a file written from
two rooms and a declared path git tracks, lists every reference across rooms, and says the claim groups.
Without the tool, check the same by hand: every tracked file in one room or said to be in none, every
unit's folder in its room, every lockfile in the room of the unit beside it.

**Expect few groups.** Where shared settings or generated contracts join most rooms, one session works in
place in that group and the rest take a tree of their own until the builds are made apart. That is the
true answer, not a failed declaration.
