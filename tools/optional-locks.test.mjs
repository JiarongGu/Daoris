/**
 * REVIEWLOCK1: the optional-locks census (FIX-LOG 2026-10-23, REVIEWLOCK1).
 *
 *   node --test tools/optional-locks.test.mjs
 *
 * `checkOptionalLocks` reads a driver source tree. Each case builds a small tree in a gitignored scratch folder of the repository
 * and holds the verdict: a git status or diff asked without `--no-optional-locks` before its command is refused; one with it, a
 * marked one, another command, an indexer, a command only shown to a person and a word in a comment pass. The case after holds the
 * repository's own driver clean.
 *
 * QUEUELOCK1: `checkToolLocks` reads the same of a tools folder's scripts (single, double and template quotes, `diff-index`, a
 * `return`ed array, tests skipped), and the last cases hold the repository's own tools clean and the merge tool and its helpers
 * among those read: each one with its flags taken out is refused.
 */
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkOptionalLocks, checkToolLocks, DRIVER, TOOLS } from './optional-locks.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'optional-locks-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

let n = 0;
/** A fresh tree from `{ 'Trees/X.cs': 'source' }` under the driver's folder; returns its root. */
function tree(files) {
  const dir = join(scratch, `t${n++}`);
  for (const [rel, text] of Object.entries(files)) {
    const full = join(dir, DRIVER, rel);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, text);
  }
  return dir;
}

const reader = (call) => `namespace Daoris.Driver;
public static class Reads
{
    public static async Task<string> Read(string tree, CancellationToken ct)
    {
        var (_, said, _) = await WorkingTree.GitAsync(tree, ${call}, ct).ConfigureAwait(false);
        return said;
    }
}
`;

const verdict = (call) => checkOptionalLocks(tree({ 'Trees/Reads.cs': reader(call) }));

test('a status without --no-optional-locks is refused, naming its line', () => {
  const { problems } = verdict('["status", "--porcelain"]');
  assert.equal(problems.length, 1);
  assert.match(problems[0], /Trees\/Reads\.cs:6: git status without --no-optional-locks/);
});

test("every shape of an unlocked read is refused: a diff, git's own options before it, and a list over lines", () => {
  for (const call of [
    '["diff", "--cached", "--quiet", "HEAD"]',
    '["-c", "core.quotePath=false", "diff", "--name-only", "HEAD~1", "HEAD"]',
    '["-C", "elsewhere", "status"]',
    '[\n            // a comment between the words\n            "-c", "core.quotePath=false",\n            "diff", "-p", range,\n        ]',
  ]) {
    assert.equal(verdict(call).problems.length, 1, call);
  }
});

test('a read with the flag before its command passes, wherever among git\'s own options it stands', () => {
  for (const call of [
    '["--no-optional-locks", "status", "--porcelain"]',
    '["-c", "core.quotePath=false", "--no-optional-locks", "diff", "--name-only", from, to]',
    '[\n            "--no-optional-locks",\n            // the path as itself\n            "-c", "core.quotePath=false",\n            "diff", range,\n        ]',
  ]) {
    assert.deepEqual(verdict(call).problems, [], call);
  }
});

test('the flag after the command is no flag', () => {
  assert.equal(verdict('["status", "--no-optional-locks"]').problems.length, 1);
});

test('another command, an indexer, a list it cannot read and a command only shown pass', () => {
  for (const call of [
    '["rev-parse", "HEAD"]',
    '["log", "--format=%H", "status"]',
    '[verb, "status"]',
    '[.. words, "diff"]',
  ]) {
    assert.deepEqual(verdict(call).problems, [], call);
  }
  const indexer = 'public static class Said\n{\n    public static void Put(JsonObject said, string status) => said["status"] = status;\n}\n';
  const shown = 'public static class Shown\n{\n    public static string Compare(string a, string b) => GitRefs.Command(["diff", $"{a}...{b}"]);\n}\n';
  assert.deepEqual(checkOptionalLocks(tree({ 'Trees/Said.cs': indexer, 'Trees/Shown.cs': shown })).problems, []);
});

test('a marked read passes and is counted; a command in a comment or a string is not one', () => {
  const marked = reader('["diff", "--name-only", "--diff-filter=U"]').replace(
    'ConfigureAwait(false);',
    'ConfigureAwait(false); // optional-locks: a replay of its own, the index its own',
  );
  assert.deepEqual(checkOptionalLocks(tree({ 'Trees/Reads.cs': marked })), { problems: [], marked: 1 });
  const quoted = reader('Words("[\\"status\\"]") /* ["diff", "HEAD"] */');
  assert.deepEqual(checkOptionalLocks(tree({ 'Trees/Reads.cs': quoted })).problems, []);
});

test('bin and obj are skipped', () => {
  const plain = reader('["status"]');
  assert.deepEqual(checkOptionalLocks(tree({ 'bin/Reads.cs': plain, 'obj/Reads.cs': plain })).problems, []);
});

test("the repository's own driver asks every status and diff with --no-optional-locks", () => {
  assert.deepEqual(checkOptionalLocks(root).problems, []);
});

// QUEUELOCK1: the tools' own scripts, where the merge queue's runner asks git of the main checkout.

/** A fresh `tools/` folder from `{ 'x.mjs': 'source' }`; returns its root. */
function tools(files) {
  const dir = join(scratch, `t${n++}`);
  for (const [rel, text] of Object.entries(files)) {
    const full = join(dir, TOOLS, rel);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, text);
  }
  return dir;
}

const script = (call) => `import { spawnGit } from './fsx.mjs';
const git = (cwd, args) => spawnGit(args, { cwd, encoding: 'utf8' });
export const clean = (home) => git(home, ${call}).stdout === '';
`;

const scripted = (call) => checkToolLocks(tools({ 'x.mjs': script(call) }));

test("a script's unlocked status or diff is refused, whatever its quotes, naming its line", () => {
  const { problems } = scripted("['status', '--porcelain', '--untracked-files=no']");
  assert.equal(problems.length, 1);
  assert.match(problems[0], /tools\/x\.mjs:3: git status without --no-optional-locks/);
  for (const call of [
    '["status", "--porcelain"]',
    '[`diff`, `--name-only`]',
    "['diff', '-z', '--name-only', '--no-renames', `HEAD...${branch}`]",
    "['-c', 'core.quotePath=false', 'diff', from, to]",
    "['diff-index', '--quiet', 'HEAD']",
    "['diff-files', '--quiet']",
    "[\n    // the clean look\n    'status',\n    '--porcelain',\n  ]",
  ]) {
    assert.equal(scripted(call).problems.length, 1, call);
  }
});

test("a script's read with the flag before its command passes, and so do reads of commits alone", () => {
  for (const call of [
    "['--no-optional-locks', 'status', '--porcelain']",
    "['-c', 'core.quotePath=false', '--no-optional-locks', 'diff', '--name-only', from, to]",
    "['rev-parse', 'HEAD']",
    "['log', '--format=%H', 'status']",
    "['merge', '--ff-only', 'next']",
    "['add', '-A']",
    '[verb, "status"]',
    "[...args, 'diff']",
  ]) {
    assert.deepEqual(scripted(call).problems, [], call);
  }
});

test("a script's marked read passes and is counted; a command in a comment, a string, a pattern or a set is not one", () => {
  const marked = script("['diff', '--name-only', '--diff-filter=U']").replace('=== \'\';', "=== ''; // optional-locks: its own replay, the index its own");
  assert.deepEqual(checkToolLocks(tools({ 'x.mjs': marked })), { problems: [], marked: 1 });
  const quiet = [
    "// git(home, ['status'])\n/* ['diff', 'HEAD'] */\nexport const a = \"['status']\";",
    "export const b = `[\\'diff\\'] ${1}`;\nexport const c = /['status']/.test(a) ? 1 : 2 / 3;",
    "export const COMMANDS = new Set(['status', 'diff']);",
    "export const d = (found) => found['status'] ?? found[0]['diff'];",
  ];
  quiet.forEach((source, at) => assert.deepEqual(checkToolLocks(tools({ [`q${at}.mjs`]: source })).problems, [], source));
});

test('a script returning its arguments is read, and a test file is not', () => {
  assert.equal(checkToolLocks(tools({ 'r.mjs': "export function args() {\n  return ['status', '--porcelain'];\n}\n" })).problems.length, 1);
  assert.deepEqual(checkToolLocks(tools({ 'x.test.mjs': script("['status']"), 'node_modules/y.mjs': script("['status']") })).problems, []);
});

test("the repository's own tools ask every status and diff with --no-optional-locks", () => {
  assert.deepEqual(checkToolLocks(root).problems, []);
});

test('the merge tool and its helpers are among the tools read, each with a read in it held', () => {
  const read = (file) => readFileSync(join(root, TOOLS, file), 'utf8');
  for (const file of ['merge-branch.mjs', 'as-merged.mjs', 'process-reach.mjs', 'test-map.mjs']) {
    const text = read(file);
    assert.match(text, /'--no-optional-locks', '(?:status|diff)'/, `${file} asks git a read`);
    // The same source with the flag taken out is refused, so the census does read this file.
    const unflagged = text.replaceAll("'--no-optional-locks', ", '');
    const files = { [file]: unflagged };
    assert.ok(checkToolLocks(tools(files)).problems.length > 0, `${file} unflagged is refused`);
  }
});
