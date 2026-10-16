/**
 * FABRIC-PROBE1: the fabric probe kit, without a harness — the stand-in server and the hook logger as real processes, the
 * launchers' environments and arguments, and the receipt over logs shaped as the C0 audit's runs wrote them.
 *
 *   node --test tools/fabric-probe.test.mjs
 *
 * Run by `npm run verify`. No model is asked and no account is spent; a real run is `tools/fabric-probe/launch.mjs`.
 */
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  HOOK, SERVER, childEnvironment, claudeLaunch, codexLaunch, connectorSession, lineageFile, parseArguments, prompt, runFiles, toml,
} from './fabric-probe/launch.mjs';
import { callLineage, receipt, receiptText, withoutPaths } from './fabric-probe/receipt.mjs';
import { TOKEN, answer } from './fabric-probe/server.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'fabric-probe-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const lines = (file) => readFileSync(file, 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line));

test('the server answers each tool with its process token and logs every message it received, then its end', async () => {
  const log = join(scratch, 'server.log');
  const server = spawn(process.execPath, [SERVER, '--log', log], { stdio: ['pipe', 'pipe', 'inherit'], windowsHide: true });
  let out = '';
  server.stdout.on('data', (chunk) => { out += chunk; });
  const send = (message) => server.stdin.write(`${JSON.stringify(message)}\n`);
  send({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18' } });
  send({ jsonrpc: '2.0', method: 'notifications/initialized' });
  send({ jsonrpc: '2.0', id: 2, method: 'tools/list' });
  send({ jsonrpc: '2.0', id: 3, method: 'tools/call', params: { name: 'probe_read', arguments: { note: 'root' }, _meta: { 'claudecode/toolUseId': 'toolu_1' } } });
  send({ jsonrpc: '2.0', id: 4, method: 'tools/call', params: { name: 'probe_wait', arguments: { seconds: 0 } } });
  await new Promise((done) => {
    const look = setInterval(() => {
      if (out.split('\n').filter(Boolean).length >= 4) {
        clearInterval(look);
        done();
      }
    }, 20);
  });
  server.stdin.end();
  const code = await new Promise((done) => server.on('exit', done));
  assert.equal(code, 0);

  const replies = out.split('\n').filter(Boolean).map((line) => JSON.parse(line));
  assert.deepEqual(replies.map((reply) => reply.id).sort(), [1, 2, 3, 4]);
  assert.deepEqual(replies.find((reply) => reply.id === 2).result.tools.map((tool) => tool.name),
    ['probe_read', 'probe_write', 'probe_unlisted', 'probe_wait']);
  const token = replies.find((reply) => reply.id === 3).result.content[0].text;
  assert.match(token, new RegExp(`^${TOKEN}-[0-9a-f]{6} \\(probe_read\\)$`));

  const logged = lines(log);
  assert.equal(logged[0].event, 'start');
  assert.ok(logged.some((entry) => entry.event === 'in' && entry.message.method === 'notifications/initialized'),
    'a notification is logged, though nothing answers it');
  assert.deepEqual(logged.find((entry) => entry.event === 'in' && entry.message.id === 3).message.params._meta, { 'claudecode/toolUseId': 'toolu_1' });
  assert.deepEqual(logged.slice(-2).map((entry) => entry.event), ['input-end', 'exit']);
});

test('the server answers an unknown method and an unknown tool with an error, and a notification with nothing', async () => {
  assert.equal(await answer({ jsonrpc: '2.0', method: 'notifications/cancelled' }, 'abc'), null);
  assert.equal((await answer({ jsonrpc: '2.0', id: 1, method: 'resources/list' }, 'abc')).error.code, -32601);
  assert.equal((await answer({ jsonrpc: '2.0', id: 1, method: 'tools/call', params: { name: 'nope' } }, 'abc')).error.code, -32602);
  let held = null;
  const reply = await answer({ jsonrpc: '2.0', id: 2, method: 'tools/call', params: { name: 'probe_wait', arguments: {} } }, 'abc', async (seconds) => { held = seconds; });
  assert.equal(held, 60, 'probe_wait holds a minute unless told');
  assert.equal(reply.result.content[0].text, `${TOKEN}-abc (probe_wait)`);
});

test('the hook logger appends the input it was handed and decides nothing', () => {
  const log = join(scratch, 'hooks.log');
  const input = { hook_event_name: 'PreToolUse', tool_name: 'mcp__probe__probe_read', tool_use_id: 'toolu_1', agent_id: 'a1', agent_type: 'general-purpose' };
  const ran = spawnSync(process.execPath, [HOOK, log], { input: JSON.stringify(input), encoding: 'utf8', windowsHide: true });
  assert.equal(ran.status, 0);
  assert.equal(ran.stdout, '', 'no decision on stdout');
  assert.deepEqual(lines(log)[0].input, input);
});

test('a probe starts as a root with its own home: the session\'s CLAUDE variables and every inherited DAORIS variable go', () => {
  const env = childEnvironment({
    PATH: 'p', CLAUDECODE: '1', CLAUDE_CODE_SESSION_ID: 's', claude_pid: '9', DAORIS_HOME: 'live', DAORIS_SERVICE_URL: 'u', ANTHROPIC_MODEL: 'm',
  }, 'scratch-home');
  assert.deepEqual(env, { PATH: 'p', ANTHROPIC_MODEL: 'm', DAORIS_HOME: 'scratch-home' });
});

test('the Claude launch is the pipe door\'s shape: its servers alone, its settings alone, the logger on every hook', () => {
  const files = runFiles(join('runs', 'r1'));
  const { mcp, settings, args } = claudeLaunch({ files, run: 'r1', text: 'go', model: 'claude-sonnet-5-5', node: 'node' });
  for (const flag of ['--strict-mcp-config', '--verbose']) assert.ok(args.includes(flag), flag);
  assert.equal(args[args.indexOf('--output-format') + 1], 'stream-json');
  assert.equal(args[args.indexOf('--setting-sources') + 1], 'local');
  assert.equal(args[args.indexOf('--model') + 1], 'claude-sonnet-5-5');
  assert.equal(mcp.mcpServers.probe.env.DAORIS_SESSION_ID, 'probe-root-r1');
  assert.equal(mcp.mcpServers.probe.env.DAORIS_HOME, files.home);
  assert.deepEqual(settings.permissions.deny, ['mcp__probe__probe_write']);
  assert.ok(!settings.permissions.allow.includes('mcp__probe__probe_unlisted'), 'the unlisted tool is named by no rule');
  for (const event of ['PreToolUse', 'SubagentStart', 'SubagentStop']) {
    assert.deepEqual(settings.hooks[event][0].hooks[0].args, [HOOK, files.hookLog], event);
  }
  assert.equal(settings.hooks.PreToolUse[0].matcher, '*');
});

test('the Codex launch sets the stand-in by -c with its write disabled and its tools approved, never ephemeral', () => {
  const files = runFiles(join('runs', 'r2'));
  const { args } = codexLaunch({ files, run: 'r2', text: 'go', model: 'gpt-6.1-sol', effort: 'low', node: 'node' });
  assert.deepEqual(args.slice(0, 5), ['exec', '--json', '--sandbox', 'read-only', '--ignore-user-config']);
  assert.ok(!args.includes('--ephemeral'));
  const set = args.filter((_, at) => args[at - 1] === '-c');
  assert.ok(set.includes('mcp_servers.probe.disabled_tools=["probe_write"]'));
  assert.ok(set.includes('mcp_servers.probe.default_tools_approval_mode="approve"'));
  assert.ok(set.includes(`mcp_servers.probe.env={ DAORIS_SESSION_ID = "probe-root-r2", DAORIS_HOME = ${JSON.stringify(files.home)} }`));
  assert.ok(set.includes('model_reasoning_effort="low"'));
  assert.equal(args.at(-1), 'go');
  assert.equal(toml(['a', 1]), '["a", 1]');
});

test('with Daoris\'s connector, each launch hands it as the driver does, and Claude Code the driver\'s lineage hook', () => {
  const files = runFiles(join('runs', 'r3'));
  const { mcp, settings } = claudeLaunch({ files, run: 'r3', text: 'go', node: 'node', connector: 'daoris-knowledge.exe', lineageHook: 'lineage-hook.mjs' });
  const entry = mcp.mcpServers['daoris-knowledge'];
  assert.equal(entry.command, 'daoris-knowledge.exe');
  assert.deepEqual(entry.env, {
    DAORIS_HOME: files.home, DAORIS_RULES_HOME: files.home, DAORIS_SESSION_ID: 'probe-r3',
    DAORIS_KNOWLEDGE_REPOSITORY: files.repo, DAORIS_KNOWLEDGE_DB: join(files.home, 'knowledge.db'),
  });
  assert.ok(settings.permissions.allow.includes('mcp__daoris-knowledge__knowledge_search'));
  const lineage = { type: 'command', command: 'node', args: ['lineage-hook.mjs', lineageFile(files, 'r3')], timeout: 10 };
  assert.deepEqual(settings.hooks.PreToolUse[1], { matcher: 'mcp__daoris-knowledge__.*', hooks: [lineage] });
  assert.deepEqual(settings.hooks.SubagentStart[1], { hooks: [lineage] });
  assert.deepEqual(settings.hooks.SubagentStop[1], { hooks: [lineage] });
  assert.equal(lineageFile(files, 'r3'), join(files.home, 'sessions', 'probe-r3', 'lineage.jsonl'));
  assert.equal(connectorSession('a.b c'), 'probe-a-b-c', 'a session id names no folder but its own');

  const { args } = codexLaunch({ files, run: 'r3', text: 'go', node: 'node', connector: 'daoris-knowledge.exe' });
  assert.ok(args.includes('mcp_servers.daoris-knowledge.default_tools_approval_mode="approve"'));
  assert.ok(args.includes('mcp_servers.daoris-knowledge.command="daoris-knowledge.exe"'));
  assert.match(prompt('family', 'claude', { knowledge: true }), /mcp__daoris-knowledge__knowledge_search once with query "grandchild"/);
});

test('each scenario asks in its harness\'s words, and an unknown one is refused', () => {
  assert.match(prompt('child', 'claude'), /Agent tool.*mcp__probe__probe_read once with note "child"/s);
  assert.match(prompt('family', 'codex'), /sibling-a.*grandchild.*sibling-b/s);
  assert.match(prompt('kill', 'claude'), /probe_wait once with seconds 120/);
  assert.throws(() => prompt('swarm', 'claude'), /no scenario/);
  assert.deepEqual(parseArguments(['claude', '--scenario', 'kill', '--kill-after', '30']), { harness: 'claude', scenario: 'kill', killAfter: 30 });
});

test('the receipt reproduces run A: one connection served the root and its subagent, joined to the hook\'s agent_id', () => {
  const folder = join(scratch, 'run-a');
  mkdirSync(folder, { recursive: true });
  const at = '2026-10-16T00:00:00.000Z';
  const server = [
    { at, pid: 11, event: 'start', env: { DAORIS_SESSION_ID: 'probe-root-a' } },
    { at, pid: 11, event: 'in', message: { id: 0, method: 'initialize', params: {} } },
    { at, pid: 11, event: 'in', message: { id: 1, method: 'tools/call', params: { name: 'probe_read', arguments: { note: 'root' }, _meta: { 'claudecode/toolUseId': 'toolu_r', progressToken: 1 } } } },
    { at, pid: 11, event: 'in', message: { id: 2, method: 'tools/call', params: { name: 'probe_read', arguments: { note: 'child' }, _meta: { 'claudecode/toolUseId': 'toolu_c', progressToken: 2 } } } },
    { at, pid: 11, event: 'input-end' },
    { at, pid: 11, event: 'exit', code: 0 },
  ];
  const hooks = [
    { hook_event_name: 'PreToolUse', tool_name: 'mcp__probe__probe_read', tool_use_id: 'toolu_r' },
    { hook_event_name: 'SubagentStart', agent_id: 'agent-1', agent_type: 'general-purpose' },
    { hook_event_name: 'PreToolUse', tool_name: 'mcp__probe__probe_read', tool_use_id: 'toolu_c', agent_id: 'agent-1', agent_type: 'general-purpose' },
  ];
  writeFileSync(join(folder, 'server.log'), server.map((line) => JSON.stringify(line)).join('\n'));
  writeFileSync(join(folder, 'hooks.log'), hooks.map((input) => JSON.stringify({ at, pid: 5, input })).join('\n'));
  writeFileSync(join(folder, 'stream.jsonl'), [
    { type: 'system', subtype: 'task_started', task_id: 't1', task_type: 'local_agent', spawn_depth: 1, tool_use_id: 'toolu_agent' },
    { type: 'result', num_turns: 4, total_cost_usd: 0.125 },
  ].map((line) => JSON.stringify(line)).join('\n'));
  writeFileSync(join(folder, 'run.json'), JSON.stringify({ harness: 'claude', version: '2.1.299 (Claude Code)', scenario: 'child', seconds: 29, exit: 0 }));

  const seen = receipt(folder);
  assert.equal(seen.servers.length, 1);
  assert.equal(seen.servers[0].initializes, 1);
  assert.deepEqual(seen.calls.map((call) => [call.note, call.agent]), [['root', 'root'], ['child', 'agent-1']]);
  assert.ok(seen.verdicts.includes('a child\'s call reached the root\'s own server process (pid 11): the same connection'));
  assert.ok(seen.verdicts.includes('the hook\'s tool_use_id met the call\'s _meta toolUseId for 2 of 2 calls'));
  const text = receiptText(seen);
  assert.match(text, /4 turns, 0\.125 USD/);
  assert.match(text, /task_started local_agent task t1 depth 1/);

  // A run whose root called only Daoris's connector compares nothing at the stand-in, and says so.
  writeFileSync(join(folder, 'server.log'), [server[0], server[1], server[3]].map((line) => JSON.stringify(line)).join('\n'));
  assert.ok(receipt(folder).verdicts.includes('children\'s calls reached 1 server process(es), and no root call reached this server to compare with'));
});

test('Codex\'s turn metadata gives the thread and its parent, and the workspace\'s path never reaches a receipt', () => {
  const meta = {
    'x-codex-turn-metadata': JSON.stringify({
      session_id: 'root-1', thread_id: 'th-2', parent_thread_id: 'th-1', subagent_kind: 'thread_spawn', thread_source: 'subagent',
      workspaces: { ['C:' + '\\work\\repo']: { commit: 'abc' } }, cwd: ['C:', 'work', 'repo'].join('\\'),
    }),
  };
  const lineage = callLineage(meta);
  assert.deepEqual([lineage.thread, lineage.parent, lineage.kind, lineage.source, lineage.root], ['th-2', 'th-1', 'thread_spawn', 'subagent', 'root-1']);
  assert.equal(lineage.dropped, 1, 'the cwd value; a path used as a key is a key, and keys are not printed');
  assert.deepEqual(withoutPaths({ a: '/srv/x/y', b: 'plain', c: ['D:/x', 'z'] }), { value: { b: 'plain', c: ['z'] }, dropped: 2 });
});
