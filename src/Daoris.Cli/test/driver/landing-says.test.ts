import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { SQUASH_SAYS, commitModeSays, helperModeSays, ticketSays } from '../../src/driver/driverconfig.ts';

/**
 * The sentences `driver landing`, `helpers` and `commits` say as a rule is set (TWINWORDS1), held to
 * `fixtures/landing-says.json`, which the driver's `LandingSaysTwinTests` reads too: Ask Daoris's cards say them from the
 * driver's code, so a change to either side's words fails the other's test.
 */
const here = dirname(fileURLToPath(import.meta.url));
const TABLE = JSON.parse(readFileSync(join(here, '..', 'fixtures', 'landing-says.json'), 'utf8')) as {
  ticket: { key: string | null; says: string }[];
  squash: string;
  helpers: { mode: string; says: string }[];
  commits: { mode: string; says: string }[];
};

test('the ticket sentence is the table\'s, row for row', () => {
  assert.equal(TABLE.ticket.length, 2);
  for (const row of TABLE.ticket) assert.equal(ticketSays(row.key ?? undefined), row.says);
});

test('the squash sentence is the table\'s', () => {
  assert.equal(SQUASH_SAYS.join(' '), TABLE.squash);
});

test('the helpers and commits mode sentences are the table\'s, row for row', () => {
  assert.deepEqual(TABLE.helpers.map((row) => row.mode), ['ask', 'on', 'off']);
  for (const row of TABLE.helpers) assert.equal(helperModeSays(row.mode), row.says);
  assert.deepEqual(TABLE.commits.map((row) => row.mode), ['as-you-go', 'ask', 'at-end']);
  for (const row of TABLE.commits) assert.equal(commitModeSays(row.mode), row.says);
});
