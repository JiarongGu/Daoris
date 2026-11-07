---
name: rooms-and-classes
applies_when: declaring a repository's rooms or the rules for its files git does not manage, or proposing them for a repository that has none
enforces: a room declares what its builds read and write, so rooms whose builds meet are claimed together; every file git does not manage is handled by a layered rule file, each rule naming its kind, and a tracked path is never named
---

# Rooms and rules — where a session works in place, and what its unmanaged files are

**A room is a lane that one session at a time claims and works in, in place in the repository's checkout.
Its declaration names its build units and everything its builds read or write outside them, so rooms
whose builds meet are claimed together. The rule file says what each file git does not manage is, so a
tree carries only what is safe to carry. Declare both from the repository's own build files, and check
the declaration before anyone relies on it.**

## Why

Two sessions in one checkout are safe only where their builds share nothing. A build that compiles another
room's half-done sources, or writes into the folder another room runs, gives each session a result that
is the other's: it passes or fails for someone else's reasons, and nothing says so. The same silence
meets files carried from one tree to another. An intermediate is valid only at the path its tool
made it, and an output run without building looks fresh when it is not. A declaration that says what
crosses lets those meetings be found before they happen. The rule file is for a development tool, so it
restricts nothing by default: it says what each path is and what may be done with it, and a restriction
is something a person adds. Only a floor that prevents losing work stands under every layer.

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

**Rules, for every file git does not manage.** A rule file is a list of path patterns read in order, as an
ignore file is: of the rules that match a path, the last decides. Each rule names one `kind` and the `paths`
it covers, with the fields its kind takes. It is layered as an agent's permission settings are, a later
layer's match winning over an earlier one's:

1. **Found**: what the coordinating tool's analysis writes from git's view of the repository, its lockfiles
   and manifests, its known toolchains and its own scripts. A guess, replaced whole at each reading, and
   never edited by hand.
2. **The repository's**: the rule file tracked beside the gates and lanes files, shared by every machine,
   changed by landing a change. It is read at the commit the line stands at, never from a working file or a
   session's branch, so a session's change to it acts once it lands.
3. **The person's**: their own file on their own machine, for every repository there and for this one. It
   is where a restriction (a deny or an ask over running, copying or sharing) is added; an allow may also
   come from the place's appointed supervisor under its role's grants.
4. **The floor**, which no layer sets aside: a path git tracks is git's; a nested repository, a linked
   worktree or a bare repository is never touched; a harness's or an editor's own settings are never
   copied; and nothing the tool did not make is deleted, only set aside.

Nothing waits on anyone accepting an entry, and nothing is restricted beyond the floor until a person adds
a restriction. A change to any layer acts at the next open, take or finish, never on a tree under way.

The repository's layer holds what only its owner knows; the found layer already covers what a signature
recognises, and a field a winning rule leaves out takes the signature's value for that kind and folder.
The kinds, with their fields:

- `dependency`: each folder a package manager installs from a lockfile, readable at any path, with its
  `install` command, its `key` (the tracked lockfile and the manager's own configuration, such as an
  `.npmrc`, and the repository's statement that no other file changes what the install makes), the
  `toolchain` commands whose output keys it with them, the `env` its install is given, the `caches` its
  tools write inside it, `bound` where it names its own path and so is installed in each tree and never
  stored, and `share`. Its paths are everything one install makes. One with no install and no key acts on
  nothing.
- `intermediate`: what the build tool checks itself and makes again when stale, valid only at its
  path: `obj/`, `target/`, `.gradle/`, a CMake build tree, a virtual environment whose launchers name it,
  with the `toolchain` commands that key it.
- `output`: what a runner uses without building first, or what is cheap to make again: `bin/`, `dist/`,
  a folder a host serves, a generated index. The kind belongs to the runner, not the folder: a folder a
  script runs without building is an output even where a tool checks it.
- `cache`: a tool's own cache inside the tree, keyed by its `toolchain`.
- `local`: a machine-local input, with `copy`, to be copied in read-only from the checkout.
- `scratch`: everything else ignored that no rule names; it goes with its session's record.
- `repository` and `unknown`: another repository or tree, which the floor keeps whole, and a path nothing
  names.
- A rule matches the ignored paths git lists at their tops, never a path inside one: a dependency's or a
  repository's folder is one unit. Never name a path git tracks: it is git's. A tracked entry point under
  `bin/` means naming the outputs folder by folder.

**Proposing them.** Read the build files, never the folder names: the units, their references and the
settings they import. Write each rule with its reason, as a change on the repository's own branch for the
person to accept where it lands; for another repository, send the request to whoever works there. Where
the tool's analysis already finds a rule, leave it out of the repository's layer and write only what the
analysis cannot know. Then check it. Where the coordinating tool runs, its room check reads the lanes
file, the rule file and git's tracked files; it refuses overlapping rooms, a unit or a lockfile split
across rooms, a file written from two rooms and a rule naming a path git tracks, lists every reference
across rooms, and says the claim groups. Without the tool, check the same by hand: every tracked file in
one room or said to be in none, every unit's folder in its room, every lockfile in the room of the unit
beside it.

**Expect few groups.** Where shared settings or generated contracts join most rooms, one session works in
place in that group and the rest take a tree of their own until the builds are made apart. That is the
true answer, not a failed declaration.
