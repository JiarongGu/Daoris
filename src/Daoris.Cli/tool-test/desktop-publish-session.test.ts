import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, resolve } from 'node:path';
// @ts-expect-error — untyped workspace tooling; see desktop-tool.test.ts for why
import { SESSION_VARIABLE, sessionRefusal } from '../../../tools/desktop-publish.mjs';

const repo = resolve('repo-root');

test('a session\'s terminal is refused a stage or a publish into an install, and says the stage is the person\'s', () => {
  const said = sessionRefusal(resolve('install'), repo, { [SESSION_VARIABLE]: 's-1' });

  assert.match(said, /DAORIS_SESSION_ID/);
  assert.match(said, /drains the install/);
  assert.match(said, /person's/);
  assert.match(said, /Nothing was done/);
});

test('the person\'s terminal, and a rehearsal\'s install under the scratch folder, publish as before', () => {
  assert.equal(sessionRefusal(resolve('install'), repo, {}), null);
  assert.equal(sessionRefusal(join(repo, '_fixtures', 'rehearsal', 'install'), repo, { [SESSION_VARIABLE]: 's-1' }), null);
});
