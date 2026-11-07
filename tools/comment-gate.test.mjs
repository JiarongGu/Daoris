/**
 * The comment gate (`tools/comment-gate.mjs`): a changed file's comment share does not rise, and a comment it adds names no
 * row id, decision number or history note.
 *
 *   node --test tools/comment-gate.test.mjs
 *
 * The scanner cases read source text. The change cases build a small repository in a gitignored scratch folder, commit a base
 * on `main`, change the working tree or a branch, and hold the verdict: a decision number and a row id are refused, so are a
 * history note and a rise in the share; a change that only removes comments, a code-only change, a string that looks like a
 * comment and a moved comment pass. The last case runs the tool's command line.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { added, checkChange, comments, judge, namings, share } from './comment-gate.mjs';
import { execGit } from './fsx.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'comment-gate-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const BASE = `namespace Daoris;
// Reads the file once.
public static class Reads
{
    public static string One() => "one";
    public static string Two() => "two";
}
`;

let n = 0;
/** A repository whose `main` holds `files`; returns its folder and a `git` for it. */
function repo(files) {
  const dir = join(scratch, `r${n++}`);
  mkdirSync(dir, { recursive: true });
  const git = (...args) => execGit(['-c', 'user.name=t', '-c', 'user.email=t@example.com', '-c', 'commit.gpgsign=false', ...args], { cwd: dir, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  git('init', '-q', '-b', 'main');
  put(dir, files);
  git('add', '-A');
  git('commit', '-q', '-m', 'base');
  return { dir, git };
}

function put(dir, files) {
  for (const [rel, text] of Object.entries(files)) {
    mkdirSync(dirname(join(dir, rel)), { recursive: true });
    writeFileSync(join(dir, rel), text);
  }
}

const verdict = (dir) => checkChange(dir, 'main').problems;

test('comments reads line, block and doc comments and leaves strings, chars and regular expressions alone', () => {
  const src = [
    'var a = "// not one"; // one',
    "var b = '/'; /* two */",
    'var re = /https?:\\/\\//; // three',
    'var raw = @"// no',
    '// no";',
    '/// <summary>four</summary>',
    '/*',
    ' * five',
    ' */',
    'var t = `// no ${x} // no`;',
  ].join('\n');
  const found = comments(src);
  assert.deepEqual(found.map((c) => [c.line, c.text, c.whole]), [
    [1, 'one', false],
    [2, 'two', false],
    [3, 'three', false],
    [6, '<summary>four</summary>', true],
    [7, '', true],
    [8, 'five', true],
    [9, '', true],
  ]);
});

test('the share counts comment-only lines among the non-blank ones', () => {
  assert.deepEqual(share(BASE), { comment: 1, total: 7 });
  assert.deepEqual(share('// a\n\n// b\ncode(); // c\n'), { comment: 2, total: 3 });
});

test('namings finds row ids, decision numbers and history, and lets a standard stand', () => {
  assert.deepEqual(namings('since LANDAUTO1 the lock is held').map(([, w]) => w), ['LANDAUTO1']);
  assert.deepEqual(namings('see D99').map(([k]) => k), ['a decision number']);
  assert.deepEqual(namings('this used to retry, and was slower').map(([, w]) => w), ['used to', 'was']);
  assert.deepEqual(namings('hashes UTF8 text with SHA256 and a HTTP2 frame, D is a letter, LOG2b ends it').map(([, w]) => w), ['LOG2b']);
  assert.deepEqual(namings('Holds the lock so two writers never interleave.'), []);
});

test('added is the comments a text gains, a moved or kept one not', () => {
  const before = '// keep\ncode();\n// move\n';
  const after = '// move\ncode();\n// keep\n// fresh\n';
  assert.deepEqual(added(before, after).map((c) => c.text), ['fresh']);
});

test('a change that adds a D99 comment is refused', () => {
  const { dir } = repo({ 'src/Reads.cs': BASE });
  put(dir, { 'src/Reads.cs': BASE.replace('    public static string One()', '    // Holds what D99 decided.\n    public static string One()') + 'public static class Pad1 { }\npublic static class Pad2 { }\npublic static class Pad3 { }\npublic static class Pad4 { }\npublic static class Pad5 { }\npublic static class Pad6 { }\n' });
  const problems = verdict(dir);
  assert.equal(problems.length, 1, problems.join('\n'));
  assert.match(problems[0], /src\/Reads\.cs:5: a comment names a decision number \(D99\)/);
});

test('a change that adds a row id is refused, in a trailing comment too', () => {
  const { dir } = repo({ 'tools/x.mjs': 'export const a = 1;\nexport const b = 2;\nexport const c = 3;\nexport const d = 4;\n// Keeps one thing.\n' });
  put(dir, { 'tools/x.mjs': 'export const a = 1;\nexport const b = 2; // LANDAUTO1 wants this\nexport const c = 3;\nexport const d = 4;\nexport const e = 5;\nexport const f = 6;\nexport const g = 7;\n// Keeps one thing.\n' });
  const problems = verdict(dir);
  assert.equal(problems.length, 1, problems.join('\n'));
  assert.match(problems[0], /tools\/x\.mjs:2: a comment names a row id \(LANDAUTO1\)/);
});

test('a history note is refused, and a rise in the share is refused', () => {
  const { dir } = repo({ 'src/Reads.cs': BASE });
  put(dir, { 'src/Reads.cs': BASE.replace('// Reads the file once.', '// Reads the file once.\n// It used to read twice.\n// More words.\n// Still more.') });
  const problems = verdict(dir);
  assert.equal(problems.length, 2, problems.join('\n'));
  assert.ok(problems.some((p) => /comments rose from 14\.3% \(1\/7\) to 40\.0% \(4\/10\)/.test(p)));
  assert.ok(problems.some((p) => /a history note \(used to\)/.test(p)));
});

test('a change that only removes comments passes, and so does one that only adds code', () => {
  const { dir } = repo({ 'src/Reads.cs': BASE, 'src/More.cs': BASE });
  put(dir, { 'src/Reads.cs': BASE.replace('// Reads the file once.\n', ''), 'src/More.cs': `${BASE}public static class Three { }\n` });
  assert.deepEqual(verdict(dir), []);
});

test('a file that already names a row keeps it, and a string that looks like a comment is none', () => {
  const old = '// D99 holds this.\nexport const a = 1;\nexport const b = 2;\nexport const c = 3;\n';
  const { dir } = repo({ 'tools/old.mjs': old });
  put(dir, { 'tools/old.mjs': `${old}export const d = "// D98 and LOG2b was here";\nexport const e = 5;\n` });
  assert.deepEqual(verdict(dir), []);
});

test('a new file is held to the naming rule alone, and a renamed one to its old share', () => {
  const { dir, git } = repo({ 'src/Old.cs': BASE });
  git('mv', 'src/Old.cs', 'src/New.cs');
  put(dir, { 'src/Fresh.cs': '// Says why.\npublic static class Fresh { }\n' });
  assert.deepEqual(verdict(dir), []);
  put(dir, { 'src/New.cs': BASE.replace('// Reads', '// D12: reads'), 'src/Fresh.cs': '// Says why, as D7 did.\npublic static class Fresh { }\n' });
  const problems = verdict(dir);
  assert.equal(problems.length, 2, problems.join('\n'));
  assert.ok(problems.some((p) => p.startsWith('src/New.cs:2:') && p.includes('D12')));
  assert.ok(problems.some((p) => p.startsWith('src/Fresh.cs:1:') && p.includes('D7')));
});

test('other files and generated or build folders are not read', () => {
  const { dir } = repo({ 'README.md': 'a\n' });
  put(dir, { 'README.md': '# D99 was here\n', 'src/obj/Gen.cs': '// D99\n', 'src/data/generated/service.ts': '// D99\n', 'src/Moved.cs': 'public static class Moved { }\n' });
  assert.deepEqual(verdict(dir), []);
});

test('a merge diff is read from a branch tip, apart from the working tree', () => {
  const { dir, git } = repo({ 'src/Reads.cs': BASE });
  git('checkout', '-q', '-b', 'topic');
  put(dir, { 'src/Reads.cs': `${BASE}// D99 says so.\n` });
  git('commit', '-q', '-am', 'topic');
  git('checkout', '-q', 'main');
  assert.deepEqual(checkChange(dir, 'main'), { problems: [], checked: 0 });
  const { problems } = checkChange(dir, 'main', 'topic');
  assert.equal(problems.length, 2, problems.join('\n'));
  assert.match(problems.join('\n'), /D99/);
});

test('the command line exits 0 on a clean change, 1 on a breach and 2 where there is no main', () => {
  const { dir, git } = repo({ 'src/Reads.cs': BASE });
  const run = (...args) => spawnSync(process.execPath, [join(root, 'tools', 'comment-gate.mjs'), '--root', dir, ...args], { encoding: 'utf8' });
  assert.equal(run().status, 0);
  put(dir, { 'src/Reads.cs': `${BASE}// Names D99.\n` });
  const refused = run();
  assert.equal(refused.status, 1);
  assert.match(refused.stderr, /comment-gate: src\/Reads\.cs:8: a comment names a decision number \(D99\)/);
  assert.equal(run('--base', 'nowhere').status, 2);
  git('checkout', '-q', '--', '.');
});

test('judge passes a file that gains code and keeps its comments', () => {
  assert.deepEqual(judge('src/Reads.cs', BASE, `${BASE}public static class Three { }\npublic static class Four { }\n`), []);
});
