// Azure DevOps pull request (`azure-devops-pull-request`): a plugin that opens and reads a pull request through Azure DevOps' REST API.
// It runs no git and no CLI. Daoris pushed the branch before this step ran (a `push` node, through its own doors); this process is
// handed the API token of the git identity serving origin, for this process alone, in DAORIS_HOST_TOKEN, and is not started where
// no identity serves origin, the identity is read only, git access denies Write, or the identity has no API token.
// Two things it answers, both over JSON-RPC 2.0, one frame per line, on stdin and stdout (stdout is the wire; stderr is its console):
//  - `work/perform` for its one step, `pull-request`: opens the pull request from the pushed branch, or finds the active one already
//  open for it, once per idempotency key (the kit's work.mjs keeps that), and reports its address as evidence and as its
//  `pullRequest` output. The branch and checkout come from the units `work/watch` tells it.
//  - `work/state`: a landed branch's pull request, found by its address or its source branch, and its state in Daoris's words.
//  DAORIS_PLUGIN_DATA the data folder (the kit's journal)
//  DAORIS_HOST_TOKEN the identity's personal access token: never logged, never written, sent only to the API as Basic's password
//  DAORIS_HOST_API the API's base address where it is not Azure DevOps' (a test's stand-in), put before the organization's name
import { createInterface } from 'node:readline';
import { Refusal, TRANSIENT, addressOf, call, choose, fromBranch, heads, idOf, located, withoutHeads, azureOf, originOf } from './azure.mjs';
import { work } from './work.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

const RECEIPT = 'azure-devops-pull-request/receipt@1';
const PULL_REQUEST = 'azure-devops-pull-request/pull-request@1';

/** The awaited units `work/watch` last told, which a step's attempt is matched to. */
let units = [];

const HOW = { noFastForward: 'merge', squash: 'squash', rebase: 'rebase', rebaseMerge: 'rebase-merge' };
const STATE = { active: 'open', completed: 'completed', abandoned: 'abandoned' };

/** The `pull-request` step: the active pull request from the pushed branch, opened here where there is none. */
async function pullRequest(attempt) {
  const unit = units.find((each) => each.tip === attempt.subjectRevision && (each.awaiting ?? []).some((step) => step === 'pull-request' || step.endsWith('/pull-request')))
    ?? units.find((each) => each.tip === attempt.subjectRevision);
  if (!unit) return failed('no-unit', `no unit awaits this step at ${attempt.subjectRevision}`);
  try {
    const repo = located(unit);
    const active = (await fromBranch(repo, unit.branch)).find((pr) => pr.status === 'active');
    if (active) return reached(attempt, repo, active, `pull request ${active.pullRequestId} is already active from \`${unit.branch}\`.`);

    const target = unit.line ? heads(unit.line) : (await call(repo, 'GET', '')).json?.defaultBranch;
    if (!target) return failed('rejected', `Azure DevOps names no default branch for ${repo.repository}`);
    const made = await call(repo, 'POST', '/pullrequests', {
      sourceRefName: heads(unit.branch),
      targetRefName: target,
      title: unit.branch,
      description: `Opened by Daoris from ${unit.branch} at ${unit.tip.slice(0, 8)}, in ${unit.repository}.`,
    });
    if (made.status === 201 || made.status === 200) {
      return reached(attempt, repo, made.json, `opened pull request ${made.json.pullRequestId} into \`${withoutHeads(target)}\`.`);
    }
    // 409 says one is active for the branch already: a second attempt of this one, found rather than refused.
    const again = made.status === 409 ? (await fromBranch(repo, unit.branch)).find((pr) => pr.status === 'active') : null;
    if (again) return reached(attempt, repo, again, `pull request ${again.pullRequestId} is already active from \`${unit.branch}\`.`);
    return failed('rejected', `Azure DevOps did not open the pull request (${made.status}): ${made.json?.message ?? 'no reason given'}`);
  } catch (error) {
    if (error instanceof Refusal) return failed(error.code, error.message);
    throw error;
  }
}

function reached(attempt, repo, pr, message) {
  return {
    state: 'reached',
    evidence: { ref: `${RECEIPT}:${attempt.invocation}`, url: addressOf(repo, pr), message },
    outputs: { 'pull-request': { ref: `${PULL_REQUEST}:${pr.pullRequestId}` } },
  };
}

function failed(code, message) {
  return { state: 'failed', code, evidence: { message: message.slice(0, 300) } };
}

/** `work/state`: a landed branch's pull request, found by its address or its source branch, read and said in Daoris's words. */
async function state(frame) {
  const repo = azureOf(frame.origin) ?? azureOf(originOf(frame.root));
  if (!repo) return { state: 'unknown', pullRequest: frame.pullRequest ?? null, message: 'origin names no Azure DevOps repository.' };
  let id = idOf(frame.pullRequest);
  if (id === undefined) {
    const chosen = choose(await fromBranch(repo, frame.branch), frame.line);
    if (!chosen) return { state: 'unknown', pullRequest: null, message: `no pull request from \`${frame.branch}\` was found in Azure DevOps.` };
    id = String(chosen.pullRequestId);
  }
  const read = await call(repo, 'GET', `/pullrequests/${id}`);
  if (read.status !== 200) throw new Error(`Azure DevOps answered ${read.status} for pull request ${id}`);
  const pr = read.json;
  const word = STATE[pr.status] ?? 'unknown';
  const target = withoutHeads(pr.targetRefName);
  const address = addressOf(repo, pr);
  const at = typeof pr.closedDate === 'string' && word !== 'open' ? pr.closedDate : null;
  if (word !== 'completed') {
    return {
      state: word, pullRequest: address, mergeCommit: null, sourceCommit: null, target, how: null, at,
      message: word === 'unknown' ? `pull request ${id} is \`${pr.status ?? 'without a status'}\`, a status Daoris has no word for.` : `pull request ${id} is ${word}.`,
    };
  }
  const merge = pr.lastMergeCommit?.commitId;
  const source = pr.lastMergeSourceCommit?.commitId;
  if (typeof merge !== 'string' || typeof source !== 'string') {
    return {
      state: 'unknown', pullRequest: address, mergeCommit: null, sourceCommit: null, target, how: null, at,
      message: `pull request ${id} completed, and Azure DevOps names no merge commit${typeof source === 'string' ? '' : ' or source commit'} for it, so nothing can be confirmed.`,
    };
  }
  const how = HOW[pr.completionOptions?.mergeStrategy] ?? null;
  return {
    state: 'completed', pullRequest: address, mergeCommit: merge, sourceCommit: source, target, how, at,
    message: `pull request ${id} completed${how ? ` by ${how}` : ''} into \`${target ?? 'its target'}\`.`,
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
  // An attempt an earlier process began: the pull request active from its branch says whether the attempt made one.
  check: async (attempt) => {
    const unit = units.find((each) => each.tip === attempt.subjectRevision);
    if (!unit) return null;
    try {
      const repo = located(unit);
      const active = (await fromBranch(repo, unit.branch)).find((pr) => pr.status === 'active');
      return active ? { state: 'done', outcome: reached(attempt, repo, active, `pull request ${active.pullRequestId} is active from \`${unit.branch}\`.`) } : { state: 'not-started' };
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

const answers = { ...points, ...kit.calls };

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
