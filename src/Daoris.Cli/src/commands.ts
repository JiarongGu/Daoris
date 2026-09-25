import type { CommandArgs, Harness } from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, listMarkdown } from './fsx.ts';
import { readCanon, resolveCanonRoot, resolveSelection } from './canon.ts';
import { MANIFEST_FILE, lockIndex, readLock, readManifest, writeManifest } from './config.ts';
import { planChanges } from './materialize.ts';
import { notesBetween } from './notes.ts';
import { inspect } from './drift.ts';
import { HARNESSES, DEFAULT_HARNESS } from './harness.ts';
import { readRemotes, redactKey } from './remotemap.ts';
import { DaorisError } from './errors.ts';

const DEFAULT_TARGET = HARNESSES[DEFAULT_HARNESS]!.defaultTarget;

/**
 * Everything under the target dir that the lock does not claim is this repo's
 * own. Skills count: a repo's own skill is exactly as invisible to the tool as
 * its own rule, and just as much worth naming before a first sync.
 */
function localDocs(root: string, target: string, harness: Harness = HARNESSES[DEFAULT_HARNESS]!): string[] {
  const locked = lockIndex(readLock(root));
  const found = [];

  for (const tier of Object.values(harness.tiers)) {
    // The always-loaded tier is a span in a file the repository owns (D59), and a repository's OWN
    // always-loaded text lives in that same file outside the region — which is its own prose, not a
    // document this can enumerate.
    if (!tier.dir) continue;
    const dir = join(root, target, tier.dir);
    if (tier.entryFile) {
      const suffix = `/${tier.entryFile}`;
      for (const file of listFiles(dir)) {
        if (file.endsWith(suffix) && !locked.has(`${tier.dir}/${file}`)) found.push(`${tier.dir}/${file}`);
      }
    } else {
      for (const file of listMarkdown(dir)) {
        if (!locked.has(`${tier.dir}/${file}`)) found.push(`${tier.dir}/${file}`);
      }
    }
  }
  return found;
}

/**
 * Writes a core-only manifest and then REPORTS. It deliberately does not guess
 * which packs a repo wants: a wrong guess installs the wrong always-loaded
 * core, which is the one thing that is expensive on every future session.
 */
export function commandInit(
  { root, write, packageRoot }: Pick<CommandArgs, 'root' | 'write' | 'packageRoot'>,
): ExitCode {
  if (existsSync(join(root, MANIFEST_FILE))) {
    throw new DaorisError(`${MANIFEST_FILE} already exists — edit it, or delete it to start over`);
  }
  const canon = readCanon(resolveCanonRoot(packageRoot));

  writeManifest(root, {
    source: `github:JiarongGu/Daoris#v${canon.version}`,
    packs: [],
    target: DEFAULT_TARGET,
    coreBudgetBytes: 30000,
    // Scaffolded empty and deliberately so. Filling it in is how a repository registers what it is and
    // what it can be asked for; a guess written by the tool would be worse than a blank someone
    // notices, because nobody edits a field that already looks answered.
    domain: { summary: '', owns: [], accepts: [] },
  });

  write(`daoris: wrote ${MANIFEST_FILE} (core only — add packs deliberately)`);
  write('');
  write('  fill in `domain` — what this repo is, what it owns, what it accepts. It is how');
  write('  siblings know what is worth asking of you.');
  write('  available packs:');
  for (const pack of [...canon.packs.values()].filter((entry) => entry.name !== 'core')) {
    write(`    ${pack.name.padEnd(20)} — ${pack.description}`);
    // Said at the moment of choosing (D71): a pack that would take a core row out is a different
    // choice from one that only adds, and nothing it offers goes off until the manifest confirms it.
    const switches = Object.keys(pack.switchesOff);
    if (switches.length) {
      write(`    ${''.padEnd(20)}   switches off core ${switches.join(', ')} — only once you confirm it`);
    }
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

export function commandStatus(
  { root, argv = [], write, packageRoot }:
    Pick<CommandArgs, 'root' | 'write' | 'packageRoot'> & { argv?: CommandArgs['argv'] },
): ExitCode {
  const manifest = readManifest(root);
  const lock = readLock(root);
  const canonRoot = resolveCanonRoot(packageRoot);

  // The facts are computed ONCE, then rendered as JSON or as text. Two paths that compute
  // separately are two paths that can disagree about whether an update exists — the same reasoning
  // that gave the service one QuestExchange for its two hosts.
  const inspection = lock ? inspect({ root, manifest, lock }) : null;
  const local = localDocs(root, manifest.target, manifest.harnessDescriptor);
  const canonAvailable = existsSync(canonRoot);

  let update: {
    available: string;
    locked: string;
    changed: string[];
    added: string[];
    retired: string[];
    versionOnly: boolean;
  } | null = null;
  let notes: ReturnType<typeof notesBetween> = [];

  // What is switched off, from the LOCK — what is actually true here — with each pack's reason when
  // the canon can supply it; and what a selected pack offers that nobody confirmed (D71). The owner-
  // facing half of "never silent": `check` names the rows, and this says why.
  const canon = canonAvailable ? readCanon(canonRoot) : null;
  // A summary reports a defect rather than dying of it: an unknown pack or a confirmation no pack
  // offers is `sync`'s refusal to make, and `status` is where a person goes to find out why.
  let selection: ReturnType<typeof resolveSelection> | null = null;
  let selectionProblem: string | null = null;
  if (canon) {
    try {
      selection = resolveSelection(canon, manifest.packs, manifest.switchedOff ?? {});
    } catch (error) {
      if (!(error instanceof DaorisError)) throw error;
      selectionProblem = error.message;
    }
  }
  const switchedOff = (lock?.switchedOff ?? []).map(({ target, by }) => ({
    target,
    by,
    because: canon?.packs.get(by)?.switchesOff[target] ?? null,
  }));
  const offers = selection?.offers ?? [];

  // status may reach the canon; `check` deliberately may not (D8), which is why
  // "a newer canon exists" is reported here and never gates a build.
  if (canon && lock && !selectionProblem) {
    if (canon.version !== lock.canonVersion) {
      // Naming what moved is the difference between a prompt to act and a
      // prompt to investigate. All of it comes from the lock, so it stays offline.
      const changes = planChanges({ root, manifest, canon, lock });
      update = {
        available: canon.version,
        locked: lock.canonVersion,
        changed: changes.changed,
        added: changes.added,
        retired: changes.retired,
        versionOnly: !changes.changed.length && !changes.added.length && !changes.retired.length,
      };
      // Which files moved comes from the lock; whether it MATTERS is a sentence
      // only the author of the change can write, so the canon carries it.
      notes = notesBetween(canonRoot, lock.canonVersion, canon.version);
    }
  }

  // The machine's WIRING, when asked for (D50). The manifest says MAY this repository's material
  // leave; the machine says WHERE it would go — two different questions with two different homes
  // (D48 §2), and a person debugging a sync needs them side by side. Offline: it reads one more
  // file under the Daoris home and asks nothing.
  const wiring = argv.includes('--machine') ? readRemotes() : null;
  const machine = wiring === null ? null : {
    source: wiring.source,
    path: wiring.path,
    remotes: [...wiring.remotes.keys()].sort().map((workspace) => ({
      workspace,
      url: wiring.remotes.get(workspace)!.url,
      // The audit prefix only — `status` is the command most likely to be pasted into an issue.
      key: redactKey(wiring.remotes.get(workspace)!.key),
    })),
  };

  // For the agent operator (D37): the same facts, in a shape it can act on rather than parse.
  if (argv.includes('--json')) {
    write(JSON.stringify({
      harness: manifest.harnessDescriptor.name,
      source: manifest.source,
      packs: ['core', ...manifest.packs],
      target: manifest.target,
      synced: lock !== null,
      canonVersion: lock?.canonVersion ?? null,
      files: lock?.entries.length ?? 0,
      coreBytes: inspection?.coreBytes ?? null,
      coreBudgetBytes: manifest.coreBudgetBytes,
      drifted: inspection?.drifted ?? [],
      missing: inspection?.missing ?? [],
      stalePacks: inspection?.stalePacks ?? [],
      // Everything `check` fails on, so "why is check red?" is answered here too (REV3 CLI F11).
      staleSwitches: inspection?.staleSwitches ?? [],
      indexStale: inspection?.indexStale ?? false,
      switchedOff,
      offers,
      selectionProblem,
      // The one disclosure control in the manifest (D47 §4) — "what is this repository sharing?" is
      // exactly the question status exists to answer, and silence means local.
      remote: manifest.remote ?? null,
      local,
      canonSourceAvailable: canonAvailable,
      update: update ? { ...update, notes } : null,
      // Absent unless asked for: the wiring is machine state, and a repository's status answer is
      // about the repository. `--machine` is the person saying they want both.
      ...(machine ? { machine } : {}),
    }, null, 2));
    return 0;
  }

  write(`  harness       ${manifest.harnessDescriptor.name}`);
  write(`  source        ${manifest.source}`);
  write(`  packs         ${['core', ...manifest.packs].join(', ')}`);
  write(`  canon         ${lock ? `${lock.canonVersion} (${lock.entries.length} files)` : 'never synced'}`);
  if (manifest.remote) {
    write(`  remote        join${manifest.remote.knowledge ? ' + knowledge' : ''} (declared in daoris.json)`);
  }

  if (machine) {
    write(machine.path === null
      ? '  machine       no Daoris home — set DAORIS_HOME to read this machine\'s wiring (D63)'
      : `  machine       ${machine.path}${machine.source === 'environment' ? ' (overridden by the environment)' : ''}`);
    if (machine.remotes.length === 0) {
      write('  wiring        none — every workspace on this machine stays local (D21)');
    }
    for (const remote of machine.remotes) {
      write(`  wiring        ${remote.workspace} → ${remote.url}  ${remote.key}`);
    }
  }

  if (inspection) {
    write(`  core budget   ${inspection.coreBytes} / ${manifest.coreBudgetBytes} bytes`);
    if (inspection.drifted.length) write(`  drifted       ${inspection.drifted.join(', ')}`);
    if (inspection.missing.length) write(`  missing       ${inspection.missing.join(', ')}`);
    if (inspection.stalePacks.length) write(`  stale packs   ${inspection.stalePacks.join(', ')}`);
    for (const stale of inspection.staleSwitches) write(`  stale         ${stale} — run 'daoris sync'`);
    if (inspection.indexStale) {
      write("  roster        the doctrine region's on-demand tables are out of date — run 'daoris sync'");
    }
  }

  if (selectionProblem) write(`  selection     ${selectionProblem}`);
  for (const row of switchedOff) {
    write(`  switched off  ${row.target} — by pack '${row.by}'${row.because ? `: ${row.because}` : ''}`);
  }
  for (const offer of offers) {
    write(`  offered       ${offer.target} — pack '${offer.by}' would switch it off: ${offer.because}`);
    write(`                it stays on until daoris.json confirms it: "switchedOff": { "${offer.target}": "${offer.by}" }`);
  }

  if (local.length) write(`  local         ${local.join(', ')}`);

  if (!canonAvailable) {
    write(`  canon source  unavailable at '${canonRoot}' (check still works)`);
  } else if (update) {
    write(
      `  update        canon ${update.available} available (lock has ${update.locked}) — run 'daoris sync'`,
    );
    for (const target of update.changed) write(`                  changed  ${target}`);
    for (const target of update.added) write(`                  new      ${target}`);
    for (const target of update.retired) write(`                  retired  ${target}`);
    if (update.versionOnly) {
      write('                  (version only — no document changed)');
    }

    for (const note of notes) {
      write('');
      write(`  why ${note.version}`);
      for (const line of note.body.split('\n')) write(line ? `    ${line}` : '');
    }
  }
  return 0;
}
