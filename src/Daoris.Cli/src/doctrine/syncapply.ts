import type { CoreSwitch, Harness, Lock, Manifest, PlannedWrite, SyncPlan } from './records.ts';
import { existsSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { writeBytesAtomic, writeTextAtomic } from '../platform/fsx.ts';
import { writeLock } from './config.ts';
import { rosterExtras } from './indexgen.ts';
import { onDemandExtras, renderTier } from './tierrender.ts';
import { ensureImport, writeRegion } from './region.ts';
import { resolveHarness } from '../layout/harness.ts';
import { contained, layoutFields, pruneEmpty } from '../layout/layout.ts';
import { describeLink } from '../layout/links.ts';
import { describeDocumentLink } from '../layout/documents.ts';
import { DaorisError } from '../platform/errors.ts';

function groupBy<T>(items: T[], key: (item: T) => string): Map<string, T[]> {
  const groups = new Map<string, T[]>();
  for (const item of items) {
    const at = key(item);
    if (!groups.has(at)) groups.set(at, []);
    groups.get(at)!.push(item);
  }
  return groups;
}

/**
 * Every path daoris writes or deletes must resolve INSIDE the root that declares it, over every
 * declared root: the target, the lock's old root, the mirror root, the rooms.
 *
 * Anything absent from the lock is invisible to the tool; this is its complement. A lock entry
 * containing `..` would escape the target and reach arbitrary files — and the lock is *generated*,
 * so it is exactly the file nobody reads closely in review. A merge-mangled entry and a
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
export function refuseWhatForceCannot(plan: SyncPlan): void {
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
  // A keep is this repository's own text of a rule, never a core rule's absence, which only a pack's
  // switch the repository confirms takes out.
  const absentCore = (plan.kept ?? []).filter((keep) => keep.absentCore).map((keep) => keep.target);
  if (absentCore.length) {
    throw new DaorisError(
      `${absentCore.length} core file(s) kept in daoris.json and absent here: ${absentCore.join(', ')}\n`
      + "  a keep is this repository's own text of a file, never a core file's absence: write its text, or take the keep\n"
      + "  out of daoris.json's kept, then 'daoris sync'",
      1);
  }
  // A kept file stays where it is, and a move would leave it behind its layout: the move waits for the keep.
  if (plan.layout.from.target !== plan.layout.to.target) {
    const moving = plan.writes.filter((write) => write.kept && !write.in).map((write) => write.target);
    if (moving.length) {
      throw new DaorisError(
        `the move to ${plan.layout.to.target}/ would carry ${moving.length} kept file(s): ${moving.join(', ')}\n`
        + "  the move waits for each: adopt the canon's text, or copy yours aside as this repository's own document, then\n"
        + "  take the keep out of daoris.json's kept and 'daoris sync'",
        1);
    }
  }
}

export function applySync(
  { root, manifest, plan, canonVersion, canonDigest, force }:
  { root: string; manifest: Manifest; plan: SyncPlan; canonVersion: string; canonDigest?: string; force?: boolean },
): Lock {
  refuseWhatForceCannot(plan);

  const mirrorReader = manifest.harnessDescriptor?.mirror?.reader ?? 'the agent that reads only there';
  const indexCollides = plan.index.collision;
  if ((plan.collisions.length || plan.moveCollisions.length || plan.mirrors.collisions.length || indexCollides) && !force) {
    const lines = [
      ...(plan.collisions.length ? [`this repo already has its own ${plan.collisions.join(', ')}`] : []),
      ...plan.moveCollisions.map((collision) => collision.from
        ? `${collision.to} is this repo's own, and daoris is moving ${collision.from} there`
        : `${collision.to} is this repo's own, where daoris writes the canonical file`),
      ...(plan.mirrors.collisions.length
        ? [`this repo already has its own ${plan.mirrors.collisions.join(', ')}, at the mirror path(s) daoris `
          + `writes for ${mirrorReader} — a skill kept for that agent alone needs a name no source skill has`]
        : []),
      // A file of the repository's own where the generated index goes. Renaming it is the
      // answer the canon's changelog gives an adopter.
      ...(indexCollides
        ? [`${plan.index.path} is this repo's own, where daoris writes the index of the knowledge and skills`]
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
    // The mirror is named with its source, and what to do with the edit.
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
    // and the refusal says so first.
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
  // the lock's: the two are one folder unless this sync is a move.
  const from = plan.layout.from.target;
  const to = plan.layout.to.target;
  const files = plan.writes.filter((write) => !write.in);

  const writes = files.map((write) => ({
    write,
    abs: containedPath(root, to, write.target),
  }));
  // 🔴 Never a span's old path: what sits there now is the repository's own file.
  const leaving = new Set(plan.leavesRegion ?? []);
  const deletes = [
    ...plan.deletes.filter((target) => !leaving.has(target)).map((target) => containedPath(root, from, target)),
    ...plan.moves.map((move) => containedPath(root, from, move.target)),
  ];
  // A mirror is written under the manifest's mirror root and retired under the one its lock is
  // written for — the same folder unless the layout changed.
  const writeRoot = manifest.harnessDescriptor?.mirror?.root ?? '';
  const retireRoot = resolveHarness(plan.layout.from.harness).mirror?.root ?? manifest.harnessDescriptor?.mirror?.root;
  const mirrorWrites = plan.mirrors.writes.map((write) => ({ write, abs: contained(root, writeRoot, write.path) }));
  const mirrorRetire = [
    ...plan.mirrors.retire,
    ...(force ? plan.mirrors.editedGone : []),
  ].map((path) => {
    // Containment for the mirror's half: a lock written under a layout with no mirror root records no mirror,
    // so one there is a crafted or mangled entry, and the repository root is no containment for a delete.
    if (retireRoot === undefined) {
      throw new DaorisError(
        `daoris.lock records a mirror at '${path}', but neither its layout nor the manifest's keeps one — `
        + 'refusing to touch it.\n  daoris.lock is corrupt or has been tampered with');
    }
    return contained(root, retireRoot, path);
  });

  for (const { write, abs } of writes) {
    if (write.kept) continue;
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
  // exactly where its mirror sits.
  const written = new Set([...writes.map(({ abs }) => abs), ...mirrorWrites.map(({ abs }) => abs)]);
  const retired = mirrorRetire.filter((abs) => !written.has(abs));
  for (const abs of retired) rmSync(abs, { force: true });

  // The index, after the tiers it lists are on disk: its content is decided over the tiers
  // as this sync leaves them, and a move writes it at the new root and deletes the one the lock named.
  const indexAt = contained(root, to, plan.index.path);
  if (plan.index.state !== 'unchanged' || force) writeTextAtomic(indexAt, plan.index.content);
  const oldIndex = plan.index.old === null ? null : contained(root, from, plan.index.old);
  if (oldIndex !== null && oldIndex !== indexAt) rmSync(oldIndex, { force: true });

  // A folder a move or a retired mirror emptied goes too, and only when empty.
  pruneEmpty(root, [...(from !== to ? deletes : []), ...retired, ...(oldIndex !== null ? [oldIndex] : [])]);

  const layout = layoutFields(manifest.harnessDescriptor ?? resolveHarness(manifest.harness), to);
  const lock: Lock = {
    canonVersion,
    // Which canon of that version: `status` tells two apart by it, and never orders them by it.
    ...(canonDigest !== undefined ? { canonDigest } : {}),
    source: manifest.source,
    // A kept file's entry unchanged: the disk against it stays what this repository changed.
    entries: plan.writes.map(({ pack, source, target, sha256: digest, in: within, kept }) => kept ? { ...kept } : ({
      pack,
      source,
      target,
      canonVersion,
      sha256: digest,
      ...(within ? { in: within } : {}),
    })),
    // What is off, and by whom, so `check` can say so offline.
    ...(plan.switchedOff?.length
      ? { switchedOff: plan.switchedOff.map(({ target, by }) => ({ target, by })) }
      : {}),
    // Where the files are now, every mirror and every room, each only when there is one.
    ...(layout ?? {}),
    // The index this sync writes, which makes it Daoris's to rewrite.
    index: plan.index.path,
    ...(plan.mirrors.writes.length
      ? { mirrors: plan.mirrors.writes.map(({ path, of, sha256: digest }) => ({ path, of, sha256: digest })) }
      : {}),
    ...(plan.rooms.records.length ? { rooms: plan.rooms.records } : {}),
  };

  // The region LAST: its rooms' rows read each room as it stands, and its pointer names the index
  // this sync just wrote.
  writeSpans({ root, manifest, writes: plan.writes, canonVersion, off: plan.switchedOff ?? [], from });
  writePointers(root, plan);
  writeLock(root, lock);
  return lock;
}

/** Each room's pointer, and each undeclared room's region taken back out. */
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
 * Assemble every span into its region, and point the harness's own file at it. One write per region
 * file, never one per rule: each rewrite of the same file would re-read all that the last one just wrote.
 */
function writeSpans(
  { root, manifest, writes, canonVersion, off, from }:
  {
    root: string; manifest: Manifest; writes: PlannedWrite[]; canonVersion: string;
    off: CoreSwitch[]; from: string;
  },
): void {
  const spans = writes.filter((write) => write.in);
  if (!spans.length) return;
  const harness: Harness = manifest.harnessDescriptor ?? resolveHarness(manifest.harness);

  for (const [file, within] of groupBy(spans, (write) => write.in!)) {
    const tier = Object.values(harness.tiers).find((candidate) => candidate.region?.file === file);
    if (!tier?.region) continue;

    const input = {
      rules: within.map((write) => ({
        file: { pack: write.pack, source: write.source, target: write.target },
        text: write.content,
        // Carried from `planSync`, where the canon is in hand — a span holds its body alone.
        ...(write.meta ? { meta: write.meta } : {}),
      })),
      ...onDemandExtras(writes),
      version: canonVersion,
      target: manifest.target,
      // The rows this repository switched off: the session loading the region learns what is
      // not there, and which pack said so, rather than meeting a doctrine with a silent hole in it.
      off,
      // The mirror sentence, the rooms and where the records are, rendered by the
      // function `check` rebuilds them with.
      ...rosterExtras(root, harness, manifest.target, manifest.rooms ?? [], manifest.documents ?? []),
    };

    // 🔴 The RAW bytes: `writeRegion` keeps everything outside the region byte for byte and takes the
    // file's own line ending — both of which a normalized read erases.
    const abs = join(root, file);
    const held = existsSync(abs) ? readFileSync(abs, 'utf8') : '';
    const body = renderTier(input);
    writeTextAtomic(abs, writeRegion(held, tier.region.name, body));

    // 🔴 The migration's last piece. An older repository has a generated `RULES_INDEX.md` in the
    // directory the tier just left. It is not in the lock — generated files never are — so no
    // retirement rule reaches it, and it would sit there as a roster that looks authoritative and
    // is frozen at the moment the tier moved. Only ever a file daoris wrote.
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
