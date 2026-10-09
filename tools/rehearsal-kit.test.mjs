/**
 * What a rehearsal keeps of a failed check (DEV3b): the print it read and the files it names, under a folder of the run's
 * own named for the check, so a rerun keeps the sighting it is rerun after. And how a phase waits on a session and restarts
 * the host it talks to (DEV3d): by the clock, and only between the session's requests.
 *
 *   node --test tools/rehearsal-kit.test.mjs
 *
 * `npm run verify` runs it beside the tooling's other tests. The cases write into a gitignored folder of this repository.
 */
import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { setTimeout as sleep } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import {
  ACP_STUB_AGENT, checkFolderName, evidenceFolder, keepEvidence, makeChecker, restartBetweenRequests, waitFor,
} from './rehearsal-kit.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'rehearsal-kit-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const LOSING = '…and when b reaches the remote again, its driver stops its own losing session, with the reason on the record';

const folder = (name) => {
  const at = join(scratch, name);
  rmSync(at, { recursive: true, force: true });
  mkdirSync(at, { recursive: true });
  return at;
};

/** What `fn` prints through `console.log`, which the checker writes its lines with. */
function printed(fn) {
  const lines = [];
  const log = console.log;
  console.log = (...args) => lines.push(args.join(' '));
  try {
    fn();
  } finally {
    console.log = log;
  }
  return lines;
}

test('a check is named by its words, lower case and hyphened, cut at a word within sixty characters', () => {
  assert.equal(checkFolderName(LOSING), 'and-when-b-reaches-the-remote-again-its-driver-stops-its-own');
  assert.equal(checkFolderName('`daoris-driver sync` exits 2 — naming the wall'), 'daoris-driver-sync-exits-2-naming-the-wall');
  assert.equal(checkFolderName('中文 only'), 'only');
  assert.equal(checkFolderName('……'), 'check');
  assert.ok(checkFolderName('a'.repeat(90)).length <= 60);
});

test("a run's folder sits beside the transcripts and is named for the rehearsal and the moment it started", () => {
  assert.equal(
    evidenceFolder('/repo', 'family', new Date('2026-10-07T09:04:27.824Z')),
    join('/repo', '_fixtures', 'rehearsal-logs', 'family-2026-10-07T09-04-27-824Z'));
});

test('a failed check keeps its print and a copy of each file it names, under a folder named for it', () => {
  const at = folder('keeps');
  const transcript = join(at, 'sessions', 'b3f0f72d.log');
  mkdirSync(dirname(transcript), { recursive: true });
  writeFileSync(transcript, 'stub: lingering\n');
  const run = join(at, 'family-run');

  const kept = keepEvidence(run, LOSING, { print: 'driver: syncing each tick\n  sync  default: …', files: [transcript] });

  assert.equal(kept, join(run, checkFolderName(LOSING)));
  assert.equal(readFileSync(join(kept, 'print.txt'), 'utf8'), 'driver: syncing each tick\n  sync  default: …');
  assert.equal(readFileSync(join(kept, 'b3f0f72d.log'), 'utf8'), 'stub: lingering\n');
  assert.ok(existsSync(transcript), 'the file it read is copied, never moved');
});

test("two files of one name, each home's log, are both kept, the second named for the folders above it", () => {
  const at = folder('same-name');
  const logs = ['home', 'home-b'].map((home) => {
    const log = join(at, home, 'logs', '2026-10-07.driver.jsonl');
    mkdirSync(dirname(log), { recursive: true });
    writeFileSync(log, `${home}\n`);
    return log;
  });

  const kept = keepEvidence(join(at, 'run'), 'a check', { files: [...logs, logs[1]] });

  assert.equal(readFileSync(join(kept, '2026-10-07.driver.jsonl'), 'utf8'), 'home\n');
  assert.equal(readFileSync(join(kept, 'home-b-logs-2026-10-07.driver.jsonl'), 'utf8'), 'home-b\n');
  assert.equal(readFileSync(join(kept, 'home-b-logs-2-2026-10-07.driver.jsonl'), 'utf8'), 'home-b\n');
});

test('a file the check names that is not there is said, and nothing fails', () => {
  const run = folder('missing');
  const gone = join(run, 'never-written.log');

  const kept = keepEvidence(run, 'a check', { print: 'out', files: [gone, undefined, ''] });

  assert.deepEqual(readdirSync(kept).sort(), ['missing.txt', 'print.txt']);
  assert.equal(readFileSync(join(kept, 'missing.txt'), 'utf8'), `${gone}\n`);
});

test('a second failure of the same check in one run keeps both', () => {
  const run = folder('twice');

  const first = keepEvidence(run, 'the same check', { print: 'first' });
  const second = keepEvidence(run, 'the same check', { print: 'second' });

  assert.notEqual(first, second);
  assert.equal(readFileSync(join(first, 'print.txt'), 'utf8'), 'first');
  assert.equal(readFileSync(join(second, 'print.txt'), 'utf8'), 'second');
});

test("a rerun keeps the failed run's evidence: each run writes a folder of its own", () => {
  const root = folder('reruns');
  const failed = makeChecker({ keep: evidenceFolder(root, 'family', new Date('2026-10-07T09:04:27.824Z')) });
  const rerun = makeChecker({ keep: evidenceFolder(root, 'family', new Date('2026-10-07T09:14:31.000Z')) });

  printed(() => failed.check(LOSING, false, 'detail', { print: 'the failed run' }));
  printed(() => rerun.check(LOSING, false, 'detail', { print: 'the rerun' }));

  const runs = readdirSync(join(root, '_fixtures', 'rehearsal-logs')).sort();
  assert.deepEqual(runs, ['family-2026-10-07T09-04-27-824Z', 'family-2026-10-07T09-14-31-000Z']);
  assert.equal(
    readFileSync(join(root, '_fixtures', 'rehearsal-logs', runs[0], checkFolderName(LOSING), 'print.txt'), 'utf8'),
    'the failed run');
});

test('the FAIL line names where its evidence was kept, and the evidence is asked for only on a failure', () => {
  const keep = folder('checker');
  const { check, totals } = makeChecker({ keep });
  let asked = 0;
  const evidence = () => {
    asked += 1;
    return { print: 'what the run printed' };
  };

  const passed = printed(() => check('a check that holds', true, 'detail', evidence));
  const failed = printed(() => check('a check that does not', false, 'detail', evidence));

  assert.deepEqual(passed, ['  ok    a check that holds']);
  assert.equal(asked, 1);
  assert.equal(totals.failures, 1);
  const kept = join(keep, checkFolderName('a check that does not'));
  assert.deepEqual(failed, ['  FAIL  a check that does not\n          detail', `          kept: ${kept}`]);
  assert.equal(readFileSync(join(kept, 'print.txt'), 'utf8'), 'what the run printed');
});

test('evidence that cannot be kept is said on the FAIL line, and the check fails once, as it would have', () => {
  const at = folder('unwritable');
  const notAFolder = join(at, 'a-file');
  writeFileSync(notAFolder, 'a file where the run folder would go');
  const { check, totals } = makeChecker({ keep: notAFolder });

  const failed = printed(() => check('a check', false, '', { print: 'out' }));

  assert.equal(totals.failures, 1);
  assert.equal(failed.length, 2);
  assert.match(failed[1], /^ {10}kept: nothing, /);
});

test('a check with no evidence, or a checker with nowhere to keep it, keeps nothing and prints as it did', () => {
  const keep = folder('nothing');
  const bare = makeChecker();
  const keeping = makeChecker({ keep });

  const unkept = printed(() => bare.check('no folder', false, 'detail', { print: 'out' }));
  const noEvidence = printed(() => keeping.check('no evidence', false, 'detail'));

  assert.deepEqual(unkept, ['  FAIL  no folder\n          detail']);
  assert.deepEqual(noEvidence, ['  FAIL  no evidence\n          detail']);
  assert.deepEqual(readdirSync(keep), []);
});

// DEV3d: the family rehearsal's lost-claim phase restarted host b with the lingering stub's take still in flight. Its wait
// for the stub's lingering was 160 asks, which ran out before a session that started 41 s into its run under load had said
// it, and the phase went on to the restart; the stub crashed on the reset connection before the driver could stop it.

test('a wait answers what its ask found once it is ready, asking until then', async () => {
  let asks = 0;
  const waited = await waitFor(() => {
    asks += 1;
    return asks === 3 ? 'stub: lingering' : null;
  }, { within: 5_000, every: 5 });

  assert.equal(waited.ended, 'ready');
  assert.equal(waited.value, 'stub: lingering');
  assert.equal(asks, 3);
});

test('a wait is bounded by the clock, however long each ask takes, and says it ran out', async () => {
  const began = Date.now();
  const waited = await waitFor(async () => {
    await sleep(40);
    return false;
  }, { within: 200, every: 5 });

  assert.equal(waited.ended, 'timeout');
  assert.ok(waited.ms >= 200, `it ran out after ${waited.ms} ms, before its bound`);
  assert.ok(Date.now() - began < 1_000, `it ran out after ${Date.now() - began} ms, long past its bound`);
});

test('an ask that throws, as a host does while it restarts, is not ready, and the wait goes on', async () => {
  let asks = 0;
  const waited = await waitFor(() => {
    asks += 1;
    if (asks < 3) throw new Error('connect ECONNREFUSED');
    return true;
  }, { within: 5_000, every: 5 });

  assert.equal(waited.ended, 'ready');
  assert.equal(asks, 3);
});

test('a wait ends when what it waits beside settles first: a run that has ended will never say it', async () => {
  const run = sleep(30).then(() => ({ code: 0 }));
  const waited = await waitFor(() => false, { within: 10_000, every: 5, settled: run });

  assert.equal(waited.ended, 'settled');
  assert.ok(waited.ms < 5_000, `it waited ${waited.ms} ms past the run's end`);
});

test('a host is restarted only once the session using it is between requests, and the answer says it was', async () => {
  const done = [];
  let asks = 0;
  const restarted = await restartBetweenRequests({
    quiet: () => {
      asks += 1;
      done.push(`quiet ${asks >= 3}`);
      return asks >= 3;
    },
    stop: () => { done.push('stop'); },
    start: async () => {
      done.push('start');
      return 'host b';
    },
    within: 5_000,
    every: 5,
    gap: 5,
  });

  assert.deepEqual(done, ['quiet false', 'quiet false', 'quiet true', 'stop', 'start']);
  assert.equal(restarted.quiet, true);
  assert.equal(restarted.host, 'host b');
});

test('a session never between requests within the bound: the host is restarted after it, and the answer says so', async () => {
  const began = Date.now();
  let stoppedAt = -1;
  const restarted = await restartBetweenRequests({
    quiet: () => false,
    stop: () => { stoppedAt = Date.now() - began; },
    start: () => 'host b',
    within: 150,
    every: 5,
    gap: 5,
  });

  assert.equal(restarted.quiet, false);
  assert.ok(stoppedAt >= 150, `the host was stopped ${stoppedAt} ms in, before the bound`);
  assert.ok(restarted.waited >= 150);
  assert.equal(restarted.host, 'host b');
});

// ——— the ACP stub's set-up step (REVIEWENV1h, D154): the family rehearsal's actor for a review, tried here against a service and
// a connector that stand in, so a stub that stopped saying its set-up fails in a second rather than in an eight-minute gate.

/** A connector speaking MCP on stdio, as the driver hands one over the protocol: each tool call kept as a line in `CALLS`. */
const STAND_IN_CONNECTOR = `
import { appendFileSync } from 'node:fs';
import { createInterface } from 'node:readline';
const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\\n');
createInterface({ input: process.stdin }).on('line', (line) => {
  const frame = JSON.parse(line);
  if (frame.method === 'initialize') {
    send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: frame.params.protocolVersion, capabilities: { tools: {} }, serverInfo: { name: 'stand-in', version: '0' } } });
  } else if (frame.method === 'tools/call') {
    appendFileSync(process.env.CALLS, JSON.stringify(frame.params) + '\\n');
    send({ jsonrpc: '2.0', id: frame.id, result: { content: [{ type: 'text', text: 'kept ' + frame.params.name }] } });
  }
});
`;

/** A service that stands in: every request's path and body kept, each answered 200. */
async function standInService() {
  const asked = [];
  const server = createServer((request, response) => {
    let body = '';
    request.on('data', (chunk) => { body += chunk; });
    request.on('end', () => {
      asked.push({ path: request.url, body: body ? JSON.parse(body) : null });
      response.writeHead(200, { 'content-type': 'application/json' });
      response.end('{}');
    });
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  return { url: `http://127.0.0.1:${server.address().port}`, asked, close: () => new Promise((resolve) => server.close(resolve)) };
}

/**
 * One process of the stub, as the driver runs one: `initialize`, then `opening` (a new session or a resumed one), then one prompt;
 * its input closed once the prompt is answered. The answer, and what it said on stderr.
 */
function stubTurn(stub, { cwd, env, opening, words }) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [stub], { cwd, env: { ...process.env, ...env }, stdio: ['pipe', 'pipe', 'pipe'] });
    let buffered = '';
    let said = '';
    let answer = null;
    const timer = setTimeout(() => { child.kill(); reject(new Error(`the stub answered no prompt within 30s:\n${said}`)); }, 30_000);
    child.stdout.on('data', (chunk) => {
      buffered += chunk;
      let cut;
      while ((cut = buffered.indexOf('\n')) >= 0) {
        const frame = JSON.parse(buffered.slice(0, cut));
        buffered = buffered.slice(cut + 1);
        if (frame.id === 3 && answer === null) {
          answer = frame;
          child.stdin.end();
        }
      }
    });
    child.stderr.on('data', (chunk) => { said += chunk; });
    child.on('close', () => {
      clearTimeout(timer);
      resolve({ answer, said });
    });
    for (const frame of [
      { jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1 } },
      { jsonrpc: '2.0', id: 2, ...opening },
      { jsonrpc: '2.0', id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: words }] } },
    ]) child.stdin.write(`${JSON.stringify(frame)}\n`);
  });
}

const git = (cwd, ...args) => execFileSync('git', ['-c', 'user.name=Kit Test', '-c', 'user.email=kit@example.invalid', ...args], {
  cwd, encoding: 'utf8',
}).trim();

test("a set-up step's stub says what it showed through its connector and closes done, committing nothing; the person's not yet "
  + 'reaches its next turn, which corrects the work with a commit and says a new set-up', async (t) => {
  const at = folder('stub-set-up');
  const stub = join(at, 'acp-agent.mjs');
  writeFileSync(stub, ACP_STUB_AGENT);
  const connector = join(at, 'connector.mjs');
  writeFileSync(connector, STAND_IN_CONNECTOR);
  const calls = join(at, 'calls.jsonl');
  const tree = join(at, 'tree');
  mkdirSync(tree);
  git(tree, 'init', '-q');
  writeFileSync(join(tree, 'README.md'), '# the tree\n');
  git(tree, 'add', '-A');
  git(tree, 'commit', '-q', '-m', 'the work of #q1');
  const service = await standInService();
  t.after(() => service.close());

  const env = {
    DAORIS_SERVICE_URL: service.url,
    DAORIS_QUEST_ID: 'q2',
    DAORIS_QUEST_TITLE: 'Show #q1 in `dev` for review',
    DAORIS_QUEST_BODY: "Show the work of #q1 in `dev`, at https://dev.example.test/report, for the person's review.",
  };
  const servers = [{ name: 'daoris-knowledge', command: process.execPath, args: [connector], env: [{ name: 'CALLS', value: calls }] }];
  const before = git(tree, 'rev-parse', 'HEAD');
  const readCalls = () => readFileSync(calls, 'utf8').trim().split('\n').map((line) => JSON.parse(line));

  const first = await stubTurn(stub, {
    cwd: tree, env, words: 'This quest is a set-up step.',
    opening: { method: 'session/new', params: { cwd: tree, mcpServers: servers } },
  });

  assert.equal(first.answer?.result?.stopReason, 'end_turn', first.said);
  assert.deepEqual(service.asked.map((each) => `${each.path} ${each.body?.action}`),
    ['/api/quests/q2/respond take', '/api/quests/q2/respond done'], first.said);
  const [ready] = readCalls();
  assert.equal(ready.name, 'review_ready');
  assert.equal(ready.arguments.look, 'https://dev.example.test/report');
  assert.ok(ready.arguments.shows.length > 0 && ready.arguments.shows.length <= 300, ready.arguments.shows);
  assert.ok(ready.arguments.again.includes('https://dev.example.test/report'), ready.arguments.again);
  assert.equal(git(tree, 'rev-parse', 'HEAD'), before, 'a set-up makes no commit of its own');

  const words = 'the report still reads the old name';
  const again = await stubTurn(stub, {
    cwd: tree, env, words,
    opening: { method: 'session/resume', params: { sessionId: 'acp-session-1', cwd: tree, mcpServers: servers } },
  });

  assert.equal(again.answer?.result?.stopReason, 'end_turn', again.said);
  assert.match(again.said, new RegExp(`acp-agent: the set-up step heard not yet: ${words}`));
  assert.equal(service.asked.length, 2, 'its quest is done and stays so: the resumed turn moves nothing');
  assert.equal(git(tree, 'rev-list', '--count', `${before}..HEAD`), '1');
  assert.match(git(tree, 'log', '-1', '--format=%s'), /^review: correct what the person said not yet to \(quest q2\)$/);
  const [, readyAgain] = readCalls();
  assert.equal(readyAgain?.name, 'review_ready');
  assert.ok(readyAgain.arguments.shows.includes(words), readyAgain.arguments.shows);
});
