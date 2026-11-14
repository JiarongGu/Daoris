import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { readFindings } from './ci-background.mjs';
import { settleInto } from './heavy-run.mjs';
import { mainStandsRed } from './merge/reached-classify.ts';
import { readHeavy } from './merge/heavy-after.ts';

const commit = '0123456789abcdef0123456789abcdef01234567';
const running = { id: 'heavy-0123456-1', state: 'running', commit, base: 'fedcba9', branches: ['feature-a'], at: '2026-11-14T10:00:00.000Z' };

test('a red heavy run files a finding naming the merge commit, which the next merge reads as main\'s red', () => {
  const dir = join('_fixtures', 'heavy-run-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'local'), { recursive: true });
  try {
    const outcome = { ran: 3, replayed: 0, flaked: [], mainRed: [], red: ['driver-process|Daoris.Desktop.Driver.Tests.BrokenProcessTests'], deferred: [] };
    const { run, lines } = settleInto(dir, running, outcome, 1, '2026-11-14T10:20:00.000Z');
    assert.equal(run.state, 'red');
    assert.match(lines[0], /heavy tier: RED 0123456/);
    const { findings } = readFindings(dir);
    assert.equal(findings.length, 1);
    assert.equal(findings[0].state, 'red');
    assert.equal(findings[0].gate, 'driver-process');
    assert.equal(findings[0].lastSeen.commit, commit);
    assert.equal(findings[0].firstSeen.kind, 'heavy');
    assert.equal(mainStandsRed({ suite: 'driver-process', unit: 'Daoris.Desktop.Driver.Tests.BrokenProcessTests' }, findings, {}), true);
    assert.equal(readHeavy(dir)[0].state, 'red');
    assert.match(readFileSync(join(dir, 'local', 'heavy-runs.jsonl'), 'utf8'), /"state":"red"/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a green heavy run files no finding and a run with no counts is an error that files none either', () => {
  const dir = join('_fixtures', 'heavy-run-green-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'local'), { recursive: true });
  try {
    settleInto(dir, running, { ran: 3, replayed: 1, flaked: [], mainRed: [], red: [], deferred: [] }, 0, '2026-11-14T10:20:00.000Z');
    assert.equal(readFindings(dir).findings.length, 0);
    assert.equal(readHeavy(dir)[0].state, 'green');
    const lost = settleInto(dir, { ...running, id: 'heavy-0123456-2' }, null, null, '2026-11-14T10:30:00.000Z');
    assert.equal(lost.run.state, 'error');
    assert.equal(readFindings(dir).findings.length, 0);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
