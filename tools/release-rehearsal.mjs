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
  copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync,
} from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(repoRoot, '_fixtures', 'release-rehearsal');
const consumer = join(scratch, 'consumer');
const canonV2 = join(scratch, 'canon-v2');

let checks = 0;
let failures = 0;

function check(label, condition, detail = '') {
  checks += 1;
  if (condition) {
    console.log(`  ok    ${label}`);
  } else {
    failures += 1;
    console.log(`  FAIL  ${label}${detail ? `\n          ${detail}` : ''}`);
  }
}

function section(title) {
  console.log(`\n${title}`);
}

/** Run in the consumer repo through the installed bin, capturing output and exit code. */
function daoris(args) {
  try {
    const stdout = execSync(`npx --no-install daoris ${args}`, {
      cwd: consumer,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    return { code: 0, out: stdout };
  } catch (error) {
    return { code: error.status ?? -1, out: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
}

/** Recursive copy. Deliberately not fs.cpSync — it has crashed on this platform. */
function copyTree(from, to) {
  mkdirSync(to, { recursive: true });
  for (const entry of readdirSync(from, { withFileTypes: true })) {
    const src = join(from, entry.name);
    const dest = join(to, entry.name);
    if (entry.isDirectory()) copyTree(src, dest);
    else copyFileSync(src, dest);
  }
}

const read = (rel) => readFileSync(join(consumer, rel), 'utf8');
const has = (rel) => existsSync(join(consumer, rel));

// ---------------------------------------------------------------- 1. package

section('1. Package the artefact');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });

const packed = execSync(`npm pack --pack-destination "${scratch}"`, {
  cwd: repoRoot,
  encoding: 'utf8',
  stdio: ['ignore', 'pipe', 'ignore'], // npm writes its file listing to stderr
}).trim().split('\n').pop().trim();
const tarball = join(scratch, packed);
check('npm pack produced a tarball', existsSync(tarball), tarball);

const pkg = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8'));
const version = pkg.version;
check(`tarball is named for version ${version}`, packed.includes(version), packed);

// What matters is what SHIPS, not what sits in the checkout. `files` is a
// whitelist, so anything not named there is only present by npm's own rules.
const shipped = new Set(
  JSON.parse(
    execSync('npm pack --dry-run --json', {
      cwd: repoRoot, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'],
    }),
  )[0].files.map((f) => f.path),
);
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

// The repo writes its own rule BEFORE adopting, so the collision path is real.
mkdirSync(join(consumer, '.claude', 'rules'), { recursive: true });
writeFileSync(join(consumer, '.claude/rules/sensitive-info.md'), '# Our own rule\n\nWritten here first.\n');
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
check('`sync` refuses to clobber the pre-existing rule', collide.code === 1, collide.out);
check('...and says which file', /sensitive-info/.test(collide.out), collide.out);
check(
  '...and leaves it untouched',
  read('.claude/rules/sensitive-info.md').includes('Written here first'),
);

rmSync(join(consumer, '.claude/rules/sensitive-info.md'));
const sync = daoris('sync');
check('`sync` exits 0 once the collision is resolved', sync.code === 0, sync.out);
check('rules are materialized', has('.claude/rules/sensitive-info.md'));
check('the generated index is written', has('.claude/rules/RULES_INDEX.md'));
check('skills survive packing as directories', has('.claude/skills/doc-loader/SKILL.md'));
check('the lock is written', has('daoris.lock'));

const skill = read('.claude/skills/doc-loader/SKILL.md');
check('a skill still starts with its frontmatter', skill.startsWith('---\n'), skill.slice(0, 60));
check('...with the provenance header beneath it', /---\n<!-- daoris: /.test(skill));

const index = read('.claude/rules/RULES_INDEX.md');
check("the index marks the repo's own skill local", /house-deploy.*\(local\)/.test(index));
check('the index lists canonical skills unmarked', /\[doc-loader\]/.test(index));

const checkRun = daoris('check');
check('`check` exits 0 on a freshly synced repo', checkRun.code === 0, checkRun.out);
check('`check` reports the always-loaded budget', /bytes/.test(checkRun.out), checkRun.out);

const doctor = daoris('doctor');
check('`doctor` exits 0 (advisory, never fails)', doctor.code === 0, doctor.out);

// ---------------------------------------------------- 4. drift and the return

section('4. Drift, and the return path');
const ruleFile = join(consumer, '.claude/rules/task-lifecycle.md');
writeFileSync(ruleFile, `${readFileSync(ruleFile, 'utf8')}\nA local improvement.\n`);

const drifted = daoris('check');
check('`check` catches a local edit', drifted.code === 1, drifted.out);
check('...naming the file', /task-lifecycle/.test(drifted.out), drifted.out);

const refused = daoris('sync');
check('`sync` refuses rather than discarding the edit', refused.code === 1, refused.out);
check('...and points at `upstream`', /upstream/.test(refused.out), refused.out);

// A consumer cannot upstream into a read-only install, which is correct: the
// canon lives in the package. Point at a writable copy, as a canon developer does.
copyTree(join(consumer, 'node_modules', 'daoris', 'canon'), canonV2);
const upstreamed = (() => {
  try {
    return {
      code: 0,
      out: execSync('npx --no-install daoris upstream task-lifecycle.md', {
        cwd: consumer, encoding: 'utf8', env: { ...process.env, DAORIS_CANON: canonV2 },
      }),
    };
  } catch (error) {
    return { code: error.status ?? -1, out: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
})();
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

/** The consumer, pointed at the writable canon copy that now plays "upstream". */
const withV2 = (args) => {
  try {
    return {
      code: 0,
      out: execSync(`npx --no-install daoris ${args}`, {
        cwd: consumer, encoding: 'utf8', env: { ...process.env, DAORIS_CANON: canonV2 },
      }),
    };
  } catch (error) {
    return { code: error.status ?? -1, out: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
};
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
check('...and the pack installs', has('.claude/rules/windows-machine.md'));
check(
  '...with the promotion intact',
  read('.claude/rules/task-lifecycle.md').includes('A local improvement.'),
);

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
check('...the old file is gone', !has('.claude/rules/windows-machine.md'));
check('...and the new one is present', has('.claude/rules/windows-traps.md'));

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
  read('.claude/rules/sensitive-info.md').includes('never in a screenshot'),
);
check('`check` is clean afterwards', withV2('check').code === 0);

// ----------------------------------------------------------------- 6. report

section('6. Result');
console.log(`\n  ${checks - failures}/${checks} checks passed`);
if (failures) {
  console.log(`  ${failures} FAILED — do not tag a release until these pass.`);
  console.log(`  Scratch left at _fixtures/release-rehearsal for inspection.\n`);
  process.exit(1);
}
console.log('  The packaged tool installs into a clean repo and drives the full lifecycle:');
console.log('  adopt, collide, sync, drift, promote, upgrade, rename, and check.\n');
rmSync(scratch, { recursive: true, force: true });
