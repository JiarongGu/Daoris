/**
 * READSHARE1: the shared-read census (FIX-LOG 2026-10-22, BINDRACE1).
 *
 *   node --test tools/read-shares.test.mjs
 *
 * `checkReadShares` reads a driver source tree. Each case builds a small tree in a gitignored scratch folder of the repository and
 * holds the verdict: a plain read in a source that writes through AtomicFile, or in one naming where such a write lands, is refused;
 * a shared read, a marked read and a read in a source that does neither pass. The last case holds the repository's own tree clean.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkReadShares, DRIVER } from './read-shares.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'read-shares-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

let n = 0;
/** A fresh tree from `{ 'Platform/X.cs': 'source' }` under the driver's folder; returns its root. */
function tree(files) {
  const dir = join(scratch, `t${n++}`);
  for (const [rel, text] of Object.entries(files)) {
    const full = join(dir, DRIVER, rel);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, text);
  }
  return dir;
}

const writer = (read) => `namespace Daoris.Driver;
public static class Kept
{
    public static string PathOf(string home) => Path.Combine(home, "kept.json");
    public static void Keep(string home, string text) => AtomicFile.WriteText(PathOf(home), text);
    public static string Read(string home) => ${read};
}
`;

test('a plain read in a source that writes through AtomicFile is refused, naming its line', () => {
  const { problems } = checkReadShares(tree({ 'Platform/Kept.cs': writer('File.ReadAllText(PathOf(home))') }));
  assert.equal(problems.length, 1);
  assert.match(problems[0], /Platform\/Kept\.cs:6: File\.ReadAllText in a source which writes through AtomicFile/);
});

test('a shared read passes: AtomicFile.ReadText, ReadBytes, and a stream that shares deletion', () => {
  for (const read of [
    'AtomicFile.ReadText(PathOf(home))',
    'Convert.ToBase64String(AtomicFile.ReadBytes(PathOf(home)))',
    'new StreamReader(new FileStream(PathOf(home), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)).ReadToEnd()',
  ]) {
    assert.deepEqual(checkReadShares(tree({ 'Platform/Kept.cs': writer(read) })).problems, [], read);
  }
});

test('every plain shape is refused: bytes, lines, an open, and a stream that does not share deletion', () => {
  for (const read of [
    'Convert.ToBase64String(File.ReadAllBytes(PathOf(home)))',
    'string.Join("", File.ReadLines(PathOf(home)))',
    'System.IO.File.OpenRead(PathOf(home)).ToString()',
    'new StreamReader(new FileStream(PathOf(home), FileMode.Open, FileAccess.Read, FileShare.Read)).ReadToEnd()',
  ]) {
    assert.equal(checkReadShares(tree({ 'Platform/Kept.cs': writer(read) })).problems.length, 1, read);
  }
});

test('a plain read in another source naming where the write lands is refused', () => {
  const dir = tree({
    'Platform/Kept.cs': writer('AtomicFile.ReadText(PathOf(home))'),
    'Loop/Reader.cs': 'public static class Reader\n{\n    public static string Read(string home) => File.ReadAllText(Kept.PathOf(home));\n}\n',
  });
  const { problems } = checkReadShares(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /Loop\/Reader\.cs:3: .* which names Kept\.PathOf, where AtomicFile writes/);
});

test('a plain read in another source naming a home entry a writer keeps is refused', () => {
  const dir = tree({
    'Platform/Remotes.cs': 'public static class Remotes\n{\n    public static void Keep(string home) => JsonFile.Write(DaorisHome.Entry(home, HomeEntry.Remotes), node);\n}\n',
    'Loop/Reader.cs': 'public static class Reader\n{\n    public static string Read(string home)\n        => File.ReadAllText(DaorisHome.Entry(home, HomeEntry.Remotes));\n}\n',
    'Loop/Other.cs': 'public static class Other\n{\n    public static string Read(string home) => File.ReadAllText(DaorisHome.Entry(home, HomeEntry.Logs));\n}\n',
  });
  const { problems } = checkReadShares(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /Loop\/Reader\.cs:4: .* which names HomeEntry\.Remotes, where AtomicFile writes/);
});

test('a source that neither writes nor names a write reads plainly', () => {
  const dir = tree({ 'Trees/Heads.cs': 'public static class Heads\n{\n    public static string Head(string git) => File.ReadAllText(Path.Combine(git, "HEAD"));\n}\n' });
  assert.deepEqual(checkReadShares(dir).problems, []);
});

test('a marked read passes and is counted; a read in a comment or a string is not one', () => {
  const marked = writer('File.ReadAllText(Path.Combine(home, "HEAD")) // read-shares: git writes HEAD, not AtomicFile');
  assert.deepEqual(checkReadShares(tree({ 'Platform/Kept.cs': marked })), { problems: [], marked: 1 });
  const quoted = writer('"File.ReadAllText(path)" /* File.ReadAllBytes(path) */');
  assert.deepEqual(checkReadShares(tree({ 'Platform/Kept.cs': quoted })).problems, []);
});

test('a claim that deletes on close is no read; bin and obj are skipped', () => {
  const claim = writer('new FileStream(PathOf(home), FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.DeleteOnClose).ToString()');
  assert.deepEqual(checkReadShares(tree({ 'Platform/Kept.cs': claim })).problems, []);
  const plain = writer('File.ReadAllText(PathOf(home))');
  assert.deepEqual(checkReadShares(tree({ 'bin/Kept.cs': plain, 'obj/Kept.cs': plain })).problems, []);
});

test("the repository's own driver reads every file AtomicFile writes sharing its deletion", () => {
  assert.deepEqual(checkReadShares(root).problems, []);
});
