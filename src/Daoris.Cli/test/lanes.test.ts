import { test } from 'node:test';
import assert from 'node:assert/strict';
import { LANES_FILE, laneWords, lanesProblems, readLanes } from '../src/lanes.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * D115 §2.2 (DEV4): the words a registration keeps of a repository's lanes, read by one rule — the
 * table the service's `Declared.Lanes` holds too, row for row (twins, `RegistrationStoreTests`):
 * absent and empty are none; the id, title and summary are trimmed; an id that is not lower-case
 * letters, digits and dashes starting with a letter names no lane and is dropped; a repeated id is
 * dropped, the first kept; only the first lane marked steward keeps the mark; a missing title or
 * summary is empty; paths and gates never reach the words.
 */
const WORDS_TABLE: [string, unknown, string][] = [
  ['absent', undefined, ''],
  ['empty', [], ''],
  ['one lane', [{ id: 'core', title: 'Core', summary: 'The runtime.' }], 'core|Core|The runtime.|'],
  ['trimmed', [{ id: ' core ', title: ' Core ', summary: ' The runtime. ' }], 'core|Core|The runtime.|'],
  ['upper case names no lane', [{ id: 'Core', title: 'Core' }], ''],
  ['a digit first names no lane', [{ id: '1st' }], ''],
  ['an underscore names no lane', [{ id: 'a_b' }], ''],
  ['a blank id names no lane', [{ id: '' }], ''],
  ['a missing entry names no lane', [null], ''],
  ['a repeat keeps the first', [{ id: 'core', title: 'First' }, { id: 'core', title: 'Again' }], 'core|First||'],
  [
    'one steward',
    [{ id: 'records', steward: true }, { id: 'core', steward: true }],
    'records|||steward;core|||',
  ],
  [
    'words only',
    [{ id: 'core', title: 'Core', summary: 'S', paths: ['src/**'], gates: ['cli'] }, { id: 'docs-2', title: 'Docs' }],
    'core|Core|S|;docs-2|Docs||',
  ],
];

const said = (words: ReturnType<typeof laneWords>) =>
  words.map((lane) => `${lane.id}|${lane.title}|${lane.summary}|${lane.steward ? 'steward' : ''}`).join(';');

test('a lane\'s words read by one rule, the service\'s twin', () => {
  for (const [name, lanes, expected] of WORDS_TABLE) {
    assert.equal(said(laneWords(lanes)), expected, name);
  }
});

test('the words carry exactly id, title, summary and steward, never a glob', () => {
  const [lane] = laneWords([{ id: 'core', title: 'Core', summary: 'S', paths: ['src/**'], gates: ['cli'] }]);
  assert.deepEqual(Object.keys(lane!).sort(), ['id', 'steward', 'summary', 'title']);
});

/**
 * D115 §2.1: the file is unreadable when a lane's id is malformed or repeated, a lane owns nothing, or
 * two lanes are the steward's — the merge tool's sentences, so a person reads one wording wherever the
 * file is judged. `gates` names the declared gates only the queue judges (DEV5), so connect says nothing
 * of them.
 */
test('an unreadable lanes file names each problem', () => {
  assert.deepEqual(lanesProblems([]), ['the file is not a JSON object']);
  assert.deepEqual(lanesProblems({}), []);
  assert.deepEqual(lanesProblems({ lanes: [] }), []);
  assert.deepEqual(lanesProblems({ lanes: 'core' }), ["'lanes' is not a list"]);
  assert.deepEqual(
    lanesProblems({
      lanes: [
        { id: 'Core', paths: ['a/**'] },
        { id: 'docs', paths: ['docs/**'] },
        { id: 'docs', paths: ['more/**'] },
        { id: 'empty', paths: ['!carved/**'] },
        { id: 'records', steward: true, paths: ['TASKS.md'] },
        { id: 'keeper', steward: true, paths: ['x'], gates: ['anything'] },
      ],
    }),
    [
      "lane 'Core': its id must be lower-case letters, digits and dashes, starting with a letter",
      "lane 'docs': the id is used twice",
      "lane 'empty': it has no paths",
      'two stewards (records, keeper): at most one lane keeps the records',
    ],
  );
});

test('no lanes file is no lanes', () => {
  const fx = makeFixture('lanes-absent');
  assert.equal(readLanes(fx.root), null);
  fx.cleanup();
});

test('a readable lanes file gives its lanes\' words, in the file\'s order', () => {
  const fx = makeFixture('lanes-read');
  fx.write(LANES_FILE, JSON.stringify({
    _why: 'a reason',
    lanes: [
      { id: 'core', title: 'Core', summary: 'The runtime.', paths: ['runtime/**'] },
      { id: 'assets', title: 'Assets', summary: 'The pipeline.', paths: ['assets/**'] },
    ],
    laneless: [{ what: 'docs', paths: ['docs/**'] }],
  }));
  assert.equal(said(readLanes(fx.root)!), 'core|Core|The runtime.|;assets|Assets|The pipeline.|');
  fx.cleanup();
});

test('an unreadable lanes file is refused as policy, naming the file and each problem', () => {
  const fx = makeFixture('lanes-unreadable');
  fx.write(LANES_FILE, JSON.stringify({ lanes: [{ id: 'core' }] }));
  const refused = captureError(() => readLanes(fx.root));
  assert.equal(refused.exitCode, 1);
  assert.match(refused.message, /daoris\.lanes\.json cannot be read/);
  assert.match(refused.message, /lane 'core': it has no paths/);

  fx.write(LANES_FILE, '{ not json');
  const broken = captureError(() => readLanes(fx.root));
  assert.equal(broken.exitCode, 1);
  assert.match(broken.message, /daoris\.lanes\.json is not valid JSON/);
  fx.cleanup();
});
