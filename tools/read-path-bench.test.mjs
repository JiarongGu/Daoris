import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { indexPassages } from './question-bench.mjs';
import {
  DEFAULT_QUESTIONS, declaredRecords, exactLookup, main, readQuestions, scoreFloor, tenseOf,
} from './read-path-bench.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const records = declaredRecords({
  documents: { decisions: 'docs/decisions', archive: 'docs/task-archive.md', fixes: 'docs/FIX-LOG.md', changelog: 'CHANGELOG.md' },
});
const doc = (path, text) => ({ path, lines: text.split('\n') });

test('a path is history by its declared record or an archive folder, dated by its date, else present', () => {
  assert.equal(tenseOf('docs/decisions/D10.md', records), 'history');
  assert.equal(tenseOf('docs/task-archive.md', records), 'history');
  assert.equal(tenseOf('docs/FIX-LOG.md', records), 'history');
  assert.equal(tenseOf('CHANGELOG.md', records), 'history');
  assert.equal(tenseOf('docs/archive/2026-09-01-gate-design.md', records), 'history');
  assert.equal(tenseOf('docs/2026-09-01-gate-design.md', records), 'dated');
  assert.equal(tenseOf('docs/PLAN_2026-10-16.md', records), 'dated');
  assert.equal(tenseOf('docs/areas/gates.md', records), 'present');
  assert.equal(tenseOf('AGENTS.md', records), 'present');
});

test('the committed questions read, twelve of each, and a malformed set is refused', () => {
  const read = readQuestions(readFileSync(DEFAULT_QUESTIONS, 'utf8'));
  assert.equal(read.present.length, 12);
  assert.equal(read.history.length, 12);
  assert.throws(() => readQuestions('{"present":[],"history":[{"question":"x","answer":["a"]}]}'), /neither id nor words/);
  assert.throws(() => readQuestions('{"present":[{"question":"x","answer":[]}],"history":[]}'), /names no answer/);
});

test('an identifier is looked up exactly: a decision by its file, a row in the records before the rest', () => {
  const corpus = [
    doc('docs/2026-09-01-gate-design.md', 'The design for GATE1, the gate.'),
    doc('docs/decisions/D10.md', '## D10 — The gate'),
    doc('docs/task-archive.md', '## GATE1 — the gate (2026-09-01)\n\n> Outcome: the gate refuses history.'),
  ];
  assert.equal(exactLookup('D10', corpus, records), 'docs/decisions/D10.md');
  assert.equal(exactLookup('GATE1', corpus, records), 'docs/task-archive.md');
  assert.equal(exactLookup('GATE12', corpus, records), null);
});

test('the floor scores a question set: history buries the rule, the present finds it', () => {
  const area = doc('docs/areas/gates.md', '# Gates\n\n## Its rules\n\n1. The comment gate refuses a comment naming a row.');
  const history = doc('docs/decisions/D10.md',
    '## D10 — The comment gate refuses\n\nThe comment gate refuses.\n\nThe comment gate refuses rows.\n\nThe comment gate refuses dates.');
  const items = [{ question: 'what does the comment gate refuse', answer: ['docs/areas/gates.md'] }];

  const asItIs = scoreFloor(items, indexPassages([area, history]), records);
  const present = scoreFloor(items, indexPassages([area]), records);

  assert.equal(asItIs.first, 0);
  assert.ok(asItIs.history >= 2);
  assert.equal(present.first, 1);
  assert.equal(present.history, 0);
});

test('a run over a checkout reports the corpora, the history questions and the bytes at a change\'s start', () => {
  const root = join(here, '..', '_fixtures', `read-path-bench-${process.pid}`);
  const write = (path, text) => {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  };
  try {
    write('daoris.json', JSON.stringify({ documents: {
      index: 'docs/index/README.md', areas: { path: 'docs/areas' }, decisions: 'docs/decisions',
      backlog: { path: 'TASKS.md' }, archive: 'docs/task-archive.md', fixes: 'docs/FIX-LOG.md',
    } }));
    write('AGENTS.md', 'a'.repeat(100));
    write('TASKS.md', '# Tasks\n\n- [ ] **ONE1 — one** a row\n- [ ] **TWO1 — two** a longer row here\n');
    write('docs/index/README.md', 'i'.repeat(50));
    write('docs/areas/gates.md', '# Gates\n\n1. The comment gate refuses a comment naming a row.\n');
    write('docs/areas/README.md', '# Areas\n');
    write('docs/decisions/D10.md', '## D10 — The comment gate refuses\n\nThe comment gate refuses rows.\n');
    write('docs/task-archive.md', '## GATE1 — the gate\n');
    write('docs/2026-09-01-gate-design.md', '# Gate design\n');
    write('docs/archive/2026-08-01-old.md', '# Old\n');
    write('questions.json', JSON.stringify({
      present: [{ question: 'what does the comment gate refuse', answer: ['docs/areas/gates.md'] }],
      history: [{ question: 'GATE1', by: 'id', answer: ['docs/task-archive.md'] },
        { question: 'why does the comment gate refuse rows', by: 'words', answer: ['docs/decisions/D10.md'] }],
    }));
    let printed = '';
    const code = main(['--root', root, '--questions', join(root, 'questions.json'), '--json'], { out: (text) => { printed += text; } });
    assert.equal(code, 0);
    const result = JSON.parse(printed);
    assert.equal(result.present['the present'].first, 1);
    assert.equal(result.history.byId.right, 1);
    assert.equal(result.history.byWords.first, 1);
    assert.equal(result.start.brief, 100);
    assert.equal(result.start.index, 50);
    assert.equal(result.start.area.largest, result.start.area.median);
    assert.ok(result.start.row.largest > result.start.row.median);
    assert.deepEqual(result.documents, { datedAtTop: 1, datedAtTopBytes: 14, filed: 1 });
    assert.equal(main(['--root', root, '--questions', join(root, 'missing.json')], { err: () => {} }), 2);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
