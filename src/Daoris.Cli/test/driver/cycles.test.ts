import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { commandDriver, landingEdited, landingRuleUnread, readDriverChoices, writeDriverChoices } from '../../src/driver/driverconfig.ts';
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
    const said = cycleStrays(readDriverChoices(fx.write('driver.json', JSON.stringify(row.config))), landingRuleUnread);
    assert.deepEqual({ name: row.name, strays: said.map(({ at, code }) => ({ at, code })) }, { name: row.name, strays: row.strays });
    for (const stray of said) assert.ok(stray.words.includes(`\`${stray.at.at(-1)}\``), stray.words);
  }
});

test('graph-homes: `driver list` names a landing rule that does not read, and every value no unit reads (FABRIC-GRAPH1d)', () => {
  const fx = makeFixture('cycles-strays-list');
  const path = fx.write('driver.json', JSON.stringify({ landings: { engine: { tidy: true } }, workCycles: { engine: { chore: {} } } }));
  const { out } = run(['list'], path);
  assert.match(out, /\n {2}unread {5}`engine` in `landings` does not read as a landing rule, so work there lands as though it set none: it names no `form`, `merge` or `branch`\. It is kept as written, and nothing reads it\./);
  assert.match(out, /\n {2}unread {5}`chore` under `engine` in `workCycles` is no kind of work with a cycle of its own/);
  // Its words say why: the twin's sentence for a rule that could not land work.
  fx.write('driver.json', JSON.stringify({ workspaceLandings: { shop: { form: 'branch' } } }));
  assert.match(run(['list'], path).out, /`shop` in `workspaceLandings` does not read as a landing rule, so work there lands as though it set none: a branch pattern needs a name/);
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

/** `daoris driver …` against the file at `path`, its lines said; a refusal thrown as the CLI throws it. */
function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_DRIVER_CONFIG;
  process.env.DAORIS_DRIVER_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = commandDriver({ root: dirname(path), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_DRIVER_CONFIG;
    else process.env.DAORIS_DRIVER_CONFIG = saved;
  }
}

const QA = { nodes: [{ id: 'qa', kind: 'human', version: 1, reach: 'pressed' }], wires: [{ id: 'a', kind: 'after', from: 'line', to: 'qa' }], end: 'qa' };

test('graph-homes: `driver landing --cycle` sets a rule with its cycle, judged after its landing as the driver\'s door judges it', () => {
  const fx = makeFixture('cycles-landing-door');
  const path = fx.write('driver.json', '{}');
  fx.write('qa.json', JSON.stringify(QA));
  const said = run(['landing', 'engine', 'merge', '--cycle', 'qa.json'], path);
  assert.equal(said.code, 0);
  assert.match(said.out, /After the landing, its cycle: 1 node and 1 wire, ending at `qa`\./);
  assert.deepEqual(readDriverChoices(path).landings['engine'], { form: 'merge', cycle: QA });

  // A rule set again with no field for its cycle keeps it, judged under the rule as it now stands; `--no-cycle` takes it.
  const kept = run(['landing', 'engine', 'merge', '--tidy'], path);
  assert.match(kept.out, /Its cycle is kept: 1 node and 1 wire, ending at `qa`\./);
  assert.deepEqual(readDriverChoices(path).landings['engine'], { form: 'merge', tidy: true, cycle: QA });
  const cleared = run(['landing', 'engine', 'merge', '--no-cycle'], path);
  assert.match(cleared.out, /Its cycle is cleared: the work ends when the line holds it\./);
  assert.deepEqual(readDriverChoices(path).landings['engine'], { form: 'merge' });

  // A form that changed under a kept cycle keeps it, and says it does not hold (FABRIC-GRAPH1's choice 3).
  const branch ={ landings: { engine: { form: 'branch', pattern: 'feature/{quest}', cycle: { end: 'pull-request' } } } };
  fx.write('driver.json', JSON.stringify(branch));
  const merged = run(['landing', 'engine', 'merge'], path);
  assert.match(merged.out, /Its cycle is kept as written, and does not hold under this rule: the end `pull-request` names no node of this graph\./);
  assert.deepEqual(readDriverChoices(path).landings['engine'], { form: 'merge', cycle: { end: 'pull-request' } });

  // A person's words in an option are kept as written, whatever their letters: the driver reads the same text.
  const words = { ...QA, nodes: [{ id: 'qa', kind: 'human', version: 1, reach: 'pressed', options: { brief: 'Vérifier la livraison' } }] };
  fx.write('words.json', JSON.stringify(words));
  run(['landing', 'engine', 'merge', '--cycle', 'words.json'], path);
  assert.deepEqual(readDriverChoices(path).landings['engine'], { form: 'merge', cycle: words });
});

test('graph-homes: a cycle that does not hold is refused at the CLI\'s door with the driver\'s words, and nothing is written', () => {
  const fx = makeFixture('cycles-landing-refused');
  const path = fx.write('driver.json', JSON.stringify({ landings: { engine: { form: 'merge' } } }));
  const before = fx.read('driver.json');
  const refused = (cycle: unknown, words: RegExp, argv: string[] = ['landing', 'engine', 'merge']): void => {
    fx.write('cycle.json', typeof cycle === 'string' ? cycle : JSON.stringify(cycle));
    assert.throws(() => run([...argv, '--cycle', 'cycle.json'], path), (error: Error) => words.test(error.message) && /Nothing was written\./.test(error.message));
    assert.equal(fx.read('driver.json'), before);
  };

  // A loop: §E3's first refusal, said as the driver says it.
  refused({ nodes: [{ id: 'qa', kind: 'human', version: 1, reach: 'pressed' }, { id: 'qb', kind: 'human', version: 1, reach: 'pressed' }],
    wires: [{ id: 'a', kind: 'after', from: 'line', to: 'qa' }, { id: 'b', kind: 'after', from: 'qa', to: 'qb' }, { id: 'c', kind: 'after', from: 'qb', to: 'qa' }],
    end: 'qb' }, /nodes `qa`, `qb`: they wait on each other through `b`, `c`; a graph's wires never come back/);
  // A merge's landing holds no pull request to end at.
  refused({ end: 'pull-request' }, /the end `pull-request` names no node of this graph\./);
  // What is not a graph's shape, and what is not JSON.
  refused({ nodes: 3 }, /the graph's `nodes` is not a graph's shape: it holds no list there\./);
  refused('{ not json', /`cycle\.json` is not readable JSON\./);
  // A value this build would write into driver.json other than as read: a number's own text, a key that is a number out of order.
  refused('{"nodes":[{"id":"qa","kind":"human","version":1,"reach":"pressed","options":{"weight":1.0}}],'
    + '"wires":[{"id":"a","kind":"after","from":"line","to":"qa"}],"end":"qa"}', /holds a value this build would write differently than the driver reads it/);
  refused('{"nodes":[{"id":"qa","kind":"human","version":1,"reach":"pressed","options":{"order":{"2":"b","1":"a"}}}],'
    + '"wires":[{"id":"a","kind":"after","from":"line","to":"qa"}],"end":"qa"}', /a number by other text than JavaScript's, a key that is a number/);
  // A cleared rule has no cycle to set.
  fx.write('cycle.json', JSON.stringify(QA));
  assert.throws(() => run(['landing', 'engine', '--clear', '--cycle', 'cycle.json'], path), /`--clear` takes the rule and its cycle with it/);
  assert.equal(fx.read('driver.json'), before);
});

test('graph-homes: `driver work-cycle` sets a quest\'s or an ask\'s cycle, a whole graph judged as the driver\'s door judges it', () => {
  const fx = makeFixture('cycles-work-door');
  const path = fx.write('driver.json', JSON.stringify({ workCycles: { Engine: { ask: QUEST } } }));
  fx.write('quest.json', JSON.stringify(QUEST));
  const said = run(['work-cycle', 'engine', 'quest', 'quest.json'], path);
  assert.equal(said.code, 0);
  assert.match(said.out, /a quest in `engine` follows its cycle: 1 node and 0 wires, ending at `qa`/);
  // The owner is the one already written, in its case, its other kind kept.
  assert.deepEqual(readDriverChoices(path).workCycles, { Engine: { ask: QUEST, quest: QUEST } });

  run(['work-cycle', '--workspace', 'shop', 'ask', 'quest.json'], path);
  assert.deepEqual(readDriverChoices(path).workspaceWorkCycles, { shop: { ask: QUEST } });

  const cleared = run(['work-cycle', 'engine', 'ask', '--clear'], path);
  assert.match(cleared.out, /an ask in `engine` takes its workspace's cycle again, else none/);
  assert.deepEqual(readDriverChoices(path).workCycles, { Engine: { quest: QUEST } });

  const before = fx.read('driver.json');
  fx.write('broken.json', JSON.stringify({ ...QUEST, end: 'nowhere' }));
  assert.throws(() => run(['work-cycle', 'engine', 'quest', 'broken.json'], path),
    /the end `nowhere` names no node of this graph\. Nothing was written\./);
  assert.throws(() => run(['work-cycle', 'engine', 'branch', 'quest.json'], path), /a branch's cycle is its landing rule's `cycle`/);
  assert.throws(() => run(['work-cycle', 'engine', 'chore', 'quest.json'], path), /`chore` is no kind of work with a cycle of its own here — `quest` or `ask`\./);
  assert.equal(fx.read('driver.json'), before);
});

const QUEST = { nodes: [{ id: 'qa', kind: 'human', version: 1, reach: 'pressed' }], entries: ['qa'], end: 'qa' };

test('graph-homes: the kinds with a work cycle are the driver\'s', () => {
  const source = readFileSync(driverFile('NodeGraphs.cs'), 'utf8');
  const declared = /WorkCycleKinds \{ get; \} = \[([^\]]*)\]/.exec(source)?.[1];
  assert.equal(declared, WORK_CYCLE_KINDS.map((kind) => `WorkUnitKind.${kind[0]!.toUpperCase()}${kind.slice(1)}`).join(', '));
});
