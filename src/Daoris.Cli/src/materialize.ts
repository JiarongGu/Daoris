import type {
  Canon, CommandArgs, CoreSwitch, Harness, Lock, Manifest, Move, PlannedWrite, Rename, SyncPlan,
} from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { readText, sha256, writeBytesAtomic, writeTextAtomic } from './fsx.ts';
import { parseFrontmatter, renderCanonFile, stripFrontmatter, stripHeader } from './document.ts';
import { significantTokens, containment } from './twins.ts';
import { isSwitchedOff, readCanon, resolveCanonRoot, resolveSelection } from './canon.ts';
import { lockIndex, readLock, readManifest, writeLock } from './config.ts';
import { readTier, rosterExtras } from './indexgen.ts';
import { renderTier, spanBody, tierRuleBody } from './tierrender.ts';
import { ensureImport, findRegion, writeRegion } from './region.ts';
import { resolveHarness } from './harness.ts';
import {
  contained, folderTiers, isFile, layoutFields, lockLayout, planLeftBehind, pruneEmpty,
} from './layout.ts';
import { mirrorSources, planMirrors } from './mirror.ts';
import { planRooms } from './rooms.ts';
import { describeLink, linkProblems, shortLink } from './links.ts';
import { declaredPaths, describeDocumentLink, documentLinks, present, shortDocumentLink } from './documents.ts';
import { DaorisError } from './errors.ts';

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
  // What installs, after the manifest's confirmations (D71): a confirmed switch takes a core row out
  // of the selection, so from here on it is a row the canon no longer asks for — D19's new cells.
  const selection = resolveSelection(canon, manifest.packs, manifest.switchedOff ?? {});
  const selected = selection.files;
  const writes: PlannedWrite[] = [];
  const drifted: string[] = [];
  const collisions: string[] = [];

  // The always-loaded tier is a SPAN in a file the repository owns, not a directory of its own
  // (D59). Which tier that is comes from the harness descriptor, so a second harness is a descriptor
  // rather than a branch here.
  const harness = manifest.harnessDescriptor ?? resolveHarness(manifest.harness);

  // 🔴 THE MOVE (D117 §5.4). The lock, not the manifest, says where the files are: a manifest flipped
  // to another layout names the new root while every file is still at the old. Files are read and
  // deleted at `from` and written at `to`; with the two equal, every line below is D19 as it was.
  const was = lockLayout(root, lock, manifest);
  const from = was.target;
  const to = manifest.target;
  const moving = from !== to;
  const moves: Move[] = [];
  const moveCollisions: { from: string | null; to: string }[] = [];
  // A mirror the lock records is Daoris's file, so a move landing on its path (flipping back to the
  // older layout) takes the path; the mirror table speaks for one that was edited.
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

  for (const file of selected) {
    const body = readText(join(canon.root, file.source));
    const region = regionOf(file.target);

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

      let state: PlannedWrite['state'] = 'create';
      if (onDisk !== null) {
        state = onDisk === trimmed ? 'unchanged' : 'update';
        // The same reading as a file: against the LOCK, not against the canon (D13). A body matching
        // the canon is never drift whatever the lock says — that is the state right after `upstream`.
        if (entry && sha256(onDisk) !== entry.sha256 && onDisk !== trimmed) drifted.push(file.target);
      }

      const { meta } = parseFrontmatter(body, []);
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

    // A canonical document the lock holds under the old root: the move's cells, before D19's apply
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
        // D19's reading: a body matching the canon now is the state right after `upstream`, not drift.
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
   * What a target USED to say, before this sync rewrites anything.
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

  // 🔴 THE MIGRATION (design §5). A repository that adopted before D59 has this rule as a FILE, and
  // its lock entry says so. The rule is still wanted — it has just moved into the region — so the
  // retirement rule above does not catch it, and without this the adopter ends up holding it in both
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
  // thing sync does, and it was the least guarded: a retained file that drifted
  // refused, while a retired one was deleted silently. It is also the worst
  // moment to lose an edit, because the canonical file it belonged to is gone,
  // so `upstream` is no longer a way to save it.
  // 🔴 Span-aware, or this silently stops working. A retired rule that lives in the REGION has no
  // file at `target`, so a check that only stats a path finds nothing, calls it an untouched
  // retirement, and deletes the edit without a word — which is precisely the fourth bug D19's last
  // row was written from, reintroduced by the tier moving.
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
  // one (D71): it is a decision the manifest names, and the canonical file still exists — so an edit
  // to it refuses as drift would, with `upstream` still a real route, not as an edited retirement.
  const switchedOffAt = (target: string) => isSwitchedOff(selection.switchedOff, locked.get(target)!);
  const retiring = deletes.filter((target) => !switchedOffAt(target));
  const editedRetirements = retiring.filter(edited);
  const editedSwitchedOff = deletes.filter((target) => switchedOffAt(target) && edited(target));

  // 🔴 Never paired as a rename: a pack's replacement reads like the core row it switches off, by
  // design, and "renamed task-lifecycle -> ticket-lifecycle" would hide the decision behind a move.
  const renames = detectRenames({ writes, deletes: retiring, previous: previousAt });
  // A span that leaves is gone once the region is rewritten; only a FILE-backed entry (pre-D59, or a
  // migrating rule's old file) has anything on disk at its target for `applySync` to remove.
  const leavesRegion = deletes.filter((target) => locked.get(target)?.in !== undefined);

  // The mirror (D117 §3.2), over the source tree as this sync leaves it: the canonical files with the
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

  const rooms = planRooms({ root, manifest, lock, harness });
  const behind = moving
    ? planLeftBehind({
      root, from, to, was: was.harness, now: harness,
      owned: (target) => locked.has(target), mirrors: mirrored,
    })
    : { leftBehind: [], bothRoots: [], keptRules: [] };

  // Every path this sync would write, checked for a link before anything is (D117 §5.4, the last table).
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
    ],
  });

  // The declared documents (D122 §2.7). Nothing here is written to them: the table names them, so one
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

  return {
    writes, deletes, leavesRegion, drifted, collisions, renames, editedRetirements,
    switchedOff: selection.switchedOff, offers: selection.offers, editedSwitchedOff,
    layout: { from: { harness: was.harness.id, target: from }, to: { harness: harness.id, target: to } },
    moves, moveCollisions, ...behind, mirrors, rooms, links, documents,
  };
}

/**
 * A canonical file renamed upstream arrives as a delete plus an add, which loses
 * nothing and explains nothing. Pair them up by CONTENT rather than by a
 * declared `renamedFrom` field: a metadata ledger is a second source of truth
 * that can claim a rename which never happened, while content cannot lie about
 * what moved. This is how version control has always done it, for the same reason.
 *
 * Reporting only — the delete and the create still happen exactly as before.
 * Deliberately conservative: below the bar it stays a retirement plus an
 * addition, which is the honest description of two unrelated changes.
 */
const RENAME_SIMILARITY = 0.6;

function detectRenames(
  { writes, deletes, previous }:
  { writes: PlannedWrite[]; deletes: string[]; previous: (target: string) => string | null },
): Rename[] {
  const created = writes.filter((write) => write.state === 'create');
  if (!created.length || !deletes.length) return [];

  const renames: Rename[] = [];
  const claimed = new Set<string>();
  for (const from of deletes) {
    const was = previous(from);
    if (was === null) continue;
    const old = significantTokens(was);

    let best: { target: string; score: number } | null = null;
    for (const write of created) {
      // 🔴 Never itself. A rule MIGRATING out of its old file into the region is a delete and a
      // create at the same target, and pairing those reads as "x was renamed to x".
      if (write.target === from) continue;
      if (claimed.has(write.target)) continue;
      const score = containment(old, significantTokens(write.content));
      if (score >= RENAME_SIMILARITY && (!best || score > best.score)) {
        best = { target: write.target, score };
      }
    }
    if (best) {
      claimed.add(best.target);
      renames.push({ from, to: best.target });
    }
  }
  return renames;
}

/**
 * What actually changed in the doctrine since this repo last synced.
 *
 * Computed from the lock and the shipped canon, so it needs no network and no
 * version control — the lock already records a per-file hash, which is a better
 * marker than "commits since last run" because it survives a shallow clone.
 *
 * Bodies are compared with the provenance header stripped: a version bump
 * rewrites every header, and listing all of them as changes is noise that
 * teaches people to stop reading the list. A file the repo has edited itself is
 * skipped rather than guessed at — that is drift, and it is reported as drift.
 */
export function planChanges(
  { root, manifest, canon, lock }:
  { root: string; manifest: Manifest; canon: Canon; lock: Lock | null },
): { added: string[]; changed: string[]; retired: string[] } {
  const locked = lockIndex(lock);
  const selection = resolveSelection(canon, manifest.packs, manifest.switchedOff ?? {});
  const selected = selection.files;
  const added: string[] = [];
  const changed: string[] = [];

  const harness = manifest.harnessDescriptor ?? resolveHarness(manifest.harness);

  for (const file of selected) {
    const entry = locked.get(file.target);
    if (!entry) {
      added.push(file.target);
      continue;
    }

    if (entry.in) {
      // A span (D59): what is held is the rule's BODY, so the comparison is body against body. Read
      // by the LOCK's provenance, as every other reader does — what is on disk is what was written.
      const body = spanBody(root, harness, entry);
      // Untouched here, and different from what the canon now says: an upstream improvement.
      if (body === null || sha256(body) !== entry.sha256) continue;
      if (body !== stripFrontmatter(readText(join(canon.root, file.source)))) changed.push(file.target);
      continue;
    }

    const abs = join(root, manifest.target, file.target);
    if (!existsSync(abs)) continue;
    const onDisk = readText(abs);
    if (sha256(onDisk) !== entry.sha256) continue;
    if (stripHeader(onDisk) !== readText(join(canon.root, file.source))) changed.push(file.target);
  }

  const wanted = new Set(selected.map((file) => file.target));
  // A row a confirmed switch takes out is not RETIRED — the canon still ships it (D71) — and `status`
  // names it as switched off, with its pack and reason, rather than here.
  return {
    added,
    changed,
    retired: [...locked.keys()].filter((t) => !wanted.has(t) && !isSwitchedOff(selection.switchedOff, locked.get(t)!)),
  };
}

/**
 * Every path daoris writes or deletes must resolve INSIDE the root that declares it (D18, and since
 * D117 over every declared root: the target, the lock's old root, the mirror root, the rooms).
 *
 * D5 makes anything absent from the lock invisible to the tool; this is its
 * complement, and it was missing. A lock entry containing `..` escaped the
 * target and reached arbitrary files — and the lock is *generated*, so it is
 * exactly the file nobody reads closely in review. A merge-mangled entry and a
 * crafted one in a pull request both arrive at the same delete.
 *
 * Refuses rather than sanitising: a path that tried to leave is not a path to
 * quietly correct, it is a sign the lock is wrong or hostile.
 */
function containedPath(root: string, target: string, rel: string): string {
  return contained(root, target, target === '' ? rel : `${target}/${rel}`);
}

/**
 * The refusals `--force` never overrides. Each is a choice only the repository can make: what to put
 * where a link stands, where its own documents go, what its rooms say, and which file a declared record
 * really is. `--force` discards an EDIT; none of these is one.
 */
function refuseWhatForceCannot(plan: SyncPlan): void {
  if (plan.links.length) {
    throw new DaorisError(
      `${plan.links.length} path(s) daoris writes are not a plain file or folder, and daoris will not write `
      + `through them:\n${plan.links.map((problem) => `  ${describeLink(problem)}`).join('\n')}`,
      1);
  }
  if (plan.leftBehind.length || plan.bothRoots.length) {
    const lines = [
      ...plan.leftBehind.map((doc) => `  ${doc.path} — ${doc.move}`),
      ...plan.bothRoots.map((pair) => `  ${pair.old} and ${pair.neu} — two copies, under both roots: keep one`),
    ];
    throw new DaorisError(
      `the move to ${plan.layout.to.target}/ would leave this repository's own documents where no index lists `
      + `them:\n${lines.join('\n')}\n`
      + "  moving them is this repository's own act (D5), and which copy is meant is its call. Then 'daoris sync'",
      1);
  }
  if (plan.rooms.missing.length) {
    throw new DaorisError(
      `${plan.rooms.missing.length} declared room(s) with no instructions of their own: ${plan.rooms.missing.join(', ')}\n`
      + "  a room's text is this repository's own and daoris never writes it — write it, or take the room\n"
      + "  out of daoris.json's rooms, then 'daoris sync'",
      1);
  }
  if (plan.documents.links.length) {
    throw new DaorisError(
      `${plan.documents.links.length} declared document(s) the region would send every session through a link to:\n`
      + `${plan.documents.links.map((link) => `  ${describeDocumentLink(link)}`).join('\n')}\n`
      + "  then 'daoris sync'",
      1);
  }
}

export function applySync(
  { root, manifest, plan, canonVersion, force }:
  { root: string; manifest: Manifest; plan: SyncPlan; canonVersion: string; force?: boolean },
): Lock {
  refuseWhatForceCannot(plan);

  const mirrorReader = manifest.harnessDescriptor?.mirror?.reader ?? 'the agent that reads only there';
  if ((plan.collisions.length || plan.moveCollisions.length || plan.mirrors.collisions.length) && !force) {
    const lines = [
      ...(plan.collisions.length ? [`this repo already has its own ${plan.collisions.join(', ')}`] : []),
      ...plan.moveCollisions.map((collision) => collision.from
        ? `${collision.to} is this repo's own, and daoris is moving ${collision.from} there`
        : `${collision.to} is this repo's own, where daoris writes the canonical file`),
      ...(plan.mirrors.collisions.length
        ? [`this repo already has its own ${plan.mirrors.collisions.join(', ')}, at the mirror path(s) daoris `
          + `writes for ${mirrorReader} — a skill kept for that agent alone needs a name no source skill has`]
        : []),
    ];
    throw new DaorisError(
      `${lines.join('\n')}\n` +
        `  daoris did not write those files and will not overwrite them. Move each aside\n` +
        `  (or fold anything worth keeping into the canon), then 'daoris sync' — or accept\n` +
        `  the canonical version with 'daoris sync --force'`,
      1,
    );
  }
  if ((plan.drifted.length || plan.mirrors.edited.length) && !force) {
    const lines: string[] = [];
    if (plan.drifted.length) {
      lines.push(
        `${plan.drifted.length} vendored file(s) edited locally: ${plan.drifted.join(', ')}\n` +
          `  promote the edit with 'daoris upstream <file>', or discard it with 'daoris sync --force'`);
    }
    // D117 §3.3's second telling: the mirror is named with its source, and what to do with the edit.
    for (const mirror of plan.mirrors.edited) {
      lines.push(mirror.sourceEdited
        ? `${mirror.path} — a mirror of ${mirror.of}, edited here, and ${mirror.of} was edited too: two edits `
          + 'are a merge only a person can make. Keep both aside, put the one you mean in the source, then '
          + "'daoris sync'"
        : `${mirror.path} — a mirror of ${mirror.of}, edited here. Move the edit into ${mirror.of} and `
          + `'daoris sync'${mirror.canonical ? `, or promote it with 'daoris upstream ${mirror.path}'` : ''}; `
          + "or discard it with 'daoris sync --force'");
    }
    throw new DaorisError(lines.join('\n'), 1);
  }
  if ((plan.editedRetirements?.length || plan.mirrors.editedGone.length) && !force) {
    const lines: string[] = [];
    if (plan.editedRetirements.length) {
      lines.push(
        `${plan.editedRetirements.length} file(s) retired upstream, but edited here: ` +
          `${plan.editedRetirements.join(', ')}\n` +
          `  these are leaving the canon, so 'daoris upstream' cannot save the edit. Copy each\n` +
          `  aside to keep it as this repo's own document, then 'daoris sync' — or accept the\n` +
          `  retirement and lose the edit with 'daoris sync --force'`);
    }
    if (plan.mirrors.editedGone.length) {
      lines.push(
        `${plan.mirrors.editedGone.length} mirror(s) of a skill that went, edited here: ${plan.mirrors.editedGone.join(', ')}\n`
          + '  nothing can receive the edit: copy each aside as a skill of this repo\'s own, then\n'
          + "  'daoris sync' — or discard it with 'daoris sync --force'");
    }
    throw new DaorisError(lines.join('\n'), 1);
  }
  if (plan.editedSwitchedOff?.length && !force) {
    // Unlike an edited retirement, the canonical file still exists — so `upstream` is a real route,
    // and the refusal says so first (D71).
    const by = (target: string) =>
      plan.switchedOff.find((row) => target === row.target || target.startsWith(`${row.target}/`))?.by;
    throw new DaorisError(
      `${plan.editedSwitchedOff.length} file(s) switched off by a pack, but edited here: ` +
        `${plan.editedSwitchedOff.map((target) => `${target} (by '${by(target)}')`).join(', ')}\n` +
        `  promote the edit with 'daoris upstream <file>' first — the canonical file still exists —\n` +
        `  or copy it aside as this repo's own document, then 'daoris sync'; or withdraw the switch in\n` +
        `  daoris.json; or discard the edit with 'daoris sync --force'`,
      1,
    );
  }

  // Resolve every path BEFORE touching anything, so a bad entry anywhere aborts
  // the whole apply rather than half-applying it. Written at the manifest's root, read and deleted at
  // the lock's (D117 §5.1): the two are one folder unless this sync is a move.
  const from = plan.layout.from.target;
  const to = plan.layout.to.target;
  const files = plan.writes.filter((write) => !write.in);
  const spans = plan.writes.filter((write) => write.in);

  const writes = files.map((write) => ({
    write,
    abs: containedPath(root, to, write.target),
  }));
  // 🔴 Never a span's old path: what sits there now is the repository's own file (REV3).
  const leaving = new Set(plan.leavesRegion ?? []);
  const deletes = [
    ...plan.deletes.filter((target) => !leaving.has(target)).map((target) => containedPath(root, from, target)),
    ...plan.moves.map((move) => containedPath(root, from, move.target)),
  ];
  // A mirror is written under the manifest's mirror root and retired under the one its lock was
  // written for — the same folder unless the layout changed.
  const writeRoot = manifest.harnessDescriptor?.mirror?.root ?? '';
  const retireRoot = resolveHarness(plan.layout.from.harness).mirror?.root ?? manifest.harnessDescriptor?.mirror?.root;
  const mirrorWrites = plan.mirrors.writes.map((write) => ({ write, abs: contained(root, writeRoot, write.path) }));
  const mirrorRetire = [
    ...plan.mirrors.retire,
    ...(force ? plan.mirrors.editedGone : []),
  ].map((path) => {
    // D18 for the mirror's half: a lock written under a layout with no mirror root records no mirror,
    // so one there is a crafted or mangled entry, and the repository root is no containment for a delete.
    if (retireRoot === undefined) {
      throw new DaorisError(
        `daoris.lock records a mirror at '${path}', but neither its layout nor the manifest's keeps one — `
        + 'refusing to touch it.\n  daoris.lock is corrupt or has been tampered with');
    }
    return contained(root, retireRoot, path);
  });

  for (const { write, abs } of writes) {
    if (write.state !== 'unchanged' || force) writeTextAtomic(abs, write.content);
  }
  for (const abs of deletes) {
    rmSync(abs, { force: true });
  }
  for (const { write, abs } of mirrorWrites) {
    if (write.state === 'unchanged' && !force) continue;
    if (typeof write.content === 'string') writeTextAtomic(abs, write.content);
    else writeBytesAtomic(abs, write.content);
  }
  // 🔴 Never a path this sync just wrote: flipping back to the older layout puts the canonical skill
  // exactly where its mirror was.
  const written = new Set([...writes.map(({ abs }) => abs), ...mirrorWrites.map(({ abs }) => abs)]);
  const retired = mirrorRetire.filter((abs) => !written.has(abs));
  for (const abs of retired) rmSync(abs, { force: true });
  // A folder a move or a retired mirror emptied goes too, and only when empty (D117 §5.2).
  pruneEmpty(root, [...(from !== to ? deletes : []), ...retired]);

  const layout = layoutFields(manifest.harnessDescriptor ?? resolveHarness(manifest.harness), to);
  const lock: Lock = {
    canonVersion,
    source: manifest.source,
    entries: plan.writes.map(({ pack, source, target, sha256: digest, in: within }) => ({
      pack,
      source,
      target,
      canonVersion,
      sha256: digest,
      ...(within ? { in: within } : {}),
    })),
    // What is off, and by whom, so `check` can say so offline (D71, D8).
    ...(plan.switchedOff?.length
      ? { switchedOff: plan.switchedOff.map(({ target, by }) => ({ target, by })) }
      : {}),
    // Where the files are now, every mirror and every room (D117 §5.1), each only when there is one.
    ...(layout ?? {}),
    ...(plan.mirrors.writes.length
      ? { mirrors: plan.mirrors.writes.map(({ path, of, sha256: digest }) => ({ path, of, sha256: digest })) }
      : {}),
    ...(plan.rooms.records.length ? { rooms: plan.rooms.records } : {}),
  };

  // The region LAST, after the on-demand tiers are on disk: its roster lists what is actually there,
  // local documents included, and a roster written before the files it names would be a roster of the
  // previous sync.
  writeSpans({ root, manifest, spans, canonVersion, lock, off: plan.switchedOff ?? [], from });
  writePointers(root, plan);
  writeLock(root, lock);
  return lock;
}

/** Each room's pointer, and each undeclared room's region taken back out (D117 §5.4). */
function writePointers(root: string, plan: SyncPlan): void {
  for (const pointer of plan.rooms.pointers) {
    if (pointer.content !== null) writeTextAtomic(contained(root, '', pointer.path), pointer.content);
  }
  for (const pointer of plan.rooms.unpoint) {
    const abs = contained(root, '', pointer.path);
    if (pointer.content === null) rmSync(abs, { force: true });
    else writeTextAtomic(abs, pointer.content);
  }
}

/**
 * Assemble every span into its region, and point the harness's own file at it (D59).
 *
 * @remarks
 * One write per region file, never one per rule: the region is a single span, and eight successive
 * rewrites of the same file would each re-read what the last one wrote.
 */
function writeSpans(
  { root, manifest, spans, canonVersion, lock, off, from }:
  {
    root: string; manifest: Manifest; spans: PlannedWrite[]; canonVersion: string; lock: Lock;
    off: CoreSwitch[]; from: string;
  },
): void {
  if (!spans.length) return;
  const harness: Harness = manifest.harnessDescriptor ?? resolveHarness(manifest.harness);

  for (const [file, within] of groupBy(spans, (write) => write.in!)) {
    const tier = Object.values(harness.tiers).find((candidate) => candidate.region?.file === file);
    if (!tier?.region) continue;

    const knowledge = harness.tiers.knowledge;
    const skills = harness.tiers.skills;
    const input = {
      rules: within.map((write) => ({
        file: { pack: write.pack, source: write.source, target: write.target },
        text: write.content,
        // Carried from `planSync`, where the canon was in hand — a span holds its body alone.
        ...(write.meta ? { meta: write.meta } : {}),
      })),
      knowledge: knowledge?.dir
        ? readTier({ root, target: manifest.target, tier: knowledge.dir, lock })
        : [],
      skills: skills?.dir
        ? readTier({
          root,
          target: manifest.target,
          tier: skills.dir,
          lock,
          ...(skills.entryFile ? { entryFile: skills.entryFile } : {}),
        })
        : [],
      version: canonVersion,
      target: manifest.target,
      // The rows this repository switched off (D71): the session loading the region learns what is
      // not there, and which pack said so, rather than meeting a doctrine with a silent hole in it.
      off,
      // The mirror sentence, the rooms (D117 §5.3) and where the records are (D122 §2.7), rendered by the
      // function `check` rebuilds them with.
      ...rosterExtras(root, harness, manifest.target, manifest.rooms ?? [], manifest.documents ?? []),
    };

    // 🔴 The RAW bytes: `writeRegion` keeps everything outside the region byte for byte and takes the
    // file's own line ending — both of which a normalized read had already erased (REV3).
    const abs = join(root, file);
    const held = existsSync(abs) ? readFileSync(abs, 'utf8') : '';
    const body = renderTier(input);
    writeTextAtomic(abs, writeRegion(held, tier.region.name, body));

    // 🔴 The migration's last piece. A repository that adopted before D59 has a generated
    // `RULES_INDEX.md` in the directory the tier just left. It is not in the lock — generated files
    // never were — so no retirement rule reaches it, and it would sit there as a roster that looks
    // authoritative and is frozen at the moment the tier moved. Only ever a file daoris wrote.
    const legacy = join(root, from, 'rules', 'RULES_INDEX.md');
    if (existsSync(legacy)) rmSync(legacy, { force: true });

    // The pointer, for the one harness that reads another file and follows imports.
    if (harness.pointer) {
      const pointer = join(root, harness.pointer.file);
      const made = ensureImport(existsSync(pointer) ? readFileSync(pointer, 'utf8') : null, harness.pointer.imports);
      if (made !== null) writeTextAtomic(pointer, made);
    }
  }
}

function groupBy<T>(items: T[], key: (item: T) => string): Map<string, T[]> {
  const groups = new Map<string, T[]>();
  for (const item of items) {
    const at = key(item);
    if (!groups.has(at)) groups.set(at, []);
    groups.get(at)!.push(item);
  }
  return groups;
}

export function commandSync({ root, argv, write, packageRoot }: CommandArgs): ExitCode {
  const manifest = readManifest(root);
  const canon = readCanon(resolveCanonRoot(packageRoot));
  const plan = planSync({ root, manifest, canon, lock: readLock(root) });

  // A rename is reported in place of the delete and the add it is made of,
  // because "these two are the same rule" is the part a reader cannot recover.
  const renamedFrom = new Set(plan.renames.map((rename) => rename.from));
  const renamedTo = new Set(plan.renames.map((rename) => rename.to));

  // A switched-off row's files leave by the same delete a retirement uses, and are named apart from
  // one (D71): what happened is a decision the manifest records, not the canon letting go of a file.
  const lockEntries = lockIndex(readLock(root));
  const offDeletes = new Set(plan.deletes.filter((target) => {
    const entry = lockEntries.get(target);
    return entry !== undefined && isSwitchedOff(plan.switchedOff, entry);
  }));

  // 🔴 Never silent (D71): every sync names what is off and every offer still waiting, so neither a
  // missing core rule nor a pack's pending switch can be a surprise to the person reading the output.
  const sayWhatIsOff = () => {
    for (const row of plan.switchedOff) write(`  off       ${row.target} (switched off by pack '${row.by}')`);
    for (const offer of plan.offers) {
      write(`  offered   ${offer.target} — pack '${offer.by}' would switch it off: ${offer.because}`);
      write(`            it stays on; to confirm, add to daoris.json: "switchedOff": { "${offer.target}": "${offer.by}" }`);
    }
  };

  // What a move and the layout do, said beside what D19 does (D117 §5.2): each move with both paths,
  // each pointer, and every old `rules/` file that stays where only one agent reads it.
  const moved = new Set(plan.moves.map((move) => move.target));
  const sayTheLayout = (dry: boolean) => {
    for (const move of plan.moves) write(`  moved     ${move.from} -> ${move.to}`);
    for (const pointer of plan.rooms.pointers) {
      if (pointer.state !== 'unchanged') write(`  pointer   ${pointer.path}`);
    }
    for (const pointer of plan.rooms.unpoint) write(`  pointer   ${pointer.path} (removed: no longer a room)`);
    for (const path of plan.keptRules) {
      write(`  kept      ${path} — read by Claude Code alone; its text could go in AGENTS.md, above the region`);
    }
    // Said, never refused (D122 §2.8): an absent record is `check`'s fact, and the table still names it.
    for (const doc of plan.documents.missing) {
      write(`  document  ${doc.path} (${doc.role}) is declared in daoris.json, and absent — 'daoris check' fails until it is there`);
    }
    if (!dry) return;
    for (const mirror of plan.mirrors.writes) {
      if (mirror.state !== 'unchanged') write(`  mirror    ${mirror.path}`);
    }
    for (const path of plan.mirrors.retire) write(`  retire    ${path}`);
  };

  if (argv.includes('--dry-run')) {
    for (const rename of plan.renames) write(`  renamed   ${rename.from} -> ${rename.to}`);
    for (const entry of plan.writes) {
      if (entry.state !== 'unchanged' && !renamedTo.has(entry.target) && !moved.has(entry.target)) {
        write(`  ${entry.state.padEnd(9)} ${entry.target}`);
      }
    }
    for (const target of plan.deletes) {
      if (!renamedFrom.has(target) && !offDeletes.has(target)) write(`  retire    ${target}`);
    }
    sayTheLayout(true);
    sayWhatIsOff();
    for (const problem of plan.links) write(`  LINK      ${problem.path} — ${shortLink(problem)}`);
    for (const link of plan.documents.links) write(`  LINK      ${link.declared} (${link.role}) — ${shortDocumentLink(link)}`);
    for (const doc of plan.leftBehind) write(`  LEFT BEHIND ${doc.path} — ${doc.move}`);
    for (const pair of plan.bothRoots) write(`  TWO COPIES ${pair.old} and ${pair.neu}`);
    for (const path of plan.rooms.missing) write(`  NO ROOM   ${path} — declared in daoris.json, and absent`);
    for (const target of plan.drifted) write(`  DRIFTED   ${target}`);
    for (const mirror of plan.mirrors.edited) write(`  DRIFTED   ${mirror.path} — a mirror of ${mirror.of}, edited here`);
    for (const target of plan.collisions) write(`  COLLIDES  ${target} (this repo's own)`);
    for (const collision of plan.moveCollisions) write(`  COLLIDES  ${collision.to} (this repo's own)`);
    for (const path of plan.mirrors.collisions) write(`  COLLIDES  ${path} (this repo's own, at a mirror path)`);
    for (const target of plan.editedRetirements) write(`  AT RISK   ${target} (retired, but edited here)`);
    for (const target of plan.editedSwitchedOff) write(`  AT RISK   ${target} (switched off, but edited here)`);
    for (const path of plan.mirrors.editedGone) write(`  AT RISK   ${path} (a mirror of a skill that went, edited here)`);
    write(`daoris: ${plan.writes.length} file(s) selected, ${plan.deletes.length - offDeletes.size} to retire`);
    return refuses(plan) ? 1 : 0;
  }

  const force = argv.includes('--force');
  // Before the lines --force prints: none of these is an edit it could discard.
  refuseWhatForceCannot(plan);

  // --force is the only way to lose work with this tool. A refusal names the
  // file it is protecting; the override that overrules that refusal has to name
  // it too, or nothing anywhere records what was destroyed.
  if (force) {
    for (const target of plan.drifted) write(`  overwrote ${target} (local edit discarded)`);
    for (const target of plan.collisions) write(`  overwrote ${target} (this repo's own file)`);
    for (const collision of plan.moveCollisions) write(`  overwrote ${collision.to} (this repo's own file)`);
    for (const mirror of plan.mirrors.edited) write(`  overwrote ${mirror.path} (edit to a mirror discarded)`);
    for (const path of plan.mirrors.collisions) write(`  overwrote ${path} (this repo's own file)`);
    for (const target of plan.editedRetirements) write(`  discarded ${target} (retired, edited here)`);
    for (const target of plan.editedSwitchedOff) write(`  discarded ${target} (switched off, edited here)`);
    for (const path of plan.mirrors.editedGone) write(`  discarded ${path} (a mirror of a skill that went, edited here)`);
  }

  applySync({ root, manifest, plan, canonVersion: canon.version, force });
  for (const rename of plan.renames) write(`  renamed   ${rename.from} -> ${rename.to}`);
  sayTheLayout(false);
  sayWhatIsOff();
  const retired = plan.deletes.length - plan.renames.length - offDeletes.size;
  const mirrored = plan.mirrors.writes.length ? `; mirrored ${plan.mirrors.writes.length}` : '';
  write(`daoris: synced ${plan.writes.length} file(s); retired ${retired}${mirrored}`);
  return 0;
}

/** Whether applying this plan would refuse without `--force` — the dry run's exit code. */
function refuses(plan: SyncPlan): boolean {
  return [
    plan.links, plan.documents.links, plan.leftBehind, plan.bothRoots, plan.rooms.missing, plan.drifted, plan.collisions,
    plan.moveCollisions, plan.editedRetirements, plan.editedSwitchedOff,
    plan.mirrors.edited, plan.mirrors.collisions, plan.mirrors.editedGone,
  ].some((list) => list.length > 0);
}
