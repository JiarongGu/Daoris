import { test } from 'vitest';
import assert from 'node:assert/strict';
import { VERDICTS_KEPT_DAYS, VERDICTS_KEPT_PER_GATE, inUse, keptVerdicts } from './verdict-keep.ts';

const NOW = Date.parse('2026-11-18T12:00:00Z');
const daysAgo = (days: number, minute = 0): string => new Date(NOW - days * 86_400_000 + minute * 60_000).toISOString();
const verdict = (gate: string, tree: string, at: string, result = 'PASS', kind?: string) => ({ gate, verdict: result, tree, at, ...(kind ? { kind } : {}) });

test(`a verdict past ${VERDICTS_KEPT_DAYS} days goes unless it is its gate's newest or newest pass, of the scoped run's and of the rest; every newer one stays`, () => {
  const verdicts = [
    verdict('cli', 'old-pass', daysAgo(20)),
    verdict('cli', 'older-pass', daysAgo(30)),
    verdict('cli', 'old-fail', daysAgo(10), 'FAIL'),
    verdict('cli', 'recent', daysAgo(2), 'FAIL'),
    verdict('cli', 'recent-pass', daysAgo(1)),
    verdict('web', 'only', daysAgo(40), 'FAIL'),
    verdict('web', 'last-pass', daysAgo(50)),
    verdict('web', 'gone', daysAgo(60)),
    verdict('cli', 'scoped-old', daysAgo(25), 'PASS', 'scoped'),
    verdict('cli', 'scoped-older', daysAgo(26), 'PASS', 'scoped'),
  ];
  const kept = inUse(verdicts, NOW).map((entry) => entry.tree);
  assert.deepEqual(kept, ['recent', 'recent-pass', 'only', 'last-pass', 'scoped-old'],
    "cli's old verdicts go behind its recent pass; web keeps its newest verdict and its newest pass; the scoped run's newest stays apart");
  assert.deepEqual(inUse(verdicts.slice(3, 5), NOW), verdicts.slice(3, 5), 'within the age, every verdict stays');
  assert.deepEqual(inUse([], NOW), []);
});

test('of verdicts given at the same time, the later in the store is the newest, as the stage reads them', () => {
  const at = daysAgo(9);
  const kept = inUse([verdict('cli', 'first', at), verdict('cli', 'second', at)], NOW).map((entry) => entry.tree);
  assert.deepEqual(kept, ['second']);
});

test('the count rule is unchanged: each gate keeps its newest of the scoped run and of the rest', () => {
  const day = Array.from({ length: VERDICTS_KEPT_PER_GATE + 5 }, (_, i) => verdict('cli', `c${i}`, daysAgo(1, i), 'PASS', 'scoped'));
  const kept = keptVerdicts([verdict('cli', 'night', daysAgo(1), 'PASS', 'nightly'), ...day]);
  assert.equal(kept.filter((entry) => entry.kind === 'scoped').length, VERDICTS_KEPT_PER_GATE);
  assert.equal(kept[0]!.tree, 'night');
  assert.equal(kept.at(-1)!.tree, `c${VERDICTS_KEPT_PER_GATE + 4}`);
});
