// A plugin that LANDS work (WSR4, D100): once Daoris has put a session's accepted work on a branch, the
// landing speaks one frame to this process — `hook/work/land` — and it pushes that branch to `origin`
// and opens an Azure DevOps pull request with `az repos pr create` (the az CLI's devops extension).
// Daoris itself runs neither: this process does, signed in as `az` is on this machine.
//
// It runs only for a landing: started for the one frame, told to go after it, never kept beside the
// driver loop — and only where a person installed it and named it in a workspace's landing rule.
//
// It also answers a query (PLUGHOOK1a, D148): `hook/work/state`, whether a landed branch's pull request
// completed and with which commit, asked only at a look that may remove the branch. A squash leaves git no
// ancestor to see; Azure DevOps knows, and Daoris acts on the answer only where git confirms it.
//
// The organization, project and repository are named from the frame's `origin` (GITAUTH1b, D198), the address Daoris's one
// reader gives, with `--detect false`: under Daoris's doors the checkout's own remotes read `daoris::https://…`, which `az`
// cannot parse. An `origin` that is no Azure DevOps address leaves `az` to detect from the remotes, run without Daoris's routing.
//
// The wire is JSON-RPC 2.0, one frame per line, on this process's stdin and stdout. Nothing else is on
// stdout; stderr is the plugin's own console, shown under its name.
//
// Self-contained on purpose: a plugin is a folder copied into the Daoris home, so it imports nothing
// from beside it. The GitHub example carries the same two helpers.
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { delimiter, join } from 'node:path';
import { createInterface } from 'node:readline';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

/** The last line a command said, for a sentence. */
const lastLine = (text) => (text ?? '').trim().split(/\r?\n/).at(-1)?.trim() ?? '';

/** Where a command is, as this machine starts it: on Windows by PATH and PATHEXT, elsewhere by name. */
function which(command) {
  if (process.platform !== 'win32') return command;
  const extensions = (process.env.PATHEXT ?? '.COM;.EXE;.BAT;.CMD').split(';').filter(Boolean);
  for (const directory of (process.env.PATH ?? '').split(delimiter).filter(Boolean)) {
    for (const extension of extensions) {
      const candidate = join(directory, command + extension);
      if (existsSync(candidate)) return candidate;
    }
  }
  return null;
}

/**
 * Run a command with its arguments, never through a shell — except a Windows `.cmd` or `.bat`, which only
 * cmd can start (`az` is one). cmd reads the line again, so every argument is quoted, and one holding
 * what cmd would still act on inside quotes (a double quote, a percent sign, a line break) is refused.
 */
function run(command, args, cwd, env = process.env) {
  const found = which(command);
  if (found === null) return { ok: false, out: '', why: `\`${command}\` is not on this machine's PATH` };

  let result;
  if (process.platform === 'win32' && /\.(cmd|bat)$/i.test(found)) {
    const unsafe = args.find((arg) => /["%\r\n]/.test(arg));
    if (unsafe !== undefined) {
      return { ok: false, out: '', why: `\`${command}\` is a cmd script here, and \`${unsafe}\` cannot pass through cmd safely` };
    }
    const line = [found, ...args].map((arg) => `"${arg.replace(/(\\+)$/, '$1$1')}"`).join(' ');
    result = spawnSync(process.env.ComSpec ?? 'cmd.exe', ['/d', '/s', '/c', `"${line}"`],
      { cwd, encoding: 'utf8', windowsVerbatimArguments: true, env });
  } else {
    result = spawnSync(found, args, { cwd, encoding: 'utf8', env });
  }

  if (result.error) return { ok: false, out: '', why: `\`${command}\` could not run: ${result.error.message}` };
  return {
    ok: result.status === 0,
    out: result.stdout ?? '',
    why: lastLine(result.stderr) || lastLine(result.stdout) || `it exited ${result.status}`,
  };
}

/**
 * This environment without Daoris's routing (GITAUTH1b, D198 point 2; the git-auth design §4.4): under Daoris's doors git shows every
 * HTTP and HTTPS remote as `daoris::<address>`, which `az`'s detection cannot parse. Where the frame names no repository `az` can be
 * told, it detects from the checkout's remotes as they are written, run so.
 */
function unrouted() {
  const env = { ...process.env };
  for (const name of Object.keys(env)) if (/^GIT_CONFIG_(COUNT|KEY_\d+|VALUE_\d+)$/i.test(name)) delete env[name];
  return env;
}

/**
 * The Azure DevOps repository `origin` names (GITAUTH1b): the frame's `origin`, as Daoris's one reader gives it, read in each form
 * Azure DevOps serves — `https://[user@]dev.azure.com/<org>/<project>/_git/<repo>`, `https://<org>.visualstudio.com/[DefaultCollection/]
 * <project>/_git/<repo>`, and SSH's `…:v3/<org>/<project>/<repo>`. Null for any other, where `az` detects as before, unrouted.
 */
function azureOf(origin) {
  if (typeof origin !== 'string') return null;
  const text = origin.trim().replace(/^daoris::/, '');
  const decode = (part) => { try { return decodeURIComponent(part); } catch { return part; } };
  let m = /^https?:\/\/(?:[^@/]+@)?dev\.azure\.com\/([^/]+)\/([^/]+)\/_git\/([^/?#]+?)\/?$/i.exec(text);
  if (m) return { organization: `https://dev.azure.com/${m[1]}`, project: decode(m[2]), repository: decode(m[3]) };
  m = /^https?:\/\/(?:[^@/]+@)?([^./]+)\.visualstudio\.com\/(?:DefaultCollection\/)?([^/]+)\/_git\/([^/?#]+?)\/?$/i.exec(text);
  if (m) return { organization: `https://${m[1]}.visualstudio.com`, project: decode(m[2]), repository: decode(m[3]) };
  m = /^(?:ssh:\/\/)?(?:[^@/]+@)?(ssh\.dev\.azure\.com|vs-ssh\.visualstudio\.com)[:/]v3\/([^/]+)\/([^/]+)\/([^/]+?)\/?$/i.exec(text);
  if (m) {
    const organization = m[1].toLowerCase() === 'ssh.dev.azure.com' ? `https://dev.azure.com/${m[2]}` : `https://${m[2]}.visualstudio.com`;
    return { organization, project: decode(m[3]), repository: decode(m[4]) };
  }
  return null;
}

/**
 * How an `az repos` call is told its repository (GITAUTH1b, the plugin contract): named from `origin` with detection off, the whole
 * repository where the call takes it (`create`, `list`) and the organization alone where it does not (`show`); else run unrouted.
 */
function azure(p, whole) {
  const named = azureOf(p.origin);
  if (named === null) return { args: [], env: unrouted() };
  const args = ['--organization', named.organization, '--detect', 'false'];
  if (whole) args.push('--project', named.project, '--repository', named.repository);
  return { args, env: process.env };
}

/** Words cmd can carry on Windows: quotes made single, percent signs spelled, one line. */
const safe = (text) => (process.platform === 'win32'
  ? text.replace(/"/g, "'").replace(/%/g, ' percent').replace(/[\r\n]+/g, ' ')
  : text);

/** Who accepted the work, as the frame says it: a person's press, or the rule's switch at the quest's done (LAND2c). */
const accepted = (p) => (p.acceptedBy === 'auto'
  ? 'accepted automatically when its quest was done'
  : 'accepted by the person who reviewed it');

/**
 * The description, a line per argument — `az repos pr create --description` makes each value a line of
 * its own. A line starting with a dash would read as a flag, so it is set in by a space.
 */
function description(p) {
  const lines = [];
  if (p.quest) lines.push(`Quest #${p.quest.id}${p.quest.title ? `: ${p.quest.title}` : ''}.`, '');
  lines.push(`Work from Daoris session ${p.session} in ${p.repository}, ${accepted(p)}.`);
  if (p.commits?.length) {
    lines.push('', 'Commits:', '');
    for (const commit of p.commits) lines.push(`- ${commit.subject} (${commit.sha.slice(0, 8)})`);
  }
  return lines.map((line) => safe(line)).map((line) => (line.startsWith('-') ? ` ${line}` : line));
}

/**
 * Push the work to `origin` (LANDPIN2): the commit the landing judged, as the frame's `tip` names it, to the branch, so a commit
 * made on the branch after Daoris judged it never rides the push; the branch as it stands only where a Daoris from before names none.
 */
const push = (p) => run('git', ['push', '--quiet', 'origin', p.tip ? `${p.tip}:refs/heads/${p.branch}` : p.branch], p.root);

/** Where a person reads the pull request: the repository's web address and the pull request's number. */
function address(out) {
  try {
    const created = JSON.parse(out);
    const web = created?.repository?.webUrl;
    return typeof web === 'string' && created.pullRequestId !== undefined ? `${web}/pullrequest/${created.pullRequestId}` : null;
  } catch {
    return null;
  }
}

/**
 * The state of the pull request at an address, as `az` reads it by the number the address ends in: open (`active`), or the
 * word Azure DevOps gives it, or why it could not say.
 */
function state(p) {
  const pullRequest = p.pullRequest;
  const named = azure(p, false);
  const id = /\/pullrequest\/(\d+)\/?$/i.exec(pullRequest)?.[1];
  if (id === undefined) return { error: `${pullRequest} does not end in a pull request's number` };
  const read = run('az', ['repos', 'pr', 'show', '--id', id, '--output', 'json', ...named.args], p.root, named.env);
  if (!read.ok) return { error: read.why };
  try {
    const word = String(JSON.parse(read.out).status ?? '');
    return word ? { open: word === 'active', word } : { error: 'az answered no status' };
  } catch {
    return { error: 'az answered something that is not JSON' };
  }
}

/**
 * An advance (LAND2c, D149): Daoris moved the chain's branch on to a later step's work, and `pullRequest` is the one open from
 * it. Push only while it is open, and open no second one: commits pushed to a pull request that is completed or abandoned
 * would ride none, and a second one from this branch would carry the first's work again.
 */
function advance(p) {
  const read = state(p);
  if (read.error) {
    return {
      pushed: false,
      pullRequest: p.pullRequest,
      message: `could not read the state of its pull request ${p.pullRequest} — ${read.error}; nothing was pushed, since commits pushed to one that is not open would ride no pull request.`,
    };
  }
  if (!read.open) {
    return {
      pushed: false,
      pullRequest: p.pullRequest,
      message: `its pull request ${p.pullRequest} is ${read.word}, so nothing was pushed: commits pushed now would ride no pull request. Open a new one for this work.`,
    };
  }

  const pushed = push(p);
  if (!pushed.ok) return { pushed: false, pullRequest: p.pullRequest, message: `git push to origin failed — ${pushed.why}` };
  return {
    pushed: true,
    pullRequest: p.pullRequest,
    message: `pushed \`${p.branch}\` to origin; its pull request carries the new commits, and no second one was opened.`,
  };
}

/** `az`'s JSON, or the reason it is not. */
function json(read) {
  try {
    return { value: JSON.parse(read.out) };
  } catch {
    return { error: 'az answered something that is not JSON' };
  }
}

/** Azure DevOps' word for how a pull request completed, in Daoris's (PLUGHOOK1a): any other is none. */
const HOW = { noFastForward: 'merge', squash: 'squash', rebase: 'rebase', rebaseMerge: 'rebase-merge' };

/** Azure DevOps' status in Daoris's words: active is open, completed and abandoned keep theirs, anything else is unknown. */
const STATE = { active: 'open', completed: 'completed', abandoned: 'abandoned' };

const withoutHeads = (ref) => (typeof ref === 'string' ? ref.replace(/^refs\/heads\//, '') : null);

/**
 * Which of the pull requests from a branch is the one asked about (PLUGHOOK1a, design §2.6): among those into the line where
 * there are any, a completed one, else an active one, else the newest abandoned one.
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

/** A failure of `az` is the call's error, in az's last line: never a state, so nothing moves on it. */
class Failed extends Error {}

/**
 * The query (PLUGHOOK1a, D148): a landed branch's pull request, found by its address or by the branch as its source, read
 * with `az repos pr show`, and answered in Daoris's words. Daoris acts on a completed answer only where git confirms its merge
 * commit on the line, so a reading of Azure DevOps that is wrong keeps the branch rather than losing it.
 */
function query(p) {
  let id = typeof p.pullRequest === 'string' ? /\/pullrequest\/(\d+)\/?$/i.exec(p.pullRequest)?.[1] : undefined;
  if (id === undefined) {
    const listed = run('az', ['repos', 'pr', 'list', '--source-branch', p.branch, '--status', 'all', '--output', 'json', ...azure(p, true).args], p.root, azure(p, true).env);
    if (!listed.ok) throw new Failed(listed.why);
    const found = json(listed);
    if (found.error) throw new Failed(found.error);
    const chosen = Array.isArray(found.value) ? choose(found.value, p.line) : null;
    if (chosen === null) return { state: 'unknown', pullRequest: null, message: `no pull request from \`${p.branch}\` was found in Azure DevOps.` };
    id = String(chosen.pullRequestId);
  }

  const shown = run('az', ['repos', 'pr', 'show', '--id', id, '--output', 'json', ...azure(p, false).args], p.root, azure(p, false).env);
  if (!shown.ok) throw new Failed(shown.why);
  const read = json(shown);
  if (read.error) throw new Failed(read.error);
  const pr = read.value ?? {};
  const web = pr.repository?.webUrl;
  const address = typeof web === 'string' && pr.pullRequestId !== undefined ? `${web}/pullrequest/${pr.pullRequestId}` : null;
  const word = STATE[pr.status] ?? 'unknown';
  const target = withoutHeads(pr.targetRefName);
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
      message: `pull request ${id} completed, and az names no merge commit${typeof source === 'string' ? '' : ' or source commit'} for it, so nothing can be confirmed.`,
    };
  }

  const how = HOW[pr.completionOptions?.mergeStrategy] ?? null;
  return {
    state: 'completed', pullRequest: address, mergeCommit: merge, sourceCommit: source, target, how, at,
    message: `pull request ${id} completed${how ? ` by ${how}` : ''} into \`${target ?? 'its target'}\`.`,
  };
}

/** The landing: push, then open the pull request. Each failure is the answer's own sentence. */
function land(p) {
  if (p.pullRequest) return advance(p);

  const pushed = push(p);
  if (!pushed.ok) return { pushed: false, message: `git push to origin failed — ${pushed.why}` };

  const args = ['repos', 'pr', 'create', '--source-branch', p.branch, '--title', safe(p.title ?? p.branch), '--output', 'json'];
  if (p.base) args.push('--target-branch', p.base);
  const named = azure(p, true);
  args.push(...named.args);
  // Last, because its values run on to the end of the line.
  args.push('--description', ...description(p));
  const opened = run('az', args, p.root, named.env);
  if (!opened.ok) {
    return {
      pushed: true,
      message: `pushed \`${p.branch}\` to origin; az did not open the pull request — ${opened.why}. Open it in Azure DevOps from the branch.`,
    };
  }

  return {
    pushed: true,
    pullRequest: address(opened.out),
    message: `pushed \`${p.branch}\` to origin and opened a pull request into \`${p.base ?? 'the default branch'}\`.`,
  };
}

for await (const line of createInterface({ input: process.stdin })) {
  if (!line.trim()) continue;
  const frame = JSON.parse(line);

  switch (frame.method) {
    // The handshake: the points this process actually listens on, a subset of what the manifest declared.
    case 'initialize': {
      const declared = Array.isArray(frame.params?.points) ? frame.params.points : [];
      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: ['work/land', 'work/state'].filter((point) => declared.includes(point)) } });
      break;
    }

    case 'hook/work/land': {
      const answer = land(frame.params);
      say(answer.message);
      send({ jsonrpc: '2.0', id: frame.id, result: answer });
      break;
    }

    case 'hook/work/state': {
      try {
        const answer = query(frame.params);
        say(answer.message);
        send({ jsonrpc: '2.0', id: frame.id, result: answer });
      } catch (error) {
        const why = error instanceof Failed ? error.message : `could not read the pull request: ${error?.message ?? error}`;
        say(why);
        send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: why } });
      }
      break;
    }

    case 'shutdown':
      process.exit(0);
      break;

    default:
      if (frame.id !== undefined) {
        send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
      }
  }
}
