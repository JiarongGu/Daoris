import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText } from '../src/fsx.ts';

// package.json and the sources are this package's; the canon, the manifest and
// the README belong to the workspace two levels up.
const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(cliRoot));
const read = (rel: string) => readText(join(repoRoot, rel));
const readCli = (rel: string) => readText(join(cliRoot, rel));
const version = () => JSON.parse(readCli('package.json')).version;

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
  assert.equal(JSON.parse(read('daoris.json')).source.endsWith(`#v${version()}`), true);
});

test('the README install lines name the current version', () => {
  const refs = [...read('README.md').matchAll(/Daoris#v(\d+\.\d+\.\d+)/g)].map((m) => m[1]);
  assert.ok(refs.length >= 3, 'the README should show the pinned install reference');
  for (const ref of refs) assert.equal(ref, version());
});

test('the version is a semver triple', () => {
  assert.match(version(), /^\d+\.\d+\.\d+$/);
});

/**
 * `npx` resolves the ref in `source` literally, and GitHub's repository name is
 * capitalised while the npm package name is not. A lower-cased ref is the kind
 * of mistake that works on a case-insensitive checkout and fails for everyone
 * else, so it is pinned rather than left to chance.
 */
test('every shipped reference names the repository exactly', () => {
  const REPO = 'github:JiarongGu/Daoris#v';
  assert.ok(read('daoris.json').includes(REPO));
  assert.ok(read('README.md').includes(REPO));
  assert.ok(readCli('src/commands.ts').includes('github:JiarongGu/Daoris#v'), 'what init writes');
  assert.equal(/OWNER/.test(readCli('src/commands.ts')), false, 'the placeholder must not ship');
});

/**
 * The dispatcher and the two documents that enumerate it are a counterpart set, and counterpart sets
 * rot: `harness` and `driver` shipped, the README's table grew two rows, and the changelog went on
 * saying "Twelve commands" — a sentence nothing read, wrong for two whole landings.
 *
 * Asserted as a SET rather than a count, because a count agrees with itself while naming the wrong
 * command, and a renamed verb is exactly the change that would keep the number right.
 */
const dispatcherCommands = (): string[] => {
  const table = readCli('src/cli.ts').match(/const commands[^{]*\{([\s\S]*?)\n\};/);
  assert.ok(table, 'the dispatcher table must be findable — this test is worthless if it is not');
  return [...table[1]!.matchAll(/^\s{2}([a-z]+):/gm)].map((m) => m[1]!).sort();
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
