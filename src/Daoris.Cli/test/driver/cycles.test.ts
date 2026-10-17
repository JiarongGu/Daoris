import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { landingEdited, readDriverChoices, writeDriverChoices } from '../../src/driver/driverconfig.ts';
import { WORK_CYCLE_KINDS, cycleStrays, workCycleFor } from '../../src/driver/cycles.ts';
import { driverFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * FABRIC-GRAPH1: the cycle of work a repository or a workspace defines (the fabric events design §C2, §D7, §E7), read by the CLI as
 * the driver reads it. One table, `fixtures/graph-homes.json`, row for row, the driver's `NodeGraphHomesTests` reading it as this file
 * does: which cycle governs a unit, and from where, with D164's whole-rule inheritance.
 */

type Row = {
  name: string; config: Record<string, unknown>; repository: string; workspace: string | null; kind: string;
  source: string; cycle: Record<string, unknown> | null;
};

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'graph-homes.json'), 'utf8')) as { rows: Row[] };

test('graph-homes: each row of the shared table reads as the driver reads it', () => {
  const fx = makeFixture('cycles-table');
  for (const row of TABLE.rows) {
    const path = fx.write('driver.json', JSON.stringify(row.config));
    const home = workCycleFor(readDriverChoices(path), row.repository, row.workspace, row.kind);
    assert.deepEqual({ name: row.name, source: home.source, cycle: home.cycle, kind: home.kind },
      { name: row.name, source: row.source, cycle: row.cycle, kind: row.kind });
  }
});

type Stray = { at: string[]; code: string };
const STRAYS = (JSON.parse(readFileSync(join(here, 'fixtures', 'graph-homes.json'), 'utf8')) as { strays: { name: string; config: Record<string, unknown>; strays: Stray[] }[] }).strays;

test('graph-homes: what the file keeps that no unit reads is said as the driver says it', () => {
  const fx = makeFixture('cycles-strays');
  for (const row of STRAYS) {
    const said = cycleStrays(readDriverChoices(fx.write('driver.json', JSON.stringify(row.config))));
    assert.deepEqual({ name: row.name, strays: said.map(({ at, code }) => ({ at, code })) }, { name: row.name, strays: row.strays });
    for (const stray of said) assert.ok(stray.words.includes(`\`${stray.at.at(-1)}\``), stray.words);
  }
});

test('graph-homes: a cycle that is not an object, and a kind no unit reads, are kept by an edit beside them', () => {
  const fx = makeFixture('cycles-kept');
  const path = fx.write('driver.json', JSON.stringify({ landings: { engine: { form: 'merge', cycle: [] } }, workCycles: { engine: { quest: 7, chore: { end: 'done' } } } }));
  const choices = readDriverChoices(path);
  assert.deepEqual(choices.landings['engine']?.cycle, []);
  const { quest: _quest, ...kinds } = choices.workCycles['engine']!;
  writeDriverChoices(path, { ...landingEdited(choices, 'engine', null, { form: 'merge', tidy: true }), workCycles: { engine: kinds } });
  const written = JSON.parse(fx.read('driver.json')) as Record<string, unknown>;
  assert.deepEqual(written['landings'], { engine: { form: 'merge', tidy: true, cycle: [] } });
  assert.deepEqual(written['workCycles'], { engine: { chore: { end: 'done' } } });
});

test('graph-homes: a cycle and the work cycles are written back as read, and a file with neither gains neither', () => {
  const fx = makeFixture('cycles-written');
  const cycle = { nodes: [{ id: 'qa', kind: 'human', version: 1, reach: 'pressed' }], wires: [{ id: 'a', kind: 'after', from: 'line', to: 'qa' }], end: 'qa' };
  const quest = { nodes: [{ id: 'qa', kind: 'human', version: 1, reach: 'pressed' }], entries: ['qa'], end: 'qa' };
  const path = fx.write('driver.json', JSON.stringify({
    landings: { engine: { form: 'merge', tidy: true, cycle } },
    workCycles: { engine: { quest, branch: quest, chore: quest } },
    workspaceWorkCycles: { shop: { ask: quest } },
  }));

  writeDriverChoices(path, readDriverChoices(path));
  const written = JSON.parse(fx.read('driver.json')) as Record<string, unknown>;
  assert.deepEqual(written['landings'], { engine: { form: 'merge', tidy: true, cycle } });
  // Every kind is kept as written (the review's finding 2): only a kind with a work cycle governs, a branch's being its landing rule's
  // (§D7), and the others are said (`cycleStrays`).
  assert.deepEqual(written['workCycles'], { engine: { quest, branch: quest, chore: quest } });
  assert.deepEqual(written['workspaceWorkCycles'], { shop: { ask: quest } });

  const plain = fx.write('plain.json', JSON.stringify({ landings: { engine: { form: 'merge' } } }));
  writeDriverChoices(plain, readDriverChoices(plain));
  const text = fx.read('plain.json');
  assert.equal(text.includes('cycle'), false);
  assert.equal(text.includes('workCycles'), false);
  assert.equal(text.includes('workspaceWorkCycles'), false);
});

test('graph-homes: the kinds with a work cycle are the driver\'s', () => {
  const source = readFileSync(driverFile('NodeGraphs.cs'), 'utf8');
  const declared = /WorkCycleKinds \{ get; \} = \[([^\]]*)\]/.exec(source)?.[1];
  assert.equal(declared, WORK_CYCLE_KINDS.map((kind) => `WorkUnitKind.${kind[0]!.toUpperCase()}${kind.slice(1)}`).join(', '));
});
