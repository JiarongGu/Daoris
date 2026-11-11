// GitHub (`daoris-github`): Daoris's own plugin for GitHub, which opens and reads a pull request through GitHub's REST API.
// It runs no git and no CLI. Daoris pushed the branch before this step ran (a `push` node, through its own doors); this process is
// not started where origin is served by a read-only identity or git access denies Write. It signs in itself: its `login` command keeps a
// token in its own private storage (credential.mjs), and each API call carries that token.
// Over JSON-RPC 2.0, a frame a line on stdio, it answers `work/perform` for `pull-request` (the pull request from the pushed branch,
// opened or found, once per idempotency key, its address the evidence and the `pullRequest` output; the unit from `work/watch`, by the
// attempt's tip), `work/state` (a landed branch's pull request in Daoris's words) and its commands (commands.mjs).
//  DAORIS_PLUGIN_DATA the kit's journal; DAORIS_PLUGIN_PRIVATE where login keeps the token; DAORIS_HOST_API a stand-in's API (github.mjs)
import { createInterface } from 'node:readline';
import { work } from './work.mjs';
import { run } from './commands.mjs';
import { Refusal, TRANSIENT, call, fromBranch, newest, originOf, repoOf, repoPath, wordOf } from './github.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

const RECEIPT = 'github-pull-request/receipt@1';
const PULL_REQUEST = 'github-pull-request/pull-request@1';

/** The awaited units `work/watch` last told, which a step's attempt is matched to. */
let units = [];

function located(unit) {
  const repo = repoOf(originOf(unit.root));
  if (!repo) throw new Refusal('no-origin', `${unit.root} has no origin that names a GitHub repository`);
  return repo;
}

/** The `pull-request` step: the open pull request from the pushed branch, opened here where there is none. */
async function pullRequest(attempt) {
  const unit = units.find((each) => each.tip === attempt.subjectRevision && (each.awaiting ?? []).some((step) => step === 'pull-request' || step.endsWith('/pull-request')))
    ?? units.find((each) => each.tip === attempt.subjectRevision);
  if (!unit) return failed('no-unit', `no unit awaits this step at ${attempt.subjectRevision}`);
  try {
    const repo = located(unit);
    const open = (await fromBranch(repo, unit.branch)).find((pr) => pr.state === 'open');
    if (open) return reached(attempt, open, `pull request #${open.number} is already open from \`${unit.branch}\`.`);

    const base = unit.line || (await call(repo, 'GET', repoPath(repo))).json?.default_branch;
    if (!base) return failed('rejected', `GitHub names no default branch for ${repo.owner}/${repo.repo}`);
    const made = await call(repo, 'POST', `${repoPath(repo)}/pulls`, {
      title: unit.branch,
      head: unit.branch,
      base,
      body: `Opened by Daoris from \`${unit.branch}\` at ${unit.tip.slice(0, 8)}, in \`${unit.repository}\`.\n`,
    });
    if (made.status === 201) return reached(attempt, made.json, `opened pull request #${made.json.number} into \`${base}\`.`);
    // GitHub answers 422 where one is open for the branch already: a second attempt of this one, found rather than refused.
    const again = made.status === 422 ? (await fromBranch(repo, unit.branch)).find((pr) => pr.state === 'open') : null;
    if (again) return reached(attempt, again, `pull request #${again.number} is already open from \`${unit.branch}\`.`);
    return failed('rejected', `GitHub did not open the pull request (${made.status}): ${made.json?.message ?? 'no reason given'}`);
  } catch (error) {
    if (error instanceof Refusal) return failed(error.code, error.message);
    throw error;
  }
}

function reached(attempt, pr, message) {
  return {
    state: 'reached',
    evidence: { ref: `${RECEIPT}:${attempt.invocation}`, url: pr.html_url, message },
    outputs: { 'pull-request': { ref: `${PULL_REQUEST}:${pr.number}` } },
  };
}

function failed(code, message) {
  return { state: 'failed', code, evidence: { message: message.slice(0, 300) } };
}

/** `work/state`: the pull request at an address, else the one from the branch, read and said in Daoris's words. */
async function state(frame) {
  const repo = repoOf(frame.origin) ?? repoOf(originOf(frame.root));
  if (!repo) return { state: 'unknown', pullRequest: frame.pullRequest ?? null, message: 'origin names no GitHub repository.' };
  let number = typeof frame.pullRequest === 'string' ? /\/pull\/(\d+)\/?$/.exec(frame.pullRequest)?.[1] : undefined;
  if (number === undefined) {
    const chosen = newest(await fromBranch(repo, frame.branch));
    if (!chosen) return { state: 'unknown', pullRequest: null, message: `no pull request from \`${frame.branch}\` was found on GitHub.` };
    number = String(chosen.number);
  }
  const read = await call(repo, 'GET', `${repoPath(repo)}/pulls/${number}`);
  if (read.status !== 200) throw new Error(`GitHub answered ${read.status} for pull request ${number}`);
  const pr = read.json;
  const word = wordOf(pr);
  const target = pr.base?.ref ?? null;
  const done = word === 'completed' && typeof pr.merge_commit_sha === 'string' && typeof pr.head?.sha === 'string';
  return {
    state: word === 'completed' && !done ? 'unknown' : word,
    pullRequest: pr.html_url ?? null,
    mergeCommit: done ? pr.merge_commit_sha : null,
    sourceCommit: done ? pr.head.sha : null,
    target,
    how: null,
    at: word === 'open' ? null : pr.closed_at ?? null,
    message: `pull request ${number} is ${word}.`,
  };
}

const kit = work({
  data: process.env.DAORIS_PLUGIN_DATA,
  send,
  say,
  act: async (attempt) => {
    if (String(attempt.step).split('/').pop() !== 'pull-request') return failed('unknown-step', `no step ${attempt.step}`);
    return pullRequest(attempt);
  },
  // An attempt an earlier process began: the pull request open from its branch says whether the attempt made one.
  check: async (attempt) => {
    const unit = units.find((each) => each.tip === attempt.subjectRevision);
    if (!unit) return null;
    try {
      const open = (await fromBranch(located(unit), unit.branch)).find((pr) => pr.state === 'open');
      return open ? { state: 'done', outcome: reached(attempt, open, `pull request #${open.number} is open from \`${unit.branch}\`.`) } : { state: 'not-started' };
    } catch (error) {
      if (!(error instanceof Refusal) || !TRANSIENT.includes(error.code)) return null;
      say(`${error.message}; attempt ${attempt.invocation} is asked again later.`);
      return { state: 'running' };
    }
  },
  remove: async () => ({ disposition: 'already-gone' }),
});

const points = {
  'work/watch': async (frame) => {
    units = Array.isArray(frame.units) ? frame.units : [];
    return kit.points['work/watch'](frame);
  },
  'work/step': null,
  'work/perform': kit.points['work/perform'],
  'work/state': state,
};

const answers = { ...points, ...kit.calls, 'command/run': run };

const answer = async (frame) => {
  try {
    send({ jsonrpc: '2.0', id: frame.id, result: await answers[frame.method.slice(5)](frame.params ?? {}) });
  } catch (error) {
    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: String(error?.message ?? error) } });
  }
};

for await (const line of createInterface({ input: process.stdin })) {
  if (!line.trim()) continue;
  let frame;
  try {
    frame = JSON.parse(line);
  } catch {
    say(`not a frame: ${line}`);
    continue;
  }

  if (frame.method === 'initialize') {
    const declared = frame.params?.points ?? [];
    send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: Object.keys(points).filter((point) => declared.includes(point)), capabilities: kit.capabilities } });
  } else if (frame.method === 'shutdown') {
    process.exit(0);
  } else if (typeof frame.method === 'string' && frame.method.startsWith('hook/') && answers[frame.method.slice(5)]) {
    answer(frame);
  } else if (frame.id !== undefined) {
    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
  }
}
