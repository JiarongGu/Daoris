// GitHub pull request (`github-pull-request`): a plugin that opens and reads a pull request through GitHub's REST API (D241 point 5).
//
// It runs no git and no CLI. Daoris pushed the branch before this step ran (a `push` node, through its own doors); this process is
// handed the API token of the git identity serving origin, for this process alone, in DAORIS_HOST_TOKEN, and is not started where
// no identity serves origin, the identity is read only, git access denies Write, or the identity has no API token.
//
// Two things it answers, both over JSON-RPC 2.0, one frame per line, on stdin and stdout (stdout is the wire; stderr is its console):
//   - `work/perform` for its one step, `pull-request`: opens the pull request from the pushed branch, or finds the one already
//     open for it, once per idempotency key (the kit's work.mjs keeps that), and reports its address as evidence and as its
//     `pullRequest` output. The branch and checkout come from the units `work/watch` tells it; the step's attempt names its unit
//     by the tip it was made at.
//   - `work/state`: a landed branch's pull request, found by its address or its source branch, and its state in Daoris's words.
//
//   DAORIS_PLUGIN_DATA  the data folder (the kit's journal)
//   DAORIS_HOST_TOKEN   the identity's API token: never logged, never written, sent only to the API
//   DAORIS_HOST_API     the API's base address where it is not GitHub's (a test's stand-in); else api.github.com, or
//                       https://<host>/api/v3 for a GitHub Enterprise host the origin names
import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { work } from './work.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

const RECEIPT = 'github-pull-request/receipt@1';
const PULL_REQUEST = 'github-pull-request/pull-request@1';

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

/** `{ host, owner, repo }` from an address, under Daoris's `daoris::` routing or not; null for one that names no such repository. */
function repoOf(origin) {
  if (typeof origin !== 'string') return null;
  const text = origin.trim().replace(/^daoris::/, '');
  if (/^([A-Za-z]:[\\/]|[\\/.]|file:)/.test(text)) return null;
  const m = /^(?:[a-z][a-z0-9+.-]*:\/\/)?(?:[^@/]+@)?([^/:]+)(?::\d+)?[:/]+([^/]+)\/([^/]+?)(?:\.git)?\/*$/i.exec(text);
  return m ? { host: m[1].toLowerCase(), owner: m[2], repo: m[3] } : null;
}

function apiOf(host) {
  const named = process.env.DAORIS_HOST_API;
  if (named) return named.replace(/\/+$/, '');
  return host === 'github.com' ? 'https://api.github.com' : `https://${host}/api/v3`;
}

/** One API call: its status and JSON, or a thrown `Refusal` saying why nothing can be told (a transient code where it may pass). */
class Refusal extends Error {
  constructor(code, message) {
    super(message);
    this.code = code;
  }
}

async function call(repo, method, path, body) {
  const token = process.env.DAORIS_HOST_TOKEN;
  if (!token) throw new Refusal('no-token', 'no API token was handed to this process');
  let response;
  try {
    response = await fetch(`${apiOf(repo.host)}${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        'User-Agent': 'daoris-github-pull-request',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(30000),
    });
  } catch (error) {
    throw new Refusal('unavailable', `GitHub could not be reached: ${error?.message ?? error}`);
  }
  let json = null;
  try {
    json = await response.json();
  } catch { /* an answer with no body */ }
  if (response.status === 401 || response.status === 403 && !/rate limit/i.test(json?.message ?? '')) {
    throw new Refusal('unauthorized', `GitHub refused the token (${response.status}): ${json?.message ?? 'no reason given'}`);
  }
  if (response.status === 429 || response.status === 403) throw new Refusal('rate-limited', 'GitHub is limiting this token for now');
  if (response.status >= 500) throw new Refusal('unavailable', `GitHub answered ${response.status}`);
  return { status: response.status, json };
}

const repoPath = (repo) => `/repos/${encodeURIComponent(repo.owner)}/${encodeURIComponent(repo.repo)}`;

/** The pull requests from a branch, all states. */
async function fromBranch(repo, branch) {
  const found = await call(repo, 'GET', `${repoPath(repo)}/pulls?head=${encodeURIComponent(`${repo.owner}:${branch}`)}&state=all&per_page=30`);
  return found.status === 200 && Array.isArray(found.json) ? found.json : [];
}

const newest = (list) => [...list].sort((a, b) => b.number - a.number)[0];

/** GitHub's state in Daoris's words: open, completed (merged) or abandoned (closed unmerged). */
const wordOf = (pr) => (pr.state === 'open' ? 'open' : pr.merged_at ? 'completed' : pr.state === 'closed' ? 'abandoned' : 'unknown');

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
  // An attempt an earlier process began: the pull request open from its branch says whether it was made.
  check: async (attempt) => {
    const unit = units.find((each) => each.tip === attempt.subjectRevision);
    if (!unit) return null;
    try {
      const open = (await fromBranch(located(unit), unit.branch)).find((pr) => pr.state === 'open');
      return open ? { state: 'done', outcome: reached(attempt, open, `pull request #${open.number} is open from \`${unit.branch}\`.`) } : { state: 'not-started' };
    } catch {
      return null;
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
    try {
      send({ jsonrpc: '2.0', id: frame.id, result: await answers[frame.method.slice(5)](frame.params ?? {}) });
    } catch (error) {
      send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: String(error?.message ?? error) } });
    }
  } else if (frame.id !== undefined) {
    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
  }
}
