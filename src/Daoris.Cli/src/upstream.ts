import type { CommandArgs, Lock, LockEntry, Manifest, MirrorEntry } from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync } from 'node:fs';
import { join, resolve, sep } from 'node:path';
import { readText, sha256, writeTextAtomic } from './fsx.ts';
import { frontmatterEnd, stripHeader } from './document.ts';
import { resolveCanonRoot } from './canon.ts';
import { lockIndex, readLock, readManifest } from './config.ts';
import { spanBody } from './tierrender.ts';
import { declared, lockLayout } from './layout.ts';
import { DaorisError } from './errors.ts';

/**
 * The 衍 half: a refinement found in one repo flows back and evolves the canon.
 * A one-way push would be distribution; this is what keeps the canon from
 * ossifying, which is why it ships in v0.1 rather than later.
 *
 * It takes a canonical document's own path, a mirror's path (D117 §3.3), or an old path after a move,
 * which it answers with where the document went.
 */
export function upstreamFile(
  { root, manifest, lock, canonRoot, file }:
  { root: string; manifest: Manifest; lock: Lock | null; canonRoot: string; file: string },
): { target: string; source: string } {
  const locked = lockIndex(lock);
  // Where the files are is the lock's answer (D117 §5.1): between a flip and the sync that moves them,
  // the manifest's root holds nothing yet.
  const { target: base } = lockLayout(root, lock, manifest);
  const path = declared(file);

  const mirror = lock?.mirrors?.find((candidate) => candidate.path === path);
  if (mirror) return upstreamMirror({ root, locked, canonRoot, mirror, file, base });

  const strip = (prefix: string) => (path.startsWith(`${prefix}/`) ? path.slice(prefix.length + 1) : null);
  const normalized = strip(base) ?? strip(manifest.target) ?? path;
  const entry =
    locked.get(normalized) ??
    [...locked.values()].find((candidate) => candidate.target.endsWith(`/${normalized}`));

  if (!entry) {
    // An old path after a move: said where the document went, rather than called local — calling it
    // local would send the person looking for an edit that is not lost.
    const [head, ...rest] = path.split('/');
    const moved = rest.length ? locked.get(rest.join('/')) : undefined;
    if (moved && !moved.in && head !== base) {
      throw new DaorisError(`'${file}' moved to ${base}/${moved.target} with the layout — upstream that path`);
    }
    throw new DaorisError(
      `'${file}' is not canonical in this repo — it is local, so there is nothing to upstream`,
    );
  }
  if (!existsSync(canonRoot)) throw new DaorisError(`no canon at '${canonRoot}'`);
  const canonFile = canonPath(canonRoot, entry.source);

  if (entry.in) {
    // A span (D59). The region carries PROSE; the frontmatter was stripped on the way in, so the
    // canon's own is kept and only the body is replaced. That is the honest limit of promoting from a
    // region, and it is stated rather than discovered: an improvement to `applies_when` or `enforces`
    // is a canon edit, not something a repository can push from its instruction file.
    const body = spanBody(root, manifest.harnessDescriptor, entry);
    if (body === null) {
      throw new DaorisError(
        `'${file}' is canonical here but is not in the doctrine region of ${entry.in} —\n`
        + "  run 'daoris sync' to put it back, then edit it there.",
      );
    }

    const held = existsSync(canonFile) ? readText(canonFile) : '';
    const end = frontmatterEnd(held);
    const front = end === -1 ? '' : `${held.slice(0, end)}\n`;
    writeTextAtomic(canonFile, `${front}${body}\n`);
    return { target: entry.target, source: entry.source };
  }

  const body = stripHeader(readText(join(root, base, entry.target)));
  writeTextAtomic(canonFile, body);
  return { target: entry.target, source: entry.source };
}

/**
 * The canon file a lock entry names — D18's containment, for the write this direction makes: the lock
 * is generated, so it is the file nobody reads closely, and a merge-mangled or crafted `source` would
 * otherwise land anywhere.
 */
function canonPath(canonRoot: string, source: string): string {
  const base = resolve(canonRoot);
  const canonFile = resolve(base, source);
  if (!canonFile.startsWith(base + sep)) {
    throw new DaorisError(
      `'${source}' resolves outside the canon — refusing to write it.\n`
      + '  a lock entry whose source escapes the canon means daoris.lock is corrupt or has been tampered with');
  }
  return canonFile;
}

/**
 * Promote an edit made in a mirror (D117 §3.3, the third telling). The copy's header is stripped and the
 * rest goes to the canon; the next `sync` carries it to the source and back to the mirror by D13's
 * *improved upstream, untouched here*.
 *
 * Refused for a mirror of the repository's own skill — there is nothing canonical to promote, and the
 * source is where the edit belongs — and for a mirror whose source was edited too: two edits are a merge
 * only a person can make.
 */
function upstreamMirror(
  { root, locked, canonRoot, mirror, file, base }:
  {
    root: string; locked: Map<string, LockEntry>; canonRoot: string; mirror: MirrorEntry; file: string;
    base: string;
  },
): { target: string; source: string } {
  const target = mirror.of.startsWith(`${base}/`) ? mirror.of.slice(base.length + 1) : mirror.of;
  const entry = locked.get(target);
  if (!entry || entry.in) {
    throw new DaorisError(`'${file}' mirrors this repo's own skill — nothing canonical to promote; the source is `
      + `${mirror.of}, and an edit belongs there`);
  }
  const source = join(root, mirror.of);
  if (existsSync(source) && sha256(readText(source)) !== entry.sha256) {
    throw new DaorisError(
      `'${file}' and its source ${mirror.of} were both edited here — two edits are a merge only a person can `
      + 'make. Keep the one you mean in the source, then upstream that path',
      1);
  }
  if (!existsSync(canonRoot)) throw new DaorisError(`no canon at '${canonRoot}'`);
  writeTextAtomic(canonPath(canonRoot, entry.source), stripHeader(readText(join(root, mirror.path))));
  return { target: entry.target, source: entry.source };
}

/**
 * Promote every canonical file that differs from what the lock recorded. A
 * working session usually improves several rules at once, and one command per
 * file is friction on exactly the direction that must stay easy (D9).
 *
 * An edited mirror of a canonical skill counts (D117 §3.3), unless its source was edited too: that
 * one is `upstreamFile`'s refusal to make, one path at a time.
 */
export function upstreamAll(
  { root, manifest, lock, canonRoot }:
  { root: string; manifest: Manifest; lock: Lock | null; canonRoot: string },
): Array<{ target: string; source: string }> {
  const promoted = [];
  const { target: base } = lockLayout(root, lock, manifest);
  const locked = lockIndex(lock);
  for (const entry of lock?.entries ?? []) {
    if (entry.in) {
      const body = spanBody(root, manifest.harnessDescriptor, entry);
      if (body === null || sha256(body) === entry.sha256) continue;
      promoted.push(upstreamFile({ root, manifest, lock, canonRoot, file: entry.target }));
      continue;
    }

    const abs = join(root, base, entry.target);
    if (!existsSync(abs)) continue;
    if (sha256(readText(abs)) === entry.sha256) continue;
    promoted.push(upstreamFile({ root, manifest, lock, canonRoot, file: entry.target }));
  }

  for (const mirror of lock?.mirrors ?? []) {
    const abs = join(root, mirror.path);
    if (!existsSync(abs) || sha256(readText(abs)) === mirror.sha256) continue;
    const target = mirror.of.startsWith(`${base}/`) ? mirror.of.slice(base.length + 1) : mirror.of;
    const entry = locked.get(target);
    if (!entry || entry.in || promoted.some((done) => done.target === entry.target)) continue;
    promoted.push(upstreamFile({ root, manifest, lock, canonRoot, file: mirror.path }));
  }
  return promoted;
}

export function commandUpstream({ root, argv, write, packageRoot }: CommandArgs): ExitCode {
  const canonRoot = resolveCanonRoot(packageRoot);

  if (argv.includes('--all')) {
    const promoted = upstreamAll({
      root,
      manifest: readManifest(root),
      lock: readLock(root),
      canonRoot,
    });
    if (!promoted.length) {
      write('daoris: nothing to upstream — no canonical file differs from the lock');
      return 0;
    }
    for (const result of promoted) write(`  ${result.target} -> canon ${result.source}`);
    write(`daoris: promoted ${promoted.length} file(s)`);
    write("daoris: review and commit them in the canon repo, then 'daoris sync' here");
    return 0;
  }

  const file = argv.find((arg) => !arg.startsWith('--'));
  if (!file) throw new DaorisError('usage: daoris upstream <file> | daoris upstream --all');

  const result = upstreamFile({
    root,
    manifest: readManifest(root),
    lock: readLock(root),
    canonRoot,
    file,
  });
  write(`daoris: ${result.target} -> canon ${result.source}`);
  write("daoris: review and commit it in the canon repo, then 'daoris sync' here");
  return 0;
}
