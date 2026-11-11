// Daoris's permission ask: under `-p` the harness refuses a call its rules do not allow, having no wire to ask on, so this hook asks
// Daoris instead. Run on `PreToolUse` for every tool as
//  node permission-ask.mjs <requests folder> <session> <settings file> <bound seconds>
// with the hook's input as JSON on stdin. For a call the harness would ask about it writes a `permit` request to the session
// requests box and waits beside it up to the hold's bound: allow lets the call run, anything else refuses it, saying why.
// Zero dependencies: it runs inside every session that is handed it.
// 🔴 A call it does not judge as asked gets no word from it: nothing on stdout, exit 0, and the harness decides as its rules say.

import { existsSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { randomBytes } from 'node:crypto';
import { fileURLToPath } from 'node:url';

// The tools that never ask in any mode, and those `acceptEdits` takes unasked.
const READS = new Set(['Read', 'Glob', 'Grep', 'LS', 'NotebookRead', 'TodoWrite', 'TodoRead', 'Task', 'Agent', 'WebSearch',
  'ExitPlanMode', 'BashOutput', 'KillShell', 'KillBash', 'ListMcpResourcesTool', 'ReadMcpResourceTool', 'Skill', 'SlashCommand']);
const EDITS = new Set(['Edit', 'Write', 'MultiEdit', 'NotebookEdit']);
// The modes that ask nothing of the person: the harness decides alone.
const UNASKED = new Set(['bypassPermissions', 'plan', 'dontAsk']);
const POLL_MS = 500;

/** One rule as Claude Code writes it, `Tool` or `Tool(specifier)`: its tool and specifier, or null for one it cannot read. */
export function ruleOf(text) {
  const match = /^([^()\s]+)(?:\((.*)\))?$/s.exec(String(text ?? '').trim());
  return match ? { tool: match[1], spec: match[2] } : null;
}

/** Whether `rule` covers the call: its tool, and a specifier it reads (a Bash command, a fetch's domain, a path pattern). */
export function covers(rule, tool, input, cwd) {
  if (!rule) return false;
  const edit = rule.tool === 'Edit' && EDITS.has(tool);
  if (rule.tool !== tool && !edit && !(rule.tool.startsWith('mcp__') && (tool === rule.tool || tool.startsWith(`${rule.tool.replace(/__\*$/, '')}__`)))) {
    return false;
  }
  if (rule.spec === undefined || rule.spec === '*' || rule.spec === '') return true;
  if (tool === 'Bash' || tool === 'PowerShell') {
    const command = String(input?.command ?? '').trim();
    const spec = rule.spec.trim();
    if (spec.endsWith(':*')) return command === spec.slice(0, -2) || command.startsWith(`${spec.slice(0, -2)} `);
    if (spec.endsWith('*')) return command.startsWith(spec.slice(0, -1));
    return command === spec;
  }
  if (tool === 'WebFetch' && rule.spec.startsWith('domain:')) {
    try {
      return new URL(String(input?.url ?? '')).hostname === rule.spec.slice('domain:'.length);
    } catch {
      return false;
    }
  }
  const target = pathOf(input);
  return target !== null && pathMatches(rule.spec, target, cwd);
}

/** The file a call names, for the tools a path rule covers; null where it names none. */
function pathOf(input) {
  const named = input?.file_path ?? input?.notebook_path ?? input?.path;
  return typeof named === 'string' && named ? named : null;
}

const slashed = (text) => (process.platform === 'win32' ? text.replace(/\\/g, '/').toLowerCase() : text);

/** A gitignore-style pattern as a regular expression: `**` crosses folders, `*` and `?` stay inside one. */
function patternOf(pattern) {
  let source = '';
  for (let at = 0; at < pattern.length; at++) {
    const here = pattern[at];
    if (here === '*' && pattern[at + 1] === '*') {
      const folders = pattern[at + 2] === '/';
      source += folders ? '(?:.*/)?' : '.*';
      at += folders ? 2 : 1;
    } else if (here === '*') source += '[^/]*';
    else if (here === '?') source += '[^/]';
    else source += here.replace(/[\\^$.|+()[\]{}]/g, '\\$&');
  }
  return new RegExp(`^${source}$`);
}

/**
 * Whether a path rule's pattern covers `target`, as the harness reads one: `//x` from the filesystem root, `~/x` from the home, `/x`
 * and `x` from the session's folder (`cwd`), and a pattern with no slash at any depth. A pattern it cannot place covers nothing.
 */
export function pathMatches(spec, target, cwd, home = process.env.HOME ?? process.env.USERPROFILE ?? '') {
  const base = typeof cwd === 'string' && cwd ? slashed(cwd).replace(/\/+$/, '') : null;
  const absolute = (text) => /^([a-z]:)?\//i.test(text);
  let pattern = slashed(spec.trim());
  if (pattern.startsWith('//')) pattern = pattern.slice(1);
  else if (pattern.startsWith('~/')) pattern = `${slashed(home).replace(/\/+$/, '')}/${pattern.slice(2)}`;
  else if (pattern.startsWith('/')) {
    if (base === null) return false;
    pattern = `${base}${pattern}`;
  } else {
    if (base === null) return false;
    pattern = pattern.startsWith('./') ? `${base}/${pattern.slice(2)}` : pattern.includes('/') ? `${base}/${pattern}` : `${base}/**/${pattern}`;
  }
  let file = slashed(target);
  if (!absolute(file)) {
    if (base === null) return false;
    file = `${base}/${file.replace(/^\.\//, '')}`;
  }
  return patternOf(pattern).test(file);
}

/** Whether the harness would ask about this call under `permission_mode`, given the rules. */
export function wouldAsk(input, rules) {
  const tool = String(input?.tool_name ?? '');
  const mode = String(input?.permission_mode ?? 'default');
  if (!tool || UNASKED.has(mode) || READS.has(tool)) return false;
  if (mode === 'acceptEdits' && EDITS.has(tool)) return false;
  const any = (list) => (Array.isArray(list) ? list : []).some((text) => covers(ruleOf(text), tool, input?.tool_input, input?.cwd));
  if (any(rules?.deny)) return false;
  if (any(rules?.ask)) return true;
  return !any(rules?.allow);
}

/** The rules a settings file hands: its `permissions`, or none where it does not read. */
export function rulesOf(file) {
  try {
    return JSON.parse(readFileSync(file, 'utf8'))?.permissions ?? {};
  } catch {
    return {};
  }
}

/** The request the loop holds for the person: a `permit` for the session, keyed, naming the tool and the call. */
export function requestOf(session, key, input, at = new Date()) {
  const command = input?.tool_input?.command ?? input?.tool_input?.file_path ?? input?.tool_input?.url;
  const title = `${input?.tool_name ?? 'a tool'}${typeof command === 'string' ? `: ${command}` : ''}`;
  return { session, move: 'permit', parked: false, by: 'hook', at: at.toISOString(), text: title.slice(0, 160), key, operation: String(input?.tool_name ?? '') };
}

/** The harness's decision on the answer the loop wrote, or a refusal saying nobody answered. */
export function decisionOf(answer, bound) {
  const allowed = answer?.done === true;
  const reason = answer
    ? (typeof answer.said === 'string' && answer.said ? answer.said : allowed ? 'Allowed once by the person.' : 'Refused by the person.')
    : `Nobody answered within ${Math.round(bound / 60)} minutes, so Daoris refused it.`;
  return { hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: allowed ? 'allow' : 'deny', permissionDecisionReason: reason } };
}

/** Write the request, then wait for its answer up to `bound` seconds; the request is withdrawn where nobody took it. */
export async function ask(folder, session, input, bound, sleep = (ms) => new Promise((done) => setTimeout(done, ms))) {
  const key = randomBytes(6).toString('hex');
  const file = path.join(folder, `${session}.${key}.json`);
  const answered = path.join(folder, `${session}.${key}.answer.json`);
  writeFileSync(`${file}.writing`, JSON.stringify(requestOf(session, key, input)));
  renameSync(`${file}.writing`, file);
  const until = Date.now() + bound * 1000;
  while (Date.now() < until) {
    if (existsSync(answered)) {
      try {
        const answer = JSON.parse(readFileSync(answered, 'utf8'));
        rmSync(answered, { force: true });
        return answer;
      } catch {
        // Read while it is written: read again at the next look.
      }
    }
    await sleep(POLL_MS);
  }
  rmSync(file, { force: true });
  return null;
}

async function main() {
  const [folder, session, settings, boundText] = process.argv.slice(2);
  if (!folder || !session || !settings) return;
  let input;
  try {
    input = JSON.parse(readFileSync(0, 'utf8'));
  } catch {
    return;
  }
  if (!wouldAsk(input, rulesOf(settings))) return;
  const bound = Number(boundText) > 0 ? Number(boundText) : 900;
  process.stdout.write(JSON.stringify(decisionOf(await ask(folder, session, input, bound), bound)));
}

// Run only as the hook — imported, it is its judges alone.
const same = (a, b) => (process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b);
if (process.argv[1] && same(path.resolve(process.argv[1]), fileURLToPath(import.meta.url))) {
  main().then(() => process.exit(0), () => process.exit(0));
}
