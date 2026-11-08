import type { CommandArgs } from '../platform/types.ts';
import type { Lock, LockEntry, Manifest, MirrorEntry } from './records.ts';
import type { ExitCode } from '../platform/errors.ts';
import { existsSync } from 'node:fs';
import { isAbsolute, join, relative, resolve, sep } from 'node:path';
import { readText, sha256, writeTextAtomic } from '../platform/fsx.ts';
import { frontmatterEnd, stripHeader } from './document.ts';
import { resolveCanonRoot } from './canon.ts';
import { lockIndex, readLock, readManifest } from './config.ts';
import { canonVersionAt, refuseNewerLock } from './lockversion.ts';
import { spanBody } from './tierrender.ts';
import { declared, lockLayout } from '../layout/layout.ts';
import { DaorisError } from '../platform/errors.ts';

/** One file's text going back to the canon: whose, and the repository's text without the tool's header. */
interface Promotion {
  target: string;
  source: string;
  body: string;
  /** A rule held as a span: only its body goes, under the canon's own frontmatter. */
  span: boolean;
}

/**
 * The 衍 half: a refinement found in one repo flows back and evolves the canon.
 * A one-way push would be distribution; this is what keeps the canon from
 * ossifying, which is why it ships in v0.1 rather than later.
 *
 * It takes a canonical document's own path, a mirror's path, or an old path after a move,
 * which it answers with where the document went. It writes into the canon, which only the canon's own checkout does
 * (`commandUpstream`); anywhere else the same text becomes a request's words.
 */
export function upstreamFile(
  { root, manifest, lock, canonRoot, file }:
  { root: string; manifest: Manifest; lock: Lock | null; canonRoot: string; file: string },
): { target: string; source: string } {
  const promotion = promotionOf({ root, manifest, lock, file });
  writePromotion(canonRoot, promotion);
  return { target: promotion.target, source: promotion.source };
}

/** Into the canon: a span's body under the canon's own frontmatter, a file's text whole. */
function writePromotion(canonRoot: string, { source, body, span }: Promotion): void {
  if (!existsSync(canonRoot)) throw new DaorisError(`no canon at '${canonRoot}'`);
  const canonFile = canonPath(canonRoot, source);
  if (!span) {
    writeTextAtomic(canonFile, body);
    return;
  }
  // The region carries PROSE; the frontmatter is stripped on the way in, so the canon's own is kept and only the body is
  // replaced. That is the honest limit of promoting from a region, and it is stated rather than discovered: an
  // improvement to `applies_when` or `enforces` is a canon edit, not something a repository can push from its
  // instruction file.
  const held = existsSync(canonFile) ? readText(canonFile) : '';
  const end = frontmatterEnd(held);
  const front = end === -1 ? '' : `${held.slice(0, end)}\n`;
  writeTextAtomic(canonFile, `${front}${body}\n`);
}

/** What `file` promotes, refused as `upstreamFile` refuses it; reads only this repository. */
function promotionOf(
  { root, manifest, lock, file }: { root: string; manifest: Manifest; lock: Lock | null; file: string },
): Promotion {
  const locked = lockIndex(lock);
  // Where the files are is the lock's answer: between a flip and the sync that moves them,
  // the manifest's root holds nothing yet.
  const { target: base } = lockLayout(root, lock, manifest);
  const path = declared(file);

  const mirror = lock?.mirrors?.find((candidate) => candidate.path === path);
  if (mirror) return mirrorPromotion({ root, locked, mirror, file, base });

  const strip = (prefix: string) => (path.startsWith(`${prefix}/`) ? path.slice(prefix.length + 1) : null);
  const normalized = strip(base) ?? strip(manifest.target) ?? path;
  // A bare file name is a convenience for one canonical path; naming two is the person's to settle, never the lock's order.
  const named = locked.has(normalized)
    ? [locked.get(normalized)!]
    : [...locked.values()].filter((candidate) => candidate.target.endsWith(`/${normalized}`));
  if (named.length > 1) {
    throw new DaorisError(`'${file}' names more than one canonical file: ${named.map((candidate) => candidate.target).join(', ')} — `
      + 'give the path of the one to upstream');
  }
  const entry = named[0];

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
  // Containment before anything is read for it: a source that leaves the canon names no canon file, to write or to propose.
  canonPath(resolve(sep, 'canon'), entry.source);
  if (entry.in) {
    // A span: its body alone, which `writePromotion` puts under the canon's own frontmatter.
    const body = spanBody(root, manifest.harnessDescriptor, entry);
    if (body === null) {
      throw new DaorisError(
        `'${file}' is canonical here but is not in the doctrine region of ${entry.in} —\n`
        + "  run 'daoris sync' to put it back, then edit it there.",
      );
    }
    return { target: entry.target, source: entry.source, body, span: true };
  }

  const copy = join(root, base, entry.target);
  if (!existsSync(copy)) {
    throw new DaorisError(`${entry.target} is canonical here but absent from ${base}/ — run 'daoris sync' to put it back, then edit it there.`);
  }
  return { target: entry.target, source: entry.source, body: stripHeader(readText(copy)), span: false };
}

/**
 * The canon file a lock entry names — contained, for the write this direction makes: the lock
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
 * Promote an edit made in a mirror. The copy's header is stripped and the
 * rest goes to the canon; the next `sync` carries it to the source and back to the mirror as
 * *improved upstream, untouched here*.
 *
 * Refused for a mirror of the repository's own skill — there is nothing canonical to promote, and the
 * source is where the edit belongs — and for a mirror whose source is edited too: two edits are a merge
 * only a person can make.
 */
function mirrorPromotion(
  { root, locked, mirror, file, base }:
  { root: string; locked: Map<string, LockEntry>; mirror: MirrorEntry; file: string; base: string },
): Promotion {
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
  return { target: entry.target, source: entry.source, body: stripHeader(readText(join(root, mirror.path))), span: false };
}

/**
 * Promote every canonical file that differs from what the lock recorded. A
 * working session usually improves several rules at once, and one command per
 * file is friction on exactly the direction that must stay easy.
 *
 * An edited mirror of a canonical skill counts, unless its source is edited too: that
 * one is `upstreamFile`'s refusal to make, one path at a time.
 */
export function upstreamAll(
  { root, manifest, lock, canonRoot }:
  { root: string; manifest: Manifest; lock: Lock | null; canonRoot: string },
): Array<{ target: string; source: string }> {
  const promotions = promotionsAll({ root, manifest, lock });
  for (const promotion of promotions) writePromotion(canonRoot, promotion);
  return promotions.map(({ target, source }) => ({ target, source }));
}

/** Every edited canonical file's promotion, each refused as one alone would be, before any is written. */
function promotionsAll(
  { root, manifest, lock }: { root: string; manifest: Manifest; lock: Lock | null },
): Promotion[] {
  const promoted: Promotion[] = [];
  const { target: base } = lockLayout(root, lock, manifest);
  const locked = lockIndex(lock);
  for (const entry of lock?.entries ?? []) {
    if (entry.in) {
      const body = spanBody(root, manifest.harnessDescriptor, entry);
      if (body === null || sha256(body) === entry.sha256) continue;
      promoted.push(promotionOf({ root, manifest, lock, file: entry.target }));
      continue;
    }

    const abs = join(root, base, entry.target);
    if (!existsSync(abs)) continue;
    if (sha256(readText(abs)) === entry.sha256) continue;
    promoted.push(promotionOf({ root, manifest, lock, file: entry.target }));
  }

  for (const mirror of lock?.mirrors ?? []) {
    const abs = join(root, mirror.path);
    if (!existsSync(abs) || sha256(readText(abs)) === mirror.sha256) continue;
    const target = mirror.of.startsWith(`${base}/`) ? mirror.of.slice(base.length + 1) : mirror.of;
    const entry = locked.get(target);
    if (!entry || entry.in || promoted.some((done) => done.target === entry.target)) continue;
    promoted.push(promotionOf({ root, manifest, lock, file: mirror.path }));
  }
  return promoted;
}

/**
 * Whether this is the canon's own checkout: the canon this tool reads sits inside the repository. Only there does
 * `upstream` write; anywhere else the canon is an installed package's copy, or another repository's checkout. A package
 * installed in the repository's own `node_modules` is a copy too, and a write into it would be lost with the next install.
 */
export function insideCanon(root: string, canonRoot: string): boolean {
  const rel = relative(resolve(root), resolve(canonRoot));
  const parts = rel.split(/[\\/]/);
  return rel !== '' && parts[0] !== '..' && !isAbsolute(rel) && !parts.includes('node_modules');
}

/**
 * The request's words, offline: a title and a body for the repository that keeps the shared doctrine, carrying this
 * repository's text of each file whole, for the person or the session to send. In the canon's words, as a quest is.
 */
export function composeRequest(
  { manifest, lock, promotions }: { manifest: Manifest; lock: Lock | null; promotions: readonly Promotion[] },
): string {
  const title = promotions.length === 1
    ? `Take this repository's text of \`${promotions[0]!.source}\` into the shared doctrine`
    : `Take this repository's text of ${promotions.length} files into the shared doctrine`;
  const from = lock ? `\`${lock.source}\`, canon ${lock.canonVersion}` : `\`${manifest.source}\``;
  const lines = [
    title,
    '',
    `A repository that took up the shared doctrine (from ${from}) holds its own text of the canon's `
      + `${promotions.length === 1 ? 'file' : 'files'} below, and proposes it: it was found where the doctrine is used. `
      + 'Judge each as that: take it, take part of it, or decline with the reason. A rule held in an instruction file '
      + "carries its body alone, so the canon's own frontmatter stays as it is.",
    '',
  ];
  for (const promotion of promotions) {
    lines.push(`**\`${promotion.source}\`**, its \`${promotion.target}\` there:`, '', '~~~~markdown', promotion.body.trimEnd(), '~~~~', '');
  }
  lines.push('Why it was changed there: <say what was found that this text answers>');
  return `${lines.join('\n')}\n`;
}

export function commandUpstream({ root, argv, write, packageRoot }: CommandArgs): ExitCode {
  const canonRoot = resolveCanonRoot(packageRoot);
  const lock = readLock(root);
  // A lock a newer canon wrote holds text this canon never had. A canon with no version
  // to read is left to the refusals below, which name it.
  const carried = canonVersionAt(canonRoot);
  if (carried !== null) refuseNewerLock(lock, carried, ['upstream', ...argv]);

  const all = argv.includes('--all');
  const file = argv.find((arg) => !arg.startsWith('--'));
  if (!all && !file) throw new DaorisError('usage: daoris upstream <file> | daoris upstream --all');
  const manifest = readManifest(root);
  const promotions = all ? promotionsAll({ root, manifest, lock }) : [promotionOf({ root, manifest, lock, file: file! })];
  if (!promotions.length) {
    write('daoris: nothing to upstream — no canonical file differs from the lock');
    return 0;
  }

  // Outside the canon's own checkout nothing is written across: the edit travels as a request, and its words are
  // composed here, offline, for whoever sends it.
  if (!insideCanon(root, canonRoot)) {
    write(`daoris: nothing was written; the request below is for the repository that keeps the shared doctrine `
      + `(${lock?.source ?? manifest.source}) — publish it there, or hand it to its owner`);
    write('');
    write(composeRequest({ manifest, lock, promotions }).trimEnd());
    return 0;
  }

  for (const promotion of promotions) writePromotion(canonRoot, promotion);
  if (!all) {
    write(`daoris: ${promotions[0]!.target} -> canon ${promotions[0]!.source}`);
    write("daoris: review and commit it in the canon repo, then 'daoris sync' here");
    return 0;
  }
  for (const result of promotions) write(`  ${result.target} -> canon ${result.source}`);
  write(`daoris: promoted ${promotions.length} file(s)`);
  write("daoris: review and commit them in the canon repo, then 'daoris sync' here");
  return 0;
}
