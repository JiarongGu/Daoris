import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, listMarkdown } from './fsx.mjs';
import { readCanon, resolveCanonRoot } from './canon.mjs';
import { MANIFEST_FILE, lockIndex, readLock, readManifest, writeManifest } from './config.mjs';
import { planChanges } from './materialize.mjs';
import { notesBetween } from './notes.mjs';
import { inspect } from './drift.mjs';
import { DaorisError } from './errors.mjs';

const DEFAULT_TARGET = '.claude';

/**
 * Everything under the target dir that the lock does not claim is this repo's
 * own. Skills count: a repo's own skill is exactly as invisible to the tool as
 * its own rule, and just as much worth naming before a first sync.
 */
function localDocs(root, target) {
  const locked = lockIndex(readLock(root));
  const found = [];
  for (const tier of ['rules', 'knowledge']) {
    for (const file of listMarkdown(join(root, target, tier))) {
      if (file === 'RULES_INDEX.md') continue;
      if (!locked.has(`${tier}/${file}`)) found.push(`${tier}/${file}`);
    }
  }
  for (const file of listFiles(join(root, target, 'skills'))) {
    if (file.endsWith('/SKILL.md') && !locked.has(`skills/${file}`)) found.push(`skills/${file}`);
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

  // status may reach the canon; `check` deliberately may not (D8), which is why
  // "a newer canon exists" is reported here and never gates a build.
  if (!existsSync(canonRoot)) {
    write(`  canon source  unavailable at '${canonRoot}' (check still works)`);
  } else if (lock) {
    const canon = readCanon(canonRoot);
    if (canon.version !== lock.canonVersion) {
      write(
        `  update        canon ${canon.version} available (lock has ${lock.canonVersion}) — run 'daoris sync'`,
      );
      // Naming what moved is the difference between a prompt to act and a
      // prompt to investigate. All of it comes from the lock, so it stays offline.
      const changes = planChanges({ root, manifest, canon, lock });
      for (const target of changes.changed) write(`                  changed  ${target}`);
      for (const target of changes.added) write(`                  new      ${target}`);
      for (const target of changes.retired) write(`                  retired  ${target}`);
      if (!changes.changed.length && !changes.added.length && !changes.retired.length) {
        write('                  (version only — no document changed)');
      }

      // Which files moved comes from the lock; whether it MATTERS is a sentence
      // only the author of the change can write, so the canon carries it.
      for (const note of notesBetween(canonRoot, lock.canonVersion, canon.version)) {
        write('');
        write(`  why ${note.version}`);
        for (const line of note.body.split('\n')) write(line ? `    ${line}` : '');
      }
    }
  }
  return 0;
}
