// Azure DevOps pull request (`azure-devops-pull-request`): a plugin that opens and reads a pull request through Azure DevOps' REST API.
// It runs no git and no CLI. Daoris pushed the branch before this step ran (a `push` node, through its own doors); this process is
// handed the API token of the git identity serving origin, for this process alone, in DAORIS_HOST_TOKEN, and is not started where
// no identity serves origin, the identity is read only, git access denies Write, or the identity has no API token.
// Two things it answers, both over JSON-RPC 2.0, one frame per line, on stdin and stdout (stdout is the wire; stderr is its console):
//  - `work/perform` for its one step, `pull-request`: opens the pull request from the pushed branch, or finds the active one already
//  open for it, once per idempotency key (the kit's work.mjs keeps that), and reports its address as evidence and as its
//  `pullRequest` output. The branch and checkout come from the units `work/watch` tells it; the step's attempt names its unit
//  by the tip it is made at.
//  - `work/state`: a landed branch's pull request, found by its address or its source branch, and its state in Daoris's words, with
//  the merge commit where it completed.
//  DAORIS_PLUGIN_DATA the data folder (the kit's journal)
//  DAORIS_HOST_TOKEN the identity's personal access token: never logged, never written, sent only to the API as Basic's password
//  DAORIS_HOST_API the API's base address where it is not Azure DevOps' (a test's stand-in), put before the organization's name
import { existsSync, readFileSync, statSync } from 'node:fs';
import { isAbsolute, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { work } from './work.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

const RECEIPT = 'azure-devops-pull-request/receipt@1';
const PULL_REQUEST = 'azure-devops-pull-request/pull-request@1';
const VERSION = 'api-version=7.1';

/** The awaited units `work/watch` last told, which a step's attempt is matched to. */
let units = [];

/** `origin`'s address as the checkout's git configuration writes it: read as a file, never by running git. */
function originOf(root) {
  try {
    let folder = join(root, '.git');
    if (statSync(folder).isFile()) {
      // A linked work tree's `.git` is a file naming its own folder, whose `commondir` holds the configuration.
      const gitdir = /^gitdir:\s*(.+)$/m.exec(readFileSync(folder, 'utf8'))?.[1].trim();
      if (!gitdir) return null;
      folder = isAbsolute(gitdir) ? gitdir : resolve(root, gitdir);
      const common = join(folder, 'commondir');
      if (existsSync(common)) folder = resolve(folder, readFileSync(common, 'utf8').trim());
    }
    const config = readFileSync(join(folder, 'config'), 'utf8');
    const section = /\[remote "origin"\]([^[]*)/.exec(config)?.[1] ?? '';
    return /^\s*url\s*=\s*(.+?)\s*$/m.exec(section)?.[1] ?? null;
  } catch {
    return null;
  }
}

/** `{ base, project, repository }` from an Azure DevOps address (HTTPS, a `visualstudio.com` one, or SSH v3), `base` being the organization's API root; else null. */
function azureOf(origin) {
  if (typeof origin !== 'string') return null;
  const text = origin.trim().replace(/^daoris::/, '');
  const decode = (part) => { try { return decodeURIComponent(part); } catch { return part; } };
  const named = process.env.DAORIS_HOST_API?.replace(/\/+$/, '');
  let m = /^https?:\/\/(?:[^@/]+@)?dev\.azure\.com\/([^/]+)\/([^/]+)\/_git\/([^/?#]+?)\/?$/i.exec(text);
  if (m) return { base: `${named ?? 'https://dev.azure.com'}/${m[1]}`, project: decode(m[2]), repository: decode(m[3]) };
  m = /^https?:\/\/(?:[^@/]+@)?([^./]+)\.visualstudio\.com\/(?:DefaultCollection\/)?([^/]+)\/_git\/([^/?#]+?)\/?$/i.exec(text);
  if (m) return { base: named ? `${named}/${m[1]}` : `https://${m[1]}.visualstudio.com`, project: decode(m[2]), repository: decode(m[3]) };
  m = /^(?:ssh:\/\/)?(?:[^@/]+@)?(ssh\.dev\.azure\.com|vs-ssh\.visualstudio\.com)[:/]v3\/([^/]+)\/([^/]+)\/([^/]+?)\/?$/i.exec(text);
  if (m) {
    const base = m[1].toLowerCase() === 'ssh.dev.azure.com' ? `${named ?? 'https://dev.azure.com'}/${m[2]}` : (named ? `${named}/${m[2]}` : `https://${m[2]}.visualstudio.com`);
    return { base, project: decode(m[3]), repository: decode(m[4]) };
  }
  return null;
}

/** A thrown reason with the code the step fails with; a transient code is one the manifest lets Daoris retry. */
class Refusal extends Error {
  constructor(code, message) {
    super(message);
    this.code = code;
  }
}

/** The refusals that may pass: Azure DevOps down, or limiting the token for now. */
const PASSING = ['unavailable', 'rate-limited'];

async function call(repo, method, path, body) {
  const token = process.env.DAORIS_HOST_TOKEN;
  if (!token) throw new Refusal('no-token', 'no API token was handed to this process');
  const url = `${repo.base}/${encodeURIComponent(repo.project)}/_apis/git/repositories/${encodeURIComponent(repo.repository)}${path}`;
  let response;
  try {
    response = await fetch(`${url}${url.includes('?') ? '&' : '?'}${VERSION}`, {
      method,
      headers: {
        Authorization: `Basic ${Buffer.from(`:${token}`).toString('base64')}`,
        Accept: 'application/json',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(30000),
    });
  } catch (error) {
    throw new Refusal('unavailable', `Azure DevOps could not be reached: ${error?.message ?? error}`);
  }
  let json = null;
  try {
    json = await response.json();
  } catch { /* an answer with no body, or a sign-in page */ }
  // A refused personal access token is sent on to a sign-in page (203) as often as it is answered 401.
  if ([401, 403, 203].includes(response.status) || (response.status === 200 && json === null)) {
    throw new Refusal('unauthorized', `Azure DevOps refused the token (${response.status})`);
  }
  if (response.status === 429) throw new Refusal('rate-limited', 'Azure DevOps is limiting this token for now');
  if (response.status >= 500) throw new Refusal('unavailable', `Azure DevOps answered ${response.status}`);
  return { status: response.status, json };
}

const heads = (branch) => `refs/heads/${branch}`;
const withoutHeads = (ref) => (typeof ref === 'string' ? ref.replace(/^refs\/heads\//, '') : null);

/** The pull requests from a branch, all statuses. */
async function fromBranch(repo, branch) {
  const found = await call(repo, 'GET', `/pullrequests?searchCriteria.sourceRefName=${encodeURIComponent(heads(branch))}&searchCriteria.status=all`);
  return found.status === 200 && Array.isArray(found.json?.value) ? found.json.value : [];
}

/** The address a person opens: the repository's web address and the pull request's id. */
const addressOf = (repo, pr) => `${pr.repository?.webUrl ?? `${repo.base}/${encodeURIComponent(repo.project)}/_git/${encodeURIComponent(repo.repository)}`}/pullrequest/${pr.pullRequestId}`;

const HOW = { noFastForward: 'merge', squash: 'squash', rebase: 'rebase', rebaseMerge: 'rebase-merge' };
const STATE = { active: 'open', completed: 'completed', abandoned: 'abandoned' };

/**
 * Which of the pull requests from a branch is the one asked about: among those into the line where there are
 * any, a completed one, else an active one, else the newest abandoned one.
 */
function choose(found, line) {
  const into = found.filter((each) => withoutHeads(each?.targetRefName) === line);
  const pool = line && into.length > 0 ? into : found;
  const newest = (list) => [...list].sort((a, b) => String(b.closedDate ?? b.creationDate ?? '').localeCompare(String(a.closedDate ?? a.creationDate ?? ''))
    || Number(b.pullRequestId) - Number(a.pullRequestId))[0];
  return newest(pool.filter((each) => each?.status === 'completed'))
    ?? newest(pool.filter((each) => each?.status === 'active'))
    ?? newest(pool.filter((each) => each?.status === 'abandoned'))
    ?? null;
}

function located(unit) {
  const repo = azureOf(originOf(unit.root));
  if (!repo) throw new Refusal('no-origin', `${unit.root} has no origin that names an Azure DevOps repository`);
  return repo;
}

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
  let id = typeof frame.pullRequest === 'string' ? /\/pullrequest\/(\d+)\/?$/i.exec(frame.pullRequest)?.[1] : undefined;
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
  // An attempt an earlier process began: the pull request active from its branch says whether the attempt made one. Azure DevOps down
  // or limiting the token cannot say yet, so the attempt is running and asked again; a refused token stops the step.
  check: async (attempt) => {
    const unit = units.find((each) => each.tip === attempt.subjectRevision);
    if (!unit) return null;
    try {
      const repo = located(unit);
      const active = (await fromBranch(repo, unit.branch)).find((pr) => pr.status === 'active');
      return active ? { state: 'done', outcome: reached(attempt, repo, active, `pull request ${active.pullRequestId} is active from \`${unit.branch}\`.`) } : { state: 'not-started' };
    } catch (error) {
      if (!(error instanceof Refusal) || !PASSING.includes(error.code)) return null;
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

// One frame answered once its handler settles, never after the frame before it: a slow read holds back no other answer, and
// Daoris waits 10 s for each. A handler that throws is a refusal, which the driver says in this plugin's name.
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
