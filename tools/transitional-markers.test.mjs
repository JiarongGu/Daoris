/**
 * ONEWAY1ad-i: the transitional-markers gate (D228; docs/2026-11-02-oneway-design.md §1.3).
 *
 *   node --test tools/transitional-markers.test.mjs
 *
 * `checkTransitional` reads a tree. Each case builds a small one in a gitignored scratch folder of the repository: the register,
 * some sources, `TASKS.md` and the archive. A clean tree passes; a marker naming no entry, an entry with no marker, a closed
 * row, a marker inside a string, a prose row and an id used twice are told apart. The last case holds this repository clean.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkTransitional, isOpen, REGISTER } from './transitional-markers.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'transitional-markers-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const TASKS = '- [ ] **ROWA1 — a row** (driver)\n- [ ] **ONEWAY1 — one way** Rows, each `ONEWAY1`-prefixed: **wave 6** — af, c-ii.\n';
const ARCHIVE = '## ROWB1 — a finished row (2026-11-01)\n';

const register = (...entries) => `namespace Daoris.Driver;
public static class TransitionalRegister
{
    public static IReadOnlyList<TransitionalEntry> Entries { get; } =
    [
${entries.map(([id, row]) => `        new() { Id = "${id}", Kept = "A.cs:1", Row = "${row}" },`).join('\n')}
    ];
}
`;

let n = 0;
/** A fresh tree: `{ register, sources: { 'X/A.cs': text }, tasks, archive }`; returns its root. */
function tree({ entries = [], sources = {}, tasks = TASKS, archive = ARCHIVE }) {
  const dir = join(scratch, `t${n++}`);
  const put = (rel, text) => {
    const full = join(dir, rel);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, text);
  };
  put(REGISTER, register(...entries));
  for (const [rel, text] of Object.entries(sources)) put(`src/${rel}`, text);
  put('TASKS.md', tasks);
  put('docs/task-archive.md', archive);
  return dir;
}

const site = (id) => `class A\n{\n    // transitional: ${id}\n    void Read() { }\n}\n`;

test('a clean tree passes, and counts what it read', () => {
  const dir = tree({ entries: [['one', 'ROWA1']], sources: { 'D/A.cs': site('one') } });
  const { problems, entries, markers } = checkTransitional(dir);
  assert.deepEqual(problems, []);
  assert.equal(entries, 1);
  assert.equal(markers, 1);
});

test('a marker that names no entry is refused', () => {
  const dir = tree({ entries: [['one', 'ROWA1']], sources: { 'D/A.cs': site('one'), 'D/B.cs': site('stray') } });
  const { problems } = checkTransitional(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /D\/B\.cs:3: .*stray.*names no entry/);
});

test('an entry with no marker is refused', () => {
  const dir = tree({ entries: [['one', 'ROWA1'], ['two', 'ROWA1']], sources: { 'D/A.cs': site('one') } });
  const { problems } = checkTransitional(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /two.*no marker/);
});

test('an entry whose row is archived, or named nowhere, is refused', () => {
  const dir = tree({
    entries: [['one', 'ROWB1'], ['two', 'ROWZ9']],
    sources: { 'D/A.cs': `${site('one')}${site('two')}` },
  });
  const { problems } = checkTransitional(dir);
  assert.equal(problems.length, 2);
  assert.match(problems[0], /one.*ROWB1.*not open/);
  assert.match(problems[1], /two.*ROWZ9.*not open/);
});

test('a ONEWAY1 row is open while the ONEWAY1 entry lists its letter and the archive has no heading for it', () => {
  const dir = tree({ entries: [['one', 'ONEWAY1af'], ['two', 'ONEWAY1c-ii']], sources: { 'D/A.cs': `${site('one')}${site('two')}` } });
  assert.deepEqual(checkTransitional(dir).problems, []);
  const closed = tree({
    entries: [['one', 'ONEWAY1af']],
    sources: { 'D/A.cs': site('one') },
    archive: `${ARCHIVE}## ONEWAY1af — the end (2026-11-09)\n`,
  });
  assert.match(checkTransitional(closed).problems[0], /ONEWAY1af.*not open/);
});

test('isOpen matches whole ids: a longer id or a letter inside a word does not open a row', () => {
  const tasks = '- [ ] **ROWA10 — ten**\n- [ ] **ONEWAY1 — x** Rows, each `ONEWAY1`-prefixed: wave — ad-ii, afoot.\n';
  assert.equal(isOpen('ROWA1', tasks, ''), false);
  assert.equal(isOpen('ROWA10', tasks, ''), true);
  assert.equal(isOpen('ONEWAY1ad', tasks, ''), false);
  assert.equal(isOpen('ONEWAY1ad-ii', tasks, ''), true);
  assert.equal(isOpen('ONEWAY1af', tasks, ''), false);
  assert.equal(isOpen('ONEWAY1ad-ii', tasks, '## ONEWAY1 — x (2026-11-09)\n'), false);
});

test('ONEWAY1ad-ii: a row of another entry is open while that open entry names it in bold or as `row <letters>`', () => {
  const tasks = '- [ ] **WORKFLOWCLEAN1 — wave** then **1z** the paths deleted, **1j** only if found.\n'
    + '- [ ] **FOUNDATION1 — one foundation** all built but two: row v, records last, waits.\n';
  assert.equal(isOpen('WORKFLOWCLEAN1z', tasks, ''), true);
  assert.equal(isOpen('WORKFLOWCLEAN1j', tasks, ''), true);
  assert.equal(isOpen('WORKFLOWCLEAN1y', tasks, ''), false);
  assert.equal(isOpen('FOUNDATION1v', tasks, ''), true);
  assert.equal(isOpen('FOUNDATION1w', tasks, ''), false);
  assert.equal(isOpen('WORKFLOWCLEAN1z', tasks, '## WORKFLOWCLEAN1z — the paths deleted (2026-12-01)\n'), false);
  assert.equal(isOpen('WORKFLOWCLEAN1z', tasks, '## WORKFLOWCLEAN1 — the cleanup (2026-12-01)\n'), false);
  assert.equal(isOpen('WORKFLOWCLEAN1z', tasks.replace('- [ ] **WORKFLOWCLEAN1', '- [x] **WORKFLOWCLEAN1'), ''), false);
});

test('ONEWAY1ad-ii: a marker in the web\'s TypeScript is read, and one in its tests or stories is not', () => {
  const ts = "export function read(kept: string) {\n  // transitional: one\n  return kept === 'roles' ? 'setup' : kept;\n}\n";
  const dir = tree({
    entries: [['one', 'ROWA1']],
    sources: { 'Daoris.Web/src/a.ts': ts, 'Daoris.Web/src/a.test.ts': "// transitional: stray\n", 'Daoris.Web/src/A.stories.tsx': "// transitional: stray2\n" },
  });
  const { problems, markers } = checkTransitional(dir);
  assert.deepEqual(problems, []);
  assert.equal(markers, 1);
});

test('a marker inside a string, or in a block comment, is not read; one in a trailing comment is', () => {
  const src = 'class A\n{\n    string s = "// transitional: stray";\n    /* // transitional: also */\n    int x; // transitional: one\n}\n';
  const dir = tree({ entries: [['one', 'ROWA1']], sources: { 'D/A.cs': src } });
  const { problems, markers } = checkTransitional(dir);
  assert.deepEqual(problems, []);
  assert.equal(markers, 1);
});

test('a row named in prose is not a marker', () => {
  const src = 'class A\n{\n    // The owed door names MODELROLE1h; it goes with ONEWAY1a.\n    // transitional: one\n}\n';
  const dir = tree({ entries: [['one', 'ROWA1']], sources: { 'D/A.cs': src } });
  assert.deepEqual(checkTransitional(dir).problems, []);
});

test('tests and build output are not read; the register itself holds no marker', () => {
  const dir = tree({
    entries: [['one', 'ROWA1']],
    sources: { 'D/A.cs': site('one'), 'D.Tests/T.cs': site('stray'), 'D/obj/G.cs': site('stray2') },
  });
  assert.deepEqual(checkTransitional(dir).problems, []);
});

test('an id twice in the register is refused', () => {
  const dir = tree({ entries: [['one', 'ROWA1'], ['one', 'ROWA1']], sources: { 'D/A.cs': site('one') } });
  assert.match(checkTransitional(dir).problems[0], /one.*twice/);
});

test('a tree with no register throws, so the runner exits 2', () => {
  const dir = join(scratch, 'empty');
  mkdirSync(dir, { recursive: true });
  assert.throws(() => checkTransitional(dir));
});

test('this repository is clean', () => {
  const { problems, entries } = checkTransitional(root);
  assert.deepEqual(problems, []);
  assert.ok(entries >= 4, 'the first entries are registered');
});
