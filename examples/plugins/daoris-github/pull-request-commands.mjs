// The pull request's own commands, which an agent performing a workflow's pull-request step calls as tools: open one from a pushed
// branch, read how it stands, and complete it. Each names the repository by its origin's address, as `git remote get-url origin`
// says it, since a command's round carries no checkout. Each answers its lines and, for the agent, `data` with the pull request.
import { Refusal, call, fromBranch, newest, repoOf, repoPath, wordOf } from './github.mjs';

const METHOD = { merge: 'merge', squash: 'squash', rebase: 'rebase' };

export const NAMES = ['create-pull-request', 'pull-request-status', 'complete-pull-request'];

const shown = (pr) => ({ id: pr.number, url: pr.html_url, state: wordOf(pr), source: pr.head?.ref ?? null, target: pr.base?.ref ?? null, mergeCommit: pr.merge_commit_sha ?? null });

const answered = (line, exit, pr) => ({ said: [line], exit, ...(pr ? { data: shown(pr) } : {}) });

function located(args) {
  const repo = repoOf(args.origin);
  if (!repo) throw new Refusal('no-origin', `\`${args.origin}\` names no GitHub repository`);
  return repo;
}

/** The pull request a call names: by its address or number, else the newest from its branch. */
async function named(repo, args) {
  const number = /(?:\/pull\/)?(\d+)\/?$/.exec(String(args['pull-request'] ?? ''))?.[1];
  if (number) {
    const read = await call(repo, 'GET', `${repoPath(repo)}/pulls/${number}`);
    return read.status === 200 ? read.json : null;
  }
  return args.branch ? newest(await fromBranch(repo, args.branch)) ?? null : null;
}

async function create(args) {
  const repo = located(args);
  const open = (await fromBranch(repo, args.branch)).find((pr) => pr.state === 'open');
  if (open) return answered(`Pull request #${open.number} is already open from \`${args.branch}\`: ${open.html_url}`, 0, open);
  const base = args.target || (await call(repo, 'GET', repoPath(repo))).json?.default_branch;
  if (!base) return answered(`GitHub names no default branch for ${repo.owner}/${repo.repo}.`, 1);
  const made = await call(repo, 'POST', `${repoPath(repo)}/pulls`, { head: args.branch, base, title: args.title || args.branch, body: args.description ?? '' });
  if (made.status === 201) return answered(`Opened pull request #${made.json.number} into \`${base}\`: ${made.json.html_url}`, 0, made.json);
  return answered(`GitHub did not open the pull request (${made.status}): ${made.json?.message ?? 'no reason given'}`, 1);
}

async function status(args) {
  const repo = located(args);
  const pr = await named(repo, args);
  if (!pr) return answered('GitHub holds no such pull request.', 1);
  return answered(`Pull request #${pr.number} is ${wordOf(pr)}: ${pr.html_url}`, 0, pr);
}

async function complete(args) {
  const repo = located(args);
  const pr = await named(repo, args);
  if (!pr) return answered('GitHub holds no such pull request.', 1);
  if (pr.state !== 'open') return answered(`Pull request #${pr.number} is ${wordOf(pr)}, so it is not merged again.`, pr.merged_at ? 0 : 1, pr);
  const merged = await call(repo, 'PUT', `${repoPath(repo)}/pulls/${pr.number}/merge`, { merge_method: METHOD[args.strategy] ?? 'merge', sha: pr.head?.sha });
  if (merged.status !== 200) return answered(`GitHub did not merge pull request #${pr.number} (${merged.status}): ${merged.json?.message ?? 'no reason given'}`, 1);
  if (args['delete-source-branch'] === true) await call(repo, 'DELETE', `${repoPath(repo)}/git/refs/heads/${encodeURIComponent(pr.head.ref)}`);
  const read = await call(repo, 'GET', `${repoPath(repo)}/pulls/${pr.number}`);
  return answered(`Pull request #${pr.number} is merged into \`${pr.base?.ref}\`.`, 0, read.status === 200 ? read.json : pr);
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
