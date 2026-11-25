/**
 * What a test file's text does that a unit test may not: starts a process, reads git through a helper that starts it, waits on the
 * wall clock. A net over calls read by their bare name, with strings and comments blanked: a start behind a helper of another file, or a
 * wait a library makes, passes it.
 */

const LITERALS = /\/\/[^\n]*|\/\*[\s\S]*?\*\/|'(?:[^'\\\n]|\\.)*'|"(?:[^"\\\n]|\\.)*"/g;

/** The text between the parenthesis at `at` and its pair, the pair found in `code` (comments and strings blanked), the text read from `text`. */
function argumentsAt(code, text, at) {
  let depth = 0;
  for (let i = at; i < code.length; i++) {
    if (code[i] === '(') depth++;
    else if (code[i] === ')' && --depth === 0) return text.slice(at + 1, i).trim();
  }
  return text.slice(at + 1).trim();
}

/** `text` with each comment and string blanked, its lines kept. */
export const blanked = (text) => text.replace(LITERALS, (each) => each.replace(/[^\n]/g, ' '));

/** Each call of `name` in `text` by that bare name (a method, a declaration, a string or a comment is none): its line and its arguments. */
export function callsOf(text, name) {
  const code = blanked(text);
  const found = [];
  for (const match of code.matchAll(new RegExp(`(?<![.\\w$])${name}\\s*\\(`, 'g'))) {
    const before = code.slice(code.lastIndexOf('\n', match.index) + 1, match.index).trim();
    if (/(?:^|\s)function\*?$/.test(before)) continue;
    found.push({ line: text.slice(0, match.index).split('\n').length, args: argumentsAt(code, text, match.index + match[0].length - 1) });
  }
  return found;
}

/** The runtime's own starts, a call of which in a file that imports `child_process` starts one. */
const STARTS = ['spawn', 'spawnSync', 'execFile', 'execFileSync', 'exec', 'execSync', 'fork'];

/** The helpers that start git for a caller, which a file defining one of its own starts through only where it starts processes itself. */
const GIT = ['spawnGit', 'execGit', 'git', 'gitSight', 'repositoryFiles', 'sourceReader'];

/** The timers a test waits on; fake timers make them a clock the test moves. */
const CLOCK = ['setTimeout', 'setInterval', 'sleep'];

/** Whether a timer call's `args` ask for no time: a callback alone or a delay of 0, which yields to the loop and waits on nothing. */
const yields = (args) => !args.includes(',') || /,\s*0\s*$/.test(args);

const declared = (code, name) => new RegExp(`\\b(?:function\\*?|const|let|var)\\s+${name}\\b`).test(code);

/** Each site a test text does one of the acts with, as `{ line, name }` per act, in line order. */
export function readActs(text) {
  const code = blanked(text);
  const spawns = /\bchild_process\b/.test(text);
  const sites = (names, keep = () => true) => names.flatMap((name) => callsOf(text, name).filter((call) => keep(call, name)).map((call) => ({ line: call.line, name }))).sort((x, y) => x.line - y.line);
  return {
    process: spawns ? sites(STARTS) : [],
    git: sites(GIT.filter((name) => spawns || !declared(code, name))),
    clock: /\buseFakeTimers\b/.test(text) ? [] : sites(CLOCK, (call, name) => name === 'sleep' || !yields(call.args)),
  };
}

/** The acts, in the order they are said. */
export const ACTS = Object.freeze(['process', 'git', 'clock']);
