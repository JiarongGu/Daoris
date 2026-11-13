import { test } from 'vitest';
import assert from 'node:assert/strict';
import { DEPENDENCY_FOLDERS } from '../merge-branch/dependencies.mjs';
import { LockInstalls, movedLockfiles, type LockSeams } from './lock-install.ts';

test('a folder installs when its own package-lock.json moved, in the declared order, and no other path makes one', () => {
  assert.deepEqual([...DEPENDENCY_FOLDERS], ['.', 'src/Daoris.Web']);
  assert.deepEqual(movedLockfiles(['package-lock.json', 'src/a.ts']), ['.']);
  assert.deepEqual(movedLockfiles(['src/Daoris.Web/package-lock.json']), ['src/Daoris.Web']);
  assert.deepEqual(movedLockfiles(['src/Daoris.Web/package-lock.json', 'package-lock.json']), ['.', 'src/Daoris.Web']);
  assert.deepEqual(movedLockfiles(['src\\Daoris.Web\\package-lock.json']), ['src/Daoris.Web'], 'a path with the platform\'s separators');
  for (const path of ['package.json', 'src/Daoris.Web/package.json', 'tools/x/package-lock.json', 'src/Daoris.Web/node_modules/y/package-lock.json', 'a/package-lock.json']) {
    assert.deepEqual(movedLockfiles([path]), [], path);
  }
  assert.deepEqual(movedLockfiles([]), []);
  assert.deepEqual(movedLockfiles(['web/package-lock.json'], ['.', 'web']), ['web'], 'the folders a caller names');
});

function seams(over: Partial<LockSeams> = {}) {
  const installed: string[] = [];
  const said: string[] = [];
  const asked: string[] = [];
  const all: LockSeams = {
    changed: (where) => {
      asked.push(where);
      return ['package-lock.json', 'src/Daoris.Web/package-lock.json', 'src/a.ts'];
    },
    current: () => false,
    install: async (where, folder) => {
      installed.push(`${where}:${folder}`);
      await new Promise((done) => setTimeout(done, 5));
      return { ok: true, note: 'exit 0' };
    },
    say: (line) => said.push(line),
    ...over,
  };
  return { all, installed, said, asked };
}

test('the gates that start at once install each moved folder once, in turn, and say so once before any gate', async () => {
  const s = seams();
  const installs = new LockInstalls(s.all);
  const answers = await Promise.all([installs.ready('tree'), installs.ready('tree'), installs.ready('tree')]);
  assert.deepEqual(answers, [null, null, null]);
  assert.deepEqual(await installs.ready('tree'), null, 'a gate after them waits for nothing');
  assert.deepEqual(s.installed, ['tree:.', 'tree:src/Daoris.Web']);
  assert.deepEqual(s.asked, ['tree'], 'the changed paths are read once');
  assert.equal(s.said.length, 1);
  assert.match(s.said[0]!, /npm ci in \. and src\/Daoris\.Web before the first gate/);
  assert.match(s.said[0]!, /package-lock\.json moved/);
});

test('a folder already installed from the lockfile as it is now installs nothing, and is said', async () => {
  const s = seams({ current: (_where, folder) => folder === '.' });
  const installs = new LockInstalls(s.all);
  assert.equal(await installs.ready('tree'), null);
  assert.deepEqual(s.installed, ['tree:src/Daoris.Web']);
  assert.match(s.said.join('\n'), /npm ci in src\/Daoris\.Web before the first gate/);
  assert.match(s.said.join('\n'), /\. is already installed from it/);
});

test('no moved lockfile installs nothing and says nothing', async () => {
  const s = seams({ changed: () => ['src/a.ts', 'package.json'] });
  const installs = new LockInstalls(s.all);
  assert.equal(await installs.ready('tree'), null);
  assert.deepEqual([s.installed, s.said], [[], []]);
});

test('a failed install is every gate\'s answer, its folder named, the folders after it not tried and the install not repeated', async () => {
  const s = seams({ install: async (where, folder) => {
    s.installed.push(`${where}:${folder}`);
    return { ok: false, note: 'exit 1' };
  } });
  const installs = new LockInstalls(s.all);
  const [first, second] = await Promise.all([installs.ready('tree'), installs.ready('tree')]);
  assert.match(String(first), /npm ci failed in \. \(exit 1\)/);
  assert.equal(second, first);
  assert.equal(await installs.ready('tree'), first);
  assert.deepEqual(s.installed, ['tree:.']);
});

test('an install that throws is a failure, not a thrown gate', async () => {
  const s = seams({ install: async () => {
    throw new Error('spawn npm ENOENT');
  } });
  const installs = new LockInstalls(s.all);
  assert.match(String(await installs.ready('tree')), /npm ci failed in \. \(spawn npm ENOENT\)/);
});

test('each tree installs its own once', async () => {
  const s = seams();
  const installs = new LockInstalls(s.all);
  await Promise.all([installs.ready('a'), installs.ready('b'), installs.ready('a')]);
  assert.deepEqual(s.installed.sort(), ['a:.', 'a:src/Daoris.Web', 'b:.', 'b:src/Daoris.Web']);
});
