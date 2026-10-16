/**
 * The fabric probe's receipt summariser (FABRIC-PROBE1; the C0 audit §2).
 *
 *   node tools/fabric-probe/receipt.mjs <run folder>
 *
 * Reads what programs wrote in one run folder (`launch.mjs`'s layout): the stand-in server's log, the hook log, the
 * harness's own event stream and `run.json`. Never the models' reports. It answers the audit's questions from them:
 * how many server processes and `initialize`s, which session each was started as, every call with the lineage its
 * `_meta` carried, each Claude call joined to its agent through the hook's `tool_use_id`, the subagents the stream and the
 * hooks announced, and what outlived a kill.
 *
 * 🔴 A receipt names ids, never a path: a `_meta` value that names a place on a disk (Codex's turn metadata carries the
 * workspace's) is counted as dropped, and not printed, so a receipt can be pasted into a decision (D46, D47).
 */
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from '../fsx.mjs';

/** The lines of a JSON-lines file that parse; none for a file that is not there. */
export function jsonLines(file) {
  if (!existsSync(file)) return [];
  return readFileSync(file, 'utf8').split('\n').flatMap((line) => {
    if (!line.trim()) return [];
    try {
      return [JSON.parse(line)];
    } catch {
      return [];
    }
  });
}

/** Whether a string names a place on a disk: a drive, a UNC share, a home, or a rooted path with a folder. */
export const pathLike = (value) => typeof value === 'string'
  && (/^[A-Za-z]:[\\/]/.test(value) || /^\\\\/.test(value) || /^~[\\/]/.test(value) || /^\/[^/\s]+\//.test(value));

/**
 * A value with every path-like string taken out, and how many went: objects and arrays walked, the rest kept as written.
 */
export function withoutPaths(value) {
  let dropped = 0;
  const walk = (item) => {
    if (pathLike(item)) {
      dropped++;
      return undefined;
    }
    if (Array.isArray(item)) return item.map(walk).filter((entry) => entry !== undefined);
    if (item !== null && typeof item === 'object') {
      return Object.fromEntries(Object.entries(item).map(([key, entry]) => [key, walk(entry)]).filter(([, entry]) => entry !== undefined));
    }
    return item;
  };
  return { value: walk(value), dropped };
}

/** Codex's turn metadata, which arrives as an object or as JSON text. */
function turnMetadata(meta) {
  const raw = meta?.['x-codex-turn-metadata'];
  if (typeof raw !== 'string') return raw ?? null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

/** The lineage one call's `_meta` carried, by name, with its paths dropped and counted. */
export function callLineage(meta) {
  if (meta === null || typeof meta !== 'object') return { keys: [], dropped: 0 };
  const turn = turnMetadata(meta);
  const { value: clean, dropped } = withoutPaths({ ...meta, ...(turn ? { 'x-codex-turn-metadata': turn } : {}) });
  const codex = clean['x-codex-turn-metadata'] ?? null;
  return {
    keys: Object.keys(meta).sort(),
    toolUseId: clean['claudecode/toolUseId'] ?? null,
    thread: codex?.thread_id ?? null,
    parent: codex?.parent_thread_id ?? null,
    forkedFrom: codex?.forked_from_thread_id ?? null,
    kind: codex?.subagent_kind ?? null,
    source: codex?.thread_source ?? null,
    root: codex?.session_id ?? null,
    codexKeys: codex ? Object.keys(turn).sort() : [],
    dropped,
  };
}

/** The receipt of one run folder. */
export function receipt(folder) {
  const run = existsSync(join(folder, 'run.json')) ? JSON.parse(readFileSync(join(folder, 'run.json'), 'utf8')) : {};
  const log = jsonLines(join(folder, 'server.log'));
  const hooks = jsonLines(join(folder, 'hooks.log')).map((line) => line.input).filter(Boolean);
  const stream = jsonLines(join(folder, 'stream.jsonl'));

  const servers = new Map();
  const server = (pid) => {
    if (!servers.has(pid)) servers.set(pid, { pid, session: null, initializes: 0, calls: 0, inputEnd: false, exit: null, cancelled: 0 });
    return servers.get(pid);
  };
  const calls = [];
  for (const entry of log) {
    const held = server(entry.pid);
    if (entry.event === 'start') held.session = entry.env?.DAORIS_SESSION_ID ?? null;
    if (entry.event === 'input-end') held.inputEnd = true;
    if (entry.event === 'exit') held.exit = entry.code;
    if (entry.event !== 'in') continue;
    const message = entry.message ?? {};
    if (message.method === 'initialize') held.initializes++;
    if (message.method === 'notifications/cancelled') held.cancelled++;
    if (message.method !== 'tools/call') continue;
    held.calls++;
    calls.push({
      pid: entry.pid,
      session: held.session,
      tool: message.params?.name ?? null,
      note: message.params?.arguments?.note ?? null,
      lineage: callLineage(message.params?._meta),
    });
  }
  for (const after of run.serversAfter ?? []) server(after.pid).aliveAfter = after.alive;

  // Claude: the hook's `tool_use_id` is the call's `_meta` toolUseId, so each call meets the agent that made it. A
  // `PreToolUse` without `agent_id` is the root's own.
  const preToolUse = hooks.filter((input) => input.hook_event_name === 'PreToolUse');
  for (const call of calls) {
    if (!call.lineage.toolUseId) continue;
    const hook = preToolUse.find((input) => input.tool_use_id === call.lineage.toolUseId);
    call.agent = hook ? (hook.agent_id ?? 'root') : 'unjoined';
    call.agentType = hook?.agent_type ?? null;
  }

  const counted = {};
  const keys = {};
  for (const input of hooks) {
    const name = input.hook_event_name ?? 'unnamed';
    counted[name] = (counted[name] ?? 0) + 1;
    keys[name] = [...new Set([...(keys[name] ?? []), ...Object.keys(input)])].sort();
  }
  const agents = [...new Map(hooks.filter((input) => input.agent_id).map((input) => [input.agent_id, input.agent_type ?? null])).entries()]
    .map(([id, type]) => ({ id, type }));

  const tasks = stream
    .filter((line) => line.type === 'system' && line.subtype === 'task_started')
    .map((line) => ({
      task: line.task_id ?? null, type: line.task_type ?? null, depth: line.spawn_depth ?? null,
      parent: line.parent_task_id ?? null, toolUseId: line.tool_use_id ?? null,
    }));
  const denied = stream
    .filter((line) => line.type === 'system' && line.subtype === 'permission_denied')
    .map((line) => ({ tool: line.tool_name ?? null, agent: line.agent_id ?? null }));
  const result = [...stream].reverse().find((line) => line.type === 'result');
  const codexUsage = stream.filter((line) => line.type === 'turn.completed' && line.usage).map((line) => line.usage);
  const usage = result
    ? { turns: result.num_turns ?? null, usd: result.total_cost_usd ?? null }
    : codexUsage.length > 0
      ? {
        input: codexUsage.reduce((sum, used) => sum + (used.input_tokens ?? 0), 0),
        output: codexUsage.reduce((sum, used) => sum + (used.output_tokens ?? 0), 0),
      }
      : null;

  // What Daoris's own connector and the driver's lineage hook wrote, where the run had them (FABRIC-C1a): lines by writer.
  const lineage = run.lineageSession
    ? jsonLines(join(folder, 'home', 'sessions', run.lineageSession, 'lineage.jsonl'))
    : [];
  const lineageBy = {};
  for (const line of lineage) {
    const name = `${line.by}${line.event ? ` ${line.event}` : ''}`;
    lineageBy[name] = (lineageBy[name] ?? 0) + 1;
  }

  return {
    harness: run.harness ?? null, version: run.version ?? null, scenario: run.scenario ?? null, model: run.model ?? null,
    seconds: run.seconds ?? null, exit: run.exit ?? null, killed: run.killed ?? null, usage,
    lineage: run.lineageSession ? lineageBy : null,
    servers: [...servers.values()], calls, hooks: { counted, keys, agents }, tasks, denied,
    verdicts: verdicts([...servers.values()], calls, agents),
  };
}

/** What the run shows, in the audit's terms. */
function verdicts(servers, calls, agents) {
  const said = [];
  const root = calls.find((call) => call.agent === 'root' || (call.lineage.thread && !call.lineage.parent));
  const children = calls.filter((call) => (call.agent && call.agent !== 'root' && call.agent !== 'unjoined') || call.lineage.parent);
  if (children.length === 0) {
    said.push('no child call reached the server');
  } else {
    const processes = new Set(children.map((call) => call.pid)).size;
    said.push(!root
      ? `children's calls reached ${processes} server process(es), and no root call reached this server to compare with`
      : children.every((call) => call.pid === root.pid)
        ? `a child's call reached the root's own server process (pid ${root.pid}): the same connection`
        : `children's calls reached ${processes} server process(es) of their own`);
    const sessions = new Set(calls.map((call) => call.session));
    said.push(`the server's session was ${[...sessions].join(', ')} for every call: the environment does not tell a child apart`);
  }
  const joined = calls.filter((call) => call.lineage.toolUseId);
  if (joined.length > 0) {
    const met = joined.filter((call) => call.agent !== 'unjoined').length;
    said.push(`the hook's tool_use_id met the call's _meta toolUseId for ${met} of ${joined.length} calls`);
  }
  if (agents.length > 0) said.push(`the hooks named ${agents.length} agent id(s): ${agents.map((agent) => agent.id).join(', ')}`);
  const threads = new Set(calls.map((call) => call.lineage.thread).filter(Boolean));
  if (threads.size > 0) said.push(`_meta named ${threads.size} thread(s), each child's parent beside it`);
  const dropped = calls.reduce((sum, call) => sum + call.lineage.dropped, 0);
  if (dropped > 0) said.push(`${dropped} path value(s) in _meta, dropped from this receipt`);
  for (const held of servers) {
    if (held.aliveAfter === true) said.push(`server pid ${held.pid} outlived the run`);
  }
  return said;
}

/** The receipt as lines a person reads. */
export function receiptText(seen) {
  const lines = [];
  const usage = seen.usage
    ? seen.usage.usd !== undefined ? `${seen.usage.turns} turns, ${seen.usage.usd} USD` : `${seen.usage.input} input and ${seen.usage.output} output tokens`
    : 'usage not measured';
  lines.push(`fabric-probe receipt: ${seen.harness} ${seen.version ?? '(version unknown)'}, scenario ${seen.scenario}, `
    + `${seen.seconds} s, exit ${seen.exit}${seen.killed ? `, killed (${seen.killed.mode}) at ${seen.killed.seconds} s` : ''}; ${usage}`);
  lines.push(`servers: ${seen.servers.length} process(es)`);
  for (const held of seen.servers) {
    lines.push(`  pid ${held.pid}: started as ${held.session ?? '(no session)'}, ${held.initializes} initialize, ${held.calls} call(s), `
      + `${held.cancelled} cancel(s), input end ${held.inputEnd ? 'seen' : 'not seen'}, exit ${held.exit ?? 'not logged'}`
      + `${held.aliveAfter === undefined ? '' : held.aliveAfter ? ', alive after the run' : ', gone after the run'}`);
  }
  lines.push(`calls: ${seen.calls.length}`);
  for (const call of seen.calls) {
    const lineage = call.lineage;
    const parts = [
      `${call.tool} note ${call.note ?? '-'}`, `pid ${call.pid}`,
      lineage.toolUseId ? `toolUseId ${lineage.toolUseId}` : null,
      call.agent ? `agent ${call.agent}${call.agentType ? ` (${call.agentType})` : ''}` : null,
      lineage.thread ? `thread ${lineage.thread}` : null,
      lineage.parent ? `parent ${lineage.parent}` : null,
      lineage.kind ? `kind ${lineage.kind}` : null,
      lineage.source ? `source ${lineage.source}` : null,
      `_meta keys ${lineage.keys.join(', ') || 'none'}`,
    ];
    lines.push(`  ${parts.filter(Boolean).join('; ')}`);
  }
  const counted = Object.entries(seen.hooks.counted).map(([name, count]) => `${name} ${count}`).join(', ');
  lines.push(`hooks: ${counted || 'none'}`);
  for (const [name, keys] of Object.entries(seen.hooks.keys)) lines.push(`  ${name} keys: ${keys.join(', ')}`);
  for (const task of seen.tasks) {
    lines.push(`stream: task_started ${task.type} task ${task.task} depth ${task.depth ?? '-'} parent ${task.parent ?? '-'} tool ${task.toolUseId ?? '-'}`);
  }
  for (const denial of seen.denied) lines.push(`stream: permission_denied ${denial.tool} agent ${denial.agent ?? '-'}`);
  if (seen.lineage) {
    const written = Object.entries(seen.lineage).map(([name, count]) => `${name} ${count}`).join(', ');
    lines.push(`lineage file: ${written || 'nothing written'}`);
  }
  for (const verdict of seen.verdicts) lines.push(`verdict: ${verdict}`);
  return `${lines.join('\n')}\n`;
}

if (isMain(import.meta.url)) {
  const folder = process.argv[2];
  if (!folder || !existsSync(folder)) {
    process.stderr.write('usage: node tools/fabric-probe/receipt.mjs <run folder>\n');
    process.exit(2);
  }
  process.stdout.write(receiptText(receipt(folder)));
}
