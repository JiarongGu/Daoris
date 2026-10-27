/**
 * QUEUELOCK1: what the queue says of a promotion held on `index.lock` (FIX-LOG 2026-10-28, QUEUELOCK1).
 *
 *   node --test tools/index-lock.test.mjs
 *
 * The statement is held on its parts (an age's words, which processes are git, the three answers a lock can have) and on one real
 * refusal: a repository in a gitignored scratch folder with a 0-byte `index.lock` 1 h 45 min old, a real `git merge --ff-only`
 * refused by it, and the note `indexLockSaid` adds to git's words, with no git process and with one. The lock is still there after:
 * the queue never removes it.
 */
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, rmSync, utimesSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { spawnGit } from './fsx.mjs';
import { ageText, gitProcessIds, indexLockNote, mentionsIndexLock, readIndexLock } from './index-lock.mjs';
import { indexLockSaid } from './merge-branch.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'index-lock-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

let n = 0;
const folder = () => {
  const dir = join(scratch, `t${n++}`);
  mkdirSync(dir, { recursive: true });
  return dir;
};

function git(cwd, ...args) {
  const run = spawnGit(['-c', 'user.name=t', '-c', 'user.email=t@example.invalid', '-c', 'commit.gpgsign=false', ...args], { cwd, encoding: 'utf8' });
  return { status: run.status, out: (run.stdout ?? '').trim(), err: (run.stderr ?? '').trim() };
}

/** A repository on `main` with a branch `next` one commit ahead of it, ready to fast-forward. */
function repository() {
  const dir = folder();
  for (const step of [['init', '-q', '-b', 'main'], ['commit', '-q', '--allow-empty', '-m', 'one'], ['branch', 'next'], ['checkout', '-q', 'next'], ['commit', '-q', '--allow-empty', '-m', 'two'], ['checkout', '-q', 'main']]) {
    const run = git(dir, ...step);
    assert.equal(run.status, 0, `git ${step.join(' ')}: ${run.err}`);
  }
  return dir;
}

/** A 0-byte lock in `dir`'s git folder, last written `ageMs` ago. */
function lock(dir, ageMs) {
  const file = join(dir, '.git', 'index.lock');
  writeFileSync(file, '');
  const then = new Date(Date.now() - ageMs);
  utimesSync(file, then, then);
  return file;
}

const HOUR = 3600 * 1000;
const stale = 1.75 * HOUR;

test('an age is said in its largest two units', () => {
  assert.equal(ageText(0), '0 s');
  assert.equal(ageText(-5000), '0 s');
  assert.equal(ageText(42_000), '42 s');
  assert.equal(ageText(7 * 60_000), '7 min');
  assert.equal(ageText(stale), '1 h 45 min');
  assert.equal(ageText(2 * HOUR), '2 h');
  assert.equal(ageText(51 * HOUR), '2 d 3 h');
});

test('a git process is one named git, by its executable or the first word of its command, and no other', () => {
  const processes = [
    { pid: 1, exe: 'C:\\Program Files\\Git\\mingw64\\bin\\git.exe', command: '"C:\\Program Files\\Git\\mingw64\\bin\\git.exe" status' },
    { pid: 2, exe: '', command: '/usr/bin/git merge --ff-only next' },
    { pid: 3, exe: '', command: '"C:\\Program Files\\Git\\cmd\\GIT.EXE" fetch' },
    { pid: 4, exe: 'C:\\x\\git-credential-manager.exe', command: 'git-credential-manager get' },
    { pid: 5, exe: 'C:\\node.exe', command: 'node tools/merge-branch.mjs git status' },
    { pid: 6, exe: '', command: '/usr/bin/gitk' },
  ];
  assert.deepEqual(gitProcessIds(processes), [1, 2, 3]);
});

test('a lock reads as its age and size, and an absent one as none', () => {
  const dir = repository();
  assert.equal(readIndexLock(join(dir, '.git')), null);
  lock(dir, stale);
  const read = readIndexLock(join(dir, '.git'));
  assert.equal(read.bytes, 0);
  assert.equal(ageText(read.ageMs), '1 h 45 min');
});

test('the note says the age and whether git runs, and says the queue never removes it', () => {
  const old = { ageMs: stale, bytes: 0 };
  assert.equal(indexLockNote(null, { processes: [] }), null);
  const idle = indexLockNote(old, { processes: [{ pid: 9, exe: '', command: 'node a.js' }] });
  assert.match(idle, /^\.git\/index\.lock is 1 h 45 min old \(0 bytes\) and no git process is running/);
  assert.match(idle, /the queue never removes it/);
  const busy = indexLockNote({ ageMs: 4000, bytes: 0 }, { processes: [{ pid: 77, exe: '', command: 'git commit -m x' }] });
  assert.match(busy, /^\.git\/index\.lock is 4 s old \(0 bytes\) and a git process is running \(pid 77\), which may hold it/);
  assert.doesNotMatch(busy, /no git process/);
  const unknown = indexLockNote(old, { error: 'the process query failed: denied' });
  assert.match(unknown, /whether a git process is running could not be read \(the process query failed: denied\)/);
  assert.doesNotMatch(unknown, /no git process is running/);
});

test("git's words naming an index.lock are told from any other refusal", () => {
  assert.equal(mentionsIndexLock("fatal: Unable to create '/r/.git/index.lock': File exists."), true);
  assert.equal(mentionsIndexLock('fatal: Not possible to fast-forward, aborting.'), false);
  assert.equal(mentionsIndexLock(undefined), false);
});

test('a real fast-forward refused by a stale lock is told with the lock and no git process, and the lock is left', () => {
  const dir = repository();
  const file = lock(dir, stale);
  const moved = git(dir, 'merge', '--ff-only', '--quiet', 'next');
  assert.notEqual(moved.status, 0);
  assert.match(moved.err, /index\.lock/);
  const said = indexLockSaid(dir, moved.err, () => ({ processes: [] }));
  assert.match(said, /^; \.git\/index\.lock is 1 h 45 min old \(0 bytes\) and no git process is running/);
  assert.equal(existsSync(file), true, 'the queue does not remove the lock');
  // The same refusal with a git running says so, and asks the list only for a refusal naming the lock.
  assert.match(indexLockSaid(dir, moved.err, () => ({ processes: [{ pid: 31, exe: '', command: 'git add -A' }] })), /a git process is running \(pid 31\)/);
  let asked = 0;
  assert.equal(indexLockSaid(dir, 'fatal: Not possible to fast-forward, aborting.', () => { asked++; return { processes: [] }; }), '');
  assert.equal(asked, 0);
  assert.equal(existsSync(file), true);
});

test('a lock gone by the time it is looked at adds nothing, and a list that could not be read is said', () => {
  const dir = repository();
  assert.equal(indexLockSaid(dir, "Unable to create '.git/index.lock': File exists.", () => ({ processes: [] })), '');
  lock(dir, 3 * 60_000);
  assert.match(indexLockSaid(dir, "Unable to create '.git/index.lock': File exists.", () => ({ error: 'ps could not start: nope' })), /3 min old.*could not be read \(ps could not start: nope\)/);
});
