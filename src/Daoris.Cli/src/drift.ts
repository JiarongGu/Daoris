import type { CommandArgs, DriftReport, Lock, Manifest } from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { listMarkdown, readText, sha256 } from './fsx.ts';
import { readLock, readManifest } from './config.ts';
import { INDEX_PATH, buildIndex } from './indexgen.ts';
import { HARNESSES, DEFAULT_HARNESS, alwaysLoadedTiers } from './harness.ts';

/**
 * Pure local hashing against the lock — no network, no canon, no package
 * resolution. That is what lets `check` sit inside a build gate in a repo that
 * has no node dependencies of its own and may be building offline.
 */
export function inspect(
  { root, manifest, lock }: { root: string; manifest: Manifest; lock: Lock | null },
): DriftReport {
  const drifted: string[] = [];
  const missing: string[] = [];

  for (const entry of lock?.entries ?? []) {
    const abs = join(root, manifest.target, entry.target);
    if (!existsSync(abs)) missing.push(entry.target);
    else if (sha256(readText(abs)) !== entry.sha256) drifted.push(entry.target);
  }

  // Offline staleness: a pack the manifest asks for that the lock has never seen.
  const syncedPacks = new Set((lock?.entries ?? []).map((entry) => entry.pack));
  const stalePacks = manifest.packs.filter((pack) => !syncedPacks.has(pack));

  // The tier is the directory, so the always-loaded footprint is measurable — and WHICH tiers those
  // are is the harness's answer, not a constant here.
  const harness = manifest.harnessDescriptor ?? HARNESSES[DEFAULT_HARNESS];
  let coreBytes = 0;
  for (const tier of alwaysLoadedTiers(harness)) {
    const dir = join(root, manifest.target, harness.tiers[tier]!.dir);
    coreBytes += listMarkdown(dir).reduce((sum, file) => sum + statSync(join(dir, file)).size, 0);
  }
  const overBudget = coreBytes > manifest.coreBudgetBytes;

  const indexFile = join(root, manifest.target, INDEX_PATH);
  const expected = buildIndex({ root, target: manifest.target, lock });
  const indexStale = !existsSync(indexFile) || readText(indexFile) !== expected;

  // The budget REPORTS; it does not fail (D54, the owner's call). Everything else here is a FACT the
  // tool established — a file drifted, one is missing, a pack was never synced, the index is behind.
  // Size is a JUDGEMENT: one byte over a number somebody chose is not wrong, and a gate that stops a
  // build over a judgement gets its number raised rather than read — which is the failure D28
  // predicted in its own words about noise. The number stays and is stated on every run, because a
  // signal nobody can see is not a signal.
  const ok = !drifted.length && !missing.length && !stalePacks.length && !indexStale;
  return { drifted, missing, stalePacks, coreBytes, overBudget, indexStale, ok };
}

export function commandCheck({ root, write }: Pick<CommandArgs, 'root' | 'write'>): ExitCode {
  const manifest = readManifest(root);
  const report = inspect({ root, manifest, lock: readLock(root) });

  for (const target of report.drifted) write(`  drifted   ${target}`);
  for (const target of report.missing) write(`  missing   ${target}`);
  for (const pack of report.stalePacks) {
    write(`  stale     pack '${pack}' is in the manifest but not the lock`);
  }
  if (report.overBudget) {
    write(
      `  budget    ${manifest.target} always-loaded is ${report.coreBytes} of ` +
        `${manifest.coreBudgetBytes} declared bytes — advisory, not a gate: split principle from ` +
        'detail into the on-demand tier, or raise it deliberately in daoris.json',
    );
  }
  if (report.indexStale) write(`  index     ${INDEX_PATH} is out of date — run 'daoris index'`);

  if (report.ok) {
    // "clean" would be the wrong word while the core is over its own declared budget, even though
    // nothing here failed — so the over case says what is true instead of reusing the happy sentence.
    write(report.overBudget
      ? `daoris: no drift — always-loaded core is ${report.coreBytes} of `
        + `${manifest.coreBudgetBytes} declared bytes, over by ${report.coreBytes - manifest.coreBudgetBytes}`
      : `daoris: clean — ${report.coreBytes} of ${manifest.coreBudgetBytes} bytes of always-loaded core`);
    return 0;
  }
  write("daoris: run 'daoris sync' to reconcile, or 'daoris upstream <file>' to keep a local edit");
  return 1;
}
