import type { Canon, Harness, IndexPlan, Lock, Manifest, Move, PlannedWrite, SyncPlan } from './records.ts';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { readText, sha256 } from '../platform/fsx.ts';
import { parseFrontmatter, renderCanonFile, stripFrontmatter, stripHeader } from './document.ts';
import { isSwitchedOff, resolveSelection } from './canon.ts';
import { holds, keepStates } from './keep.ts';
import { lockIndex } from './config.ts';
import { indexInput } from './indexgen.ts';
import { indexPath, renderIndex, spanBody, structureLine, tierRuleBody } from './tierrender.ts';
import { ensureImport, findRegion } from './region.ts';
import { resolveHarness } from '../layout/harness.ts';
import { folderTiers, isFile, lockLayout, planLeftBehind } from '../layout/layout.ts';
import { mirrorSources, planMirrors } from '../layout/mirror.ts';
import { planRooms } from '../layout/rooms.ts';
import { linkProblems } from '../layout/links.ts';
import { declaredPaths, documentLinks, present } from '../layout/documents.ts';
import { DaorisError } from '../platform/errors.ts';
import { detectRenames } from './syncchanges.ts';

/** Which directory name a tier answers to, for matching a canon target against it. */
function tierPrefix(tier: { region?: { name: string }; dir?: string }, _harness: unknown): string {
  return tier.dir ?? tier.region?.name ?? '';
}

/**
 * Planning is separate from applying so the plan can be printed (--dry-run) or
 * asserted in a test without touching disk.
 *
 * Only files the canon selects are considered; anything else in the target
 * directory is this repo's own and is invisible to the tool.
 */
export function planSync(
  { root, manifest, canon, lock }:
  { root: string; manifest: Manifest; canon: Canon; lock: Lock | null },
): SyncPlan {
  const locked = lockIndex(lock);
  // What installs, after the manifest's confirmations: a confirmed switch takes a core row out
  // of the selection, so from here on it is a row the canon no longer asks for.
  const selection = resolveSelection(canon, manifest.packs, manifest.switchedOff ?? {});
  const selected = selection.files;
  const writes: PlannedWrite[] = [];
  const drifted: string[] = [];
  const collisions: string[] = [];

  // The always-loaded tier is a SPAN in a file the repository owns, not a directory of its own.
  // Which tier that is comes from the harness descriptor, so a second harness is a descriptor
  // rather than a branch here.
  const harness = manifest.harnessDescriptor ?? resolveHarness(manifest.harness);

  // 🔴 THE MOVE. The lock, not the manifest, says where the files are: a manifest flipped
  // to another layout names the new root while every file is still at the old. Files are read and
  // deleted at `from` and written at `to`; with the two equal, every line below is the ordinary sync.
  const was = lockLayout(root, lock, manifest);
  const from = was.target;
  const to = manifest.target;
  const moving = from !== to;
  const moves: Move[] = [];
  const moveCollisions: { from: string | null; to: string }[] = [];
  // A mirror the lock records is Daoris's file, so a move landing on its path (flipping back to the
  // older layout) takes the path; the mirror table speaks for one that is edited.
  const mirrored = new Set((lock?.mirrors ?? []).map((entry) => entry.path));

  const regionOf = (target: string): { file: string; name: string } | null => {
    for (const tier of Object.values(harness.tiers)) {
      if (tier.region && tier.dir === undefined && target.startsWith(`${tierPrefix(tier, harness)}/`)) {
        return tier.region;
      }
    }
    return null;
  };

  const regionText = new Map<string, string>();
  const regionAt = (file: string): string => {
    if (!regionText.has(file)) {
      const abs = join(root, file);
      regionText.set(file, existsSync(abs) ? readText(abs) : '');
    }
    return regionText.get(file)!;
  };

  const keeps = manifest.kept ?? {};

  for (const file of selected) {
    const body = readText(join(canon.root, file.source));
    const region = regionOf(file.target);
    const keep = keeps[file.target];

    if (region) {
      // A span: `content` is the rule's BODY, and the region is assembled in `applySync` from every
      // write that names the same file. Hashed on the body alone, so drift and `upstream` stay per
      // rule inside one span (design §4).
      const held = findRegion(regionAt(region.file), region.name);
      const onDisk = held.kind === 'present'
        ? tierRuleBody(held.body, `${file.pack}/${file.source}`)
        : null;
      const trimmed = stripFrontmatter(body);
      const digest = sha256(trimmed);
      const entry = locked.get(file.target);

      // A rule is read back out of the region by its provenance line, so one holding such a line of its own
      // would be cut short there, and `check` would call the copy just written drifted.
      const structure = keep ? null : structureLine(trimmed);
      if (structure !== null) {
        throw new DaorisError(`the canon's ${file.source} holds a line the region reads as its own structure: ${structure}\n`
          + '  a rule is carried in the region whole, so no line of it may open with a daoris marker; the canon is at fault');
      }
      const { meta } = parseFrontmatter(body, []);

      // Kept: the region is rebuilt with this repository's text of the rule, its lock entry carried as a span where it still
      // names a file (the migration below deletes it). With no text here nothing is written; for a core rule `keepStates` refuses.
      if (keep) {
        if (onDisk !== null) {
          const fresh = { pack: file.pack, source: file.source, target: file.target, canonVersion: canon.version, sha256: sha256(onDisk), in: region.file };
          const was = entry?.in ? entry : { ...fresh, ...(entry ? { canonVersion: entry.canonVersion } : {}) };
          writes.push({ ...file, content: onDisk, sha256: was.sha256, state: 'unchanged', in: region.file, ...(meta ? { meta } : {}), kept: was });
        }
        continue;
      }

      let state: PlannedWrite['state'] = 'create';
      if (onDisk !== null) {
        state = onDisk === trimmed ? 'unchanged' : 'update';
        // The same reading as a file: against the LOCK, not against the canon. A body matching
        // the canon is never drift whatever the lock says — that is the state right after `upstream`.
        if (entry && sha256(onDisk) !== entry.sha256 && onDisk !== trimmed) drifted.push(file.target);
      }

      writes.push({
        ...file,
        content: trimmed,
        sha256: digest,
        state,
        in: region.file,
        ...(meta ? { meta } : {}),
      });
      continue;
    }

    const content = renderCanonFile(file, body, canon.version);
    const digest = sha256(content);
    const abs = join(root, to, file.target);
    const entry = locked.get(file.target);

    // Kept: nothing is written, `--force` included. A file the lock names keeps its entry, so its copy and its index
    // row stay; one the lock never named is this repository's own file at that path, and stays invisible.
    if (keep) {
      const at = join(root, from, file.target);
      if (entry && isFile(at)) writes.push({ ...file, content: readText(at), sha256: entry.sha256, state: 'unchanged', kept: entry });
      continue;
    }

    // A canonical document the lock holds under the old root: the move's cells, before the ordinary apply
    // at the new one. "Settled" is the new file matching what the lock recorded or what the canon
    // renders now — a move finished by hand, or interrupted — and anything else there is the
    // repository's own.
    if (moving && entry && !entry.in) {
      const oldPath = `${from}/${file.target}`;
      const newPath = `${to}/${file.target}`;
      const oldText = isFile(join(root, oldPath)) ? readText(join(root, oldPath)) : null;
      const newText = isFile(abs) && !mirrored.has(newPath) ? readText(abs) : null;
      const settled = (text: string) => sha256(text) === entry.sha256 || sha256(text) === digest;

      let state: PlannedWrite['state'] = 'create';
      if (newText !== null) state = sha256(newText) === digest ? 'unchanged' : 'update';
      if (oldText !== null) {
        // The old file goes once the new is written — or, refused, nothing goes at all.
        moves.push({ target: file.target, from: oldPath, to: newPath });
        // A body matching the canon now is the state right after `upstream`, not drift.
        const untouched = sha256(oldText) === entry.sha256 || stripHeader(oldText) === body;
        if (!untouched) drifted.push(file.target);
        else if (newText !== null && !settled(newText)) moveCollisions.push({ from: oldPath, to: newPath });
      } else if (newText !== null && !settled(newText)) {
        moveCollisions.push({ from: null, to: newPath });
      }
      writes.push({ ...file, content, sha256: digest, state });
      continue;
    }

    let state: PlannedWrite['state'] = 'create';
    if (existsSync(abs)) {
      const onDisk = sha256(readText(abs));
      state = onDisk === digest ? 'unchanged' : 'update';

      if (entry) {
        // In the lock, so compare against what daoris last WROTE, not against
        // what the canon says now. A file still matching its lock entry is
        // untouched, and the difference is an upstream improvement — refusing
        // that would break the very direction the tool exists to serve, and
        // would accuse every consumer of an edit nobody made.
        //
        // A file whose BODY matches the canon is likewise not drift, whatever
        // the lock says: that is the state right after `upstream`, where the
        // edit has already become canonical. Demand --force there and the
        // return path ends by telling the contributor to discard the
        // improvement they just promoted.
        //
        // Bodies, not whole files, because the canon usually ships as a new
        // version soon after — which rewrites the header and would otherwise
        // make the promoted copy look edited all over again.
        if (onDisk !== entry.sha256 && stripHeader(readText(abs)) !== body) {
          drifted.push(file.target);
        }
      } else if (onDisk !== digest) {
        // Not in the lock: the repo wrote this file itself, before it ever
        // adopted daoris. Silently overwriting it would destroy work the tool
        // never had any claim to.
        collisions.push(file.target);
      }
    }
    writes.push({ ...file, content, sha256: digest, state });
  }

  /**
   * What a target says before this sync rewrites anything.
   *
   * A span's old text lives in the region rather than at a path, so both callers below need it:
   * rename detection pairs retirements to additions by content, and an edited retirement is refused
   * by comparing it against the lock. A version that only stats a path finds nothing for a span and
   * reports neither — quietly, which is the worse failure of the two.
   */
  const previousAt = (target: string): string | null => {
    const entry = locked.get(target);
    if (!entry) return null;
    if (entry.in) return spanBody(root, harness, entry, regionAt);

    const abs = join(root, from, target);
    return existsSync(abs) ? readText(abs) : null;
  };

  // A file that left the canon leaves every repo — the thing copy-paste can never do.
  const wanted = new Set(selected.map((file) => file.target));
  const deletes = [...locked.keys()].filter((target) => !wanted.has(target));

  // 🔴 THE MIGRATION. A lock entry that says a rule is a FILE, for a rule now wanted in the region,
  // is not caught by the retirement rule above, and without this the adopter ends up holding it in both
  // places: the region loads it and the stale file sits beside it looking authoritative.
  //
  // A moved file that DRIFTED is not deleted. `upstream` can still save that edit, which is the whole
  // difference between this and an edited retirement, so it refuses as ordinary drift.
  for (const write of writes) {
    if (!write.in) continue;
    const entry = locked.get(write.target);
    if (!entry || entry.in) continue;

    const abs = join(root, from, write.target);
    if (!existsSync(abs)) continue;
    if (sha256(readText(abs)) !== entry.sha256) {
      if (!drifted.includes(write.target)) drifted.push(write.target);
      continue;
    }

    deletes.push(write.target);
  }

  // ...but not one the repo has improved. Retirement is the most destructive
  // thing sync does, and a retained file that drifted refuses, so a retired one must too. It is
  // also the worst moment to lose an edit, because the canonical file it belonged to is gone,
  // so `upstream` is no longer a way to save it.
  // 🔴 Span-aware, or this silently stops working. A retired rule that lives in the REGION has no
  // file at `target`, so a check that only stats a path finds nothing, calls it an untouched
  // retirement, and deletes the edit without a word.
  const edited = (target: string): boolean => {
    const entry = locked.get(target)!;
    if (entry.in) {
      const was = previousAt(target);
      return was !== null && sha256(was) !== entry.sha256;
    }

    const abs = join(root, from, target);
    return existsSync(abs) && sha256(readText(abs)) !== entry.sha256;
  };

  // A row a confirmed switch takes out is deleted exactly like a retirement, and reported apart from
  // one: it is a decision the manifest names, and the canonical file still exists — so an edit
  // to it refuses as drift would, with `upstream` still a real route, not as an edited retirement.
  const switchedOffAt = (target: string) => isSwitchedOff(selection.switchedOff, locked.get(target)!);
  const retiring = deletes.filter((target) => !switchedOffAt(target));
  const editedRetirements = retiring.filter(edited);
  const editedSwitchedOff = deletes.filter((target) => switchedOffAt(target) && edited(target));

  // 🔴 Never paired as a rename: a pack's replacement reads like the core row it switches off, by
  // design, and "renamed task-lifecycle -> ticket-lifecycle" would hide the decision behind a move.
  const renames = detectRenames({ writes, deletes: retiring, previous: previousAt });
  // A span that leaves is gone once the region is rewritten; only a FILE-backed entry (an entry from
  // before spans, or a migrating rule's old file) has anything on disk at its target for `applySync` to remove.
  const leavesRegion = deletes.filter((target) => locked.get(target)?.in !== undefined);

  // The mirror, over the source tree as this sync leaves it: the canonical files with the
  // content they are about to have, and the repository's own skills where they stand at the new root.
  // An old canonical file this sync deletes frees its path, which is how a move from `.claude` hands
  // `.claude/skills/<name>/` to the mirror.
  const canonicalFiles = new Map(writes.filter((write) => !write.in).map((write) => [write.target, write.content]));
  const vacating = new Set([
    ...moves.map((move) => move.from),
    ...deletes.filter((target) => locked.get(target)?.in === undefined).map((target) => `${from}/${target}`),
  ]);
  const mirrors = planMirrors({
    root,
    sources: mirrorSources({
      root, harness, target: to, canonical: canonicalFiles,
      owned: (target) => canonicalFiles.has(target) || locked.has(target),
    }),
    locked: lock?.mirrors ?? [],
    vacating,
    editedSources: new Set(drifted.map((target) => `${to}/${target}`)),
  });

  // The root pointer is written after the files and the region, so damaged markers in it refuse here, as a room's do,
  // and not once half the sync is on disk with the lock behind it.
  if (harness.pointer && writes.some((write) => write.in)) {
    const pointerAt = join(root, harness.pointer.file);
    try {
      ensureImport(isFile(pointerAt) ? readFileSync(pointerAt, 'utf8') : null, harness.pointer.imports);
    } catch (error) {
      if (error instanceof DaorisError) throw new DaorisError(`${harness.pointer.file}: ${error.message}`, error.exitCode);
      throw error;
    }
  }

  const rooms = planRooms({ root, manifest, lock, harness });
  const index = planIndex({ root, harness, lock, from, to, moving, writes, deletes, locked });
  const behind = moving
    ? planLeftBehind({
      root, from, to, was: was.harness, now: harness,
      owned: (target) => locked.has(target), mirrors: mirrored,
    })
    : { leftBehind: [], bothRoots: [], keptRules: [] };

  // Every path this sync would write, checked for a link before anything is.
  const pointerFile = harness.pointer?.file;
  const links = linkProblems(root, {
    files: [
      ...writes.filter((write) => !write.in).map((write) => `${to}/${write.target}`),
      ...mirrors.writes.map((write) => write.path),
      ...rooms.pointers.map((pointer) => pointer.path),
    ],
    folders: [
      to,
      ...folderTiers(harness).map(([, tier]) => `${to}/${tier.dir}`),
      ...(harness.mirror ? [harness.mirror.root] : []),
    ],
    pairs: [
      ...new Set(Object.values(harness.tiers).flatMap((tier) => (tier.region ? [tier.region.file] : []))),
      ...(pointerFile ? [pointerFile] : []),
      ...rooms.pointers.flatMap((pointer) => [`${pointer.room}/${harness.pointer!.imports}`, pointer.path]),
      ...rooms.unpoint.map((pointer) => pointer.path),
      // The index: a link, or a link held as text, refuses like a pointer does.
      index.path,
    ],
  });

  // The declared documents. Nothing here is written to them: the table names them, so one
  // that is a link refuses (every session the region sends there would read through it), and one that
  // is absent is said and not refused — it is `check`'s fact, and blocking every canon update on it
  // would hold the doctrine hostage to a record.
  const declaredDocuments = manifest.documents ?? [];
  const documentLinksFound = documentLinks(root, declaredDocuments);
  const documents = {
    links: documentLinksFound,
    missing: declaredPaths(declaredDocuments)
      .filter((doc) => !documentLinksFound.some((link) => link.declared === doc.path) && present(root, doc.path) === null),
  };

  // The keeps against this canon: each passed over above, said with whether its canon file moved since its judgement.
  const keepsRead = keepStates(manifest, canon, selected, (file) => holds(root, harness, from, file));

  return {
    writes, deletes, leavesRegion, drifted, collisions, renames, editedRetirements,
    switchedOff: selection.switchedOff, offers: selection.offers, editedSwitchedOff,
    kept: keepsRead.kept, keepsDropped: keepsRead.dropped,
    layout: { from: { harness: was.harness.id, target: from }, to: { harness: harness.id, target: to } },
    moves, moveCollisions, ...behind, mirrors, rooms, index, links, documents,
  };
}

/**
 * The index of the on-demand tiers, decided.
 *
 * @remarks
 * Generated, as the region's roster is: a file the lock names is rewritten whatever it says, since
 * `check` named any hand edit stale before. One the lock does not name is the repository's own unless
 * it is already what this sync would write — so its content is decided here, over the tiers as the
 * sync will leave them, rather than read back after the writes.
 *
 * @throws DaorisError when the lock names an index anywhere but its own root's `INDEX.md`: the
 * lock is generated, the file nobody reads closely, and a move deletes the path it names.
 */
function planIndex(
  { root, harness, lock, from, to, moving, writes, deletes, locked }:
  {
    root: string; harness: Harness; lock: Lock | null; from: string; to: string; moving: boolean;
    writes: readonly PlannedWrite[]; deletes: readonly string[]; locked: ReadonlyMap<string, unknown>;
  },
): IndexPlan {
  const path = indexPath(to);
  const recorded = lock?.index;
  if (recorded !== undefined && recorded !== indexPath(from)) {
    throw new DaorisError(
      `daoris.lock records its index at '${recorded}', and daoris writes one only at ${indexPath(from)} — `
      + 'refusing to touch it.\n  daoris.lock is corrupt or has been tampered with');
  }

  // Deletes are made at the lock's root, so they thin the listing only when that is the one written.
  const content = renderIndex(indexInput({
    root, target: to, lock, harness,
    overlay: {
      writes: new Map(writes.filter((write) => !write.in).map((write) => [write.target, write.content])),
      deletes: new Set(moving ? [] : deletes.filter((target) => locked.has(target))),
    },
  }));
  const abs = join(root, path);
  const held = isFile(abs) ? readText(abs) : null;
  return {
    path,
    old: recorded !== undefined && recorded !== path ? recorded : null,
    content,
    state: held === null ? 'create' : held === content ? 'unchanged' : 'update',
    collision: recorded !== path && held !== null && held !== content,
  };
}
