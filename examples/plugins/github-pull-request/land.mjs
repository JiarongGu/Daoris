// A plugin that LANDS work (WSR4, D100): once Daoris has put a session's accepted work on a branch, the
// landing speaks one frame to this process — `hook/work/land` — and it pushes that branch to `origin`
// and opens a GitHub pull request with the `gh` CLI. Daoris itself runs neither: this process does,
// signed in as `gh` is on this machine.
//
// It runs only for a landing: started for the one frame, told to go after it, never kept beside the
// driver loop — and only where a person installed it and named it in a workspace's landing rule.
//
// The wire is JSON-RPC 2.0, one frame per line, on this process's stdin and stdout. Nothing else is on
// stdout; stderr is the plugin's own console, shown under its name.
//   DAORIS_PLUGIN_DATA  the data folder beside the install, which an update never touches — the pull
//                       request's body is written here, one file per session
//
// Self-contained on purpose: a plugin is a folder copied into the Daoris home, so it imports nothing
// from beside it. The Azure DevOps example carries the same two helpers.
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
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
 * cmd can start. cmd reads the line again, so every argument is quoted, and one holding what cmd would
 * still act on inside quotes (a double quote, a percent sign, a line break) is refused, not passed.
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

/** A title cmd can carry on Windows: its quotes made single, its percent signs spelled, one line. */
const safe = (text) => (process.platform === 'win32'
  ? text.replace(/"/g, "'").replace(/%/g, ' percent').replace(/[\r\n]+/g, ' ')
  : text);

/** Who accepted the work, as the frame says it: a person's press, or the rule's switch at the quest's done (LAND2c). */
const accepted = (p) => (p.acceptedBy === 'auto'
  ? 'accepted automatically when its quest was done'
  : 'accepted by the person who reviewed it');

/** The pull request's body: the quest it answers, the session that did it, the commits it carries. */
function body(p) {
  const lines = [];
  if (p.quest) lines.push(`Quest \`#${p.quest.id}\`${p.quest.title ? `: ${p.quest.title}` : ''}.`, '');
  lines.push(`Work from Daoris session \`${p.session}\` in \`${p.repository}\`, ${accepted(p)}.`);
  if (p.commits?.length) {
    lines.push('', 'Commits:', '');
    for (const commit of p.commits) lines.push(`- ${commit.subject} (${commit.sha.slice(0, 8)})`);
  }
  return `${lines.join('\n')}\n`;
}

/** The state of the pull request at an address, as `gh` reads it: open, or the word GitHub gives it, or why it could not say. */
function state(pullRequest, root) {
  const read = run('gh', ['pr', 'view', pullRequest, '--json', 'state'], root);
  if (!read.ok) return { error: read.why };
  try {
    const word = String(JSON.parse(read.out).state ?? '');
    return word ? { open: word === 'OPEN', word: word.toLowerCase() } : { error: 'gh answered no state' };
  } catch {
    return { error: 'gh answered something that is not JSON' };
  }
}

/**
 * An advance (LAND2c, D149): Daoris moved the chain's branch on to a later step's work, and `pullRequest` is the one open from
 * it. Push only while it is open, and open no second one: commits pushed to a pull request that is merged or closed would ride
 * none, and a second one from this branch would carry the first's work again.
 */
function advance(p) {
  const read = state(p.pullRequest, p.root);
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

  const pushed = run('git', ['push', '--quiet', 'origin', p.branch], p.root);
  if (!pushed.ok) return { pushed: false, pullRequest: p.pullRequest, message: `git push to origin failed — ${pushed.why}` };
  return {
    pushed: true,
    pullRequest: p.pullRequest,
    message: `pushed \`${p.branch}\` to origin; its pull request carries the new commits, and no second one was opened.`,
  };
}

/** The landing: push, then open the pull request. Each failure is the answer's own sentence. */
function land(p) {
  if (p.pullRequest) return advance(p);

  const pushed = run('git', ['push', '--quiet', '-u', 'origin', p.branch], p.root);
  if (!pushed.ok) return { pushed: false, message: `git push to origin failed — ${pushed.why}` };

  const data = process.env.DAORIS_PLUGIN_DATA;
  mkdirSync(data, { recursive: true });
  const bodyFile = join(data, `pull-request-${p.session.replace(/[^A-Za-z0-9._-]/g, '-')}.md`);
  writeFileSync(bodyFile, body(p));

  const args = ['pr', 'create', '--head', p.branch, '--title', safe(p.title ?? p.branch), '--body-file', bodyFile];
  if (p.base) args.push('--base', p.base);
  const opened = run('gh', args, p.root);
  if (!opened.ok) {
    return {
      pushed: true,
      message: `pushed \`${p.branch}\` to origin; gh did not open the pull request — ${opened.why}. Open it on GitHub from the branch.`,
    };
  }

  // `gh pr create` prints the new pull request's address as its last line.
  const url = opened.out.split(/\r?\n/).map((l) => l.trim()).reverse().find((l) => /^https?:\/\//.test(l)) ?? null;
  return {
    pushed: true,
    pullRequest: url,
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
