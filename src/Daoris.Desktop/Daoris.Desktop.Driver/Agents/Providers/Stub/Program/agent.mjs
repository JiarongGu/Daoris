#!/usr/bin/env node
/**
 * The stand-in's program (AGENTPROVIDER1 row b, part 3; the agent-provider design §10, §11 row b; D213's row b part 3 note): one Node module
 * serving both doors of provider `stub`, chosen by the arguments its `provider.json` states for each (`--door native`, `--door protocol`).
 * No model, no account and no credential anywhere in it. The driver's project carries it beside the driver by one glob
 * (`Agents/Providers/*\/Program/**`), so the conformance suite runs it from its build output and the family rehearsal from its own.
 *
 * It answers the scenario contract in the stand-in's vocabulary, which is Claude Code's by reference (§3.3): `--scenario <name>` picks a
 * case, `open` where none is named, and `--seen <file>` keeps each protocol method it was sent, a line each. What it was handed it says
 * back in words the contract fixes, so a check reads them whichever stand-in spoke: a start says where it started and whether it had a
 * target, a turn says `heard: ` and the words, and a sign-in reads as alice@example.org of Acme on max.
 *
 * - The probes first, whatever the case: `--version`, `--login-state` (signed in) and `--login`.
 * - `open`, on the native door: a start (a quest in its environment) says where it started, then takes and closes its quest through the
 *   service's door its environment names, as a session's connector would; a conversation (no quest) says back each line it hears.
 * - `open`, on the protocol door: the spec's calls answered, a new conversation `s-1`, any conversation resumed, each prompt said back as
 *   `heard: ` and its first line, and a prompt with a quest taken and closed through the service's door before its turn ends.
 * - `missing` and `elsewhere`: a resume refused, as gone (`Resource not found`) or held by another client (`thread_active_writer`).
 * - `transient`: the first prompt refused overloaded, the next answered. `access` (or `refused`): the credential refused, a prompt on the
 *   protocol door and a start's end on the native one. `fatal`: the turn failed. `limit`: a limit said, with its reset.
 *
 * A failed call to the service is said, never thrown: the suite's starts name a service nobody listens on. It ends by its input closing,
 * never by `process.exit` after a call (STUB1: Node 24 on Windows could end in a libuv assertion with a socket still open).
 */
import { appendFileSync } from 'node:fs';
import { createInterface } from 'node:readline';

const args = process.argv.slice(2);
const option = (name) => {
  const at = args.indexOf(name);
  return at >= 0 ? args[at + 1] : undefined;
};

if (args.includes('--version')) {
  console.log('stub stand-in 1.0.0');
  process.exit(0);
}
if (args.includes('--login-state')) {
  console.log('logged-in as alice@example.org');
  console.log('org: Acme');
  console.log('plan: max');
  process.exit(0);
}
if (args.includes('--login')) {
  console.log('signed in');
  process.exit(0);
}

const door = option('--door');
const scenario = option('--scenario') ?? 'open';
const seen = option('--seen');
const quest = process.env.DAORIS_QUEST_ID;
const service = process.env.DAORIS_SERVICE_URL;
const key = process.env.DAORIS_SERVICE_KEY;

const REFUSED = 'Failed to authenticate. API Error: 401 API key is invalid.';
const LIMIT = "You've hit your session limit · resets 7am (UTC)";

/** One answer of the service's quest door, as a session's connector asks it. */
const respond = async (action, reason) => {
  const response = await fetch(`${service}/api/quests/${quest}/respond`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', ...(key ? { authorization: `Bearer ${key}` } : {}) },
    body: JSON.stringify({ action, reason }),
    signal: AbortSignal.timeout(30_000),
  });
  return { ok: response.ok, status: response.status };
};

/** A start's work: its quest taken and closed done, each step said through `say`; a service that does not answer is said too. */
const work = async (say) => {
  if (!service || !quest) return;
  try {
    const took = await respond('take', null);
    if (!took.ok) {
      say(`took nothing: the service answered ${took.status}`);
      return;
    }
    const done = await respond('done', 'Answered by the stand-in.');
    say(done.ok ? `closed quest ${quest}` : `could not close quest ${quest}: the service answered ${done.status}`);
  } catch (error) {
    say(`no service answered: ${error?.cause?.code ?? error?.message ?? error}`);
  }
};

if (door === 'native') {
  if (quest !== undefined) {
    if (scenario === 'access' || scenario === 'refused') {
      console.log(REFUSED);
      process.exitCode = 1;
    } else if (scenario === 'limit') {
      console.log(LIMIT);
      process.exitCode = 1;
    } else {
      const target = process.env.DAORIS_TARGET ?? '';
      console.log(`started on ${process.env.DAORIS_REPOSITORY ?? 'nothing'} with a target of ${target.length > 0 ? 'words' : 'none'}`);
      await work((line) => console.log(line));
      process.exitCode = 0;
    }
  } else {
    const lines = createInterface({ input: process.stdin });
    lines.on('line', (line) => console.log(`heard: ${line}`));
    lines.on('close', () => {
      process.exitCode = 0;
    });
  }
} else if (door === 'protocol') {
  const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
  // Stdout is the protocol's, so every word for a person goes to stderr.
  const say = (line) => process.stderr.write(`stand-in: ${line}\n`);
  const chunk = (sessionId, text) => send({
    jsonrpc: '2.0', method: 'session/update',
    params: { sessionId, update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } },
  });
  let session = null;
  let prompts = 0;

  // Handled without awaiting inside the reader: a prompt's work calls the service and must keep reading while it waits.
  const handle = async (frame) => {
    const answer = (result) => (frame.id === undefined ? undefined : send({ jsonrpc: '2.0', id: frame.id, result }));
    const refuse = (error) => send({ jsonrpc: '2.0', id: frame.id, error });
    switch (frame.method) {
      case 'initialize':
        return answer({ protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } });
      case 'session/new':
        session = 's-1';
        say(`conversation ${session} opened`);
        return answer({ sessionId: session });
      case 'session/resume':
        if (scenario === 'missing') return refuse({ code: -32002, message: 'Resource not found' });
        if (scenario === 'elsewhere') {
          return refuse({ code: -32603, message: 'Internal error: another client holds the thread', data: { reason: 'thread_active_writer' } });
        }
        session = frame.params?.sessionId ?? session;
        say(`conversation ${session} resumed`);
        return answer({});
      case 'session/prompt': {
        prompts += 1;
        if (scenario === 'transient' && prompts === 1) {
          return refuse({ code: -32603, message: 'Internal error: API Error: 529 Overloaded.', data: { errorKind: 'overloaded' } });
        }
        if (scenario === 'access' || scenario === 'refused') return refuse({ code: -32603, message: `Internal error: ${REFUSED}` });
        if (scenario === 'fatal') return refuse({ code: -32000, message: 'the turn failed' });
        if (scenario === 'limit') return refuse({ code: -32603, message: `Internal error: ${LIMIT}` });
        const words = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n').split(/\r?\n/)[0];
        const on = frame.params?.sessionId ?? session;
        chunk(on, `heard: ${words}`);
        if (quest !== undefined) await work(say);
        return answer({ stopReason: 'end_turn' });
      }
      default:
        return answer({});
    }
  };

  const lines = createInterface({ input: process.stdin });
  lines.on('line', (line) => {
    let frame;
    try {
      frame = JSON.parse(line);
    } catch {
      say(`not a frame: ${line}`);
      return;
    }
    if (!frame.method) return;
    if (seen) appendFileSync(seen, `${frame.method}\n`);
    handle(frame).catch((error) => say(`the frame failed: ${error?.message ?? error}`));
  });
  lines.on('close', () => {
    process.exitCode = 0;
    process.stdin.destroy();
  });
} else {
  console.error(`the stand-in's program is run with --door native or --door protocol, as its provider.json states; it was given ${args.join(' ') || 'nothing'}`);
  process.exitCode = 2;
}
