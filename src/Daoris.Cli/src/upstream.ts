import type { CommandArgs, Lock, LockEntry, Manifest } from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync } from 'node:fs';
import { join, resolve, sep } from 'node:path';
import { readText, sha256, writeTextAtomic } from './fsx.ts';
import { parseFrontmatter, stripHeader } from './document.ts';
import { resolveCanonRoot } from './canon.ts';
import { lockIndex, readLock, readManifest } from './config.ts';
import { findRegion } from './region.ts';
import { tierRuleBody } from './tierrender.ts';
import { resolveHarness } from './harness.ts';
import { DaorisError } from './errors.ts';

/**
 * The 衍 half: a refinement found in one repo flows back and evolves the canon.
 * A one-way push would be distribution; this is what keeps the canon from
 * ossifying, which is why it ships in v0.1 rather than later.
 */
export function upstreamFile(
  { root, manifest, lock, canonRoot, file }:
  { root: string; manifest: Manifest; lock: Lock | null; canonRoot: string; file: string },
): { target: string; source: string } {
  const locked = lockIndex(lock);
  const normalized = file.replace(/\\/g, '/').replace(new RegExp(`^${manifest.target}/`), '');
  const entry =
    locked.get(normalized) ??
    [...locked.values()].find((candidate) => candidate.target.endsWith(`/${normalized}`));

  if (!entry) {
    throw new DaorisError(
      `'${file}' is not canonical in this repo — it is local, so there is nothing to upstream`,
    );
  }
  if (!existsSync(canonRoot)) throw new DaorisError(`no canon at '${canonRoot}'`);

  // D18's containment, for the write this direction makes: the lock is generated, so it is the file
  // nobody reads closely, and a merge-mangled or crafted `source` would otherwise land anywhere.
  const base = resolve(canonRoot);
  const canonFile = resolve(base, entry.source);
  if (!canonFile.startsWith(base + sep)) {
    throw new DaorisError(
      `'${entry.source}' resolves outside the canon — refusing to write it.\n`
      + '  a lock entry whose source escapes the canon means daoris.lock is corrupt or has been tampered with');
  }

  if (entry.in) {
    // A span (D59). The region carries PROSE; the frontmatter was stripped on the way in, so the
    // canon's own is kept and only the body is replaced. That is the honest limit of promoting from a
    // region, and it is stated rather than discovered: an improvement to `applies_when` or `enforces`
    // is a canon edit, not something a repository can push from its instruction file.
    const body = regionRuleBody({ root, manifest, entry });
    if (body === null) {
      throw new DaorisError(
        `'${file}' is canonical here but is not in the doctrine region of ${entry.in} —\n`
        + "  run 'daoris sync' to put it back, then edit it there.",
      );
    }

    const held = existsSync(canonFile) ? readText(canonFile) : '';
    const { meta } = parseFrontmatter(held, []);
    const front = meta ? `${held.slice(0, held.indexOf('\n---\n', 3) + 5)}\n` : '';
    writeTextAtomic(canonFile, `${front}${body}\n`);
    return { target: entry.target, source: entry.source };
  }

  const body = stripHeader(readText(join(root, manifest.target, entry.target)));
  writeTextAtomic(canonFile, body);
  return { target: entry.target, source: entry.source };
}

/** One rule's body, out of the region its lock entry names. */
function regionRuleBody(
  { root, manifest, entry }: { root: string; manifest: Manifest; entry: LockEntry },
): string | null {
  const harness = manifest.harnessDescriptor ?? resolveHarness(manifest.harness);
  const tier = Object.values(harness.tiers).find((candidate) => candidate.region?.file === entry.in);
  if (!tier?.region) return null;

  const abs = join(root, entry.in!);
  if (!existsSync(abs)) return null;

  const held = findRegion(readText(abs), tier.region.name);
  return held.kind === 'present' ? tierRuleBody(held.body, `${entry.pack}/${entry.source}`) : null;
}

/**
 * Promote every canonical file that differs from what the lock recorded. A
 * working session usually improves several rules at once, and one command per
 * file is friction on exactly the direction that must stay easy (D9).
 */
export function upstreamAll(
  { root, manifest, lock, canonRoot }:
  { root: string; manifest: Manifest; lock: Lock | null; canonRoot: string },
): Array<{ target: string; source: string }> {
  const promoted = [];
  for (const entry of lock?.entries ?? []) {
    if (entry.in) {
      const body = regionRuleBody({ root, manifest, entry });
      if (body === null || sha256(body) === entry.sha256) continue;
      promoted.push(upstreamFile({ root, manifest, lock, canonRoot, file: entry.target }));
      continue;
    }

    const abs = join(root, manifest.target, entry.target);
    if (!existsSync(abs)) continue;
    if (sha256(readText(abs)) === entry.sha256) continue;
    promoted.push(upstreamFile({ root, manifest, lock, canonRoot, file: entry.target }));
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
