import { test } from 'node:test';
import assert from 'node:assert/strict';
import { scratchFolder } from '../../../tools/merge/fixture-home.ts';
import { readText } from '../src/platform/fsx.ts';
import { homeEntry } from '../src/platform/home.ts';
// @ts-expect-error — untyped workspace tooling, tested for its behaviour
import { askStop } from '../../../tools/desktop/stop-ask.mjs';

/** The desktop's kill and restart say who closes the window, in the file the application reads as it stops (`StopAsk`). */
test('a stop asked of the shell by the tool leaves its ask in the home, for the pids it closes', () => {
  const root = scratchFolder('daoris-stop-ask');
  assert.equal(askStop(root, [4242], { DAORIS_SESSION_ID: '76cdd5db' }), true);

  const asked = JSON.parse(readText(homeEntry(root, 'stop-ask')));
  assert.deepEqual(asked.pids, [4242]);
  assert.equal(asked.by, 'desktop-tool');
  assert.equal(asked.session, '76cdd5db');
  assert.ok(Math.abs(Date.now() - Date.parse(asked.at)) < 60_000);

  // No home, or no process, leaves nothing; an id that is no session's is not carried.
  assert.equal(askStop(undefined, [4242], {}), false);
  assert.equal(askStop(root, [], {}), false);
  askStop(root, [7], { DAORIS_SESSION_ID: '../x' });
  assert.equal('session' in JSON.parse(readText(homeEntry(root, 'stop-ask'))), false);
});
