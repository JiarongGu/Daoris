import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { makeFixture, type Fixture } from './_fixture.ts';

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
 * request. `FAKE_FAIL=<tool>` makes that one fail as a tool that is not signed in fails.
 */
function fakes(fx: Fixture): string {
  const bin = join(fx.root, 'bin');
  mkdirSync(bin, { recursive: true });
  writeFileSync(join(bin, 'fake.mjs'), [
    "import { appendFileSync } from 'node:fs';",
    'const [tool, ...args] = process.argv.slice(2);',
    "appendFileSync(process.env.FAKE_LOG, JSON.stringify({ tool, args, cwd: process.cwd() }) + '\\n');",
    "if (process.env.FAKE_FAIL === tool) { console.error(tool + ': not signed in, the fake says'); process.exit(1); }",
    "if (tool === 'gh') console.log('https://example.test/example-org/engine/pull/7');",
    "if (tool === 'az') console.log(JSON.stringify({ pullRequestId: 7, repository: { webUrl: 'https://example.test/example-org/project/_git/engine' } }));",
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

interface Spoken { initialized: Record<string, unknown>; answer: Record<string, unknown>; stderr: string }

/** Start the plugin as a landing does, speak its three frames, and hand back what it answered. */
async function land(plugin: string, fx: Fixture, bin: string, params: Record<string, unknown>, extra: Record<string, string> = {}): Promise<Spoken> {
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

  const answers = new Map<number, Record<string, unknown>>();
  const waiting = new Map<number, () => void>();
  createInterface({ input: child.stdout }).on('line', (line) => {
    const frame = JSON.parse(line) as { id: number; result: Record<string, unknown> };
    answers.set(frame.id, frame.result);
    waiting.get(frame.id)?.();
  });
  const ask = (id: number, method: string, frameParams: unknown) => new Promise<Record<string, unknown>>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`no answer to ${method} — stderr: ${stderr}`)), 30_000);
    waiting.set(id, () => { clearTimeout(timer); resolve(answers.get(id)!); });
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params: frameParams })}\n`);
  });

  const exited = new Promise<void>((resolve) => child.on('exit', () => resolve()));
  const initialized = await ask(1, 'initialize', { protocolVersion: 1, plugin, points: ['work/land'] });
  const answer = await ask(2, 'hook/work/land', params);
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'shutdown' })}\n`);
  await exited;
  return { initialized, answer, stderr };
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

test('each example plugin declares the landing point alone, so installing one runs nothing until a rule names it', () => {
  for (const plugin of ['github-pull-request', 'azure-devops-pull-request']) {
    const manifest = JSON.parse(readFileSync(join(examples, plugin, 'plugin.json'), 'utf8'));
    assert.equal(manifest.id, plugin);
    assert.deepEqual(manifest.hooks.points, ['work/land']);
    assert.ok(existsSync(join(examples, plugin, 'README.md')), `${plugin} has a README`);
  }
});
