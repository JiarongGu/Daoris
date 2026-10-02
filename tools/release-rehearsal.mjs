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
import {
  copyFileSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, symlinkSync, unlinkSync, writeFileSync,
} from 'node:fs';
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
// The pointer above them names the index of the on-demand tiers, and lists no document (WSSETUP14a, D128 §2).
check('...and the pointer to the index above them',
  /## Read on demand\n\nThe knowledge and the skills, each with when it applies, are listed in \[\.claude\/INDEX\.md\]\(\.claude\/INDEX\.md\)/
    .test(read('AGENTS.md')) && !/house-deploy|## Invoke by name/.test(read('AGENTS.md')));
check('the pointer is written', /@AGENTS\.md/.test(read('CLAUDE.md')));
check('on-demand knowledge is still a file', has('.claude/knowledge/reaching-in.md'));
check('skills survive packing as directories', has('.claude/skills/doc-loader/SKILL.md'));
check('the lock is written', has('daoris.lock'));

const skill = read('.claude/skills/doc-loader/SKILL.md');
check('a skill still starts with its frontmatter', skill.startsWith('---\n'), skill.slice(0, 60));
check('...with the provenance header beneath it', /---\n<!-- daoris: /.test(skill));

// The knowledge and skills are listed in `<target>/INDEX.md`, which `sync` writes and the lock names
// (WSSETUP14a, D128 §2.3): the rules stay in the region, and the list that grows with the repository
// is read on demand.
const roster = has('.claude/INDEX.md') ? read('.claude/INDEX.md') : '';
check('the index is written beside the tiers, and the lock names it',
  roster.startsWith('# Index\n') && /"index": "\.claude\/INDEX\.md"/.test(read('daoris.lock')));
check("the index marks the repo's own skill local", /`\.claude\/skills\/house-deploy\/SKILL\.md` _\(local\)_/.test(roster));
check('the index lists canonical skills unmarked', /\| `\.claude\/skills\/doc-loader\/SKILL\.md` \|/.test(roster));

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

// (f) An older tool never rewrites a newer lock (WSSETUP4, D124 §1.4). The upgrades above moved the lock to
//     canon 0.0.6, and the installed package still carries its own canon at `version`: the packed bin, run
//     without the newer canon, is exactly `npx daoris@<older> sync` in a repository a newer tool synced.
const lockedAt = JSON.parse(read('daoris.lock')).canonVersion;
const regionBefore = read('AGENTS.md');
const lockBefore = read('daoris.lock');
const older = daoris('sync');
check(`the packed tool (canon ${version}) refuses to sync a lock canon ${lockedAt} wrote, exit 1`, older.code === 1, older.out);
check('...naming both versions and the command at the lock\'s',
  older.out.includes(lockedAt) && older.out.includes(version) && older.out.includes(`npx daoris@${lockedAt} sync`), older.out);
check('...and rewriting nothing: the region and the lock are byte for byte as the newer tool left them',
  read('AGENTS.md') === regionBefore && read('daoris.lock') === lockBefore);
const olderUpstream = daoris('upstream --all');
check('`upstream` refuses it too, exit 1, naming the command at the lock\'s version',
  olderUpstream.code === 1 && olderUpstream.out.includes(`npx daoris@${lockedAt} upstream --all`), olderUpstream.out);
const olderStatus = daoris('status');
check('`status` says the lock is newer and names that command, rather than sending the person to `sync`',
  olderStatus.code === 0 && /sync and upstream refuse/.test(olderStatus.out) && olderStatus.out.includes(`npx daoris@${lockedAt}`)
    && !/run 'daoris sync'/.test(olderStatus.out), olderStatus.out);

// --------------------------------------------------------- 6. move the layout

// LAYOUT3 (D117 §5.6): the upgrade an adopter on the older layout makes by flipping its own manifest,
// through the packed bin. Every cell is a unit test already; this proves the ARTEFACT carries them —
// the move, the refusals, the mirror and its return path, a room, and a link held as text.
section('6. Moving the layout to agents (D117)');

// The consumer keeps a skill of its own under .claude/ since phase 2; it gains a knowledge document too.
writeFileSync(
  join(consumer, '.claude/knowledge/house-notes.md'),
  '---\nname: house-notes\napplies_when: working on this repo\nenforces: its own notes\n---\n\n# House notes\n',
);
check('the local knowledge document is listed before the move', withV2('sync').code === 0
  && /house-notes.*\(local\)/.test(read('.claude/INDEX.md')));

writeManifest((m) => { m.harness = 'agents'; m.target = '.agents'; });
const staleLayout = withV2('check');
check('`check` fails on the flipped manifest, naming both layouts', staleLayout.code === 1
  && /agents/.test(staleLayout.out) && /claude-code/.test(staleLayout.out), staleLayout.out);

const planned = withV2('sync --dry-run');
check('`sync --dry-run` lists the moves and exits 1', planned.code === 1
  && /moved\s+\.claude\/knowledge\/reaching-in\.md -> \.agents\/knowledge\/reaching-in\.md/.test(planned.out), planned.out);
check('...and names the two documents it would leave behind',
  /LEFT BEHIND \.claude\/knowledge\/house-notes\.md/.test(planned.out)
  && /LEFT BEHIND \.claude\/skills\/house-deploy/.test(planned.out), planned.out);

const leftBehind = withV2('sync');
check('`sync` refuses the move, naming each git mv', leftBehind.code === 1
  && /git mv \.claude\/knowledge\/house-notes\.md \.agents\/knowledge\/house-notes\.md/.test(leftBehind.out)
  && /git mv \.claude\/skills\/house-deploy \.agents\/skills\/house-deploy/.test(leftBehind.out), leftBehind.out);
check('...and writes nothing', !has('.agents') && has('.claude/knowledge/reaching-in.md'));

// The repository's own act (D5): the consumer is not a git repository, so a rename plays the git mv.
mkdirSync(join(consumer, '.agents', 'knowledge'), { recursive: true });
mkdirSync(join(consumer, '.agents', 'skills'), { recursive: true });
renameSync(join(consumer, '.claude/knowledge/house-notes.md'), join(consumer, '.agents/knowledge/house-notes.md'));
renameSync(join(consumer, '.claude/skills/house-deploy'), join(consumer, '.agents/skills/house-deploy'));

const moved = withV2('sync');
check('`sync` moves the canonical files once the repository has moved its own', moved.code === 0
  && has('.agents/knowledge/reaching-in.md') && has('.agents/skills/doc-loader/SKILL.md'), moved.out);
check('...removes the folder the move emptied', !has('.claude/knowledge'));
check('...writes a mirror of every skill for Claude Code, its frontmatter still first',
  read('.claude/skills/doc-loader/SKILL.md').startsWith('---\n')
  && /mirror of \.agents\/skills\/doc-loader\/SKILL\.md/.test(read('.claude/skills/doc-loader/SKILL.md'))
  && /mirror of \.agents\/skills\/house-deploy\/SKILL\.md/.test(read('.claude/skills/house-deploy/SKILL.md')));
check('...re-roots the lock and records the mirrors',
  /"harness": "agents"/.test(read('daoris.lock')) && /"target": "\.agents"/.test(read('daoris.lock'))
  && /"mirrors"/.test(read('daoris.lock')));
check('...and the index moves to .agents, listing the skills there, and the region says the mirror sentence',
  !has('.claude/INDEX.md') && has('.agents/INDEX.md')
  && /\| `\.agents\/skills\/doc-loader\/SKILL\.md` \|/.test(read('.agents/INDEX.md'))
  && /\[\.agents\/INDEX\.md\]\(\.agents\/INDEX\.md\)/.test(read('AGENTS.md')) && /mirrors them/.test(read('AGENTS.md')));
const movedCheck = withV2('check');
check('`check` is clean after the move', movedCheck.code === 0, movedCheck.out);

// A mirror edited: named with its source, refused, promoted from the copy, and closed by the next sync.
const mirrorFile = join(consumer, '.claude/skills/doc-loader/SKILL.md');
writeFileSync(mirrorFile, `${readFileSync(mirrorFile, 'utf8')}\nA local improvement, made in the mirror.\n`);
const mirrorDrift = withV2('check');
check('`check` fails on an edited mirror, naming its source', mirrorDrift.code === 1
  && /a mirror of \.agents\/skills\/doc-loader\/SKILL\.md, edited here/.test(mirrorDrift.out), mirrorDrift.out);
const mirrorRefused = withV2('sync');
check('`sync` refuses, pointing at upstream from the mirror', mirrorRefused.code === 1
  && /daoris upstream \.claude\/skills\/doc-loader\/SKILL\.md/.test(mirrorRefused.out), mirrorRefused.out);
const mirrorUp = withV2('upstream .claude/skills/doc-loader/SKILL.md');
check('`upstream` takes the mirror\'s path', mirrorUp.code === 0, mirrorUp.out);
check('...and the canon carries the edit without either header', (() => {
  const promoted = readFileSync(join(canonV2, 'core/skills/doc-loader/SKILL.md'), 'utf8');
  return promoted.includes('made in the mirror') && !promoted.includes('<!-- daoris:') && promoted.startsWith('---\n');
})());
const mirrorClosed = withV2('sync');
check('the next `sync` is clean, and the source carries the edit', mirrorClosed.code === 0
  && read('.agents/skills/doc-loader/SKILL.md').includes('made in the mirror'), mirrorClosed.out);
check('`check` is clean again', withV2('check').code === 0);

// A room: declared, it gains its pointer and a row; undeclared, it loses the pointer and keeps its words.
mkdirSync(join(consumer, 'tools'), { recursive: true });
writeFileSync(join(consumer, 'tools/AGENTS.md'), '# Tools\n\nHow this repository\'s scripts are run.\n');
writeManifest((m) => { m.rooms = ['tools']; });
const roomed = withV2('sync');
check('a declared room gains its pointer', roomed.code === 0 && /@AGENTS\.md/.test(read('tools/CLAUDE.md')), roomed.out);
check('...and a row in the roster, with its heading', /\| \[tools\]\(tools\/AGENTS\.md\) \| Tools \|/.test(read('AGENTS.md')));
check('`check` is clean with the room', withV2('check').code === 0);
writeManifest((m) => { m.rooms = []; });
const unroomed = withV2('sync');
check('an undeclared room loses its pointer, and keeps its own words', unroomed.code === 0
  && !has('tools/CLAUDE.md') && read('tools/AGENTS.md').startsWith('# Tools'), unroomed.out);

// A link held as text: the reference's CLAUDE.md, checked out where links are not.
const claudeFile = join(consumer, 'CLAUDE.md');
const claudeText = readFileSync(claudeFile, 'utf8');
writeFileSync(claudeFile, 'AGENTS.md');
const heldAsText = withV2('sync');
check('a CLAUDE.md held as the 9 bytes `AGENTS.md` is refused with the link sentence', heldAsText.code === 1
  && /looks like a link checked out as text/.test(heldAsText.out), heldAsText.out);
check('...and never written through', readFileSync(claudeFile, 'utf8') === 'AGENTS.md');
writeFileSync(claudeFile, claudeText);

// A real link, where this runner can make one: a file link first, else a folder junction, which Windows
// grants without a privilege.
let realLink = null;
try {
  rmSync(claudeFile);
  symlinkSync('AGENTS.md', claudeFile, 'file');
  realLink = { path: 'CLAUDE.md', undo: () => { unlinkSync(claudeFile); writeFileSync(claudeFile, claudeText); } };
} catch {
  if (!existsSync(claudeFile)) writeFileSync(claudeFile, claudeText);
  const skills = join(consumer, '.claude', 'skills');
  const aside = join(consumer, '.claude', 'skills-aside');
  renameSync(skills, aside);
  symlinkSync(join(consumer, '.agents', 'skills'), skills, 'junction');
  realLink = { path: '.claude/skills', undo: () => { unlinkSync(skills); renameSync(aside, skills); } };
}
const linked = withV2('sync');
realLink.undo();
check(`a real link at ${realLink.path} is refused too`, linked.code === 1
  && new RegExp(`${realLink.path.replace(/\./g, '\\.')} is a link`).test(linked.out), linked.out);
check('`check` is clean once the link is gone', withV2('check').code === 0);

// ----------------------------------------------------- 7. declare the documents

// DOC3 (D122 §2.7–§2.8): the consumer binds its records to paths in its own manifest, and the packed bin
// renders the table, holds it to the facts, reports the judgements, and refuses what it cannot honour.
// Every case is a unit test already; this proves the ARTEFACT carries them. Declared, broken, repaired.
section('7. Declaring the development documents (D122)');

mkdirSync(join(consumer, 'docs'), { recursive: true });
writeFileSync(join(consumer, 'docs/DECISIONS.md'), '# Decisions\n\n## D1 — the first\n\nWhy, and what it rejected.\n');
writeFileSync(join(consumer, 'TASKS.md'), '# Tasks\n\n- [ ] One open row.\n');
writeFileSync(join(consumer, 'CHANGELOG.md'), '# Changelog\n\n- One change.\n');

const candidates = withV2('analyze --json');
// A report that does not parse fails this check rather than the run: every later phase still reports.
const named = (() => {
  try {
    return JSON.parse(candidates.out).documents ?? [];
  } catch {
    return [];
  }
})();
check('`analyze` names the records the consumer seems to keep, by role, and declares none', candidates.code === 0
  && ['decisions docs/DECISIONS.md', 'backlog TASKS.md', 'changelog CHANGELOG.md']
    .every((pair) => named.some((row) => `${row.role} ${row.path}` === pair))
  && JSON.parse(read('daoris.json')).documents === undefined, candidates.out);

writeManifest((m) => {
  m.documents = {
    decisions: 'docs/DECISIONS.md',
    backlog: { path: 'TASKS.md', words: 40 },
    changelog: 'CHANGELOG.md',
    brief: { words: 1500 },
  };
});
const undeclaredTable = withV2('check');
check('`check` fails on a declaration the region does not list yet, naming the table', undeclaredTable.code === 1
  && /Where things are table differs/.test(undeclaredTable.out), undeclaredTable.out);

const declaredSync = withV2('sync');
check('`sync` renders Where things are into the region', declaredSync.code === 0
  && /## Where things are/.test(read('AGENTS.md')), declaredSync.out);
check('...one row per declared path, in the roles\' order, with each role\'s job',
  /\| decisions \| `docs\/DECISIONS\.md` \| numbered decisions[^\n]*\n\| backlog \| `TASKS\.md` \|[^\n]*\n\| changelog \| `CHANGELOG\.md` \|/
    .test(read('AGENTS.md')));
check('...and no row for the brief, which is the file the region sits in', !/\| brief \|/.test(read('AGENTS.md')));
const declaredCheck = withV2('check');
check('`check` is clean with the documents declared', declaredCheck.code === 0, declaredCheck.out);

// Broken: a record goes, and comes back.
const decisionsText = read('docs/DECISIONS.md');
rmSync(join(consumer, 'docs/DECISIONS.md'));
const absent = withV2('check');
check('`check` fails on a declared document that is absent, naming it and its role', absent.code === 1
  && /docs\/DECISIONS\.md \(decisions\) is declared in daoris\.json, and absent/.test(absent.out), absent.out);
writeFileSync(join(consumer, 'docs/DECISIONS.md'), decisionsText);
check('`check` is clean once it is back', withV2('check').code === 0);

// Broken: the backlog checked out as a link held as text, which nothing reads through.
const tasksText = read('TASKS.md');
mkdirSync(join(consumer, 'notes'), { recursive: true });
writeFileSync(join(consumer, 'notes/TASKS.md'), tasksText);
writeFileSync(join(consumer, 'TASKS.md'), 'notes/TASKS.md');
const heldTasks = withV2('check');
check('`check` fails on a declared document held as text', heldTasks.code === 1
  && /LINK\s+TASKS\.md \(backlog\) — looks like a link checked out as text/.test(heldTasks.out), heldTasks.out);
const heldSync = withV2('sync --force');
check('`sync` refuses it, even with --force, naming the role', heldSync.code === 1
  && /TASKS\.md, declared as backlog, looks like a link checked out as text/.test(heldSync.out), heldSync.out);
writeFileSync(join(consumer, 'TASKS.md'), tasksText);
rmSync(join(consumer, 'notes'), { recursive: true, force: true });
check('`check` is clean once the backlog is a file again', withV2('check').code === 0);

// Judged, never failed (D54): the backlog over its ceiling.
writeFileSync(join(consumer, 'TASKS.md'), `${tasksText}\n${'- [ ] another row\n'.repeat(12)}`);
const overCeiling = withV2('check');
check('`check` reports a document over its ceiling in words, and exits 0', overCeiling.code === 0
  && /words\s+TASKS\.md \(backlog\) is \d+ words of 40/.test(overCeiling.out), overCeiling.out);
writeFileSync(join(consumer, 'TASKS.md'), tasksText);

// Refused at the edge: a role the tool does not know, named with the ones it does.
writeManifest((m) => { m.documents = { ...m.documents, roadmap: 'ROADMAP.md' }; });
const unknownRole = withV2('check');
check('an unknown role is refused as a tool error, naming the roles', unknownRole.code === 2
  && /'roadmap', which is not a role daoris knows — the roles are brief, room, router/.test(unknownRole.out), unknownRole.out);
writeManifest((m) => { delete m.documents.roadmap; });

// Withdrawn: the table goes with the declaration.
writeManifest((m) => { delete m.documents; });
const withdrawnTable = withV2('check');
check('`check` fails on a table the manifest no longer declares', withdrawnTable.code === 1
  && /Where things are table differs/.test(withdrawnTable.out), withdrawnTable.out);
const unDeclared = withV2('sync');
check('`sync` takes the table out of the region', unDeclared.code === 0 && !/## Where things are/.test(read('AGENTS.md')),
  unDeclared.out);
check('`check` is clean with nothing declared', withV2('check').code === 0);

// ----------------------------------------------------------------- 8. report

section('8. Result');
console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
if (totals.failures) {
  console.log(`  ${totals.failures} FAILED — do not tag a release until these pass.`);
  console.log(`  Scratch left at _fixtures/release-rehearsal for inspection.\n`);
  // exitCode rather than exit: the transcript writer rides the exit event, and the same convention
  // as the family rehearsal means the same failure reads the same way from either gate.
  process.exitCode = 1;
} else {
  console.log('  The packaged tool installs into a clean repo and drives the full lifecycle:');
  console.log('  adopt, collide, sync, drift, promote, upgrade, rename, switch off, refuse a newer lock, move the');
  console.log('  layout, declare the documents, and check.\n');
  rmSync(scratch, { recursive: true, force: true });
}
