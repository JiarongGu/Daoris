import type { CommandArgs, Harness } from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, listMarkdown } from './fsx.ts';
import { readCanon, resolveCanonRoot, resolveSelection } from './canon.ts';
import { MANIFEST_FILE, lockIndex, readLock, readManifest, writeManifest } from './config.ts';
import { planChanges } from './materialize.ts';
import { notesBetween } from './notes.ts';
import { newerLock } from './lockversion.ts';
import { inspect } from './drift.ts';
import { HARNESSES, DEFAULT_HARNESS, resolveHarness } from './harness.ts';
import { formerDocuments, lockLayout } from './layout.ts';
import { describeLink, linkProblems } from './links.ts';
import { candidateDocuments, sayCandidates } from './documents.ts';
import { flagValue } from './args.ts';
import { readRemotes, redactKey } from './remotemap.ts';
import { DaorisError } from './errors.ts';

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
  { root, argv = [], write, packageRoot }:
    Pick<CommandArgs, 'root' | 'write' | 'packageRoot'> & { argv?: CommandArgs['argv'] },
): ExitCode {
  if (existsSync(join(root, MANIFEST_FILE))) {
    throw new DaorisError(`${MANIFEST_FILE} already exists — edit it, or delete it to start over`);
  }
  // A new adopter may choose the agents layout (D117). Resolved before anything is written, so an
  // unknown name is a tool error naming what exists and leaves no manifest behind. The older layout
  // stays the default: the family's newcomers write `.claude/knowledge/` after `init`, and the service
  // reads `.claude` until it reads the lock's root (LAYOUT4).
  const harness = resolveHarness(flagValue(argv, '--harness') ?? DEFAULT_HARNESS);
  const older = harness.id === DEFAULT_HARNESS;
  const canon = readCanon(resolveCanonRoot(packageRoot));

  writeManifest(root, {
    // The npm package the canon shipped in, at the canon's version (DIST1, D105): provenance, and the
    // command to re-run as `npx <source> sync` (D11). A git ref could not run: the repository's root
    // package is a private workspace with no `bin`.
    source: `daoris@${canon.version}`,
    packs: [],
    ...(older ? {} : { harness: harness.id }),
    target: harness.defaultTarget,
    // Declared, never found (D117 §2.2): a folder with an AGENTS.md of its own is named here.
    ...(older ? {} : { rooms: [] }),
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

  const local = localDocs(root, harness.defaultTarget, harness);
  if (local.length) {
    write('');
    write("  this repo's own docs (never synced, never touched):");
    for (const file of local) write(`    ${file}`);
  }
  // Under the root the older layout used, its index lists nothing (D117 §5.2): each is named with the
  // move that brings it under the target, which is the repository's own act (D5).
  const former = formerDocuments({ root, harness, target: harness.defaultTarget });
  if (former.length) {
    write('');
    write(`  this repo's own docs where the ${harness.id} layout's index will not list them:`);
    for (const doc of former) write(`    ${doc.path} — ${doc.move}`);
  }
  // Said now, because `sync` will refuse it (D117 §5.4): the choice of what replaces it is the repository's.
  const links = linkProblems(root, { pairs: ['AGENTS.md', 'CLAUDE.md'] });
  if (links.length) {
    write('');
    for (const problem of links) write(`  ${describeLink(problem)}`);
  }
  // Named and never written (D122 §2.7): declaring is the repository's act, done by its own session.
  sayCandidates(candidateDocuments(root), write);
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
  // Where the files are is the lock's answer (D117 §5.1), so a pending move lists them where they stand.
  const was = lockLayout(root, lock, manifest);
  const local = localDocs(root, was.target, was.harness);
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

  // A lock a newer canon wrote is not an update: `sync` and `upstream` refuse it (WSSETUP4, D124 §1.4), so
  // this says so and names the tool at the lock's version, rather than sending the person to `sync`.
  const behind = canon && lock ? newerLock(lock, canon.version) : null;

  // status may reach the canon; `check` deliberately may not (D8), which is why
  // "a newer canon exists" is reported here and never gates a build.
  if (canon && lock && !selectionProblem && !behind) {
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
      // The descriptor by its manifest name, its rooms, and how many mirror files it keeps (D117).
      layout: manifest.harnessDescriptor.id,
      source: manifest.source,
      packs: ['core', ...manifest.packs],
      target: manifest.target,
      rooms: manifest.rooms,
      mirrors: lock?.mirrors?.length ?? 0,
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
      staleLayout: inspection?.staleLayout ?? null,
      mirrorsDrifted: inspection?.mirrorsDrifted ?? [],
      mirrorsMissing: inspection?.mirrorsMissing ?? [],
      mirrorsBehind: inspection?.mirrorsBehind ?? [],
      roomsWithoutInstructions: inspection?.roomsWithoutInstructions ?? [],
      roomPointersMissing: inspection?.roomPointersMissing ?? [],
      links: inspection?.links ?? [],
      // The declared documents and their facts (D122 §2.7–§2.8).
      documents: manifest.documents,
      documentsMissing: inspection?.documentsMissing ?? [],
      documentLinks: inspection?.documentLinks ?? [],
      documentsStale: inspection?.documentsStale ?? false,
      switchedOff,
      offers,
      selectionProblem,
      // The one disclosure control in the manifest (D47 §4) — "what is this repository sharing?" is
      // exactly the question status exists to answer, and silence means local.
      remote: manifest.remote ?? null,
      local,
      canonSourceAvailable: canonAvailable,
      update: update ? { ...update, notes } : null,
      // A lock a newer canon wrote, which `sync` and `upstream` refuse, and the tool to run instead (WSSETUP4).
      newerLock: behind,
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
  if (manifest.rooms.length) write(`  rooms         ${manifest.rooms.join(', ')}`);
  if (lock?.mirrors?.length) {
    write(`  mirrors       ${lock.mirrors.length} in ${was.harness.mirror?.root ?? 'a mirror root'}`);
  }
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
    if (inspection.staleLayout) write(`  layout        ${inspection.staleLayout} — run 'daoris sync'`);
    const mirrorFacts = [...inspection.mirrorsDrifted.map((m) => m.path), ...inspection.mirrorsMissing, ...inspection.mirrorsBehind];
    if (mirrorFacts.length) write(`  mirror        ${mirrorFacts.join(', ')} — 'daoris check' says which`);
    const roomFacts = [...inspection.roomsWithoutInstructions, ...inspection.roomPointersMissing];
    if (roomFacts.length) write(`  room          ${roomFacts.join(', ')} — 'daoris check' says which`);
    for (const problem of inspection.links) write(`  link          ${problem.path} — 'daoris check' says what it is`);
    for (const doc of inspection.documentsMissing) write(`  document      ${doc.path} (${doc.role}) — 'daoris check' says which`);
    for (const link of inspection.documentLinks) write(`  link          ${link.declared} (${link.role}) — 'daoris check' says what it is`);
    if (inspection.documentsStale) write("  where         the region's Where things are table is out of date — run 'daoris sync'");
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
  } else if (behind) {
    write(`  newer lock    the lock is at canon ${behind.locked}, and this daoris carries ${behind.carried} — `
      + `sync and upstream refuse; run ${behind.run}`);
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
