import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { existsSync } from 'node:fs';
import { readCanon } from '../src/canon.ts';
import { parseFrontmatter, SKILL_FIELDS } from '../src/document.ts';
import { listFiles, readText } from '../src/fsx.ts';
import { readManifest, readLock } from '../src/config.ts';
import { inspect, commandCheck } from '../src/drift.ts';

// This package is src/Daoris.Cli; the canon and daoris's own doctrine live at
// the workspace root, because they are the project's data rather than the CLI's.
const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(cliRoot));

const isSkill = (source: string) => source.includes('/skills/');

test('every shipped canon file has complete frontmatter', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const core = canon.packs.get('core')!.files;
  assert.ok(core.length >= 6, `expected at least 6 core files, found ${core.length}`);

  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      if (isSkill(file.source)) continue;
      const { meta } = parseFrontmatter(readText(join(repoRoot, 'canon', file.source)));
      assert.ok(meta, `${file.source} is missing or has incomplete frontmatter`);
      assert.ok(meta!.name && meta!.applies_when && meta!.enforces);
      assert.equal(
        meta!.name,
        file.source.split('/').pop()!.replace(/\.md$/, ''),
        `${file.source}: frontmatter name must match the filename`,
      );
    }
  }
});

/**
 * A skill's frontmatter is the harness's, not ours: `description` is the trigger
 * it matches on, so a skill with none never fires — it installs, costs bytes and
 * silently does nothing. The name has to match the DIRECTORY, because every
 * skill's file is called SKILL.md.
 */
test('every shipped canon skill carries the frontmatter the harness needs', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const skills = [...canon.packs.values()].flatMap((pack) => pack.files).filter((f) => isSkill(f.source));
  assert.ok(skills.length >= 2, `expected at least 2 canon skills, found ${skills.length}`);

  for (const file of skills) {
    assert.ok(file.source.endsWith('/SKILL.md'), `${file.source}: a skill's file must be SKILL.md`);
    const text = readText(join(repoRoot, 'canon', file.source));
    const { meta } = parseFrontmatter(text, SKILL_FIELDS);
    assert.ok(meta, `${file.source} is missing 'name' or 'description'`);
    assert.equal(
      meta!.name,
      file.source.split('/').at(-2),
      `${file.source}: frontmatter name must match the skill's directory`,
    );
  }
});

test('every pack declares a description and ships at least one file', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const packs = [...canon.packs.values()].filter((pack) => pack.name !== 'core');
  assert.ok(packs.length >= 3, `expected at least 3 packs, found ${packs.length}`);
  for (const pack of packs) {
    assert.ok(pack.description, `pack '${pack.name}' has no description — init prints it`);
    assert.ok(pack.files.length, `pack '${pack.name}' ships no files`);
  }
});

test('no canon file names a private sibling project or a machine path', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  // Every separator a Windows home is spelled with — the devkit's sensitive gate's twin (REV3).
  const forbidden = /[A-Z]:(?:\\{1,2}|\/)Users(?:\\{1,2}|\/)|\/home\/[a-z]/i;
  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      const text = readText(join(repoRoot, 'canon', file.source));
      assert.equal(forbidden.test(text), false, `${file.source} contains a machine path`);
    }
  }
});

/**
 * Doctrine must not hard-require the tool that shipped it (D48 §2a, CANON6).
 *
 * An adopted repository stays fully workable for contributors who do not run Daoris — their agents
 * included, because those agents load the same vendored markdown. Almost everything the canon names
 * survives that: `sync` COMMITS what it writes, so a generated index, a lock file and a vendored rule
 * are all still there for someone who never installed anything.
 *
 * A **service** is the exception, and the only one: publishing a quest needs something running. A rule
 * that says "publish a quest" and stops reads as a dead end to exactly the reader who cannot, so it
 * must name the tool-absent path in the same breath — the principle is canonical, the mechanism
 * degrades.
 *
 * This fails silently without a check, which is why there is one: nothing breaks, no gate goes red,
 * and a contributor's agent simply has nothing to do when it reaches that line.
 */
test('no canon file instructs a service-only action without its tool-absent path', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  // The one service-shaped mechanism the canon names. Deliberately not a broad vocabulary scan: the
  // audit that wrote this rule first "fixed" a rule naming the generated index, which needed nothing,
  // because it matched on family words instead of on what actually needs a process running.
  const serviceOnly = /\bquests?\b/i;
  // The carve-out, in any of the shapes it is reasonable to write. A rewording that drops all three
  // fails here — a loud false positive, which is the right trade for a guarantee that is otherwise
  // invisible, and it forces the next author to decide rather than drift.
  const degrades = /where (?:none|it) (?:does|exists)|where none does not|where the quest system does not/i;

  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      const text = readText(join(repoRoot, 'canon', file.source));
      if (!serviceOnly.test(text)) continue;

      assert.ok(
        degrades.test(text),
        `${file.source} instructs a quest without naming what to do where no quest system exists. `
        + 'Doctrine must not hard-require Daoris (D48 §2a) — name the alternative in the same breath.');
    }
  }
});

/**
 * The declared gates and the release workflow are two lists that must agree — and they silently did
 * not.
 *
 * `daoris.gates.json` declared `dotnet test src/Daoris.Service` (248 tests) and the workflow never ran
 * it; the driver's 130 were in neither. What hid it for eight landings is the most useful part: the
 * rehearsals drive the service AND the driver end to end, so the workflow looked thorough and every
 * release rehearsal passed. **An end-to-end pass is not a substitute for the judgement underneath
 * it** — a store rule or a payload guard can break without any rehearsal noticing, which is precisely
 * what those unit suites exist to catch.
 *
 * Substring rather than exact match, because the workflow appends runner flags (`--nologo`). One
 * direction only: the workflow legitimately runs more than the gates (both rehearsals), and a test
 * that forbade that would be wrong about the thing it is guarding.
 */
test('every declared gate is actually run by the release workflow', () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as {
    gates: { name: string; run: string }[];
  };
  // 🔴 What the workflow RUNS, never what it says (REV3): a comment naming a command is not the command,
  // and deleting the Verify step left this green because a comment beside another step said
  // `npm run verify`. Full-line comments go; a step's own text stays.
  const workflow = readText(join(repoRoot, '.github', 'workflows', 'release.yml'))
    .split('\n').filter((line) => !/^\s*#/.test(line)).join('\n');

  assert.ok(gates.gates.length >= 4, 'the gate list emptied — that is not a pass');
  for (const gate of gates.gates) {
    assert.ok(
      workflow.includes(gate.run),
      `the '${gate.name}' gate is declared in daoris.gates.json but the release workflow never runs `
      + `it: ${gate.run}`);
  }

  // And a step that runs but whose failure is ignored runs nothing that gates.
  assert.doesNotMatch(workflow, /continue-on-error:\s*true/, 'a release step ignores its own failure');
  assert.doesNotMatch(workflow, /\|\|\s*true\b/, 'a release step swallows its exit code with `|| true`');
});

/**
 * The same gap one list further in (SEN1): the universal gates were declared and in the workflow, and
 * red on this repository for a day, because nothing a session runs before committing ran them — the
 * workflow is dispatched by hand and the pre-commit hook was never installed. Placeholder home paths
 * reached six fixtures across four landings that way. The universal half is the one that belongs in
 * `verify`: it is fast, it judges facts (D54), and a leak is cheapest caught the moment it is written.
 */
test('the universal gates run in verify, not only at release', () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as {
    gates: { name: string; run: string }[];
  };
  const universal = gates.gates.find((gate) => gate.name === 'universal');
  const verify = (JSON.parse(readText(join(repoRoot, 'package.json'))) as {
    scripts: { verify: string };
  }).scripts.verify;

  assert.ok(universal, 'the universal gate is no longer declared — that is not a pass');
  assert.ok(verify.includes(universal.run), `npm run verify never runs the universal gates: ${universal.run}`);
});

/**
 * A decision recorded without what it beat invites re-litigation — which is the failure the decisions
 * record exists to prevent, so a decision that names no alternative has not finished being made.
 *
 * Adopted from deepseek-harness, where every active note carries a mandatory `## Alternatives
 * considered` (`docs/2026-09-21-dsh-evaluation.md` §3): two ecosystems keeping the same record for the
 * same reason, which is the D17 bar. Their rule comes with the discipline that makes it honest —
 * **alternatives are recorded, never invented** — and so does this one: the 41 entries written before
 * it are grandfathered rather than backfilled, because writing a plausible rejected alternative into
 * a decision nobody actually weighed would make the record worse while making the gate green.
 *
 * The line is the number, because the numbers only increase: every entry from D51 — the working-surface
 * arc onward — must say what it turned down. `Rejected` in any of its shapes, or a `Why not X?`
 * heading; both are already the house style, which is the evidence that the rule is describing
 * practice rather than imposing it.
 */
test('every decision entry from D51 on says what it rejected', () => {
  const text = readText(join(repoRoot, 'docs', 'DECISIONS.md'));
  const GRANDFATHERED_THROUGH = 50;
  // Split rather than a lazy match with a terminating lookahead: the obvious regex for "this heading
  // to the next" needs an end-of-input anchor, JavaScript has none (`\Z` is a literal Z), and the
  // version written that way silently dropped the NEWEST entry — the one the rule most exists for.
  // The guard below caught it; without the guard it would have been a green test checking nothing.
  const entries = text.split(/^## (?=D\d)/m).slice(1)
    .map((section) => ({ number: Number(/^D(\d+)/.exec(section)?.[1]), body: section }))
    .filter((entry) => entry.number > GRANDFATHERED_THROUGH);

  // Without this the test passes loudest exactly when the pattern stops matching headings at all.
  assert.ok(entries.length >= 3, `the walk found ${entries.length} entries after D${GRANDFATHERED_THROUGH}, so it proved nothing`);

  for (const entry of entries) {
    assert.match(
      entry.body,
      /Rejected\b|Why not\b/i,
      `D${entry.number} names no rejected alternative. A decision recorded without what it beat gets `
      + 're-litigated — say what was turned down and why, in the entry itself.');
  }
});

test('daoris holds its own doctrine and checks clean', () => {
  assert.equal(existsSync(join(repoRoot, 'daoris.json')), true, 'run: node bin/daoris.mjs init');
  const lock = readLock(repoRoot);
  assert.ok(lock, 'run: node bin/daoris.mjs sync');
  const report = inspect({ root: repoRoot, manifest: readManifest(repoRoot), lock });
  assert.equal(report.ok, true, JSON.stringify(report, null, 2));
});

/**
 * D8's offline guarantee, scoped to what it actually claims.
 *
 * The guarantee is about the DOCTRINE operations — `check` above all, because it runs inside build
 * gates and a gate that can fail on a network call is not a gate. The MANAGEMENT commands are a
 * different thing (D50): `connect`, `retire` and `import` are explicit, opt-in conversations with a
 * knowledge service, never run by a gate.
 *
 * This was briefly written as "nothing anywhere in the CLI may open a socket", which is a stronger
 * claim than D8 makes and would have made a client impossible. Two assertions replace it, and together
 * they are the real invariant: exactly one module may reach the network, and nothing on `check`'s path
 * may import it — directly or three modules deep.
 *
 * The first half survived the management class growing past one command because the network itself
 * stayed in one file. Had each verb opened its own socket, "the named management modules" would be a
 * list that grows, and a list that grows is one somebody eventually appends to without thinking.
 */
/** A module named by any import spelling: `from`, a bare `import`, a dynamic `import(`, a `require(`. */
const importing = (modules: string) => new RegExp(
  String.raw`(?:\bfrom|\bimport\s*\(?|\brequire\s*\()\s*['"](?:node:)?(?:${modules})['"]`);

/**
 * Widened by REV3: it held only `node:http(s)` and `require('http(s)')`, so a bare `'https'`, `net`,
 * `tls`, `http2`, `undici` or a dynamic `import()` walked straight past it.
 */
const NETWORK = new RegExp([
  String.raw`\b(?:fetch|XMLHttpRequest|WebSocket)\s*\(`,
  importing('https?|http2|net|tls|dgram|undici').source,
].join('|'));

/** The spawning primitive, in any spelling — held to one module the way the network is. */
const SPAWNING = importing('child_process');

/** The one module permitted a network primitive. Every management command speaks through it. */
const SERVICE_CLIENT = 'service.ts';

/**
 * A check you have not watched fail proves nothing — and a pattern is only as good as the spellings it
 * was shown. These are the realistic regressions, each of which the previous patterns missed.
 */
test('the guards recognise every spelling of the primitives they hold', () => {
  for (const line of [
    "import { request } from 'https';", "import { request } from \"node:https\";",
    "import net from 'node:net';", "import { connect } from 'tls';", "import * as h2 from 'node:http2';",
    "const { request } = await import('node:https');", "import { fetch as f } from 'undici';",
    "const h = require('http');", "await fetch(url);",
  ]) assert.ok(NETWORK.test(line), `NETWORK misses: ${line}`);

  for (const line of [
    "import { spawnSync } from 'node:child_process';", "import { execSync } from \"child_process\";",
    "const cp = await import('node:child_process');", "const { spawn } = require('child_process');",
  ]) assert.ok(SPAWNING.test(line), `SPAWNING misses: ${line}`);

  // And it is not a pattern that matches everything: the words alone are not the primitive.
  for (const line of ['const releaseFetcher = () => 1;', '// a network is not a socket', "import { x } from './nettle.ts';"]) {
    assert.equal(NETWORK.test(line), false, `NETWORK matches innocent: ${line}`);
  }
});

/** The one module permitted to spawn. Doctrine commands must not reach it either (below). */
const SPAWNS = 'toolchain.ts';

test('only the toolchain may spawn a process', () => {
  for (const dir of ['src', 'bin']) {
    for (const file of listFiles(join(cliRoot, dir), (n) => n.endsWith('.ts') || n.endsWith('.mjs'))) {
      if (file === SPAWNS) continue;
      assert.equal(
        SPAWNING.test(readText(join(cliRoot, dir, file))), false,
        `${dir}/${file} imports a spawning primitive — only ${SPAWNS} may`);
    }
  }
});

test('only the service client may touch the network', () => {
  for (const dir of ['src', 'bin']) {
    for (const file of listFiles(join(cliRoot, dir), (n) => n.endsWith('.ts') || n.endsWith('.mjs'))) {
      if (file === SERVICE_CLIENT) continue;
      const text = readText(join(cliRoot, dir, file));
      assert.equal(
        NETWORK.test(text), false,
        `${dir}/${file} reaches the network — only ${SERVICE_CLIENT} may`);
    }
  }
});

/**
 * The half that matters most. A gate would not realistically break by someone adding `fetch` to
 * `drift.ts`; it would break by an innocuous import three modules deep acquiring one for it.
 *
 * Walked from every DOCTRINE command's entry point, not only `check`'s: they all run offline, and a
 * test that named one of them would be silent the day `sync` grew a "just ask the service" shortcut.
 */
const DOCTRINE = [
  'drift.ts', 'materialize.ts', 'indexgen.ts', 'upstream.ts', 'commands.ts', 'twins.ts', 'analyze.ts',
];

test('nothing a doctrine command reaches can import the service client', () => {
  for (const entry of DOCTRINE) {
    const seen = reachableFrom(entry);

    assert.equal(
      seen.has(SERVICE_CLIENT), false,
      `${entry} reaches the service client through: ${[...seen].sort().join(', ')}`);
    assert.ok(seen.size > 1, `the walk from ${entry} found nothing, so it proved nothing`);
  }
});

/**
 * The same guarantee, one door further out (D49 §4). `toolchain.ts` SPAWNS PROCESSES — each harness's
 * own installer, updater and login flow, which is the whole point of it — and a doctrine command that
 * reached it could run one of them inside a build gate.
 *
 * It is the same shape of rule as the network one and it is worth stating separately, because the
 * reasoning that makes spawning fine (opt-in, a person asked for it, never run by a gate) is exactly
 * the reasoning that stops holding the moment `check` can reach it.
 */
test('nothing a doctrine command reaches can spawn a harness', () => {
  for (const entry of DOCTRINE) {
    const seen = reachableFrom(entry);

    assert.equal(
      seen.has(SPAWNS), false,
      `${entry} reaches the harness toolchain through: ${[...seen].sort().join(', ')}`);
  }
});

/** Every module an entry point pulls in, transitively — both import spellings. */
function reachableFrom(entry: string): Set<string> {
  const seen = new Set<string>();
  const walk = (module: string): void => {
    if (seen.has(module)) return;
    seen.add(module);
    const file = join(cliRoot, 'src', module);
    if (!existsSync(file)) return;
    // `from './x.ts'`, a bare `import './x.ts'` and a dynamic `import('./x.ts')`, in either quote — the
    // side-effect import was missed at first, and the dynamic one and the double quote until REV3.
    for (const match of readText(file).matchAll(/(?:\bfrom|\bimport)\s*\(?\s*['"]\.\/([\w.-]+\.ts)['"]/g)) {
      walk(match[1]!);
    }
  };

  walk(entry);
  return seen;
}

/**
 * Most of the management class needs no network at all (D50): `remote` edits the home's `remotes.json`
 * and `status --machine` reads it. The usage text says so out loud, and prose is the one surface with
 * no compiler — so the claim is held here.
 *
 * It is also what keeps the class from becoming a list. The offline guarantee survived management
 * growing past `connect` because the network itself stayed in ONE file; a verb that quietly acquired a
 * socket through a convenience import would make "the named management modules" something people
 * append to without thinking.
 */
test('the file-local management verbs reach no network module either', () => {
  for (const entry of ['remotes.ts', 'remotemap.ts', 'toolchain.ts', 'driverconfig.ts', 'plugins.ts']) {
    const seen = reachableFrom(entry);

    assert.equal(
      seen.has(SERVICE_CLIENT), false,
      `${entry} reaches the service client through: ${[...seen].sort().join(', ')}`);
    assert.ok(seen.size > 1, `the walk from ${entry} found nothing, so it proved nothing`);
  }
});

/**
 * `agent pin` DOES reach a network since AGT2b — a maker's release channel — and the usage text says
 * it is the one verb of its class that does. It reaches it the way `connect` reaches a service:
 * through `service.ts`, which the DISPATCHER hands in. So the toolchain and everything that judges a
 * download (the channel, the signature, the archive) import no network module, and the one place the
 * two meet is a line in `cli.ts` a reviewer can read.
 */
test('agent pin reaches a release channel only through the fetcher the dispatcher hands in', () => {
  for (const entry of ['channels.ts', 'openpgp.ts', 'tarball.ts']) {
    const seen = reachableFrom(entry);
    assert.equal(seen.has(SERVICE_CLIENT), false, `${entry} reaches the service client through: ${[...seen].sort().join(', ')}`);
    assert.equal(seen.has(SPAWNS), false, `${entry} reaches the harness toolchain through: ${[...seen].sort().join(', ')}`);
  }

  const dispatcher = readText(join(cliRoot, 'src', 'cli.ts'));
  assert.match(dispatcher, /import \{ releaseFetcher \} from '\.\/service\.ts';/);
  assert.match(dispatcher, /agent: \(args\) => commandHarness\(args, releaseFetcher\(\)\)/);
});

/**
 * `check` verifies against the LOCK, which already carries a hash per file — so it needs neither the
 * canon nor a network to answer. That is what makes it usable in a hook or an air-gapped build, and it
 * is the property most easily lost by an innocent-looking "just re-read the canon to compare" change.
 */
test('check passes with no canon present at all', () => {
  const previous = process.env.DAORIS_CANON;
  // Points the resolver at a path that does not exist rather than deleting the real canon: the
  // guarantee is about `check` never NEEDING the canon, and a test that had to destroy the tree to
  // prove it could not run beside the others.
  process.env.DAORIS_CANON = join(cliRoot, '_fixtures', 'no-such-canon');
  try {
    const out: string[] = [];
    // No `packageRoot` on purpose — commandCheck does not accept one, which is the guarantee stated
    // as a signature. The env var covers the other route to a canon, so a future version that grew
    // either one would fail here rather than quietly acquiring a dependency.
    const code = commandCheck({ root: repoRoot, write: (s: string) => out.push(s) });
    assert.equal(code, 0, out.join('\n'));
  } finally {
    if (previous === undefined) delete process.env.DAORIS_CANON;
    else process.env.DAORIS_CANON = previous;
  }
});
