import assert from 'node:assert/strict';
import { test } from 'node:test';
import { dayOf, render } from './look-stages-day.mjs';

const looked = (second, stages, source = 'desktop') => ({
  time: new Date(Date.UTC(2026, 10, 19, 7, 0, second)).toISOString(), source, level: 'info', event: 'watch.looked', data: { stages },
});

test('a day sums each stage\'s held and queued ms over its looks, the work beside the look apart', () => {
  const day = dayOf([
    looked(0, '~opinions=9000x2@#a ~opinions.queue=12000 owed=3000 owed.queue=4000'),
    looked(10, '~opinions=1000 owed=500 ~nodes=2000 nodes=10'),
    looked(20, 'owed=999', 'host'),
    { time: new Date().toISOString(), source: 'desktop', event: 'look.slow', data: { stage: 'owed' } },
  ]);

  assert.equal(day.looks, 2);
  assert.deepEqual(day.stages.map((each) => [each.stage, each.held, each.queued, each.looks, each.spans]), [
    ['~opinions', 10000, 12000, 2, 3],
    ['owed', 3500, 4000, 2, 2],
    ['~nodes', 2000, 0, 1, 1],
    ['nodes', 10, 0, 1, 1],
  ]);
});

test('the lines say each stage\'s seconds held and queued, only the stages asked for where some are', () => {
  const day = dayOf([looked(0, '~opinions=16953000 ~opinions.queue=28382000 owed=8529000 owed.queue=10666000')]);

  assert.equal(render(day, ['owed']), '1 looks\nowed  held 8,529 s / queued 10,666 s  (1 looks, 1 spans)');
  assert.match(render(day), /~opinions {2}held 16,953 s \/ queued 28,382 s/);
});
