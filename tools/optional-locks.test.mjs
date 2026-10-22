/**
 * REVIEWLOCK1: the optional-locks census (FIX-LOG 2026-10-23, REVIEWLOCK1).
 *
 *   node --test tools/optional-locks.test.mjs
 *
 * `checkOptionalLocks` reads a driver source tree. Each case builds a small tree in a gitignored scratch folder of the repository
 * and holds the verdict: a git status or diff asked without `--no-optional-locks` before its command is refused; one with it, a
 * marked one, another command, an indexer, a command only shown to a person and a word in a comment pass. The last case holds the
 * repository's own tree clean.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkOptionalLocks, DRIVER } from './optional-locks.mjs';

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
