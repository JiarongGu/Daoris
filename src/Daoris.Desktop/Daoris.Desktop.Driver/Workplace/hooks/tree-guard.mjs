// Daoris's tree guard (PERM3, D72; TREESHELL1): a PreToolUse hook that refuses a file write outside the
// session's own tree. Shipped inside the driver and written under the Daoris home; the harness runs it as
//
//   node tree-guard.mjs <tree> [<target> ...]
//
// with the call as JSON on stdin. Each <target> after the tree is a checkout the person declared this
// session's repository may also write into (D107): a write there is let through as one in the tree is.
// Zero dependencies — it runs inside every session.
//
// It judges the tools that name a file (Write, Edit, MultiEdit, NotebookEdit) by their path fields,
// resolved through links, and a shell's call (Bash, PowerShell) by the write targets it can READ in the
// command: a redirect (`>`, `>>`), `tee`, `cp`, `mv`, `rm`, and PowerShell's Set-Content, Add-Content,
// Out-File, Copy-Item, Move-Item and Remove-Item, each resolved against the directory the command stands
// in at that point (a `cd` moves it). A command it cannot read, a target a variable or a substitution
// makes, and every write it has no form for are the harness's own working-directory boundary's, as before:
// a command is never refused for being unread.
//
// 🔴 It refuses STRUCTURALLY: `permissionDecision: "deny"` on stdout, exit 0. The harness reads any exit
// but 2 as a non-blocking error, so a guard that failed by exiting would let the write through. Inside
// the tree it says NOTHING — never "allow", which would lift the harness's own asking.

import { lstatSync, readlinkSync, realpathSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Whether `target` is outside `tree`. Pure: the platform is an argument, and so is the resolution of
 * links, so the judgement is the same on any machine that tests it.
 *
 * @param {{ tree: string, target: string, cwd?: string | null, platform?: string,
 *           realpath?: (p: string) => string }} call
 */
export function judge({ tree, target, cwd = null, platform = process.platform, realpath = (p) => p }) {
  const paths = platform === 'win32' ? path.win32 : path.posix;
  const fold = platform === 'win32' ? (p) => p.toLowerCase() : (p) => p;

  const root = realpath(paths.resolve(tree));
  // A relative path is the harness's own cwd's, and only then the tree's.
  const resolved = realpath(paths.resolve(cwd ?? tree, target));
  const relative = paths.relative(fold(root), fold(resolved));

  // Inside when the way from the tree to the target never climbs out of it. On Windows another drive
  // or a UNC share answers an absolute path, which is outside by definition.
  const outside = relative === '..' || relative.startsWith(`..${paths.sep}`) || paths.isAbsolute(relative);
  return { outside, resolved };
}

/**
 * The real path of a file that may not exist yet: the deepest ancestor that does, resolved through its
 * links, with the rest put back. A write through a link inside the tree that leads out of it is outside.
 *
 * 🔴 A link whose target does not exist YET fails to resolve exactly as a missing file does — and a
 * write through it lands wherever it points, creating the target. Climbing past it as though it were
 * a plain name judged such a write inside the tree (REV3). So a component that will not resolve is
 * asked whether it is a link, and a link is followed. Too many links is a refusal, never a guess.
 */
export function resolveThroughLinks(target, depth = 0) {
  if (depth > 40) throw new Error(`too many links resolving \`${target}\``);
  const rest = [];
  let at = target;
  for (;;) {
    try {
      return path.join(realpathSync.native(at), ...rest.reverse());
    } catch {
      let link = null;
      try {
        if (lstatSync(at).isSymbolicLink()) link = readlinkSync(at);
      } catch {
        // Not there at all: an ordinary name the write would create.
      }
      if (link !== null) {
        return resolveThroughLinks(path.join(path.resolve(path.dirname(at), link), ...rest.reverse()), depth + 1);
      }

      const parent = path.dirname(at);
      if (parent === at) return target;
      rest.push(path.basename(at));
      at = parent;
    }
  }
}

/** The paths a call would write, by the fields the harness gives each tool. */
export function targets(tool, input) {
  if (!input || typeof input !== 'object') return [];
  const found = [];
  if (tool === 'NotebookEdit') found.push(input.notebook_path);
  if (tool === 'Write' || tool === 'Edit' || tool === 'MultiEdit') found.push(input.file_path);
  if (tool === 'MultiEdit' && Array.isArray(input.edits)) {
    for (const edit of input.edits) found.push(edit?.file_path);
  }
  return found.filter((one) => typeof one === 'string' && one.length > 0);
}

// ——— A shell's writes (TREESHELL1).
//
// Reading a command is a lexer and a walk over its simple commands, not an interpreter: it knows quotes,
// escapes, heredocs, comments, `;` `&&` `||` `|` and parentheses, and what it does not know it does not
// guess. Whatever it cannot read yields NO target (never a refusal): an unclosed quote ends the reading
// of the whole command, and a word a variable, a substitution or a backtick makes is not a path.

/** The tools whose `command` is a shell's. */
export const SHELL_TOOLS = ['Bash', 'PowerShell'];

// What a redirect may name that is no file.
const NOT_FILES = /^(?:\/dev\/(?:null|zero|stdin|stdout|stderr|tty|fd\/\d+)|nul)$/i;
// Words that stand before a command and are not it: `if x; then cmd`, `! cmd`, `{ cmd; }`.
const RESERVED = new Set(['{', '}', '!', 'if', 'then', 'else', 'elif', 'do', 'while', 'until', 'time']);
// A command that runs another: its options are skipped, and so are the assignments after it.
const WRAPPERS = new Set(['sudo', 'doas', 'env', 'command', 'builtin', 'exec', 'nohup', 'nice']);
const ASSIGNMENT = /^[A-Za-z_][A-Za-z0-9_]*=/;
// What a backslash before it makes literal, outside quotes (bash). Any other backslash stays one, so a
// Windows path written `D:\a\b` keeps its separators.
const ESCAPABLE = ' \t"\'$`&;|<>()*?[]{}!#~\\';

/** The index just past the group that opens at `from` (the character after the opener), or -1. */
function skipGroup(text, from, open, close) {
  let depth = 1;
  for (let i = from; i < text.length; i++) {
    const c = text[i];
    if (c === '\\') { i++; continue; }
    if (c === "'" || c === '"') {
      const end = text.indexOf(c, i + 1);
      if (end < 0) return -1;
      i = end;
      continue;
    }
    if (c === open) depth++;
    else if (c === close && --depth === 0) return i + 1;
  }
  return -1;
}

/** The index just past a heredoc's body, which starts at `from`: the line that is the delimiter, or the end. */
function skipHeredoc(text, from, { delimiter, strip }) {
  let at = from;
  while (at < text.length) {
    const end = text.indexOf('\n', at);
    const line = text.slice(at, end < 0 ? text.length : end).replace(/\r$/, '');
    at = end < 0 ? text.length : end + 1;
    if ((strip ? line.replace(/^\t+/, '') : line) === delimiter) return at;
  }
  return text.length;
}

/**
 * A command's words, redirects and separators, or null when it cannot be read to its end.
 * A word is `{ text, dynamic, quoted }`: dynamic when anything in it is made by a variable, a
 * substitution or a backtick, quoted when any part of it was quoted or escaped.
 */
function lex(command, ps) {
  const items = [];
  const heredocs = [];
  const n = command.length;
  let word = null;
  let i = 0;

  const start = () => (word ??= { text: '', dynamic: false, quoted: false });
  const flush = () => {
    if (word) items.push({ type: 'word', ...word });
    word = null;
  };
  const lineEnd = (from) => {
    const at = command.indexOf('\n', from);
    return at < 0 ? n : at;
  };
  // `$(…)`, `${…}` and `$name`, at `i` on the `$`: the word is dynamic, and a group is skipped whole.
  const dollar = () => {
    start().dynamic = true;
    const next = command[i + 1];
    if (next === '(' || next === '{') {
      const end = skipGroup(command, i + 2, next, next === '(' ? ')' : '}');
      if (end < 0) return false;
      i = end;
    } else {
      i++;
    }
    return true;
  };

  while (i < n) {
    const c = command[i];

    if (c === '\n') {
      flush();
      items.push({ type: 'sep' });
      i++;
      for (const heredoc of heredocs.splice(0)) i = skipHeredoc(command, i, heredoc);
      continue;
    }
    if (c === ' ' || c === '\t' || c === '\r') {
      flush();
      i++;
      continue;
    }
    // A line continuation.
    if ((c === (ps ? '`' : '\\')) && command[i + 1] === '\n') {
      i += 2;
      continue;
    }
    if (c === '#' && !word) {
      i = lineEnd(i);
      continue;
    }

    // PowerShell's here-string: its text is a word, up to a line that starts with the closing mark.
    if (ps && c === '@' && !word && (command[i + 1] === "'" || command[i + 1] === '"')) {
      const quote = command[i + 1];
      const end = command.indexOf(`\n${quote}@`, i + 2);
      if (end < 0) return null;
      start().quoted = true;
      word.dynamic = quote === '"';
      word.text = command.slice(i + 3, end);
      i = end + 3;
      continue;
    }

    if (c === "'") {
      start().quoted = true;
      let at = i + 1;
      for (;;) {
        const end = command.indexOf("'", at);
        if (end < 0) return null;
        word.text += command.slice(at, end);
        // PowerShell doubles a quote inside quotes.
        if (ps && command[end + 1] === "'") {
          word.text += "'";
          at = end + 2;
          continue;
        }
        i = end + 1;
        break;
      }
      continue;
    }

    if (c === '"') {
      start().quoted = true;
      i++;
      for (;;) {
        if (i >= n) return null;
        const d = command[i];
        if (d === '"') {
          if (ps && command[i + 1] === '"') {
            word.text += '"';
            i += 2;
            continue;
          }
          i++;
          break;
        }
        if (d === '\\' && !ps) {
          const e = command[i + 1];
          if (e !== undefined && '"\\$`\n'.includes(e)) {
            if (e !== '\n') word.text += e;
            i += 2;
          } else {
            word.text += d;
            i++;
          }
          continue;
        }
        if (d === '`' && ps) {
          if (i + 1 >= n) return null;
          word.text += command[i + 1];
          i += 2;
          continue;
        }
        if (d === '`') {
          const end = command.indexOf('`', i + 1);
          if (end < 0) return null;
          word.dynamic = true;
          i = end + 1;
          continue;
        }
        if (d === '$') {
          if (!dollar()) return null;
          continue;
        }
        word.text += d;
        i++;
      }
      continue;
    }

    if (!ps && c === '\\') {
      const e = command[i + 1];
      if (e !== undefined && ESCAPABLE.includes(e)) {
        start().quoted = true;
        word.text += e;
        i += 2;
      } else {
        start().text += c;
        i++;
      }
      continue;
    }
    if (ps && c === '`') {
      if (i + 1 >= n) return null;
      start().quoted = true;
      word.text += command[i + 1];
      i += 2;
      continue;
    }
    if (c === '$') {
      if (!dollar()) return null;
      continue;
    }
    if (!ps && c === '`') {
      const end = command.indexOf('`', i + 1);
      if (end < 0) return null;
      start().dynamic = true;
      i = end + 1;
      continue;
    }

    if (c === ';' || c === '|' || c === '&') {
      if (c === '&' && command[i + 1] === '>' && !ps) {
        // `&>` and `&>>`, bash's both-streams redirect, handled with the others below.
      } else {
        flush();
        if (ps && c === '&' && command[i + 1] !== '&') {
          // PowerShell's call operator: it starts a command, and separates nothing.
          i++;
          continue;
        }
        items.push({ type: 'sep' });
        i += (c === '&' && command[i + 1] === '&') || (c === '|' && (command[i + 1] === '|' || command[i + 1] === '&')) ? 2 : 1;
        continue;
      }
    }
    if (c === '(') {
      flush();
      items.push({ type: 'open' });
      i++;
      continue;
    }
    if (c === ')') {
      flush();
      items.push({ type: 'close' });
      i++;
      continue;
    }

    if (c === '>' || c === '<' || (c === '&' && command[i + 1] === '>') || (ps && c === '*' && command[i + 1] === '>' && !word)) {
      // A number before it names the stream: `2>file`. Any other word before it is a word of its own.
      if (word && !word.quoted && (/^\d+$/.test(word.text) || (ps && word.text === '*'))) word = null;
      else flush();

      const write = { type: 'redirect', write: true, operand: true };
      if (c === '&' || c === '*') i++;
      if (c === '<') {
        if (command.startsWith('<<<', i)) {
          i += 3;
          items.push({ ...write, write: false });
        } else if (command.startsWith('<<', i)) {
          i += 2;
          const strip = command[i] === '-';
          if (strip) i++;
          while (command[i] === ' ' || command[i] === '\t') i++;
          // The delimiter is a word: quoted or not, it is its text without the quotes.
          let delimiter = '';
          while (i < n && !' \t\r\n;|&<>()'.includes(command[i])) {
            const d = command[i];
            if (d === "'" || d === '"') {
              const end = command.indexOf(d, i + 1);
              if (end < 0) return null;
              delimiter += command.slice(i + 1, end);
              i = end + 1;
            } else if (d === '\\' && i + 1 < n) {
              delimiter += command[i + 1];
              i += 2;
            } else {
              delimiter += d;
              i++;
            }
          }
          heredocs.push({ delimiter, strip });
        } else if (command[i + 1] === '(') {
          // Process substitution: a word nobody reads.
          const end = skipGroup(command, i + 2, '(', ')');
          if (end < 0) return null;
          start().dynamic = true;
          i = end;
        } else if (command[i + 1] === '&') {
          i += 2;
          while (/[\d-]/.test(command[i] ?? '')) i++;
        } else {
          i++;
          items.push({ ...write, write: false });
        }
        continue;
      }

      // `>`, `>>`, `>|`, `>&2`, `>&-`, `>&file`, `>(…)`; at `i` on the `>`.
      i++;
      if (command[i] === '>') i++;
      else if (command[i] === '|') i++;
      else if (command[i] === '&') {
        const dup = /^&(\d+|-)/.exec(command.slice(i, i + 24));
        if (dup) {
          i += dup[0].length;
          continue;
        }
        i++;
      } else if (command[i] === '(' && !ps) {
        const end = skipGroup(command, i + 1, '(', ')');
        if (end < 0) return null;
        start().dynamic = true;
        i = end;
        continue;
      }
      items.push(write);
      continue;
    }

    start().text += c;
    i++;
  }
  flush();
  return items;
}

/** A word's text where it names a file, or null where it is not one the guard can read. */
function pathOf(word, { ps, platform, home, base }) {
  if (!word || word.dynamic) return null;
  let text = word.text;
  if (text === '' || text === '-') return null;

  const paths = platform === 'win32' ? path.win32 : path.posix;
  if (!word.quoted && (text === '~' || text.startsWith('~/') || text.startsWith('~\\'))) text = paths.join(home, text.slice(1));
  // Git Bash's own spelling of a drive: `/d/work` is `D:/work`.
  if (!ps && platform === 'win32') {
    const drive = /^\/(?:cygdrive\/)?([A-Za-z])(?:\/(.*))?$/s.exec(text);
    if (drive) text = `${drive[1].toUpperCase()}:/${drive[2] ?? ''}`;
  }
  if (NOT_FILES.test(text)) return null;
  // A relative path of an unknown directory is not one the guard can place.
  if (base === null && !paths.isAbsolute(text)) return null;
  return text;
}

const PS_SWITCHES = ['append', 'force', 'nonewline', 'noclobber', 'passthru', 'recurse', 'whatif', 'confirm', 'verbose',
  'debug', 'raw', 'asbytestream', 'usetransaction', 'container', 'r', 'f'];
const PS_PATHS = [['path', 2], ['literalpath', 2], ['filepath', 5], ['pspath', 3]];
const PS_DESTINATION = [['destination', 4]];

/** PowerShell's arguments: `-Name value` pairs (a switch takes none), and the words that are neither, in order. */
function powerShellArguments(args) {
  const named = [];
  const positional = [];
  for (let k = 0; k < args.length; k++) {
    const word = args[k];
    const parameter = word.quoted ? null : /^-([A-Za-z]+)(?::(.*))?$/s.exec(word.text);
    if (!parameter) {
      positional.push(word);
      continue;
    }
    const name = parameter[1].toLowerCase();
    if (parameter[2] !== undefined) {
      named.push([name, { ...word, text: parameter[2] }]);
    } else if (!PS_SWITCHES.some((one) => one === name || (name.length >= 2 && one.startsWith(name))) && args[k + 1]) {
      named.push([name, args[++k]]);
    }
  }
  return { named, positional };
}

/** The words named by a parameter of one of `names`: `-Pa`, `-Path` and `-PATH` are one. */
const parameterWords = (named, names) =>
  named.filter(([name]) => names.some(([full, least]) => name.length >= least && full.startsWith(name))).map(([, word]) => word);

/** A PowerShell word as the words it lists: an unquoted `a,b` is two. */
const listed = (word) => (word.quoted || !word.text.includes(',') ? [word]
  : word.text.split(',').filter(Boolean).map((text) => ({ ...word, text })));

/**
 * A shell command's write targets it can read, each `{ target, cwd }` — the path as written, and the
 * directory it is relative to, which a `cd` earlier in the command has moved. `[]` for a command it cannot
 * read; it never throws.
 *
 * @param {string} tool `Bash` or `PowerShell`
 * @param {string} command
 * @param {{ cwd?: string | null, platform?: string, home?: string }} [where]
 */
export function shellWrites(tool, command, { cwd = null, platform = process.platform, home = homedir() } = {}) {
  try {
    return readCommand(command, tool === 'PowerShell', { cwd, platform, home }, 0) ?? [];
  } catch {
    return [];
  }
}

function readCommand(command, ps, { cwd, platform, home }, depth) {
  const items = typeof command === 'string' ? lex(command, ps) : null;
  if (items === null) return null;

  const paths = platform === 'win32' ? path.win32 : path.posix;
  const writes = [];
  const stack = [];
  let here = cwd;
  const context = { ps, platform, home };

  const write = (word) => {
    const target = pathOf(word, { ...context, base: here });
    if (target !== null) writes.push({ target, cwd: here });
  };
  const writeAll = (words) => words.flatMap(ps ? listed : (word) => [word]).forEach(write);

  const move = (word) => {
    const target = pathOf(word, { ...context, base: here });
    here = target === null ? null : paths.resolve(here ?? '', target);
  };

  const run = (words, redirects) => {
    for (const redirect of redirects) if (redirect.write) write(redirect.word);

    let at = 0;
    for (; at < words.length; at++) {
      const word = words[at];
      const text = word.text;
      if ((!word.quoted && RESERVED.has(text)) || ASSIGNMENT.test(text)) continue;
      if (word.dynamic) return;
      if (!word.quoted && WRAPPERS.has(commandName(text))) {
        while (words[at + 1]?.text.startsWith('-')) at++;
        continue;
      }
      break;
    }
    if (at >= words.length) return;

    const name = commandName(words[at].text);
    const args = words.slice(at + 1);
    if (ps) powerShellCommand(name, args);
    else bashCommand(name, args);
  };

  const bashCommand = (name, args) => {
    switch (name) {
      case 'cd':
      case 'pushd': {
        const operand = args.find((one) => one.text === '--' || !one.text.startsWith('-') || one.text === '-');
        const target = operand?.text === '--' ? args[args.indexOf(operand) + 1] : operand;
        if (target === undefined) here = name === 'cd' ? home : null;
        else move(target);
        return;
      }
      case 'popd':
        here = null;
        return;
      case 'tee':
        writeAll(shellOperands(args).operands.filter((one) => one.text !== '-'));
        return;
      case 'rm':
      case 'rmdir':
      case 'unlink':
        writeAll(shellOperands(args).operands);
        return;
      case 'cp':
      case 'mv': {
        const { operands, directory } = shellOperands(args, true);
        // A `cp` writes where it copies to; a `mv` also takes its sources away from where they were.
        if (name === 'mv') writeAll(directory ? [directory, ...operands] : operands.length > 1 ? operands : []);
        else writeAll(directory ? [directory] : operands.length > 1 ? [operands[operands.length - 1]] : []);
        return;
      }
      case 'bash':
      case 'sh':
      case 'zsh':
      case 'dash':
      case 'ksh': {
        const at = args.findIndex((one) => /^-[A-Za-z]*c$/.test(one.text));
        const script = at < 0 ? null : args[at + 1];
        if (script && !script.dynamic && depth < 3) {
          writes.push(...(readCommand(script.text, false, { cwd: here, platform, home }, depth + 1) ?? []));
        }
      }
    }
  };

  const powerShellCommand = (name, args) => {
    switch (name) {
      case 'cd':
      case 'chdir':
      case 'sl':
      case 'set-location':
      case 'pushd':
      case 'push-location': {
        const { named, positional } = powerShellArguments(args);
        const target = parameterWords(named, PS_PATHS)[0] ?? positional[0];
        if (target === undefined) here = null;
        else move(target);
        return;
      }
      case 'popd':
      case 'pop-location':
        here = null;
        return;
      case 'set-content':
      case 'sc':
      case 'add-content':
      case 'ac':
      case 'out-file':
      case 'tee':
      case 'tee-object': {
        const { named, positional } = powerShellArguments(args);
        const given = parameterWords(named, PS_PATHS);
        writeAll(given.length > 0 ? given : positional.slice(0, 1));
        return;
      }
      case 'copy-item':
      case 'cp':
      case 'copy':
      case 'cpi': {
        const { named, positional } = powerShellArguments(args);
        const given = parameterWords(named, PS_DESTINATION);
        writeAll(given.length > 0 ? given : positional.slice(1, 2));
        return;
      }
      case 'move-item':
      case 'mv':
      case 'move':
      case 'mi': {
        const { named, positional } = powerShellArguments(args);
        const sources = parameterWords(named, PS_PATHS);
        const destination = parameterWords(named, PS_DESTINATION);
        writeAll([...(sources.length > 0 ? sources : positional.slice(0, 1)), ...(destination.length > 0 ? destination : positional.slice(1, 2))]);
        return;
      }
      case 'remove-item':
      case 'rm':
      case 'del':
      case 'erase':
      case 'ri':
      case 'rmdir':
      case 'rd': {
        const { named, positional } = powerShellArguments(args);
        const given = parameterWords(named, PS_PATHS);
        writeAll(given.length > 0 ? given : positional.slice(0, 1));
      }
    }
  };

  let words = [];
  let redirects = [];
  let pending = null;
  const finish = () => {
    pending = null;
    if (words.length > 0 || redirects.length > 0) run(words, redirects);
    words = [];
    redirects = [];
  };

  for (const item of items) {
    if (item.type === 'word') {
      if (pending) {
        redirects.push({ write: pending.write, word: item });
        pending = null;
      } else {
        words.push(item);
      }
    } else if (item.type === 'redirect') {
      pending = item;
    } else {
      finish();
      if (item.type === 'open') stack.push(here);
      else if (item.type === 'close' && stack.length > 0) here = stack.pop();
    }
  }
  finish();
  return writes;
}

/** A command's name: its file name, without the folder, the extension or a backslash that skips an alias. */
function commandName(text) {
  return text.replace(/^.*[\\/]/, '').replace(/\.exe$/i, '').toLowerCase();
}

/**
 * The operands of a `tee`, `rm`, `cp` or `mv`: the words that are no option, and `-t DIR` or
 * `--target-directory=DIR` where the command takes one. After `--` every word is an operand.
 */
function shellOperands(args, takesDirectory = false) {
  const operands = [];
  let directory = null;
  let options = true;
  for (let k = 0; k < args.length; k++) {
    const word = args[k];
    const text = word.text;
    if (!options || !text.startsWith('-') || text === '-') {
      operands.push(word);
    } else if (text === '--') {
      options = false;
    } else if (takesDirectory && text.startsWith('--target-directory=')) {
      directory = { ...word, text: text.slice('--target-directory='.length) };
    } else if (takesDirectory && (text === '--target-directory' || /^-[A-Za-z]*t$/.test(text))) {
      directory = args[++k] ?? null;
    } else if (text === '--suffix' || /^-[A-Za-z]*S$/.test(text)) {
      k++;
    }
  }
  return { operands, directory };
}

function deny(reason) {
  process.stdout.write(`${JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'deny',
      permissionDecisionReason: `Daoris's tree guard: ${reason}`,
    },
  })}\n`);
}

async function main() {
  let text = '';
  for await (const chunk of process.stdin) text += chunk;

  let call;
  try {
    call = JSON.parse(text);
  } catch {
    // Fail closed where this guard is the one deciding: the harness only sends it file writes and shell calls.
    deny('it could not read this call, so it refuses it rather than guess.');
    return;
  }

  // The session's tree, as the driver named it — then the harness's own project directory. Each argument
  // after it is a declared write target (D107).
  const tree = process.argv[2] || process.env.CLAUDE_PROJECT_DIR;
  const declared = process.argv.slice(3).filter((one) => one.length > 0);
  const cwd = typeof call?.cwd === 'string' && call.cwd.length > 0 ? call.cwd : null;

  // What each tool writes, and the directory a relative path of it is relative to. A shell's is the one it
  // stands in, a relative path of it the harness's own cwd's, and only then the tree's.
  const found = SHELL_TOOLS.includes(call?.tool_name)
    ? shellWrites(call.tool_name, call.tool_input?.command, { cwd: cwd ?? tree ?? null })
    : targets(call?.tool_name, call?.tool_input).map((target) => ({ target, cwd }));
  if (found.length === 0) return;

  if (!tree) {
    deny('it was not told which tree this session works in, so it refuses every write rather than guess.');
    return;
  }

  for (const { target, cwd: base } of found) {
    let judged;
    try {
      judged = [tree, ...declared].map((root) => judge({ tree: root, target, cwd: base, realpath: resolveThroughLinks }));
    } catch (error) {
      // Fail closed: a path this guard cannot resolve is not one it lets through.
      deny(`it could not resolve \`${target}\` (${error instanceof Error ? error.message : error}), so it refuses the write.`);
      return;
    }
    if (judged.every(({ outside }) => outside)) {
      const also = declared.length === 0 ? ''
        : ` and the checkouts its repository may also write into (${declared.map((one) => `\`${one}\``).join(', ')})`;
      deny(`\`${judged[0].resolved}\` is outside this session's tree \`${tree}\`${also}. A session writes only inside `
        + 'its own tree; a change needed elsewhere is a quest to whoever owns it.');
      return;
    }
  }
}

// Run only as the hook — imported, it is the judgement alone.
const same = (a, b) => (process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b);
if (process.argv[1] && same(path.resolve(process.argv[1]), fileURLToPath(import.meta.url))) {
  await main();
}
