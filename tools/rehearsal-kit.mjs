/**
 * The rehearsal harness both gates share: the transcript, the check/section vocabulary, and the
 * capturing command runner. Extracted because the two rehearsals carried byte-identical copies that
 * had already begun to drift (one had the kill-timeout, one had `process.exit` where the other set
 * `exitCode`) — and a harness that diverges between gates makes the same failure read differently
 * depending on which gate caught it.
 */
import { execSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Every run leaves a transcript under `_fixtures/rehearsal-logs/`, outside anything a passing run
 * deletes, so a failure is evidence rather than a memory (REH1). Console output is mirrored into it;
 * `beforeExit` runs first on the way out — the family rehearsal stops its hosts there.
 */
export function openTranscript(repoRoot, name, { beforeExit } = {}) {
  const transcript = [`${name} rehearsal — ${new Date().toISOString()} — node ${process.version}`];
  const emit = console.log.bind(console);
  console.log = (...args) => {
    const line = args.join(' ');
    transcript.push(line);
    emit(line);
  };
  process.on('exit', (code) => {
    beforeExit?.();
    transcript.push(`\nexit ${code}`);
    const logDir = join(repoRoot, '_fixtures', 'rehearsal-logs');
    mkdirSync(logDir, { recursive: true });
    const logPath = join(logDir, `${name}-${new Date().toISOString().replace(/[:.]/g, '-')}.log`);
    writeFileSync(logPath, `${transcript.join('\n')}\n`);
    emit(`  transcript: ${logPath}`);
  });
}

/** The check/section vocabulary, with the counters the final summary reads. */
export function makeChecker() {
  const totals = { checks: 0, failures: 0 };
  return {
    totals,
    check(label, condition, detail = '') {
      totals.checks += 1;
      if (condition) {
        console.log(`  ok    ${label}`);
      } else {
        totals.failures += 1;
        console.log(`  FAIL  ${label}${detail ? `\n          ${detail}` : ''}`);
      }
    },
    section(title) {
      console.log(`\n${title}`);
    },
  };
}

/**
 * Run a command, capturing output and exit code — never throwing, so a failure is a FAIL line. A
 * timeout kills the child and returns its partial output, so a hung child is a captured FAIL rather
 * than a frozen gate.
 */
export function capture(command, cwd, { env = {}, timeout = 0, input } = {}) {
  try {
    const out = execSync(command, {
      cwd,
      encoding: 'utf8',
      // stdin stays closed unless a check hands the command something to read (`agent key`, AGT3).
      stdio: [input === undefined ? 'ignore' : 'pipe', 'pipe', 'pipe'],
      ...(input === undefined ? {} : { input }),
      env: { ...process.env, ...env },
      ...(timeout ? { timeout, killSignal: 'SIGKILL' } : {}),
    });
    return { code: 0, out };
  } catch (error) {
    const timedOut = error.killed || error.signal === 'SIGKILL';
    return {
      code: error.status ?? -1,
      out: `${error.stdout ?? ''}${error.stderr ?? ''}${timedOut ? '\n[killed: exceeded the timeout]' : ''}`,
    };
  }
}

/**
 * The ACP stub, as source for a rehearsal to write into its scratch and name in `commands['acp-stub']`:
 * the same fake-session trick as the family rehearsal's pipe-door stub, one door over (D46 §8). It
 * speaks JSON-RPC on stdout and nothing else — every human word goes to stderr, because stdout belongs
 * to the protocol — and it does REAL work through the same HTTP door its pipe-door twin uses. No model,
 * no account, no credential anywhere in it.
 *
 * Given a quest (`DAORIS_QUEST_ID`), a prompt is that quest's work; given none, it is a conversation,
 * each prompt answered with what was heard, open until its input closes.
 *
 * Here rather than inside the family rehearsal since DEPLOY5, whose deployment gate opens a
 * conversation on it in the installed shell: one copy, for the reason this module exists at all.
 */
export const ACP_STUB_AGENT = `
import { execSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\\n');
const say = (...parts) => console.error('acp-agent:', ...parts);

let nextId = 9000;
const pending = new Map();
const ask = (method, params) => new Promise((resolve) => {
  const id = nextId++;
  pending.set(id, resolve);
  send({ jsonrpc: '2.0', id, method, params });
});
const update = (sessionId, body) =>
  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId, update: body } });

const url = process.env.DAORIS_SERVICE_URL;
const id = process.env.DAORIS_QUEST_ID;
const respond = async (action, reason) => {
  const response = await fetch(url + '/api/quests/' + id + '/respond', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ action, reason }),
  });
  return { ok: response.ok, text: await response.text() };
};

async function work(sessionId) {
  update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'taking quest ' + id } });

  const taken = await respond('take', null);
  if (!taken.ok) { say('take refused:', taken.text); return 'end_turn'; }

  // A tool call, with its outcome — the structured boundaries the timeline needs, which the pipe
  // door could only have supplied by parsing this agent's stdout.
  update(sessionId, { sessionUpdate: 'tool_call', toolCallId: 't1', title: 'write', status: 'in_progress' });
  // Per quest, because this agent answers more than one over a run — a fixed file with fixed content
  // left the second session with nothing to commit, and a commit that failed left the turn hanging.
  writeFileSync('acp-answer-' + id + '.md', '# answered over ACP\\n\\nThe protocol door carried quest ' + id + '.\\n');
  update(sessionId, { sessionUpdate: 'tool_call_update', toolCallId: 't1', status: 'completed' });

  // The D37 boundary, asked ON THE WIRE: an outward-facing act the repository's own configuration
  // did not pre-approve. The driver must refuse it — and the agent reports what it was told, so the
  // transcript carries the refusal rather than only the driver's side of it.
  const answer = await ask('session/request_permission', {
    sessionId,
    toolCall: { title: 'git push origin main' },
    options: [
      { optionId: 'allow', name: 'Allow', kind: 'allow_once' },
      { optionId: 'deny', name: 'Reject', kind: 'reject_once' },
    ],
  });
  const outcome = JSON.stringify(answer?.outcome ?? answer);
  update(sessionId, {
    sessionUpdate: 'agent_message_chunk',
    content: { type: 'text', text: 'permission answer was ' + outcome + ' — not pushing' },
  });

  const git = 'git -c user.name="ACP Session" -c user.email="acp@example.invalid"';
  execSync(git + ' add -A', { stdio: 'ignore' });
  execSync(git + ' commit -q -m "acp: answer quest ' + id + '"', { stdio: 'ignore' });

  const done = await respond('done', 'Landed by the ACP session.');
  say('done:', done.ok);
  return 'end_turn';
}

const lines = createInterface({ input: process.stdin });
let session = null;
let holding = null;

// Frames are handled WITHOUT awaiting inside the reader, and that is not a style choice: the prompt's
// work asks the client for a permission decision and must keep reading while it waits for the answer.
// The first version awaited here and deadlocked — the agent held the quest and never read the reply,
// and the driver's own two-minute timeout is what reported it.
const handle = async (line) => {
  if (!line.trim()) return;
  let frame;
  try { frame = JSON.parse(line); } catch { say('not a frame:', line); return; }

  if (frame.method === undefined && frame.id !== undefined) {
    const waiting = pending.get(frame.id);
    if (waiting) { pending.delete(frame.id); waiting(frame.result ?? frame.error); }
    return;
  }

  switch (frame.method) {
    case 'initialize':
      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
      break;
    case 'session/new': {
      session = 'acp-session-1';
      say('session on', frame.params?.cwd);
      // 🔴 ACP4, reported from the agent's own side. The composed target tells this session to take
      // and close its quest over a connector, and the protocol is what hands it one — so the stub
      // says what it was offered, and the gate reads it back out of the transcript. Reported rather
      // than asserted here: a stub that refused to start would tell the gate nothing about WHY.
      const offered = (frame.params?.mcpServers ?? []).map((s) => s.name).join(', ');
      say('mcp servers offered:', offered || '(none)');
      send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: session } });
      break;
    }
    case 'session/prompt': {
      // A conversation serves no quest (CONV3b): each prompt is a person's message, answered with
      // what was heard — and the session is the SAME one for every message, which the gate reads back.
      if (!id) {
        const said = frame.params?.prompt?.[0]?.text ?? '';
        update(frame.params?.sessionId ?? session, {
          sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'acp heard: ' + said + ' (in ' + frame.params?.sessionId + ')' },
        });
        // An attached file arrives as a resource_link to where it is kept (CONV4c): read it, as an agent would.
        for (const block of frame.params?.prompt ?? []) {
          if (block.type !== 'resource_link') continue;
          let bytes = -1;
          try { bytes = readFileSync(fileURLToPath(block.uri)).length; } catch {}
          update(frame.params?.sessionId ?? session, {
            sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: ' · acp read file ' + block.name + ': ' + bytes + ' bytes' },
          });
        }
        // A turn that runs until it is stopped (CONV4a): answered only by session/cancel, the way a
        // real agent answers a cancelled prompt — with its own stop reason.
        if (said === 'hold this turn') { holding = frame.id; break; }
        send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
        break;
      }
      // A turn that fails is ANSWERED as a failure, the way a real agent's would be: an unanswered
      // prompt is a driver waiting on its timeout, which is a hang dressed as a session.
      try {
        const stopReason = await work(frame.params?.sessionId ?? session);
        send({ jsonrpc: '2.0', id: frame.id, result: { stopReason } });
      } catch (error) {
        say('turn failed:', error.message);
        send({ jsonrpc: '2.0', id: frame.id, error: { code: -32000, message: error.message } });
      }
      break;
    }
    case 'session/close':
      send({ jsonrpc: '2.0', id: frame.id, result: {} });
      break;
    case 'session/cancel':
      say('turn cancelled');
      if (holding !== null) {
        send({ jsonrpc: '2.0', id: holding, result: { stopReason: 'cancelled' } });
        holding = null;
      }
      break;
    default:
      if (frame.id !== undefined) {
        send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: 'no ' + frame.method } });
      }
  }
};

lines.on('line', (line) => { handle(line).catch((error) => say('handler failed:', error.message)); });
// stdin closed: end of input is the ending, and the process exits on its own.
lines.on('close', () => { say('input closed; exiting'); process.exit(0); });
`;
