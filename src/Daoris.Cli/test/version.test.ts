import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText } from '../src/fsx.ts';
import { COMMANDS } from '../src/cli.ts';

// package.json and the sources are this package's; the canon, the manifest and
// the README belong to the workspace two levels up.
const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(cliRoot));
const read = (rel: string) => readText(join(repoRoot, rel));
const readCli = (rel: string) => readText(join(cliRoot, rel));
const version = () => JSON.parse(readCli('package.json')).version;

/** The name npm publishes the CLI under — what every shipped reference must name (DIST1, D105). */
const packageName = () => JSON.parse(readCli('package.json')).name;

/** The example family's manifests, discovered as release-prep discovers them. */
const exampleManifests = () =>
  readdirSync(join(repoRoot, 'examples'), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => `examples/${entry.name}/daoris.json`)
    .filter((rel) => existsSync(join(repoRoot, rel)));

/**
 * The version appears in four live places, and a bump that misses one is
 * invisible until a consumer's provenance header disagrees with the canon that
 * wrote it. Dated design and plan documents are deliberately excluded: they are
 * records of what was decided then, not statements about what ships now.
 */
test('the canon version tracks the package version', () => {
  assert.equal(JSON.parse(read('canon/canon.json')).version, version());
});

test("daoris's own manifest pins the version it is", () => {
  assert.equal(JSON.parse(read('daoris.json')).source, `${packageName()}@${version()}`);
});

test("the examples' manifests pin the version it is", () => {
  const manifests = exampleManifests();
  assert.ok(manifests.length >= 2, 'the example family should be found — this test is worthless if it is not');
  for (const rel of manifests) assert.equal(JSON.parse(read(rel)).source, `${packageName()}@${version()}`, rel);
});

test('the README install lines name the current version', () => {
  const lines = [...read('README.md').matchAll(/npx daoris@(\d+\.\d+\.\d+) [a-z]+/g)].map((m) => m[1]);
  assert.ok(lines.length >= 3, 'the README should show the pinned install lines');
  for (const ref of lines) assert.equal(ref, version());
  // And every other mention of the package at a version, the manifest example among them.
  for (const [, ref] of read('README.md').matchAll(/\bdaoris@(\d+\.\d+\.\d+)/g)) assert.equal(ref, version());
});

test('the version is a semver triple', () => {
  assert.match(version(), /^\d+\.\d+\.\d+$/);
});

/**
 * A consumer installs the CLI from npm, `daoris@<version>` (DIST1, D105). The git ref every reference
 * used to name could not run: the repository's root package is a private workspace with no `bin`, and
 * no tag existed. So every shipped reference names the package npm publishes, and none the git ref —
 * a stale one would go on teaching a command that fails, and nothing would rewrite it.
 */
test('every shipped reference names the npm package, and none the git ref that cannot run', () => {
  assert.equal(packageName(), 'daoris', 'the published package is the one the references name');
  const GIT_REF = /github:[^\s"'`]*#v\d/;
  for (const rel of ['daoris.json', 'README.md', ...exampleManifests()]) {
    assert.equal(GIT_REF.test(read(rel)), false, `${rel} still names a git ref`);
  }
  assert.ok(read('README.md').includes(`npx daoris@${version()} init`));
  assert.ok(readCli('src/commands.ts').includes('`daoris@${canon.version}`'), 'what init writes');
  assert.equal(GIT_REF.test(readCli('src/commands.ts')), false, 'init must not write the git ref');
  assert.equal(/OWNER/.test(readCli('src/commands.ts')), false, 'the placeholder must not ship');
});

/**
 * release-prep is the only writer of the version (never by hand), and `--check` is what `verify`
 * runs. A rewrite in one spelling and a check in another would pass `--check` today and leave the
 * release's references behind, so the two are held together: every file rewritten at a new version
 * must then agree at that version.
 */
test('release-prep rewrites every reference it checks, in the spelling it checks', async () => {
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  const prep = await import('../../../tools/release-prep.mjs') as {
    rewrites: (version: string) => Array<[string, (text: string) => string]>;
    disagreements: (read: (rel: string) => string) => string[];
  };

  assert.deepEqual(prep.disagreements(read), [], 'the tree as it stands agrees');

  const rewritten = new Map(prep.rewrites('9.8.7').map(([rel, rewrite]) => [rel, rewrite(read(rel))]));
  assert.deepEqual(prep.disagreements((rel) => rewritten.get(rel) ?? read(rel)), []);
  assert.match(rewritten.get('README.md')!, /npx daoris@9\.8\.7 sync/);
  assert.equal(JSON.parse(rewritten.get('daoris.json')!).source, 'daoris@9.8.7');
  for (const rel of exampleManifests()) assert.equal(JSON.parse(rewritten.get(rel)!).source, 'daoris@9.8.7', rel);
});

test('release-prep reports the git ref, and a README that names no install line', async () => {
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  const prep = await import('../../../tools/release-prep.mjs') as {
    disagreements: (read: (rel: string) => string) => string[];
  };

  const withGitRef = prep.disagreements((rel) =>
    rel === 'README.md' ? `${read(rel)}\nnpx github:JiarongGu/Daoris#v${version()} sync\n` : read(rel));
  assert.ok(withGitRef.some((problem) => /README/.test(problem) && /git ref/.test(problem)), withGitRef.join('\n'));

  const noInstall = prep.disagreements((rel) =>
    rel === 'README.md' ? read(rel).replace(/npx daoris@[\d.]+ [a-z]+/g, 'npx something') : read(rel));
  assert.ok(noInstall.some((problem) => /README/.test(problem) && /install line/.test(problem)), noInstall.join('\n'));
});

/**
 * The dispatcher and the two documents that enumerate it are a counterpart set, and counterpart sets
 * rot: `harness` and `driver` shipped, the README's table grew two rows, and the changelog went on
 * saying "Twelve commands" — a sentence nothing read, wrong for two whole landings.
 *
 * Asserted as a SET rather than a count, because a count agrees with itself while naming the wrong
 * command, and a renamed verb is exactly the change that would keep the number right. The set is the
 * dispatcher's own table (MOD7), read rather than parsed out of its source.
 */
const dispatcherCommands = (): string[] => {
  const names = COMMANDS.map((command) => command.name).sort();
  assert.ok(names.length >= 10, 'the dispatcher table emptied — this test is worthless if it is');
  return names;
};

test('the README command table lists exactly the commands the dispatcher has', () => {
  const documented = [...read('README.md').matchAll(/^\| `([a-z]+)[ `]/gm)].map((m) => m[1]!).sort();
  assert.deepEqual(documented, dispatcherCommands());
});

test('the changelog says how many commands there actually are, and names them all', () => {
  const NUMBERS = ['zero', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine',
    'ten', 'eleven', 'twelve', 'thirteen', 'fourteen', 'fifteen', 'sixteen', 'seventeen',
    'eighteen', 'nineteen', 'twenty'];
  const commands = dispatcherCommands();
  const changelog = read('CHANGELOG.md');

  const claim = changelog.match(/\*\*([A-Z][a-z]+) commands\.\*\*/);
  assert.ok(claim, 'the changelog must state a command count');
  assert.equal(claim[1]!.toLowerCase(), NUMBERS[commands.length],
    `the changelog claims ${claim[1]} commands; the dispatcher has ${commands.length}`);

  // The count is the cheap half. A command nobody wrote a sentence for is the expensive one.
  const missing = commands.filter((name) => !changelog.includes(`\`${name}\``));
  assert.deepEqual(missing, [], 'every command must be named in the changelog');
});
