// The browser way against the stand-ins of Azure DevOps and of Daoris's browser: taken only where no token is kept or Azure DevOps refuses
// the kept one; the same outputs and evidence as the token's, each saying which way it was made; a lapsed sign-in renewed once and then
// said; a tab of the plugin's own that neither the person's nor another session's can be mistaken for; and nothing of the page's
// credentials ever read.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { POINTS, RELEASE, STEP, TOKEN, WEB, checkout, folder, performed, signedIn, standIn, start, tip, unitAt } from './stand-in.mjs';
import { browserStandIn } from './browser-stand-in.mjs';

const ORIGIN = 'https://dev.azure.com/acme/Proj/_git/app';
const ASKED = { autoComplete: true, mergeStrategy: 'squash', deleteSourceBranch: true };
const MERGED = 'c'.repeat(40);

/** The stand-ins, and the environment a plugin is started with: the browser handed, and the kept token if one is. */
async function world(token, more = {}) {
  const api = await standIn();
  Object.assign(api, more);
  const browser = await browserStandIn(api);
  const env = { DAORIS_HOST_API: api.address, DAORIS_BROWSER: browser.address, ...(token ? signedIn(token) : {}) };
  return { api, browser, env, root: checkout(ORIGIN) };
}

const run = (t, env, root, options = ASKED) => performed(t, env, unitAt(root), { options });
const bare = (message) => message.replace(/ Way: .*$/, '');

test('a token Azure DevOps accepts is used as before: the browser is never connected to, and the evidence says the token', async (t) => {
  const { api, browser, env, root } = await world(TOKEN, { session: 'in' });
  const { step } = await run(t, env, root);

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /Way: the token\.$/);
  assert.equal(api.calls.every((call) => call.basic && !call.page), true);
  assert.equal(browser.calls.length, 0);
});

test('with no token kept it opens the pull request and sets auto-complete from a page of its own, the evidence says the browser, and the tab is closed', async (t) => {
  const { api, browser, env, root } = await world(null);
  const { step } = await run(t, env, root);

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.evidence.url, `${WEB}/pullrequest/1`);
  assert.equal(step.params.outputs['pull-request'].ref, 'azure-devops-pull-request/pull-request@1:1');
  assert.match(step.params.evidence.message, /Auto-complete set \(squash, source branch deleted\)/);
  assert.match(step.params.evidence.message, /Way: the browser\.$/);
  assert.ok(step.params.evidence.message.length <= 300);
  assert.deepEqual(api.calls.filter((call) => call.method !== 'GET').map((call) => [call.method, call.path.split('/').pop()]), [['POST', 'pullrequests'], ['PATCH', '1']]);
  assert.equal(api.calls.every((call) => call.page && !call.basic), true);
  assert.deepEqual(api.pulls[0].autoCompleteSetBy, { id: 'identity-1', displayName: 'Daoris' });
  assert.deepEqual(browser.open(), []);
});

test('a token Azure DevOps refuses falls back to the browser, and the evidence says so; the next call does not ask the refused token again', async (t) => {
  const { api, browser, env, root } = await world('not-the-token');
  const { step, plugin } = await run(t, env, root);

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /Way: the browser \(the kept token was refused\)\.$/);
  assert.equal(api.calls.filter((call) => !call.page).length, 1);
  const before = api.calls.length;
  await plugin.ask('hook/work/state', { root, branch: 'daoris/work', pullRequest: `${WEB}/pullrequest/1`, origin: null, line: 'main' });
  assert.equal(api.calls.slice(before).some((call) => !call.page), false);
  assert.ok(browser.navigations.length >= 1);
});

test('the browser way gives the token way\'s outputs and evidence fields, and the watch, state and release follow the pull request the same way', async (t) => {
  const viaToken = await world(TOKEN, { session: 'in' });
  const viaBrowser = await world(null);
  const asToken = await run(t, viaToken.env, viaToken.root);
  const asBrowser = await run(t, viaBrowser.env, viaBrowser.root);

  assert.deepEqual(asBrowser.step.params.outputs, asToken.step.params.outputs);
  assert.deepEqual(Object.keys(asBrowser.step.params.evidence), Object.keys(asToken.step.params.evidence));
  assert.equal(asBrowser.step.params.evidence.url, asToken.step.params.evidence.url);
  assert.equal(bare(asBrowser.step.params.evidence.message), bare(asToken.step.params.evidence.message));

  // Azure DevOps merges it; the state the monitor reads is the same by either way.
  const states = [];
  for (const { api, root, plugin } of [{ ...viaToken, plugin: asToken.plugin }, { ...viaBrowser, plugin: asBrowser.plugin }]) {
    Object.assign(api.pulls[0], { status: 'completed', closedDate: '2026-11-12T00:00:00Z', lastMergeCommit: { commitId: MERGED },
      lastMergeSourceCommit: { commitId: tip }, completionOptions: { mergeStrategy: 'squash' } });
    states.push((await plugin.ask('hook/work/state', { root, branch: 'daoris/work', pullRequest: null, origin: null, line: 'main' })).result);
  }
  const [token, browser] = states;
  assert.deepEqual([browser.state, browser.pullRequest, browser.mergeCommit, browser.sourceCommit, browser.target, browser.how],
    [token.state, token.pullRequest, token.mergeCommit, token.sourceCommit, token.target, token.how]);
  assert.deepEqual([browser.state, browser.mergeCommit, browser.how], ['completed', MERGED, 'squash']);
  assert.match(browser.message, /Way: the browser\.$/);
  assert.match(token.message, /Way: the token\.$/);

  // The release after the merge: a build of the merged commit on main, taken by no release definition, its pipeline run's stages watched.
  const release = async ({ api, env, root }) => {
    api.builds.push({ id: 41, buildNumber: '20261112.1', definition: { id: 7, name: 'app-ci' }, project: { id: 'project-1' }, repository: { id: 'repo-1' },
      sourceBranch: 'refs/heads/main', sourceVersion: MERGED, status: 'completed', result: 'succeeded',
      _links: { web: { href: 'https://dev.azure.com/acme/Proj/_build/results?buildId=41' } } });
    api.pipelines.push({ id: 7, name: 'app-ci' });
    api.timelines[41] = [{ id: 's1', type: 'Stage', name: 'Staging', state: 'completed', result: 'succeeded' }];
    const unit = unitAt(root, ['release'], { line: 'main', pullRequest: `${WEB}/pullrequest/1` });
    return (await performed(t, env, unit, { step: RELEASE, options: { everySeconds: 0.02, findSeconds: 0.4 } })).step.params;
  };
  const releasedByToken = await release(viaToken);
  const releasedByBrowser = await release(viaBrowser);
  assert.deepEqual([releasedByBrowser.state, releasedByBrowser.evidence.url], [releasedByToken.state, releasedByToken.evidence.url]);
  assert.equal(bare(releasedByBrowser.evidence.message), bare(releasedByToken.evidence.message));
  assert.match(releasedByBrowser.evidence.message, /Way: the browser\.$/);
  assert.deepEqual(viaBrowser.browser.open(), []);
});

test('the tools an agent calls say the way in the line and in their data', async (t) => {
  const { env } = await world(null);
  const plugin = start(t, env);
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });

  const made = (await plugin.ask('hook/command/run', { command: 'create-pull-request', run: 'a'.repeat(32), args: { origin: WEB, branch: 'daoris/s-1', title: 'Fix' },
    answers: {}, from: 'agent', place: 'machine', language: 'en' })).result;

  assert.equal(made.exit, 0, JSON.stringify(made));
  assert.equal(made.data.way, 'browser');
  assert.match(made.said.at(-1), /Way: the browser\./);
});

test('a sign-in answered 401 once, or by its sign-in page, is renewed by one navigation to the organization and the call is made again', async (t) => {
  for (const signInPage of [false, true]) {
    const { api, browser, env, root } = await world(null, { lapses: 1, signInPage });
    const { step } = await run(t, env, root, undefined);

    assert.equal(step.params.state, 'reached', JSON.stringify(step.params));
    assert.match(step.params.evidence.message, /Way: the browser\./);
    // The first navigation brings the page to the organization; the second is the one renewal.
    assert.equal(browser.navigations.length, 2);
    assert.equal(browser.navigations.every((each) => each.url.endsWith('/acme/_apis/connectionData?api-version=7.1-preview')), true);
    assert.equal(api.pulls.length, 1);
  }
});

test('a sign-in that lapses again after the renewal fails the step as signed-out, naming it, with no second renewal and no no-token', async (t) => {
  const { api, browser, env, root } = await world(null, { lapses: 2 });
  const { step } = await run(t, env, root, undefined);

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'signed-out']);
  assert.match(step.params.evidence.message, /sign-in in Daoris's browser has lapsed/);
  assert.equal(browser.navigations.length, 2);
  assert.equal(api.pulls.length, 0);
  assert.deepEqual(browser.open(), []);
});

test('it works in a tab of its own: the person\'s is never used, and another session driving the browser beside it moves nothing', async (t) => {
  const { api, browser, env, root } = await world(null);
  let elsewhere = null;
  api.onRead = () => { elsewhere ??= browser.elsewhere('https://example.test/'); };
  const { step } = await run(t, env, root);

  assert.equal(step.params.state, 'reached');
  assert.ok(elsewhere !== null);
  const used = new Set(browser.calls.map((call) => call.target).filter(Boolean));
  assert.equal(used.size, 1);
  assert.equal(used.has('person-1'), false);
  assert.equal(used.has(elsewhere), false);
  assert.equal(browser.targets.get('person-1').url, 'https://dev.azure.com/acme/Proj/_git/app/pullrequest/9');
  assert.equal(browser.targets.get(elsewhere).url, 'https://example.test/');
  assert.deepEqual(browser.calls.filter((call) => !call.target).map((call) => call.method), ['Target.createTarget', 'Target.attachToTarget', 'Target.closeTarget', ...[]].slice(0, 3));
});

test('it only calls the API from inside the page: it asks the browser for tabs, navigation and evaluation, never for a cookie, a storage or a header', async (t) => {
  const { browser, env, root } = await world(null);
  const { step, plugin } = await run(t, env, root);

  const methods = new Set(browser.calls.map((call) => call.method));
  assert.deepEqual([...methods].sort(), ['Page.enable', 'Page.navigate', 'Runtime.evaluate', 'Target.attachToTarget', 'Target.closeTarget', 'Target.createTarget']);
  for (const { expression } of browser.evaluated) assert.equal(/cookie|localStorage|sessionStorage|indexedDB|Authorization/i.test(expression.replace(/credentials: 'include'/g, '')), false);
  assert.equal(JSON.stringify(step).includes('stand-in-session'), false);
  assert.equal((await plugin.stop()).includes('stand-in-session'), false);

  const source = readdirSync(folder).filter((file) => file === 'browser.mjs')
    .map((file) => readFileSync(join(folder, file), 'utf8').split('\n').filter((line) => !line.trimStart().startsWith('//')).join('\n')).join('\n');
  assert.equal(/document\.cookie|localStorage|sessionStorage|getCookies|Storage\.|Network\.|Authorization|setCookie/.test(source), false);
});

test('with no token and no browser handed it fails as no-token, and a refused token as unauthorized, as it did', async (t) => {
  const api = await standIn();
  const root = checkout(ORIGIN);
  const absent = await run(t, { DAORIS_HOST_API: api.address }, root);
  const refused = await run(t, { DAORIS_HOST_API: api.address, ...signedIn('not-the-token') }, root);

  assert.deepEqual([absent.step.params.code, refused.step.params.code], ['no-token', 'unauthorized']);
  assert.equal(STEP, 'daoris-azure-devops/pull-request');
});
