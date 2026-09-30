// A plugin that LANDS work (WSR4, D100): once Daoris has put a session's accepted work on a branch, the
// landing speaks one frame to this process — `hook/work/land` — and it pushes that branch to `origin`
// and opens an Azure DevOps pull request with `az repos pr create` (the az CLI's devops extension).
// Daoris itself runs neither: this process does, signed in as `az` is on this machine.
//
// It runs only for a landing: started for the one frame, told to go after it, never kept beside the
// driver loop — and only where a person installed it and named it in a workspace's landing rule.
//
// The organization, project and repository are the ones `az` detects from the checkout's own `origin`
// (`--detect`, on by default), so nothing here names any of them.
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
function run(command, args, cwd) {
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
      { cwd, encoding: 'utf8', windowsVerbatimArguments: true });
  } else {
    result = spawnSync(found, args, { cwd, encoding: 'utf8' });
  }

  if (result.error) return { ok: false, out: '', why: `\`${command}\` could not run: ${result.error.message}` };
  return {
    ok: result.status === 0,
    out: result.stdout ?? '',
    why: lastLine(result.stderr) || lastLine(result.stdout) || `it exited ${result.status}`,
  };
}

/** Words cmd can carry on Windows: quotes made single, percent signs spelled, one line. */
const safe = (text) => (process.platform === 'win32'
  ? text.replace(/"/g, "'").replace(/%/g, ' percent').replace(/[\r\n]+/g, ' ')
  : text);

/**
 * The description, a line per argument — `az repos pr create --description` makes each value a line of
 * its own. A line starting with a dash would read as a flag, so it is set in by a space.
 */
function description(p) {
  const lines = [];
  if (p.quest) lines.push(`Quest #${p.quest.id}${p.quest.title ? `: ${p.quest.title}` : ''}.`, '');
  lines.push(`Work from Daoris session ${p.session} in ${p.repository}, accepted by the person who reviewed it.`);
  if (p.commits?.length) {
    lines.push('', 'Commits:', '');
    for (const commit of p.commits) lines.push(`- ${commit.subject} (${commit.sha.slice(0, 8)})`);
  }
  return lines.map((line) => safe(line)).map((line) => (line.startsWith('-') ? ` ${line}` : line));
}

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

/** The landing: push, then open the pull request. Each failure is the answer's own sentence. */
function land(p) {
  const pushed = run('git', ['push', '--quiet', '-u', 'origin', p.branch], p.root);
  if (!pushed.ok) return { pushed: false, message: `git push to origin failed — ${pushed.why}` };

  const args = ['repos', 'pr', 'create', '--source-branch', p.branch, '--title', safe(p.title ?? p.branch), '--output', 'json'];
  if (p.base) args.push('--target-branch', p.base);
  // Last, because its values run on to the end of the line.
  args.push('--description', ...description(p));
  const opened = run('az', args, p.root);
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
    case 'initialize':
      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: ['work/land'] } });
      break;

    case 'hook/work/land': {
      const answer = land(frame.params);
      say(answer.message);
      send({ jsonrpc: '2.0', id: frame.id, result: answer });
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
