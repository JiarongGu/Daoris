#!/usr/bin/env node
/**
 * Release rehearsal — install the packaged tool into a clean repository and
 * drive the whole consumer lifecycle through it.
 *
 * Everything else in this repo tests the source tree. This tests the ARTEFACT:
 * the tarball npm would publish, resolved through the `bin` entry the way a
 * consumer runs it. That gap is where install stories break — a file missing
 * from `files`, a path that only resolves relative to the source checkout, a
 * skill directory that does not survive packing.
 *
 * The one thing it cannot rehearse is the GitHub tag itself, which needs the
 * owner decision (TASKS.md REL1). Everything up to that point is real.
 *
 *   node tools/release-rehearsal.mjs
 *
 * Exit 0 = the release would work. Exit 1 = it would not.
 */
import { execSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { copyTree } from './fsx.mjs';
import { capture, makeChecker, openTranscript } from './rehearsal-kit.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const cliRoot = join(repoRoot, 'src', 'Daoris.Cli'); // the publishable package
const scratch = join(repoRoot, '_fixtures', 'release-rehearsal');
const consumer = join(scratch, 'consumer');
const canonV2 = join(scratch, 'canon-v2');

// REH1: every run leaves a transcript, outside the scratch tree a passing run deletes. The failure
// this chases is intermittent — twice seen, always exactly the canon-upgrade phase, both times on a
// run straight after canon files were edited and synced — and the one thing both sightings lacked was
// the output. Capturing it by construction beats remembering to capture it before a re-run.
openTranscript(repoRoot, 'release');
const { totals, check, section } = makeChecker();

/** Run in the consumer repo through the installed bin, capturing output and exit code. */
const daoris = (args) => capture(`npx --no-install daoris ${args}`, consumer);

/** The consumer, pointed at the writable canon copy that plays "upstream" from phase 4 on. */
const withV2 = (args) => capture(`npx --no-install daoris ${args}`, consumer, {
  env: { DAORIS_CANON: canonV2 },
});

const read = (rel) => readFileSync(join(consumer, rel), 'utf8');
const has = (rel) => existsSync(join(consumer, rel));

// ---------------------------------------------------------------- 1. package

section('1. Package the artefact');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });

const packed = execSync(`npm pack --pack-destination "${scratch}"`, {
  cwd: cliRoot,
  encoding: 'utf8',
  stdio: ['ignore', 'pipe', 'ignore'], // npm writes its file listing to stderr
}).trim().split('\n').pop().trim();
const tarball = join(scratch, packed);
check('npm pack produced a tarball', existsSync(tarball), tarball);

const pkg = JSON.parse(readFileSync(join(cliRoot, 'package.json'), 'utf8'));
const version = pkg.version;
check(`tarball is named for version ${version}`, packed.includes(version), packed);

// What matters is what SHIPS, not what sits in the checkout. `files` is a
// whitelist and cannot reach outside the package, so the canon, the licence and
// the readme — all of which live at the workspace root — are only present
// because `prepack` stages them in.
const shipped = new Set(
  JSON.parse(
    execSync('npm pack --dry-run --json', {
      cwd: cliRoot, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'],
    }),
  )[0].files.map((f) => f.path),
);
check('the README ships', shipped.has('README.md'));
check(
  `a LICENSE ships alongside the "${pkg.license}" declaration in package.json`,
  shipped.has('LICENSE'),
  'a licence declared but not distributed leaves recipients without the terms',
);
check('the canon ships', [...shipped].some((p) => p.startsWith('canon/core/rules/')));
check('canon skills ship as nested directories', shipped.has('canon/core/skills/doc-loader/SKILL.md'));
check('the canon changelog ships', shipped.has('canon/CHANGELOG.md'));
check('private material does not ship', ![...shipped].some((p) => p.startsWith('local/')));
check('test fixtures do not ship', ![...shipped].some((p) => p.startsWith('_fixtures/')));

// Belt and braces beyond postpack: whatever the pack lifecycle staged is removed here too, and the
// removal is ASSERTED — a `dist/` outliving a pack is exactly how DRV5's remote landing ran month-old
// code through every bin-driven gate while the source-level suites stayed green (FIX-LOG 2026-09-20).
execSync(`node "${join(repoRoot, 'tools', 'stage-package.mjs')}" --clean`, { stdio: 'pipe' });
check(
  'nothing staged outlives the pack — no dist/ to shadow the sources, no canon to shadow the tree',
  !existsSync(join(cliRoot, 'dist')) && !existsSync(join(cliRoot, 'canon')),
  'a gitignored leftover here silently shadows the sources for every later bin-driven run',
);

// --------------------------------------------------------------- 2. install

section('2. Install into a clean repository');
mkdirSync(consumer, { recursive: true });
writeFileSync(
  join(consumer, 'package.json'),
  `${JSON.stringify({ name: 'rehearsal-consumer', version: '1.0.0', private: true }, null, 2)}\n`,
);
execSync(`npm install --no-audit --no-fund "${tarball}"`, { cwd: consumer, stdio: 'pipe' });
check('npm install succeeded', existsSync(join(consumer, 'node_modules', 'daoris')));

const versionRun = daoris('--version');
check('`daoris --version` runs through the bin entry', versionRun.code === 0);
check(`it reports ${version}`, versionRun.out.trim() === version, versionRun.out.trim());

const help = daoris('--help');
check('`daoris --help` exits 0 and lists the commands', help.code === 0 && /init/.test(help.out));

// The repo writes its own document BEFORE adopting, so the collision path is real.
//
// 🔴 A KNOWLEDGE document, because that tier is still files. Since D59 the always-loaded tier is a
// span in `AGENTS.md`, so a repository's own `.claude/rules/x.md` is no longer at a path the canon
// claims — it cannot collide, and `doctor` is what reports one restating a canonical rule.
mkdirSync(join(consumer, '.claude', 'knowledge'), { recursive: true });
writeFileSync(join(consumer, '.claude/knowledge/reaching-in.md'), '# Our own document\n\nWritten here first.\n');
mkdirSync(join(consumer, '.claude', 'skills', 'house-deploy'), { recursive: true });
writeFileSync(
  join(consumer, '.claude/skills/house-deploy/SKILL.md'),
  '---\nname: house-deploy\ndescription: This repo\'s own deploy procedure.\n---\n\nSteps.\n',
);

// ------------------------------------------------------------- 3. lifecycle

section('3. The consumer lifecycle');
const init = daoris('init');
check('`init` exits 0', init.code === 0, init.out);
check('`init` writes a manifest', has('daoris.json'));
check('`init` reports available packs', /dotnet-library|windows-machine/.test(init.out), init.out);
check("`init` names the repo's own skill as local", /house-deploy/.test(init.out), init.out);

const collide = daoris('sync');
check('`sync` refuses to clobber the pre-existing document', collide.code === 1, collide.out);
check('...and says which file', /reaching-in/.test(collide.out), collide.out);
check(
  '...and leaves it untouched',
  read('.claude/knowledge/reaching-in.md').includes('Written here first'),
);

rmSync(join(consumer, '.claude/knowledge/reaching-in.md'));
const sync = daoris('sync');
check('`sync` exits 0 once the collision is resolved', sync.code === 0, sync.out);
// The always-loaded tier lands as a span in the file every harness reads (D59), with the pointer
// beside it for the one that reads another name.
check('the doctrine region is materialized', /<!-- daoris:rules /.test(read('AGENTS.md')));
check('...carrying the rules themselves', /sensitive-info/.test(read('AGENTS.md')));
check('...and the roster above them', /## Read on demand/.test(read('AGENTS.md')));
check('the pointer is written', /@AGENTS\.md/.test(read('CLAUDE.md')));
check('on-demand knowledge is still a file', has('.claude/knowledge/reaching-in.md'));
check('skills survive packing as directories', has('.claude/skills/doc-loader/SKILL.md'));
check('the lock is written', has('daoris.lock'));

const skill = read('.claude/skills/doc-loader/SKILL.md');
check('a skill still starts with its frontmatter', skill.startsWith('---\n'), skill.slice(0, 60));
check('...with the provenance header beneath it', /---\n<!-- daoris: /.test(skill));

// The roster is part of the region now, not a file beside it — so it is loaded rather than merely
// present, which is the whole reason the tier moved.
const roster = read('AGENTS.md');
check("the roster marks the repo's own skill local", /house-deploy.*\(local\)/.test(roster));
check('the roster lists canonical skills unmarked', /\[doc-loader\]/.test(roster));

const checkRun = daoris('check');
check('`check` exits 0 on a freshly synced repo', checkRun.code === 0, checkRun.out);
check('`check` reports the always-loaded budget', /bytes/.test(checkRun.out), checkRun.out);

const doctor = daoris('doctor');
check('`doctor` exits 0 (advisory, never fails)', doctor.code === 0, doctor.out);

// ---------------------------------------------------- 4. drift and the return

section('4. Drift, and the return path');
// A rule is edited where it lives now (D59): inside the region, which is what keeps drift and the
// return path per RULE rather than per region.
const regionFile = join(consumer, 'AGENTS.md');
const editRule = (from, to) =>
  writeFileSync(regionFile, readFileSync(regionFile, 'utf8').replace(from, to));
editRule('# Task lifecycle', '# Task lifecycle\n\nA local improvement.');

const drifted = daoris('check');
check('`check` catches a local edit', drifted.code === 1, drifted.out);
check('...naming the file', /task-lifecycle/.test(drifted.out), drifted.out);

const refused = daoris('sync');
check('`sync` refuses rather than discarding the edit', refused.code === 1, refused.out);
check('...and points at `upstream`', /upstream/.test(refused.out), refused.out);

// A consumer cannot upstream into a read-only install, which is correct: the
// canon lives in the package. Point at a writable copy, as a canon developer does.
copyTree(join(consumer, 'node_modules', 'daoris', 'canon'), canonV2);
const upstreamed = withV2('upstream task-lifecycle.md');
check('`upstream` promotes the edit into the canon', upstreamed.code === 0, upstreamed.out);
check(
  '...and the canon now carries it, without the provenance header',
  (() => {
    const promoted = readFileSync(join(canonV2, 'core/rules/task-lifecycle.md'), 'utf8');
    return promoted.includes('A local improvement.') && !promoted.includes('<!-- daoris:');
  })(),
);

// -------------------------------------------------------------- 5. upgrading

section('5. Upgrading to a newer canon');

const setCanonVersion = (v) => writeFileSync(join(canonV2, 'canon.json'), `{\n  "version": "${v}"\n}\n`);
const setChangelog = (body) =>
  writeFileSync(join(canonV2, 'CHANGELOG.md'), `# Canon changelog\n\n${body}\n## 0.0.1\n\n- The first canon.\n`);

// (a) The canon ships as a new version, carrying the edit promoted in step 4,
//     and the repo adopts a pack at the same time.
const sensitive = join(canonV2, 'core/rules/sensitive-info.md');
setCanonVersion('0.0.2');
writeFileSync(sensitive, `${readFileSync(sensitive, 'utf8')}\nAlso: never paste a token into an issue.\n`);
setChangelog('## 0.0.2\n\n- `sensitive-info` now covers tokens pasted into issues.\n\n');
const manifest = JSON.parse(read('daoris.json'));
manifest.packs = ['windows-machine'];
writeFileSync(join(consumer, 'daoris.json'), `${JSON.stringify(manifest, null, 2)}\n`);

const adopt = withV2('sync');
check('a promoted edit survives the canon shipping as a new version', adopt.code === 0, adopt.out);
check('...and the pack installs', /windows-machine/.test(read('AGENTS.md')));
check('...with the promotion intact', read('AGENTS.md').includes('A local improvement.'));

// (b) A canonical file is renamed upstream.
setCanonVersion('0.0.3');
copyFileSync(
  join(canonV2, 'packs/windows-machine/rules/windows-machine.md'),
  join(canonV2, 'packs/windows-machine/rules/windows-traps.md'),
);
rmSync(join(canonV2, 'packs/windows-machine/rules/windows-machine.md'));
setChangelog('## 0.0.3\n\n- `windows-machine` renamed to `windows-traps`.\n\n');

const renamed = withV2('sync');
check('a rename is reported as a rename', /renamed\s+rules\/windows-machine\.md/.test(renamed.out), renamed.out);
// By its provenance line, not by its name: the PACK is also called `windows-machine`, so the bare
// word is still in the region legitimately and a looser assertion fails on a rename that worked.
check('...the old rule is gone', !/rules\/windows-machine\.md @/.test(read('AGENTS.md')));
check('...and the new one is present', /rules\/windows-traps\.md @/.test(read('AGENTS.md')));

// (c) A version bump that changes no document at all.
setCanonVersion('0.0.4');
const bumpOnly = withV2('status');
check('a pure version bump reports no document change', /version only/.test(bumpOnly.out), bumpOnly.out);

// (d) A real change, with its reason.
setCanonVersion('0.0.5');
writeFileSync(sensitive, `${readFileSync(sensitive, 'utf8')}\nAnd never in a screenshot.\n`);
setChangelog('## 0.0.5\n\n- `sensitive-info` extended to screenshots.\n\n');

const status = withV2('status');
check('`status` reports an available update', /update/.test(status.out), status.out);
check('...names the changed file', /changed\s+rules\/sensitive-info\.md/.test(status.out), status.out);
check('...and prints why it changed', /why 0\.0\.5/.test(status.out), status.out);
check('...quoting the canon changelog', /extended to screenshots/.test(status.out), status.out);

const upgrade = withV2('sync');
check('`sync` applies the update', upgrade.code === 0, upgrade.out);
check(
  '...and the new wording is on disk',
  read('AGENTS.md').includes('never in a screenshot'),
);
check('`check` is clean afterwards', withV2('check').code === 0);

// (e) A pack switches a core row off (D71): offered with a reason, confirmed by the repository,
//     named on every surface, and withdrawn again — through the packed bin, not the source tree.
setCanonVersion('0.0.6');
mkdirSync(join(canonV2, 'packs/checkpointing/rules'), { recursive: true });
writeFileSync(join(canonV2, 'packs/checkpointing/pack.json'), `${JSON.stringify({
  name: 'checkpointing',
  apiVersion: 1,
  description: 'A rehearsal pack whose own rule replaces a core one.',
  switchesOff: { 'rules/persist-working-state.md': 'its own checkpoints rule replaces it' },
}, null, 2)}\n`);
writeFileSync(
  join(canonV2, 'packs/checkpointing/rules/checkpoints.md'),
  '---\nname: checkpoints\napplies_when: w\nenforces: e\n---\n\n# Checkpoints\n\nWrite state down as you go.\n',
);
setChangelog('## 0.0.6\n\n- A rehearsal pack that switches a core row off.\n\n');
const writeManifest = (change) => {
  const current = JSON.parse(read('daoris.json'));
  change(current);
  writeFileSync(join(consumer, 'daoris.json'), `${JSON.stringify(current, null, 2)}\n`);
};
const coreRow = /core\/rules\/persist-working-state\.md @/;

writeManifest((m) => { m.packs = [...m.packs, 'checkpointing']; });
const offered = withV2('sync');
check('a pack offering to switch a core row off installs, and the offer is named', offered.code === 0
  && /offered\s+rules\/persist-working-state\.md/.test(offered.out), offered.out);
check('...while the core row stays on until the repository confirms it', coreRow.test(read('AGENTS.md')));

writeManifest((m) => { m.switchedOff = { 'rules/persist-working-state.md': 'checkpointing' }; });
const switched = withV2('sync');
check('a confirmed switch takes the core row out, and sync names it', switched.code === 0
  && /off\s+rules\/persist-working-state\.md/.test(switched.out), switched.out);
check('...the rule is gone from the region', !coreRow.test(read('AGENTS.md')));
check(
  '...and the roster says so, naming the pack',
  /Switched off here[^\n]*`persist-working-state` \(by `checkpointing`\)/.test(read('AGENTS.md')),
);
check('...the lock records it', /"switchedOff"/.test(read('daoris.lock')));
const offCheck = withV2('check');
check('`check` stays clean and names the row it switched off', offCheck.code === 0
  && /off\s+rules\/persist-working-state\.md/.test(offCheck.out), offCheck.out);
const offStatus = withV2('status');
check('`status` names it with its pack and reason',
  /switched off\s+rules\/persist-working-state\.md.*checkpointing.*replaces it/.test(offStatus.out), offStatus.out);

writeManifest((m) => { delete m.switchedOff; });
const withdrawn = withV2('sync');
check('withdrawing the confirmation brings the core row back', withdrawn.code === 0 && coreRow.test(read('AGENTS.md')),
  withdrawn.out);
check('`check` is clean after the withdrawal', withV2('check').code === 0);

// ----------------------------------------------------------------- 6. report

section('6. Result');
console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
if (totals.failures) {
  console.log(`  ${totals.failures} FAILED — do not tag a release until these pass.`);
  console.log(`  Scratch left at _fixtures/release-rehearsal for inspection.\n`);
  // exitCode rather than exit: the transcript writer rides the exit event, and the same convention
  // as the family rehearsal means the same failure reads the same way from either gate.
  process.exitCode = 1;
} else {
  console.log('  The packaged tool installs into a clean repo and drives the full lifecycle:');
  console.log('  adopt, collide, sync, drift, promote, upgrade, rename, switch off, and check.\n');
  rmSync(scratch, { recursive: true, force: true });
}
