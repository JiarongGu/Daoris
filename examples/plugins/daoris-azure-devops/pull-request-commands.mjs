// The pull request's own commands, which an agent performing a workflow's pull-request step calls as tools: open one from a pushed
// branch, read how it stands, and complete it. Each names the repository by its origin's address, as `git remote get-url origin`
// says it, since a command's round carries no checkout. Each answers its lines and, for the agent, `data` with the pull request.
import { Refusal, addressOf, azureOf, call, choose, fromBranch, heads, idOf, withoutHeads } from './azure.mjs';

const STATE = { active: 'open', completed: 'completed', abandoned: 'abandoned' };
const STRATEGY = { merge: 'noFastForward', squash: 'squash', rebase: 'rebase', 'rebase-merge': 'rebaseMerge' };

export const NAMES = ['create-pull-request', 'pull-request-status', 'complete-pull-request'];

const answered = (line, exit, pr, repo) => ({ said: [line], exit, ...(pr ? { data: shown(repo, pr) } : {}) });

const shown = (repo, pr) => ({
  id: pr.pullRequestId,
  url: addressOf(repo, pr),
  state: STATE[pr.status] ?? 'unknown',
  source: withoutHeads(pr.sourceRefName),
  target: withoutHeads(pr.targetRefName),
  mergeCommit: pr.lastMergeCommit?.commitId ?? null,
});

function repoOf(args) {
  const repo = azureOf(args.origin);
  if (!repo) throw new Refusal('no-origin', `\`${args.origin}\` names no Azure DevOps repository`);
  return repo;
}

/** The pull request a call names: by its address or id, else the one from its branch into its target. */
async function named(repo, args) {
  const given = args['pull-request'];
  const id = idOf(given) ?? (/^\d+$/.test(String(given ?? '')) ? String(given) : undefined);
  if (id !== undefined) {
    const read = await call(repo, 'GET', `/pullrequests/${id}`);
    return read.status === 200 ? read.json : null;
  }
  return args.branch ? choose(await fromBranch(repo, args.branch), args.target) : null;
}

async function create(args) {
  const repo = repoOf(args);
  const active = (await fromBranch(repo, args.branch)).find((pr) => pr.status === 'active');
  if (active) return answered(`Pull request ${active.pullRequestId} is already active from \`${args.branch}\`: ${addressOf(repo, active)}`, 0, active, repo);
  const target = args.target ? heads(args.target) : (await call(repo, 'GET', '')).json?.defaultBranch;
  if (!target) return answered(`Azure DevOps names no default branch for ${repo.repository}.`, 1);
  const made = await call(repo, 'POST', '/pullrequests', {
    sourceRefName: heads(args.branch), targetRefName: target, title: args.title || args.branch, description: args.description ?? '',
  });
  if (made.status === 201 || made.status === 200) {
    return answered(`Opened pull request ${made.json.pullRequestId} into \`${withoutHeads(target)}\`: ${addressOf(repo, made.json)}`, 0, made.json, repo);
  }
  return answered(`Azure DevOps did not open the pull request (${made.status}): ${made.json?.message ?? 'no reason given'}`, 1);
}

async function status(args) {
  const repo = repoOf(args);
  const pr = await named(repo, args);
  if (!pr) return answered('Azure DevOps holds no such pull request.', 1);
  return answered(`Pull request ${pr.pullRequestId} is ${STATE[pr.status] ?? pr.status}: ${addressOf(repo, pr)}`, 0, pr, repo);
}

async function complete(args) {
  const repo = repoOf(args);
  const pr = await named(repo, args);
  if (!pr) return answered('Azure DevOps holds no such pull request.', 1);
  if (pr.status !== 'active') {
    return answered(`Pull request ${pr.pullRequestId} is ${STATE[pr.status] ?? pr.status}, so it is not completed again.`, pr.status === 'completed' ? 0 : 1, pr, repo);
  }
  const done = await call(repo, 'PATCH', `/pullrequests/${pr.pullRequestId}`, {
    status: 'completed',
    lastMergeSourceCommit: pr.lastMergeSourceCommit,
    completionOptions: { mergeStrategy: STRATEGY[args.strategy] ?? 'noFastForward', deleteSourceBranch: args['delete-source-branch'] === true },
  });
  if (done.status === 200) return answered(`Pull request ${pr.pullRequestId} is completing into \`${withoutHeads(pr.targetRefName)}\`.`, 0, done.json, repo);
  return answered(`Azure DevOps did not complete pull request ${pr.pullRequestId} (${done.status}): ${done.json?.message ?? 'no reason given'}`, 1);
}

/** One round of a pull-request command; a refusal said as its line, exit 1. */
export async function pullRequestCommand(command, args = {}) {
  try {
    if (command === 'create-pull-request') return await create(args);
    if (command === 'pull-request-status') return await status(args);
    return await complete(args);
  } catch (error) {
    if (error instanceof Refusal) return { said: [error.message], exit: 1 };
    throw error;
  }
}
