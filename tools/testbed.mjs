#!/usr/bin/env node
/**
 * The testing workspace — a small real family, on the real machine registry, in a circle of its own.
 *
 *   node tools/testbed.mjs --root <folder>            build it (idempotent: re-running re-syncs)
 *   node tools/testbed.mjs --root <folder> --reset    delete the repositories and rebuild them
 *   node tools/testbed.mjs --root <folder> --retire   take them off the registry, leaving the files
 *
 * **Why this is not `examples/`.** `examples/` is a fixture the family rehearsal drives with no model
 * and no account — two miniature adopters, committed, asserted byte for byte. This is the opposite: a
 * place to *use* Daoris the way an adopter does, with a real service, real quests and real sessions.
 * The rehearsal must stay hermetic; a person needs somewhere that is not.
 *
 * **Why a workspace of its own (WSP1/WSP2).** These repositories join `testbed`, not `default`, so the
 * machine's real family cannot see them and they cannot see it: a quest across the boundary is refused
 * naming both sides, and a search answers from one circle only. That is the whole point of putting the
 * testbed on the real registry rather than in a scratch directory — it exercises the boundary instead
 * of avoiding it.
 *
 * 🔴 **The root is an argument and has no default.** A machine path in a tracked file is exactly what
 * `sensitive-info` forbids, and a default would be one. It is also the guard that matters here: this
 * script creates and deletes whole directories, and a wrong default would do that somewhere real.
 */
import { execFileSync, execSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { OWN } from './desktop-publish.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const cli = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');

const WORKSPACE = 'testbed';

/**
 * The family. Three domains that genuinely do not overlap, because a quest is only meaningful between
 * repositories that own different things — a testbed of near-identical repositories exercises the
 * plumbing and none of the premise.
 */
const FAMILY = [
  {
    name: 'testbed-core',
    summary: 'The domain model and the rules that act on it. Everything else is a consumer.',
    owns: ['the entity shapes and their invariants', 'validation, and what a rejected value means'],
    accepts: ['a field whose absence a consumer can show is blocking it', 'a relaxed rule, with the case'],
    packs: [],
    readme: 'Owns the shapes. If you are about to add a field here for a screen, publish a quest instead.',
  },
  {
    name: 'testbed-ui',
    summary: 'The screens people use, and the vocabulary they see. Renders the core; never extends it.',
    owns: ['the screens and their states', 'the words on them, and their translations'],
    accepts: ['a screen for something the core already models', 'a wording correction, with the confusion it caused'],
    packs: ['web-webview'],
    readme: 'Owns the screens. A missing field is a quest to `testbed-core`, not a local workaround.',
  },
  {
    name: 'testbed-ops',
    summary: 'How this family is built, gated and shipped. Owns the pipeline, not what runs in it.',
    owns: ['the gates and the order they run in', 'release mechanics and versioning'],
    accepts: ['a gate two repositories independently wanted', 'a release step that failed in the field'],
    packs: ['windows-machine'],
    readme: 'Owns the pipeline. A gate that belongs to one repository belongs in that repository.',
  },
];

const argv = process.argv.slice(2);
const flag = (name) => argv.includes(name);
const value = (name) => {
  const at = argv.indexOf(name);
  return at !== -1 && argv[at + 1] && !argv[at + 1].startsWith('--') ? argv[at + 1] : null;
};

const rootArg = value('--root') ?? process.env.DAORIS_TESTBED_ROOT ?? null;
if (!rootArg) {
  console.error('testbed: --root <folder> is required (or DAORIS_TESTBED_ROOT).\n'
    + '  It has no default on purpose: this script creates and deletes whole directories.');
  process.exit(2);
}
const root = isAbsolute(rootArg) ? rootArg : resolve(process.cwd(), rootArg);

const service = process.env.DAORIS_SERVICE_URL ?? 'http://localhost:5177';
const env = { ...process.env, DAORIS_SERVICE_URL: service };

/**
 * Names that may sit beside the family without making the root somebody else's.
 *
 * @remarks
 * 🔴 **Only what Daoris itself puts there, and never deleted by `--reset`.** A deployed desktop lives
 * beside the family it drives — the application at the root, published `--beside` the repositories —
 * so a root holding one is still a testbed root; but none of those names is part of the testbed, so
 * `--reset` rebuilds the repositories around them rather than through them. The names are the
 * publish's own list, read from it rather than restated: the two scripts share the folder, and a
 * name one writes that the other calls a stranger is a refusal nobody can get past. The guard below
 * is about not mistaking somebody's projects for scratch; this is about not mistaking Daoris's own
 * install for somebody's projects.
 */
const OURS = new Set(OWN);

/**
 * 🔴 The guard. This script deletes directories, so it refuses a root holding anything it did not
 * create — a folder of somebody's real projects and a folder of testbed repositories look identical
 * to `rmSync`, and only one of them is recoverable.
 */
function claimRoot() {
  if (!existsSync(root)) {
    mkdirSync(root, { recursive: true });
    return;
  }
  const strangers = readdirSync(root).filter(
    (entry) => !FAMILY.some((r) => r.name === entry) && !OURS.has(entry));
  if (strangers.length > 0) {
    console.error(`testbed: \`${root}\` holds things this script did not create: ${strangers.join(', ')}\n`
      + '  Refusing to touch it. Point --root at an empty folder, or one holding only the testbed.');
    process.exit(2);
  }
}

const run = (command, cwd) => execSync(command, { cwd, env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
const daoris = (args, cwd) => run(`node "${cli}" ${args}`, cwd);
const git = (where, args) => execFileSync('git', args, { cwd: where, encoding: 'utf8' });

/** The files this script writes wholesale on every run, as `git status` spells them. */
const WRITTEN = new Set(['.mcp.json', '.claude/settings.json', 'daoris.json']);

/** Every path the working tree currently differs in, tracked or not — the set a run compares. */
const dirty = (where) => new Set(
  git(where, ['status', '--porcelain', '--untracked-files=all']).split('\n')
    .filter(Boolean).map((line) => line.slice(3).replace(/^"(.*)"$/, '$1')));

/**
 * Commit what THIS run wrote — the adoption, the sync, the connector — and nothing else.
 *
 * 🔴 The script wrote the connector and never committed it, so a re-run that re-pointed `.mcp.json`
 * left every testbed dirty, and the driver — correctly — HELD each of their quests as *"the working
 * tree has uncommitted changes — somebody's work in progress"* (measured on the deployed shell,
 * 2026-09-23, after D63 moved the installed host). The difference between the tree before the run
 * and after it is exactly what the run owns; a path that was dirty before it is somebody's, and stays.
 */
function landed(where, before) {
  const after = dirty(where);
  // What this run wrote wholesale is this script's whether or not an earlier run left it dirty —
  // the connector and the declaration are rewritten every time, so "dirty before" is not "somebody's"
  // for them; it is the previous run's, and the previous run was this script too.
  const mine = [...after].filter((path) => !before.has(path) || WRITTEN.has(path));
  if (mine.length === 0) return false;
  git(where, ['add', '--', ...mine]);
  git(where, ['commit', '-qm', 'adopted, synced and wired by tools/testbed.mjs']);
  return true;
}

/** A repository that exists and has one commit — the state every Daoris command assumes. */
function born(repo) {
  const where = join(root, repo.name);
  if (existsSync(join(where, '.git'))) return where;
  mkdirSync(where, { recursive: true });
  const git = (args) => execFileSync('git', args, { cwd: where, encoding: 'utf8' });
  git(['init', '-q', '-b', 'main']);
  writeFileSync(join(where, 'README.md'), `# ${repo.name}\n\n${repo.summary}\n\n${repo.readme}\n`);
  git(['add', '-A']);
  git(['commit', '-qm', 'the starting point']);
  return where;
}

/**
 * The domain declaration, which `connect` requires — it is how siblings know whether a quest is
 * theirs, and the refusal for a repository without one says exactly that.
 */
function declare(where, repo) {
  const manifest = join(where, 'daoris.json');
  const held = JSON.parse(readFileSync(manifest, 'utf8'));
  held.domain = { summary: repo.summary, owns: repo.owns, accepts: repo.accepts };
  if (repo.packs.length > 0) held.packs = repo.packs;
  writeFileSync(manifest, `${JSON.stringify(held, null, 2)}\n`);
}

/**
 * The repository's own connector — the tools a driven session claims and closes its quest with.
 *
 * @remarks
 * 🔴 **Without this the testbed cannot be driven over the pipe door at all.** That door leans on the
 * repository's `.mcp.json`, and the driver may never reach in and write one (it is the very edit the
 * whole arrangement exists to prevent) — so wiring it is the *connector's* job at adoption, which
 * here means this script's. The protocol door carries its servers on `session/new` instead (ACP4) and
 * needs nothing on disk; a testbed that only worked there would be proving the easier half.
 *
 * It names an installed binary by absolute path because that is what an adopter's own file says —
 * `service-publish --install` prints this exact snippet. These repositories are scratch and
 * unpublished, which is why a machine path is fine here and in nothing this repository tracks.
 */
function connector(where) {
  // The installed host lives under the Daoris home (D63) — the same `bin/` `service-publish
  // --install` fills — and a machine with no home has no installed host to wire.
  const home = process.env.DAORIS_HOME?.trim();
  if (!home) return false;
  const host = join(home, 'bin', process.platform === 'win32'
    ? 'daoris-knowledge.exe' : 'daoris-knowledge');
  if (!existsSync(host)) return false;

  writeFileSync(join(where, '.mcp.json'), `${JSON.stringify({
    mcpServers: {
      'daoris-knowledge': {
        command: host,
        // The same store the driver reads. Passed rather than defaulted, for the reason the driver
        // passes it: a session that wrote to a different database would look like it worked. The
        // home too, because a harness spawns its servers with the environment IT was started with.
        env: { DAORIS_HOME: home, DAORIS_KNOWLEDGE_ROOT: root },
      },
    },
  }, null, 2)}\n`);

  // 🔴 **The second half, and a connector without it is decoration.** Wiring the server only makes
  // the tools OFFERED; the harness still refuses to call them unless the repository says it trusts
  // them. A driven session then does the work, cannot take or close its quest, and the driver
  // correctly records `failed — exited without touching its quest` while a tree full of good work
  // sits uncommitted. Measured on the first real deployment; DRV4 had this file and its entry named
  // the dependency in a parenthetical, which is where it stayed until the second deployment found it.
  //
  // The list is narrow on purpose and is the one DRV4 proved: the two quest verbs, and the git
  // commands a session needs to land and describe its own work. **Nothing outward-facing** — no
  // push, no publish, no release — so D37's boundary is exactly where it was. `Bash(node --test:*)`
  // is this family's gate command; a real adopter names its own, which is why this belongs to the
  // repository rather than to the canon.
  mkdirSync(join(where, '.claude'), { recursive: true });
  writeFileSync(join(where, '.claude', 'settings.json'), `${JSON.stringify({
    enableAllProjectMcpServers: true,
    permissions: {
      allow: [
        'mcp__daoris-knowledge__quest_list',
        'mcp__daoris-knowledge__quest_respond',
        'Bash(git add:*)',
        'Bash(git commit:*)',
        'Bash(git status:*)',
        'Bash(git log:*)',
        'Bash(git diff:*)',
        'Bash(node --test:*)',
        'Bash(npm test:*)',
      ],
    },
  }, null, 2)}\n`);

  return true;
}

if (flag('--retire')) {
  for (const repo of FAMILY) {
    try {
      daoris(`retire ${repo.name}`, root);
      console.log(`  retired  ${repo.name}  — every file still there`);
    } catch (error) {
      console.log(`  --       ${repo.name}: ${String(error.stdout ?? error.message).trim().split('\n')[0]}`);
    }
  }
  process.exit(0);
}

claimRoot();

if (flag('--reset')) {
  for (const repo of FAMILY) rmSync(join(root, repo.name), { recursive: true, force: true });
  console.log(`  reset    ${FAMILY.length} repositories removed from \`${root}\``);
}

console.log(`testbed: workspace \`${WORKSPACE}\` in \`${root}\`\n  service: ${service}\n`);

let connected = 0;
for (const repo of FAMILY) {
  const where = born(repo);
  // 🔴 `init` REFUSES an existing manifest, by design — it is the one command that must never
  // silently overwrite a repository's own adoption. So re-running was not idempotent at all, though
  // the header above promised it was: the second run died on the first repository. Adopt once;
  // everything after this line is safe to repeat.
  const before = dirty(where);
  if (!existsSync(join(where, 'daoris.json'))) daoris(`init --name ${repo.name}`, where);
  declare(where, repo);
  daoris('sync', where);
  const wired = connector(where);
  // Landed, so a driven session meets a clean tree: what the driver requires is what this leaves.
  const committed = landed(where, before);
  const packs = (repo.packs.length > 0 ? `  + ${repo.packs.join(', ')}` : '')
    + (wired ? '' : '  (no connector — install the service host)')
    + (committed ? '  (committed)' : '');
  try {
    // `--workspace` is the wiring, and it is a REGISTRY row rather than anything on disk (WSP1):
    // nothing in the repository records which circle it joined, which is why re-running is safe.
    const said = daoris(`connect --workspace ${WORKSPACE}`, where);
    connected += 1;
    console.log(`  ok       ${repo.name}${packs}\n           ${said.trim().split('\n')[0]}`);
  } catch (error) {
    const said = String(error.stdout ?? '') + String(error.stderr ?? '');
    console.log(`  todo     ${repo.name}${packs} — adopted and synced, not registered`);
    console.log(`           ${said.trim().split('\n').slice(0, 2).join('\n           ')}`);
  }
}

console.log(`\n  ${connected}/${FAMILY.length} registered in \`${WORKSPACE}\``);
if (connected < FAMILY.length) {
  console.log('\n  A service has to be running for the registry half. Start one, then re-run:');
  console.log('    node tools/service-publish.mjs --install   # then run daoris-knowledge-http');
  console.log(`    DAORIS_SERVICE_URL=${service} node tools/testbed.mjs --root <folder>`);
} else {
  console.log('\n  Try it:');
  console.log(`    cd <root>/testbed-ui && daoris status --machine`);
  console.log(`    publish a quest from the platform's Quests view, or over MCP`);
  console.log(`    node tools/testbed.mjs --root <folder> --retire   # unwire, keeping every file`);
}
