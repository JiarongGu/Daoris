import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { listMarkdown, readText, sha256 } from './fsx.mjs';
import { readLock, readManifest } from './config.mjs';
import { INDEX_PATH, buildIndex } from './indexgen.mjs';

/**
 * Pure local hashing against the lock — no network, no canon, no package
 * resolution. That is what lets `check` sit inside a build gate in a repo that
 * has no node dependencies of its own and may be building offline.
 */
export function inspect({ root, manifest, lock }) {
  const drifted = [];
  const missing = [];

  for (const entry of lock?.entries ?? []) {
    const abs = join(root, manifest.target, entry.target);
    if (!existsSync(abs)) missing.push(entry.target);
    else if (sha256(readText(abs)) !== entry.sha256) drifted.push(entry.target);
  }

  // Offline staleness: a pack the manifest asks for that the lock has never seen.
  const syncedPacks = new Set((lock?.entries ?? []).map((entry) => entry.pack));
  const stalePacks = manifest.packs.filter((pack) => !syncedPacks.has(pack));

  // The tier is the directory, so the always-loaded footprint is measurable.
  const rulesDir = join(root, manifest.target, 'rules');
  const coreBytes = listMarkdown(rulesDir).reduce(
    (sum, file) => sum + statSync(join(rulesDir, file)).size,
    0,
  );
  const overBudget = coreBytes > manifest.coreBudgetBytes;

  const indexFile = join(root, manifest.target, INDEX_PATH);
  const expected = buildIndex({ root, target: manifest.target, lock });
  const indexStale = !existsSync(indexFile) || readText(indexFile) !== expected;

  const ok = !drifted.length && !missing.length && !stalePacks.length && !overBudget && !indexStale;
  return { drifted, missing, stalePacks, coreBytes, overBudget, indexStale, ok };
}

export function commandCheck({ root, write }) {
  const manifest = readManifest(root);
  const report = inspect({ root, manifest, lock: readLock(root) });

  for (const target of report.drifted) write(`  drifted   ${target}`);
  for (const target of report.missing) write(`  missing   ${target}`);
  for (const pack of report.stalePacks) {
    write(`  stale     pack '${pack}' is in the manifest but not the lock`);
  }
  if (report.overBudget) {
    write(
      `  budget    ${manifest.target}/rules is ${report.coreBytes} bytes ` +
        `(limit ${manifest.coreBudgetBytes})`,
    );
  }
  if (report.indexStale) write(`  index     ${INDEX_PATH} is out of date — run 'daoris index'`);

  if (report.ok) {
    write(`daoris: clean — ${report.coreBytes} bytes of always-loaded core`);
    return 0;
  }
  write("daoris: run 'daoris sync' to reconcile, or 'daoris upstream <file>' to keep a local edit");
  return 1;
}
