#!/usr/bin/env node
/**
 * Which test classes of a `Process` half a change reaches, so the CI runs those again and keeps
 * the rest of the half's verdicts.
 *
 * ## Why
 *
 * A release's verification at `c557800` ran the driver's git half (693 tests, 272 minutes of test time in 69 of wall) and its
 * other Process half (666 tests, 130 minutes in 23), and any driver or modules change reached both whole: the CI skips a gate no
 * changed path reaches, and a gate reached runs every class in it. This goes one level finer, to the class.
 *
 * ## What a class reaches (the census, `censusOf`)
 *
 * Read from the sources, never a hand-kept list: a list goes stale the day a class is written, as the Process category's did
 * (`isProcessGate` in the merge tool). For a half (`halfOf`: the test project and the run settings its gate's command names):
 *
 * - **Its classes** are the test project's classes with a `[Fact]` or `[Theory]` of their own or a base's, whose traits (their
 *   own and their bases', as xUnit reads them, or one test's own with them) hold the run settings' `TestCaseFilter`.
 * - **A class's files** are the files that declare it and its bases, and, read through, every file of the test project that
 *   declares what they name (a type, or an extension method): a helper's code runs in the class, so what it names the class
 *   reaches.
 * - **Its reach** is the source areas its files name: each top-level type and extension method of a project the test project
 *   compiles against (`ProjectReference`, read through) is defined in a file, and its area is that project's first folder
 *   (`Daoris.Desktop.Driver/Sessions`, a driver module). A program the test project builds only to start
 *   (`ReferenceOutputAssembly="false"`) is an area of its own, reached by the classes whose strings name it, with the areas of
 *   the sources it compiles in by link, and every area where it compiles against a source project (the driver's host).
 * - Only what the files name is followed, not what the named types use in turn: the driver's modules name each other nearly
 *   all, so a closure over them would reach every class from every change. What that misses is the sweep's (the merge tool
 *   runs a whole half at least daily, and a release refuses older), and a hand-written `[Reaches]` with its reason.
 * - **A class with no reach** is reached by every source path that reaches its half: an unknown runs.
 *
 * ## What a changed path reaches (`pathReach`)
 *
 * Only a path the merge tool's lane table sends to the half's gate is asked here. Then:
 * - a file of a source project, under one of its folders: the classes whose reach holds that area; a program's file, the
 *   classes that start it;
 * - a C# file of the test project: the classes whose files hold it, none where no class of the half reads it;
 * - a file of the test project a test reads (a fixture, a golden): the classes whose strings name it or a folder above it;
 * - another project's file beside them: the classes whose strings name that project, none where none does (nothing compiles
 *   against it);
 * - the whole half for what acts without being named: a test-project file with a `global using`, an assembly or module
 *   attribute or a module initializer, a project file, a project's root, `Directory.Build.props`, the run settings, and any path
 *   this map cannot place (the lane table's every-gate files, a CLI source a test runs, the example plugins).
 *
 * ## How the CI runs a half by reach
 *
 * The merge tool keeps each class's verdict with the tree it ran on, and a class whose newest standing verdict passed is kept.
 * The half then runs with run settings of its own (`reachSettings`): its filter ANDed with `FullyQualifiedName!~<class>.` for
 * each kept class, in a file rather than on the command line (cmd's 8,191 characters: two hundred class names do not fit, and
 * VSTest reads a settings file's filter whole). Keeping a class out rather than naming each class to run means a class this
 * census never saw still runs.
 *
 *   node tools/process-reach.mjs census [<gate>…]              each half's classes and reach
 *   node tools/process-reach.mjs reach <from> <to> [<gate>…]   which classes the paths changed between two commits reach
 *
 * Exit codes: 0 answered · 2 a tool error (no such gate, a commit git cannot read).
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join, posix } from 'node:path';
import { isMain } from './fsx.mjs';
import { censusOf, classesReached, halfOf, names } from './process-reach/census.mjs';
export { readCs, namesIn } from './process-reach/source.mjs';
export { typesIn, traitsIn, reachesIn } from './process-reach/types.mjs';
export { settingsFilter, filterHolds, reachSettings } from './process-reach/settings.mjs';
export { halfOf, withSettings, referencedProjects, areaOf, censusOf, pathReach, classesReached } from './process-reach/census.mjs';

// ---------------------------------------------------------------------------------------------------
// Reading C#

/** What the census reads of a commit: the sources, projects and run settings of the desktop and the service, whose test projects the halves run, and the declared gates. */
const READ = (path) => path === 'daoris.gates.json'
  || ((path.startsWith('src/Daoris.Desktop/') || path.startsWith('src/Daoris.Service/')) && /\.(cs|csproj|runsettings|props|targets)$/.test(path));

/**
 * The repository's tracked files at `commit` (the working tree's when null), `/`-separated, and a reader of each. A commit's
 * files are read by one `git cat-file --batch`: a `git show` a file took half a second to start on this machine,
 * and the census reads about two thousand.
 */
export function repository(root, commit = null) {
  const listed = spawnSync('git', commit ? ['ls-tree', '-r', '-z', commit] : ['ls-files', '-z'], { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
  if (listed.status !== 0) throw new Error(`git could not list ${commit ?? 'the tree'}: ${String(listed.stderr).trim()}`);
  if (!commit) {
    const files = listed.stdout.split('\0').filter(Boolean);
    return { files, read: (file) => (existsSync(join(root, file)) ? readFileSync(join(root, file), 'utf8') : '') };
  }
  const blobs = new Map();
  for (const entry of listed.stdout.split('\0').filter(Boolean)) {
    const parts = /^\d+ (\w+) ([0-9a-f]+)\t(.*)$/s.exec(entry);
    if (parts && parts[1] === 'blob') blobs.set(parts[3], parts[2]);
  }
  const wanted = [...blobs].filter(([path]) => READ(path));
  const batch = spawnSync('git', ['cat-file', '--batch'], { cwd: root, windowsHide: true, input: `${wanted.map(([, sha]) => sha).join('\n')}\n`, maxBuffer: 1024 * 1024 * 1024 });
  if (batch.status !== 0) throw new Error(`git could not read ${commit}'s files: ${String(batch.stderr).trim()}`);
  const texts = new Map();
  let at = 0;
  for (const [path] of wanted) {
    const eol = batch.stdout.indexOf(10, at);
    const size = Number(/ (\d+)$/.exec(batch.stdout.subarray(at, eol).toString('utf8'))?.[1] ?? 0);
    texts.set(path, batch.stdout.subarray(eol + 1, eol + 1 + size).toString('utf8'));
    at = eol + 1 + size + 1;
  }
  return { files: [...blobs.keys()], read: (file) => texts.get(file) ?? '' };
}

/** The declared Process halves, all or those named. */
function halvesOf(declared, names) {
  const halves = declared.filter((gate) => /process\.runsettings/.test(gate.run));
  const unknown = names.filter((name) => !halves.some((gate) => gate.name === name));
  if (unknown.length) throw new Error(`no Process half named ${unknown.join(', ')}: ${halves.map((gate) => gate.name).join(', ')}`);
  return names.length ? halves.filter((gate) => names.includes(gate.name)) : halves;
}

async function main(argv) {
  const [verb, ...rest] = argv;
  const root = process.cwd();
  if (verb === 'census') {
    const { files, read } = repository(root);
    for (const gate of halvesOf(JSON.parse(read('daoris.gates.json')).gates ?? [], rest)) {
      const half = halfOf(gate.run);
      const census = censusOf(half, files, read);
      console.log(`${gate.name}: ${census.classes.length} classes (${half.project}, ${half.settings}: ${census.filter})`);
      console.log(`  compiles against ${census.sources.join(', ')}; starts ${census.programs.map((program) => program.folder).join(', ') || 'nothing'}`);
      for (const each of census.classes) {
        console.log(`  ${each.name}: ${each.reach.length ? each.reach.map((area) => posix.basename(area)).join(', ') : '(no reach: every change reaches it)'}`);
      }
      for (const problem of census.problems) console.log(`  problem: ${problem}`);
    }
    return 0;
  }
  if (verb === 'reach') {
    const [from, to, ...wanted] = rest;
    if (!from || !to) throw new Error('usage: node tools/process-reach.mjs reach <from> <to> [<gate>…]');
    const diff = spawnSync('git', ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', from, to], { cwd: root, windowsHide: true, encoding: 'utf8' });
    if (diff.status !== 0) throw new Error(`git could not compare ${from} and ${to}: ${String(diff.stderr).trim()}`);
    const paths = diff.stdout.split('\0').filter(Boolean);
    const { files, read } = repository(root, to);
    // The lane table's answer first: a path that cannot reach a half's gate reaches none of its classes.
    const { pathsReaching, readLanes } = await import('./merge-branch.mjs');
    const lanes = readLanes(root)?.lanes ?? [];
    for (const gate of halvesOf(JSON.parse(read('daoris.gates.json')).gates ?? [], wanted)) {
      const reaching = pathsReaching(gate.name, paths, { lanes });
      if (!reaching.length) {
        console.log(`${gate.name}: no changed path reaches it`);
        continue;
      }
      const census = censusOf(halfOf(gate.run), files, read);
      const reached = classesReached(reaching, census);
      const why = reached.whole.length ? `the whole half: ${reached.whole[0].why}` : `${reached.classes.length} of ${census.classes.length} classes`;
      console.log(`${gate.name}: ${why}`);
      if (!reached.whole.length) for (const name of reached.classes) console.log(`  ${name.split('.').at(-1)} (${reached.by.get(name)})`);
    }
    return 0;
  }
  console.error('usage: node tools/process-reach.mjs census [<gate>…] | reach <from> <to> [<gate>…]');
  return 2;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`process-reach: ${error.message}`);
      process.exitCode = 2;
    },
  );
}
