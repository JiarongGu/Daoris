import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { listMarkdown } from './fsx.mjs';
import { readCanon, resolveCanonRoot } from './canon.mjs';
import { MANIFEST_FILE, lockIndex, readLock, readManifest, writeManifest } from './config.mjs';
import { inspect } from './drift.mjs';
import { DaorisError } from './errors.mjs';

const DEFAULT_TARGET = '.claude';

/** Everything under the target dir that the lock does not claim is this repo's own. */
function localDocs(root, target) {
  const locked = lockIndex(readLock(root));
  const found = [];
  for (const tier of ['rules', 'knowledge']) {
    for (const file of listMarkdown(join(root, target, tier))) {
      if (file === 'RULES_INDEX.md') continue;
      if (!locked.has(`${tier}/${file}`)) found.push(`${tier}/${file}`);
    }
  }
  return found;
}

/**
 * Writes a core-only manifest and then REPORTS. It deliberately does not guess
 * which packs a repo wants: a wrong guess installs the wrong always-loaded
 * core, which is the one thing that is expensive on every future session.
 */
export function commandInit({ root, write, packageRoot }) {
  if (existsSync(join(root, MANIFEST_FILE))) {
    throw new DaorisError(`${MANIFEST_FILE} already exists — edit it, or delete it to start over`);
  }
  const canon = readCanon(resolveCanonRoot(packageRoot));

  writeManifest(root, {
    source: `github:OWNER/daoris#v${canon.version}`,
    packs: [],
    target: DEFAULT_TARGET,
    coreBudgetBytes: 24000,
  });

  write(`daoris: wrote ${MANIFEST_FILE} (core only — add packs deliberately)`);
  write('');
  write('  available packs:');
  for (const pack of [...canon.packs.values()].filter((entry) => entry.name !== 'core')) {
    write(`    ${pack.name.padEnd(20)} — ${pack.description}`);
  }

  const local = localDocs(root, DEFAULT_TARGET);
  if (local.length) {
    write('');
    write("  this repo's own docs (never synced, never touched):");
    for (const file of local) write(`    ${file}`);
  }
  write('');
  write('  then: daoris sync');
  return 0;
}

export function commandStatus({ root, write, packageRoot }) {
  const manifest = readManifest(root);
  const lock = readLock(root);
  const canonRoot = resolveCanonRoot(packageRoot);

  write(`  source        ${manifest.source}`);
  write(`  packs         ${['core', ...manifest.packs].join(', ')}`);
  write(`  canon         ${lock ? `${lock.canonVersion} (${lock.entries.length} files)` : 'never synced'}`);

  if (lock) {
    const report = inspect({ root, manifest, lock });
    write(`  core budget   ${report.coreBytes} / ${manifest.coreBudgetBytes} bytes`);
    if (report.drifted.length) write(`  drifted       ${report.drifted.join(', ')}`);
    if (report.missing.length) write(`  missing       ${report.missing.join(', ')}`);
    if (report.stalePacks.length) write(`  stale packs   ${report.stalePacks.join(', ')}`);
  }

  const local = localDocs(root, manifest.target);
  if (local.length) write(`  local         ${local.join(', ')}`);
  if (!existsSync(canonRoot)) write(`  canon source  unavailable at '${canonRoot}' (check still works)`);
  return 0;
}
