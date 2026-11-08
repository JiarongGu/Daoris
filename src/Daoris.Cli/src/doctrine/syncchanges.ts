import type { Canon, Lock, Manifest, PlannedWrite, Rename } from './records.ts';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText, sha256 } from '../platform/fsx.ts';
import { stripFrontmatter, stripHeader } from './document.ts';
import { significantTokens, containment } from './twins.ts';
import { isSwitchedOff, resolveSelection } from './canon.ts';
import { lockIndex } from './config.ts';
import { spanBody } from './tierrender.ts';
import { lockLayout } from '../layout/layout.ts';

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

export function detectRenames(
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
      // create at the same target, and pairing those reads as a rename of x to x.
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
 * Computed from the lock and the shipped canon, read where the lock says the files are, so it needs no network and no
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

  const was = lockLayout(root, lock, manifest);
  const harness = was.harness;
  const keeps = manifest.kept ?? {};

  for (const file of selected) {
    // A kept file is no update: `status` names it apart, again once the canon's file moves since its judgement.
    if (keeps[file.target]) continue;
    const entry = locked.get(file.target);
    if (!entry) {
      added.push(file.target);
      continue;
    }

    if (entry.in) {
      // A span: what is held is the rule's BODY, so the comparison is body against body. Read
      // by the LOCK's provenance, as every other reader does — what is on disk is what sync wrote.
      const body = spanBody(root, harness, entry);
      // Untouched here, and different from what the canon now says: an upstream improvement.
      if (body === null || sha256(body) !== entry.sha256) continue;
      if (body !== stripFrontmatter(readText(join(canon.root, file.source)))) changed.push(file.target);
      continue;
    }

    const abs = join(root, was.target, file.target);
    if (!existsSync(abs)) continue;
    const onDisk = readText(abs);
    if (sha256(onDisk) !== entry.sha256) continue;
    if (stripHeader(onDisk) !== readText(join(canon.root, file.source))) changed.push(file.target);
  }

  const wanted = new Set(selected.map((file) => file.target));
  // A row a confirmed switch takes out is not RETIRED — the canon still ships it — and `status`
  // names it as switched off, with its pack and reason, rather than here.
  return {
    added,
    changed,
    retired: [...locked.keys()].filter((t) => !wanted.has(t) && !isSwitchedOff(selection.switchedOff, locked.get(t)!)),
  };
}
