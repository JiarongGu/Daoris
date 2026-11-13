import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { parseStages, readEvents, render, startsOf } from './start-load-bench.mjs';

const at = (second) => new Date(Date.UTC(2026, 10, 14, 7, 0, second)).toISOString();
const line = (second, source, event, data = {}) => ({ time: at(second), source, level: 'info', event, data });

test('a look line\'s stages read with their queue, their subjects and the work beside the look', () => {
  const stages = parseStages('withdraw=4600 sweep=1400 ~nodes=5300x2@ask#7 ~nodes.queue=8400 landing.queue=2000');
  assert.deepEqual(stages.map((stage) => [stage.name, stage.beside, stage.ms, stage.queue, stage.subjects]), [
    ['withdraw', false, 4600, 0, 1],
    ['sweep', false, 1400, 0, 1],
    ['nodes', true, 5300, 8400, 2],
    ['landing', false, 0, 2000, 1],
  ]);
  assert.deepEqual(parseStages(null), []);
});

test('each start is read to the next, with its stages, its first look, its slow stages and the host\'s slow requests', () => {
  const events = [
    line(0, 'desktop', 'app.started', { version: '1.2.3' }),
    line(5, 'host', 'request.failed', { method: 'GET', route: '/api/repositories', status: 200, ms: 25900 }),
    line(10, 'desktop', 'look.slow', { stage: 'nodes', beside: true, ms: 5000, queued: 3400 }),
    line(60, 'desktop', 'watch.looked', { by: 'start', ms: 60100, stages: 'withdraw=4600 ~nodes=5300 ~nodes.queue=8400' }),
    line(65, 'desktop', 'start.stages', { main: 1231, home: 1253, driver: 2570, pipe: 9690, host: 9114, shown: 3657, browser: 9262, page: 67656, splash: 19275, by: 'bound' }),
    line(300, 'desktop', 'app.started', {}),
    line(310, 'host', 'request.failed', { method: 'GET', route: '/api/quests', status: 200, ms: 3000 }),
  ];

  const [first, second] = startsOf(events);

  assert.equal(first.version, '1.2.3');
  assert.equal(first.stages.page, 67656);
  assert.equal(first.lifted, 'bound');
  assert.equal(first.look.ms, 60100);
  assert.equal(first.requests.length, 1);
  assert.equal(first.slow[0].queued, 3400);
  assert.equal(second.stages, null);
  assert.equal(second.look, null);
  assert.equal(second.requests.length, 1);
  const text = render(first);
  assert.match(text, /page 67\.7 s/);
  assert.match(text, /~nodes {2}5\.3 s {2}\(8\.4 s queued for a process start\)/);
  assert.match(text, /queued for process starts, over every stage of it: 8\.4 s/);
  assert.match(text, /slow request {2}GET \/api\/repositories 25\.9 s/);
});

test('a line after the first minute and a half is not the first load\'s', () => {
  const events = [line(0, 'desktop', 'app.started'), line(120, 'host', 'request.failed', { method: 'GET', route: '/x', status: 200, ms: 9000 })];

  assert.equal(startsOf(events)[0].requests.length, 0);
});

test('the logs folder is read as it stands: other sources merged in time order, an unreadable line skipped, a day chosen', () => {
  const folder = join(import.meta.dirname, '..', 'local', 'scratch', 'start-load-bench-test');
  rmSync(folder, { recursive: true, force: true });
  mkdirSync(folder, { recursive: true });
  try {
    writeFileSync(join(folder, '2026-11-14.desktop.jsonl'), `${JSON.stringify(line(0, 'desktop', 'app.started'))}\nnot json\n`);
    writeFileSync(join(folder, '2026-11-14.host.jsonl'), `${JSON.stringify(line(1, 'host', 'request.failed', { ms: 1 }))}\n`);
    writeFileSync(join(folder, '2026-11-13.desktop.jsonl'), `${JSON.stringify(line(-90000, 'desktop', 'app.started'))}\n`);

    assert.deepEqual(readEvents(folder, '2026-11-14').map((event) => event.event), ['app.started', 'request.failed']);
    assert.equal(readEvents(folder).length, 3);
    assert.throws(() => readEvents(join(folder, 'missing')), /no logs folder/);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});
