import { test } from 'node:test';
import assert from 'node:assert/strict';
import { briefOf, partsOf } from './team-briefs.mjs';

const plan = {
  ask: 'a1',
  quests: { api: 'q1', page: 'q2' },
  plans: [{
    parts: [
      {
        id: 'api', repository: 'media', outcome: 'the config names the field', claims: ['src/api/**'], proof: ['api test passes'],
        pack: {
          pointers: [{ role: 'area', file: 'docs/areas/media.md', range: '10-30', blob: 'a' }, { role: 'change', file: 'src/api/Config.cs', range: '1-40', blob: 'b' }],
          tests: ['ConfigTests; filter ~Config'], words: ['field -> web/x.ts:3'], generated: ['src/gen/**'],
        },
      },
      { id: 'page', repository: 'media', outcome: 'the page shows it', claims: ['src/page/**'], proof: [] },
    ],
    edges: [{ part: 'page', on: 'api', kind: 'after', milestone: 'submitted', why: 'the page reads the field' }],
  }],
};

test('a part with a pack gets a brief in the dispatch skill\'s shape, with its siblings\' claims as what not to touch', () => {
  const brief = briefOf(plan, 'api', 'abc123');
  assert.match(brief, /^Task: api: the config names the field \(quest #q1\)\./);
  assert.match(brief, /Area: docs\/areas\/media\.md:10-30\./);
  assert.match(brief, /Change: src\/api\/Config\.cs:1-40; claims src\/api\/\*\*\./);
  assert.match(brief, /Do not touch: src\/page\/\*\*; records and generated files are the integrator's: src\/gen\/\*\*\./);
  assert.match(brief, /Start from: abc123\./);
});

test('a part with no pack says so and carries its edge as what it starts after', () => {
  const brief = briefOf(plan, 'page');
  assert.match(brief, /Start from: main, after it builds on `api` \(submitted\): the page reads the field\./);
  assert.match(brief, /Pack: none yet/);
});

test('a part the plan does not hold is refused, and every part is listed once', () => {
  assert.throws(() => briefOf(plan, 'ghost'), /no part `ghost`/);
  assert.deepEqual(partsOf(plan).map((row) => row.part.id), ['api', 'page']);
});
