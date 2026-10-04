import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { makeFixture, type Fixture } from './_fixture.ts';
import { readManifest, readNeeds } from '../src/plugins.ts';

/**
 * WSR4 (D100): the two example plugins that land work — one for GitHub, one for Azure DevOps — driven
 * over their wire exactly as a landing drives them: `initialize`, then one `hook/work/land`, then
 * `shutdown`.
 *
 * 🔴 NOTHING HERE REACHES A NETWORK. `origin` is a bare repository inside the fixture, and `gh` and `az`
 * are fakes first on the PATH the plugin is started with, which record what they were asked and answer
 * as the real tools answer. A test that could reach a real remote or a real platform is a defect.
 */

const examples = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'examples', 'plugins');
const BRANCH = 'feature/0fda18-fix-the-api-gap';

function git(cwd: string, ...args: string[]): string {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', env: { ...process.env, GIT_TERMINAL_PROMPT: '0' } });
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

/** A checkout with a branch beyond `main`, and an `origin` that is a bare repository beside it. */
function repository(fx: Fixture): { root: string; origin: string } {
  const origin = join(fx.root, 'origin.git');
  mkdirSync(origin, { recursive: true });
  git(origin, 'init', '--quiet', '--bare', '-b', 'main');

  const root = join(fx.root, 'engine');
  mkdirSync(root, { recursive: true });
  git(root, 'init', '--quiet', '-b', 'main');
  git(root, 'config', 'user.email', 'fixture@example.test');
  git(root, 'config', 'user.name', 'Fixture');
  writeFileSync(join(root, 'README.md'), '# engine\n');
  git(root, 'add', '-A');
  git(root, 'commit', '--quiet', '-m', 'first');
  git(root, 'remote', 'add', 'origin', origin);
  git(root, 'checkout', '--quiet', '-b', BRANCH);
  writeFileSync(join(root, 'work.txt'), 'the session\'s work\n');
  git(root, 'add', '-A');
  git(root, 'commit', '--quiet', '-m', 'the work');
  git(root, 'checkout', '--quiet', 'main');
  return { root, origin };
}

/**
 * `gh` and `az`, faked: each records its arguments and where it ran, and answers as the real one does —
 * `gh pr create` prints the pull request's address, `az repos pr create --output json` prints the pull
 * request, and reading one (`gh pr view … --json state`, `az repos pr show --id … --output json`) prints
 * its state, `FAKE_STATE` where it is set. `FAKE_FAIL=<tool>` makes that one fail as a tool that is not
 * signed in fails.
 */
function fakes(fx: Fixture): string {
  const bin = join(fx.root, 'bin');
  mkdirSync(bin, { recursive: true });
  writeFileSync(join(bin, 'fake.mjs'), [
    "import { appendFileSync } from 'node:fs';",
    'const [tool, ...args] = process.argv.slice(2);',
    "appendFileSync(process.env.FAKE_LOG, JSON.stringify({ tool, args, cwd: process.cwd() }) + '\\n');",
    "if (process.env.FAKE_FAIL === tool) { console.error(tool + ': not signed in, the fake says'); process.exit(1); }",
    "const web = 'https://example.test/example-org/project/_git/engine';",
    "if (tool === 'gh' && args[1] === 'view') console.log(JSON.stringify({ state: process.env.FAKE_STATE ?? 'OPEN' }));",
    "else if (tool === 'gh') console.log('https://example.test/example-org/engine/pull/7');",
    "if (tool === 'az' && args[2] === 'show') console.log(process.env.FAKE_PR ?? JSON.stringify({ pullRequestId: 7, status: process.env.FAKE_STATE ?? 'active', repository: { webUrl: web } }));",
    "else if (tool === 'az' && args[2] === 'list') console.log(process.env.FAKE_PRS ?? '[]');",
    "else if (tool === 'az') console.log(JSON.stringify({ pullRequestId: 7, repository: { webUrl: web } }));",
    '',
  ].join('\n'));
  for (const tool of ['gh', 'az']) {
    if (process.platform === 'win32') {
      writeFileSync(join(bin, `${tool}.cmd`), `@echo off\r\nnode "%~dp0fake.mjs" ${tool} %*\r\n`);
    } else {
      writeFileSync(join(bin, tool), `#!/bin/sh\nexec node "$(dirname "$0")/fake.mjs" ${tool} "$@"\n`);
      chmodSync(join(bin, tool), 0o755);
    }
  }
  return bin;
}

interface Spoken { initialized: Record<string, unknown>; answer: Record<string, unknown>; error: { message: string } | undefined; stderr: string }

/** Start the plugin as a landing does, speak its three frames, and hand back what it answered. */
function land(plugin: string, fx: Fixture, bin: string, params: Record<string, unknown>, extra: Record<string, string> = {}): Promise<Spoken> {
  return speak(plugin, fx, bin, 'work/land', ['work/land'], params, extra);
}

/** Start the plugin as an occasion's ask does (PLUGHOOK1a): the handshake naming both points, one `hook/work/state`, the shutdown. */
function ask(plugin: string, fx: Fixture, bin: string, params: Record<string, unknown>, extra: Record<string, string> = {}): Promise<Spoken> {
  return speak(plugin, fx, bin, 'work/state', ['work/land', 'work/state'], params, extra);
}

async function speak(
  plugin: string, fx: Fixture, bin: string, point: string, points: string[], params: Record<string, unknown>, extra: Record<string, string>,
): Promise<Spoken> {
  const pathKey = Object.keys(process.env).find((key) => key.toUpperCase() === 'PATH') ?? 'PATH';
  const env: Record<string, string | undefined> = {
    ...process.env,
    [pathKey]: `${bin}${delimiter}${process.env[pathKey] ?? ''}`,
    FAKE_LOG: join(fx.root, 'calls.jsonl'),
    DAORIS_PLUGIN_ID: plugin,
    DAORIS_PLUGIN_FOLDER: join(examples, plugin),
    DAORIS_PLUGIN_DATA: join(fx.root, 'data', plugin),
    GIT_TERMINAL_PROMPT: '0',
    ...extra,
  };
  const child = spawn(process.execPath, [join(examples, plugin, 'land.mjs')], { cwd: join(examples, plugin), env });
  let stderr = '';
  child.stderr.on('data', (chunk) => { stderr += String(chunk); });

  const answers = new Map<number, { result: Record<string, unknown>; error?: { message: string } }>();
  const waiting = new Map<number, () => void>();
  createInterface({ input: child.stdout }).on('line', (line) => {
    const frame = JSON.parse(line) as { id: number; result: Record<string, unknown>; error?: { message: string } };
    answers.set(frame.id, frame);
    waiting.get(frame.id)?.();
  });
  const call = (id: number, method: string, frameParams: unknown) => new Promise<{ result: Record<string, unknown>; error?: { message: string } }>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`no answer to ${method} — stderr: ${stderr}`)), 30_000);
    waiting.set(id, () => { clearTimeout(timer); resolve(answers.get(id)!); });
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params: frameParams })}\n`);
  });

  const exited = new Promise<void>((resolve) => child.on('exit', () => resolve()));
  const initialized = (await call(1, 'initialize', { protocolVersion: 1, plugin, points })).result;
  const answered = await call(2, `hook/${point}`, params);
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'shutdown' })}\n`);
  await exited;
  return { initialized, answer: answered.result, error: answered.error, stderr };
}

function frame(root: string, title = 'Fix the API gap'): Record<string, unknown> {
  return {
    repository: 'engine', workspace: 'aurora', root, branch: BRANCH, base: 'main', title,
    quest: { id: '0fda18', title }, session: 's1a2b3c4',
    commits: [{ sha: git(root, 'rev-parse', BRANCH), subject: 'the work' }],
  };
}

function calls(fx: Fixture): { tool: string; args: string[]; cwd: string }[] {
  const log = join(fx.root, 'calls.jsonl');
  return existsSync(log) ? readFileSync(log, 'utf8').trim().split('\n').map((line) => JSON.parse(line)) : [];
}

const same = (a: string, b: string) => {
  const [x, y] = [realpathSync(a), realpathSync(b)];
  return process.platform === 'win32' ? x.toLowerCase() === y.toLowerCase() : x === y;
};

test('the GitHub plugin pushes the branch to origin and opens the pull request with gh, and answers with its address', async () => {
  const fx = makeFixture('landing-plugin-github');
  const { root, origin } = repository(fx);
  const bin = fakes(fx);

  const spoken = await land('github-pull-request', fx, bin, frame(root));

  assert.deepEqual(spoken.initialized, { protocolVersion: 1, points: ['work/land'] });
  assert.equal(spoken.answer.pushed, true, String(spoken.answer.message));
  assert.equal(spoken.answer.pullRequest, 'https://example.test/example-org/engine/pull/7');
  assert.match(String(spoken.answer.message), /pushed `feature\/0fda18-fix-the-api-gap` to origin and opened a pull request into `main`/);
  // The push is the plugin's own, into the bare origin beside the checkout.
  assert.equal(git(origin, 'rev-parse', `refs/heads/${BRANCH}`), git(root, 'rev-parse', BRANCH));

  const [gh] = calls(fx);
  assert.equal(calls(fx).length, 1);
  assert.equal(gh!.tool, 'gh');
  assert.ok(same(gh!.cwd, root), `gh ran in ${gh!.cwd}`);
  const bodyFile = gh!.args[gh!.args.indexOf('--body-file') + 1]!;
  assert.deepEqual(gh!.args, ['pr', 'create', '--head', BRANCH, '--title', 'Fix the API gap', '--body-file', bodyFile, '--base', 'main']);
  const body = readFileSync(bodyFile, 'utf8');
  assert.match(body, /Quest `#0fda18`: Fix the API gap/);
  assert.match(body, /- the work \(/);
  fx.cleanup();
});

test('the Azure DevOps plugin pushes the branch and opens the pull request with az, and answers with its web address', async () => {
  const fx = makeFixture('landing-plugin-azure');
  const { root, origin } = repository(fx);
  const bin = fakes(fx);
  // A title cmd would read on Windows, where `az` is a cmd script: its quotes and percent signs are respelled there.
  const title = 'Cap the budget at 50% "now"';

  const spoken = await land('azure-devops-pull-request', fx, bin, frame(root, title));

  assert.equal(spoken.answer.pushed, true, String(spoken.answer.message));
  assert.equal(spoken.answer.pullRequest, 'https://example.test/example-org/project/_git/engine/pullrequest/7');
  assert.equal(git(origin, 'rev-parse', `refs/heads/${BRANCH}`), git(root, 'rev-parse', BRANCH));

  const [az] = calls(fx);
  assert.equal(az!.tool, 'az');
  assert.ok(same(az!.cwd, root), `az ran in ${az!.cwd}`);
  const said = process.platform === 'win32' ? "Cap the budget at 50 percent 'now'" : title;
  const described = az!.args.slice(az!.args.indexOf('--description') + 1);
  assert.deepEqual(az!.args.slice(0, az!.args.indexOf('--description')),
    ['repos', 'pr', 'create', '--source-branch', BRANCH, '--title', said, '--output', 'json', '--target-branch', 'main']);
  assert.ok(described.some((line) => line.includes('Quest #0fda18')), described.join(' | '));
  // A commit's line starts with a dash, which az would read as a flag, so it is set in by a space.
  assert.ok(described.some((line) => line.startsWith(' - the work (')), described.join(' | '));
  fx.cleanup();
});

test('a platform tool that fails leaves the push done and says so; a push that fails says it pushed nothing', async () => {
  const fx = makeFixture('landing-plugin-failing');
  const { root } = repository(fx);
  const bin = fakes(fx);

  const unopened = await land('github-pull-request', fx, bin, frame(root), { FAKE_FAIL: 'gh' });
  assert.equal(unopened.answer.pushed, true);
  assert.equal(unopened.answer.pullRequest, undefined);
  assert.match(String(unopened.answer.message), /gh did not open the pull request — gh: not signed in/);

  git(root, 'remote', 'set-url', 'origin', join(fx.root, 'no-such-origin.git'));
  const unpushed = await land('azure-devops-pull-request', fx, bin, frame(root));
  assert.equal(unpushed.answer.pushed, false);
  assert.match(String(unpushed.answer.message), /git push to origin failed/);
  // Nothing is opened for a branch that never reached the platform.
  assert.equal(calls(fx).filter((call) => call.tool === 'az').length, 0);
  fx.cleanup();
});

// ——— LAND2c (D149 point 4): a chain's branch moved on, and the pull request already open from it

const PULL = {
  'github-pull-request': 'https://example.test/example-org/engine/pull/7',
  'azure-devops-pull-request': 'https://example.test/example-org/project/_git/engine/pullrequest/7',
} as const;

/** The branch pushed once, as the chain's first landing pushed it, then moved on by a later step's commit, as Daoris moves it. */
function advanced(root: string): string {
  git(root, 'push', '--quiet', 'origin', BRANCH);
  git(root, 'checkout', '--quiet', BRANCH);
  writeFileSync(join(root, 'verify.txt'), 'the verify step\'s work\n');
  git(root, 'add', '-A');
  git(root, 'commit', '--quiet', '-m', 'the verify step');
  git(root, 'checkout', '--quiet', 'main');
  return git(root, 'rev-parse', BRANCH);
}

for (const plugin of ['github-pull-request', 'azure-devops-pull-request'] as const) {
  test(`at an advance the ${plugin} plugin pushes to the open pull request and opens no second one`, async () => {
    const fx = makeFixture(`landing-plugin-advance-${plugin}`);
    const { root, origin } = repository(fx);
    const bin = fakes(fx);
    const tip = advanced(root);

    const spoken = await land(plugin, fx, bin, { ...frame(root), pullRequest: PULL[plugin], acceptedBy: 'auto' });

    assert.equal(spoken.answer.pushed, true, String(spoken.answer.message));
    assert.equal(spoken.answer.pullRequest, PULL[plugin]);
    assert.match(String(spoken.answer.message), /its pull request carries the new commits/);
    assert.equal(git(origin, 'rev-parse', `refs/heads/${BRANCH}`), tip);
    // It read the pull request's state, and created nothing.
    const asked = calls(fx);
    assert.equal(asked.length, 1, JSON.stringify(asked));
    assert.deepEqual(asked[0]!.args, plugin === 'github-pull-request'
      ? ['pr', 'view', PULL[plugin], '--json', 'state']
      : ['repos', 'pr', 'show', '--id', '7', '--output', 'json']);
    fx.cleanup();
  });

  test(`at an advance the ${plugin} plugin pushes nothing to a pull request that is not open, or whose state it cannot read`, async () => {
    const fx = makeFixture(`landing-plugin-closed-${plugin}`);
    const { root, origin } = repository(fx);
    const bin = fakes(fx);
    const before = git(root, 'rev-parse', BRANCH);
    advanced(root);
    const told = { ...frame(root), pullRequest: PULL[plugin], acceptedBy: 'person' };

    const completed = await land(plugin, fx, bin, told, { FAKE_STATE: plugin === 'github-pull-request' ? 'MERGED' : 'completed' });
    assert.equal(completed.answer.pushed, false);
    assert.match(String(completed.answer.message), /is (merged|completed), so nothing was pushed: commits pushed now would ride no pull request/);

    const unread = await land(plugin, fx, bin, told, { FAKE_FAIL: plugin === 'github-pull-request' ? 'gh' : 'az' });
    assert.equal(unread.answer.pushed, false);
    assert.match(String(unread.answer.message), /could not read the state of its pull request .* nothing was pushed/);

    // The remote still holds what the first landing pushed, and no pull request was opened.
    assert.equal(git(origin, 'rev-parse', `refs/heads/${BRANCH}`), before);
    assert.equal(calls(fx).filter((call) => call.args.includes('create')).length, 0);
    fx.cleanup();
  });
}

test('the description each plugin writes says who accepted the work', async () => {
  const fx = makeFixture('landing-plugin-accepted');
  const { root } = repository(fx);
  const bin = fakes(fx);

  await land('github-pull-request', fx, bin, { ...frame(root), acceptedBy: 'auto' });
  const [gh] = calls(fx);
  const body = readFileSync(gh!.args[gh!.args.indexOf('--body-file') + 1]!, 'utf8');
  assert.match(body, /accepted automatically when its quest was done/);
  assert.doesNotMatch(body, /the person who reviewed it/);

  await land('azure-devops-pull-request', fx, bin, { ...frame(root), acceptedBy: 'person' });
  const az = calls(fx).find((call) => call.tool === 'az')!;
  const described = az.args.slice(az.args.indexOf('--description') + 1).join('\n');
  assert.match(described, /accepted by the person who reviewed it/);
  fx.cleanup();
});

test('each example plugin declares only points the loop never asks, so installing one runs nothing until a rule names it or a look asks', () => {
  // PLUGHOOK1a: the Azure DevOps plugin also answers the query; the GitHub one learns it in PLUGHOOK1b.
  const points = { 'github-pull-request': ['work/land'], 'azure-devops-pull-request': ['work/land', 'work/state'] };
  for (const [plugin, declared] of Object.entries(points)) {
    const manifest = JSON.parse(readFileSync(join(examples, plugin, 'plugin.json'), 'utf8'));
    assert.equal(manifest.id, plugin);
    assert.deepEqual(manifest.hooks.points, declared);
    assert.ok(existsSync(join(examples, plugin, 'README.md')), `${plugin} has a README`);
  }
});

// ——— PLUGTOOL1b (D150 point 7, the UX6 design §7.4): each plugin declares the tools its process runs

/**
 * What each plugin declares: Node.js for its `land.mjs`, Git for its push, and its platform's CLI with the checks a person
 * puts right with its `fix`. Each floor is the oldest release that has everything the plugin runs with that tool, the
 * version question Daoris asks included, and its README's *Why each version* says where each was read.
 */
const DECLARED: Record<string, { id: string; kind: string; versions: string; checks: [string, string, string][] }[]> = {
  'github-pull-request': [
    { id: 'node', kind: 'own', versions: '>=16.6.0', checks: [] },
    { id: 'git', kind: 'own', versions: '>=1.7.0', checks: [] },
    { id: 'gh', kind: 'known', versions: '>=1.9.0', checks: [['gh auth status', 'Signed in', 'gh auth login']] },
  ],
  'azure-devops-pull-request': [
    { id: 'node', kind: 'own', versions: '>=16.6.0', checks: [] },
    { id: 'git', kind: 'own', versions: '>=1.7.0', checks: [] },
    {
      id: 'az', kind: 'known', versions: '>=2.0.79', checks: [
        ['az extension show --name azure-devops --output none', 'Its devops extension is added', 'az extension add --name azure-devops'],
        ['az account show --output none', 'Signed in', 'az login'],
      ],
    },
  ],
};

for (const [plugin, declared] of Object.entries(DECLARED)) {
  test(`the ${plugin} plugin declares Node.js, Git and its platform's CLI, each with its floor, read with no problem`, () => {
    const { manifest, problem } = readManifest(plugin, join(examples, plugin), true);
    assert.equal(problem, null);
    assert.equal(manifest.toolsProblem, null);
    assert.deepEqual(manifest.tools.map((tool) => tool.problem), declared.map(() => null));
    assert.deepEqual(
      manifest.tools.map((tool) => ({ id: tool.id, kind: tool.kind, versions: tool.versions, checks: tool.ready.map((check) => [check.run.join(' '), check.says, check.fix]) })),
      declared,
    );
    for (const tool of manifest.tools) {
      assert.ok(tool.for, `${tool.id} says why the plugin runs it`);
      // A check runs its own tool, so it is found as that tool is.
      for (const check of tool.ready) assert.equal(check.run[0], tool.id);
    }
  });

  test(`the ${plugin} plugin declares exactly the programs it starts`, () => {
    const { manifest } = readManifest(plugin, join(examples, plugin), true);
    const source = readFileSync(join(examples, plugin, 'land.mjs'), 'utf8');
    const started = new Set([manifest.hooks!.command[0]!, ...[...source.matchAll(/\brun\('([^']+)'/g)].map((found) => found[1]!)]);
    assert.deepEqual([...started].sort(), manifest.tools.map((tool) => tool.id).sort());
  });

  test(`the ${plugin} plugin's README points at its manifest for what it needs, and says where each floor was read`, () => {
    const folder = join(examples, plugin);
    const { manifest } = readManifest(plugin, folder, true);
    assert.ok(readNeeds(folder).some((need) => need.includes('`plugin.json`') && need.includes('`tools`')), readNeeds(folder).join(' | '));
    // No floor is written twice: the manifest holds each, and the README's needs name none.
    for (const tool of manifest.tools) {
      const floor = tool.versions!.slice(2);
      assert.ok(!readNeeds(folder).some((need) => need.includes(floor)), `${tool.id}'s floor ${floor} is in the needs`);
    }

    const readme = readFileSync(join(folder, 'README.md'), 'utf8').replace(/\r\n/g, '\n');
    const start = readme.indexOf('\n## Why each version\n');
    assert.ok(start >= 0, 'the README has its Why each version section');
    const why = readme.slice(start + 1).split(/\n## /)[0]!;
    for (const tool of manifest.tools) {
      assert.match(why, new RegExp(`^- \\*\\*${tool.name!.replace('.', '\\.')} ${tool.versions!.slice(2).replace(/\./g, '\\.')}\\*\\*`, 'm'), `${tool.id}'s floor is cited`);
    }
  });
}

// ——— PLUGHOOK1a (D148 point 7, the plugin hooks design §2.6): the Azure DevOps plugin answers `work/state`

const AZ_WEB = 'https://example.test/example-org/project/_git/engine';
const MERGE = 'a'.repeat(40);
const SOURCE = 'b'.repeat(40);

/** A pull request as `az repos pr show --output json` prints one: the fields the plugin reads, and a few it does not. */
function pr(id: number, status: string, more: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    pullRequestId: id, status, sourceRefName: `refs/heads/${BRANCH}`, targetRefName: 'refs/heads/main',
    creationDate: `2026-10-0${id % 9}T09:00:00Z`, repository: { webUrl: AZ_WEB, name: 'engine' }, ...more,
  };
}

function stateFrame(root: string, pullRequest: string | null = `${AZ_WEB}/pullrequest/7`): Record<string, unknown> {
  return { repository: 'engine', workspace: 'aurora', root, branch: BRANCH, line: 'main', pullRequest, pushedTip: SOURCE };
}

test('the Azure DevOps plugin answers a pull request completed by squash with both commits, its target, how and when', async () => {
  const fx = makeFixture('state-azure-squash');
  const { root } = repository(fx);
  const bin = fakes(fx);
  const completed = pr(7, 'completed', {
    lastMergeCommit: { commitId: MERGE }, lastMergeSourceCommit: { commitId: SOURCE }, closedDate: '2026-10-04T14:02:11.513Z',
    completionOptions: { mergeStrategy: 'squash', deleteSourceBranch: true },
  });

  const spoken = await ask('azure-devops-pull-request', fx, bin, stateFrame(root), { FAKE_PR: JSON.stringify(completed) });

  assert.deepEqual(spoken.initialized, { protocolVersion: 1, points: ['work/land', 'work/state'] });
  assert.equal(spoken.error, undefined, spoken.stderr);
  assert.deepEqual({ ...spoken.answer, message: undefined }, {
    state: 'completed', pullRequest: `${AZ_WEB}/pullrequest/7`, mergeCommit: MERGE, sourceCommit: SOURCE, target: 'main',
    how: 'squash', at: '2026-10-04T14:02:11.513Z', message: undefined,
  });
  assert.match(String(spoken.answer.message), /completed by squash into `main`/);
  // It read the one pull request the address names, in the checkout, and nothing else.
  const asked = calls(fx);
  assert.equal(asked.length, 1, JSON.stringify(asked));
  assert.deepEqual(asked[0]!.args, ['repos', 'pr', 'show', '--id', '7', '--output', 'json']);
  assert.ok(same(asked[0]!.cwd, root), `az ran in ${asked[0]!.cwd}`);
  fx.cleanup();
});

test('the Azure DevOps plugin maps each status and merge strategy, and answers a completed one with no merge commit as unknown', async () => {
  const fx = makeFixture('state-azure-statuses');
  const { root } = repository(fx);
  const bin = fakes(fx);
  const asked = async (shown: Record<string, unknown>) =>
    (await ask('azure-devops-pull-request', fx, bin, stateFrame(root), { FAKE_PR: JSON.stringify(shown) })).answer;

  assert.equal((await asked(pr(7, 'active'))).state, 'open');
  const abandoned = await asked(pr(7, 'abandoned', { closedDate: '2026-10-03T10:00:00Z' }));
  assert.deepEqual([abandoned.state, abandoned.at, abandoned.mergeCommit], ['abandoned', '2026-10-03T10:00:00Z', null]);
  assert.equal((await asked(pr(7, 'notSet'))).state, 'unknown');
  for (const [strategy, how] of [['noFastForward', 'merge'], ['rebase', 'rebase'], ['rebaseMerge', 'rebase-merge'], [undefined, null]] as const) {
    const answer = await asked(pr(7, 'completed', {
      lastMergeCommit: { commitId: MERGE }, lastMergeSourceCommit: { commitId: SOURCE },
      ...(strategy ? { completionOptions: { mergeStrategy: strategy } } : {}),
    }));
    assert.equal(answer.how, how, `${strategy}`);
  }
  const noMerge = await asked(pr(7, 'completed', { lastMergeSourceCommit: { commitId: SOURCE } }));
  assert.equal(noMerge.state, 'unknown');
  assert.match(String(noMerge.message), /completed, and az names no merge commit/);
  fx.cleanup();
});

test('with no address the Azure DevOps plugin finds the pull request by its source branch, preferring a completed one into the line', async () => {
  const fx = makeFixture('state-azure-by-branch');
  const { root } = repository(fx);
  const bin = fakes(fx);
  const found = [
    pr(3, 'abandoned', { closedDate: '2026-10-01T09:00:00Z' }),
    pr(4, 'completed', { targetRefName: 'refs/heads/release' }),
    pr(5, 'active'),
    pr(6, 'completed'),
  ];
  const completed = pr(6, 'completed', { lastMergeCommit: { commitId: MERGE }, lastMergeSourceCommit: { commitId: SOURCE } });

  const spoken = await ask('azure-devops-pull-request', fx, bin, stateFrame(root, null),
    { FAKE_PRS: JSON.stringify(found), FAKE_PR: JSON.stringify(completed) });

  assert.equal(spoken.answer.state, 'completed', String(spoken.answer.message));
  assert.equal(spoken.answer.pullRequest, `${AZ_WEB}/pullrequest/6`);
  assert.deepEqual(calls(fx).map((each) => each.args), [
    ['repos', 'pr', 'list', '--source-branch', BRANCH, '--status', 'all', '--output', 'json'],
    ['repos', 'pr', 'show', '--id', '6', '--output', 'json'],
  ]);
  fx.cleanup();
});

test('the Azure DevOps plugin answers unknown where no pull request is from the branch, and az failing is the call\'s error', async () => {
  const fx = makeFixture('state-azure-none');
  const { root } = repository(fx);
  const bin = fakes(fx);

  const none = await ask('azure-devops-pull-request', fx, bin, stateFrame(root, null), { FAKE_PRS: '[]' });
  assert.equal(none.answer.state, 'unknown');
  assert.equal(none.answer.pullRequest, null);
  assert.match(String(none.answer.message), /no pull request from `feature\/0fda18-fix-the-api-gap`/);
  // Nothing was read once nothing was found.
  assert.equal(calls(fx).filter((call) => call.args.includes('show')).length, 0);

  const failed = await ask('azure-devops-pull-request', fx, bin, stateFrame(root), { FAKE_FAIL: 'az' });
  assert.equal(failed.answer, undefined);
  assert.match(String(failed.error?.message), /az: not signed in, the fake says/);
  fx.cleanup();
});
