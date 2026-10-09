/**
 * The rehearsal harness both gates share: the transcript, the check/section vocabulary, and the
 * capturing command runner. Extracted because the two rehearsals carried byte-identical copies that
 * had already begun to drift (one had the kill-timeout, one had `process.exit` where the other set
 * `exitCode`) — and a harness that diverges between gates makes the same failure read differently
 * depending on which gate caught it.
 */
import { execSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, statSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { setTimeout as sleep } from 'node:timers/promises';

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

/**
 * Where a run keeps what its failed checks read (DEV3b): a folder of its own beside the transcripts, which no run deletes,
 * named for the rehearsal and the moment it started. The rehearsal's scratch is wiped at every start, so evidence kept
 * there went with the rerun that followed the failure, and the next sighting was a quote.
 */
export function evidenceFolder(repoRoot, name, started = new Date()) {
  return join(repoRoot, '_fixtures', 'rehearsal-logs', `${name}-${started.toISOString().replace(/[:.]/g, '-')}`);
}

/** How long a kept check's folder name may be: a label is a sentence, and a deep checkout's paths are long. */
const NAME_LIMIT = 60;

/** A check's label as a folder name: its letters and digits, lower case, hyphened, cut at a word within the limit. */
export function checkFolderName(label) {
  const words = label.toLowerCase().split(/[^a-z0-9]+/).filter(Boolean);
  let name = '';
  for (const word of words) {
    const next = name ? `${name}-${word}` : word;
    if (next.length > NAME_LIMIT) break;
    name = next;
  }

  if (!name && words.length > 0) name = words[0].slice(0, NAME_LIMIT);
  return name || 'check';
}

/**
 * Keep what a failed check read (DEV3b): its print as `print.txt` and a copy of each file it names, under `root`, in a
 * folder named for the check, a second failure of the same check beside the first. A file that is not there is listed in
 * `missing.txt`, and the checker says a folder it could not write on the FAIL line: keeping evidence fails no check.
 *
 * @returns {string} the folder, for the FAIL line to name
 */
export function keepEvidence(root, label, { print = '', files = [] } = {}) {
  const name = checkFolderName(label);
  let folder = join(root, name);
  for (let n = 2; existsSync(folder); n += 1) folder = join(root, `${name}-${n}`);
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, 'print.txt'), print);

  const missing = [];
  for (const file of files.filter(Boolean)) {
    if (!existsSync(file) || !statSync(file).isFile()) {
      missing.push(file);
      continue;
    }

    // Two homes write their logs under one name each day: a copy whose name is taken is named for the two folders above it.
    const above = dirname(file).split(/[\\/]/).filter(Boolean).slice(-2).join('-');
    let target = join(folder, basename(file));
    for (let n = 1; existsSync(target); n += 1) {
      target = join(folder, `${above}-${n > 1 ? `${n}-` : ''}${basename(file)}`);
    }
    copyFileSync(file, target);
  }

  if (missing.length > 0) writeFileSync(join(folder, 'missing.txt'), `${missing.join('\n')}\n`);
  return folder;
}

/**
 * The check/section vocabulary, with the counters the final summary reads.
 *
 * With `keep` (DEV3b, `evidenceFolder`), a failed check that hands `evidence` (`{ print, files }`, or a function answering
 * it, asked only on a failure) keeps it there, and its FAIL line names the folder.
 */
export function makeChecker({ keep } = {}) {
  const totals = { checks: 0, failures: 0 };
  return {
    totals,
    check(label, condition, detail = '', evidence) {
      totals.checks += 1;
      if (condition) {
        console.log(`  ok    ${label}`);
      } else {
        totals.failures += 1;
        console.log(`  FAIL  ${label}${detail ? `\n          ${detail}` : ''}`);
        if (keep && evidence) {
          try {
            console.log(`          kept: ${keepEvidence(keep, label, typeof evidence === 'function' ? evidence() : evidence)}`);
          } catch (error) {
            // The check has failed already; evidence that could not be kept is said, never a second failure.
            console.log(`          kept: nothing, ${error.message}`);
          }
        }
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
 * Ask `ready` every `every` ms until it answers something truthy, `within` ms have passed, or `settled` settles first
 * (DEV3d). Bounded by the clock rather than by a count of asks, because under load each ask is slow and a count ran out
 * before a late session had said what the phase waited for. An ask that throws (a host refusing while it restarts) is not
 * ready. Each ask is awaited whole, so a slow one can carry the wait past `within` by its own length.
 *
 * @returns {Promise<{ ended: 'ready' | 'timeout' | 'settled', value?: unknown, ms: number }>} `settled` when what the
 * wait ran beside (a run) ended first, since it will never say it then.
 */
export async function waitFor(ready, { within, every = 250, settled } = {}) {
  const began = Date.now();
  let over = false;
  const ending = settled ? Promise.resolve(settled).then(() => { over = true; }, () => { over = true; }) : null;
  for (;;) {
    let value;
    try {
      value = await ready();
    } catch {
      value = undefined;
    }

    const ms = Date.now() - began;
    if (value) return { ended: 'ready', value, ms };
    if (over) return { ended: 'settled', ms };
    if (ms >= within) return { ended: 'timeout', ms };
    const pause = sleep(Math.min(every, within - ms));
    await (ending ? Promise.race([pause, ending]) : pause);
  }
}

/**
 * Restart a host only between the requests of a session that talks to it (DEV3d): `quiet` is asked, by `waitFor`, until it
 * says no request of the session's is in flight, and then the host is stopped, `gap` ms pass and it is started again.
 * A restart cuts a request in flight off at the session's end, which then fails as a crash, not as anything the phase means
 * to prove. `quiet` must stay true for as long as the restart takes: a session that lingers between requests is how.
 *
 * A session never quiet within `within` still has its host restarted after the bound, because the phases after need the
 * host, and the answer says so for the check that reads the session to name.
 *
 * @returns {Promise<{ quiet: boolean, waited: number, host: unknown }>} `host` is what `start` answered.
 */
export async function restartBetweenRequests({ quiet, stop, start, within, every = 250, gap = 700 }) {
  const waited = await waitFor(quiet, { within, every });
  await stop();
  await sleep(gap);
  const host = await start();
  return { quiet: waited.ended === 'ready', waited: waited.ms, host };
}

/**
 * The ACP stub, as source for a rehearsal to write into its scratch and name in `commands['acp-stub']`:
 * the same fake-session trick as the family rehearsal's pipe-door stub, one door over (D46 §8). It
 * speaks JSON-RPC on stdout and nothing else — every human word goes to stderr, because stdout belongs
 * to the protocol — and it does REAL work through the same HTTP door its pipe-door twin uses. No model,
 * no account, no credential anywhere in it.
 *
 * Given a quest (`DAORIS_QUEST_ID`), a prompt is that quest's work; given none, it is a conversation,
 * each prompt answered with what was heard, open until its input closes. A quest titled as a set-up
 * (LAYOUT7a) is done as its body says: the doctrine tool run by its bare name from the PATH the driver
 * handed the session, each verb said on stderr with whether the body asks for it, then a commit.
 *
 * It resumes a conversation (ANSWER1b, D131): `initialize` advertises `session/resume`, and a resumed
 * conversation's next prompt is the person's answer. A quest titled to ask the person first asks which
 * port and ends its turn holding the quest; resumed, it serves the report on the port it was told. Any
 * other quest's conversation, resumed with words said after it ended (MSG1e3), says what it heard and
 * ends its turn a few seconds later, touching nothing. The stub keeps no history, so it resumes
 * whichever conversation it is asked, and says which on stderr.
 *
 * A quest titled as Daoris titles a set-up step (REVIEWENV1h, D154) says what it showed through the
 * connector the protocol handed it, `review_ready` over MCP on stdio, and closes done with no commit of
 * its own; resumed with the person's *not yet*, it corrects the work in its tree and says a new set-up.
 *
 * Here rather than inside the family rehearsal since DEPLOY5, whose deployment gate opens a
 * conversation on it in the installed shell: one copy, for the reason this module exists at all.
 */
export const ACP_STUB_AGENT = `
import { execSync, spawn } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
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

// A SET-UP (LAYOUT7a, D124 §2): the quest asks this repository's own session to take up the doctrine and
// initialise its knowledge, and the stub does it as the body says. The doctrine tool is run by its bare name,
// so it is found on the PATH the driver handed the session or not at all. Each verb goes on the wire as a tool
// call and on stderr, which the transcript keeps, with whether the body asks for it. A verb that fails, or a
// tool that answers another version than the body says, is the decline the body names.
const SET_UP = 'Set up this repository for every agent (';
const tick = String.fromCharCode(96);

async function setUp(sessionId) {
  const body = process.env.DAORIS_QUEST_BODY ?? '';
  const taken = await respond('take', null);
  if (!taken.ok) { say('take refused:', taken.text); return 'end_turn'; }
  update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'setting up for quest ' + id } });

  let calls = 0;
  const daoris = (words) => {
    const command = 'daoris ' + words;
    const toolCallId = 'setup-' + (++calls);
    update(sessionId, { sessionUpdate: 'tool_call', toolCallId, title: command, kind: 'execute', status: 'in_progress' });
    let printed;
    try {
      printed = execSync(command, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    } catch (error) {
      update(sessionId, { sessionUpdate: 'tool_call_update', toolCallId, status: 'failed' });
      const said = (String(error.stdout ?? '') + String(error.stderr ?? '')).trim().split('\\n')[0];
      throw new Error(tick + command + tick + ' failed: ' + (said || 'it printed nothing'));
    }
    update(sessionId, { sessionUpdate: 'tool_call_update', toolCallId, status: 'completed' });
    say('setup ran ' + tick + command + tick + (body.includes(tick + command + tick)
      ? ', which the quest asks for' : ', which the quest does NOT ask for'));
    return printed;
  };

  try {
    const version = daoris('--version').trim();
    const promise = 'Run ' + tick + 'daoris --version' + tick + '. It prints ' + tick;
    const at = body.indexOf(promise);
    const promised = at < 0 ? null : body.slice(at + promise.length, body.indexOf(tick, at + promise.length));
    say('setup: daoris --version printed ' + version + ', and the quest said it prints ' + (promised ?? '(nothing)'));
    if (version !== promised) throw new Error('it printed ' + version + ', and the quest said ' + (promised ?? 'nothing'));
    daoris('init --harness agents');
    daoris('sync --dry-run');
    daoris('sync');

    // Initialising the knowledge, as a stub can: the domain in the manifest and one document of its own.
    const repository = process.env.DAORIS_REPOSITORY ?? 'this repository';
    const manifest = JSON.parse(readFileSync('daoris.json', 'utf8'));
    manifest.domain = {
      summary: repository + ', as its README says it is.',
      owns: ['what its README says it serves'],
      accepts: ['a request about what it serves, with the evidence'],
    };
    writeFileSync('daoris.json', JSON.stringify(manifest, null, 2) + '\\n');
    mkdirSync('.agents/knowledge', { recursive: true });
    writeFileSync('.agents/knowledge/what-this-repository-owns.md', [
      '---',
      'name: what-this-repository-owns',
      'applies_when: a session in another repository asks what this one owns, and where',
      'enforces: each fact with the place in the code that holds it, and what the code did not confirm said',
      '---',
      '',
      '# What this repository owns',
      '',
      'What its README says it serves. Not confirmed in the code: the set-up found none to read.',
      '',
    ].join('\\n'));
    daoris('sync');
    daoris('check');

    const git = 'git -c user.name="Setup Session" -c user.email="setup@example.invalid"';
    execSync(git + ' add -A', { stdio: 'ignore' });
    execSync(git + ' commit -q -m "setup: take up the doctrine and initialise the knowledge (quest ' + id + ')"', { stdio: 'ignore' });
  } catch (error) {
    say('setup declines: the doctrine command could not run here:', error.message);
    await respond('decline', 'The doctrine command could not run here: ' + error.message);
    return 'end_turn';
  }

  const done = await respond('done', 'Set up on the agents layout: the domain declared, one knowledge document written, check clean.');
  say('done:', done.ok);
  return 'end_turn';
}

// A quest that asks the person first (ANSWER1b, D131 §1). Not resumed, the session takes the quest, asks which port and
// ends its turn holding it. A take through the service's HTTP door marks no session as its taker, so the driver reads
// that ending as a park only from a session that carries a take on (D80); a first session's would read as a stand-down.
// Resumed, the prompt is the person's answer, verbatim, and the report is served where it says, then committed and done.
const ASKS_FIRST = 'Ask the person first: ';

async function askFirst(sessionId, said) {
  if (!resumed) {
    // Carried on, the quest is already this machine's take, and a refused take is the answer expected here.
    await respond('take', null);
    update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'Which port should the report listen on?' } });
    say('asked the person which port, holding quest ' + id);
    return 'end_turn';
  }

  say('the answer, as the resumed conversation heard it: ' + said);
  update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'Serving on the port you named: ' + said } });
  writeFileSync('acp-port-' + id + '.md', '# the report\\n\\nIt listens where the person said: ' + said + '\\n');
  const git = 'git -c user.name="ACP Session" -c user.email="acp@example.invalid"';
  execSync(git + ' add -A', { stdio: 'ignore' });
  execSync(git + ' commit -q -m "acp: serve the report where the person said (quest ' + id + ')"', { stdio: 'ignore' });
  const done = await respond('done', 'Serving the report on the port the person named.');
  say('done:', done.ok);
  return 'end_turn';
}

// Words said to a session after it ended (MSG1e3, D137 §2.2). Its own conversation is resumed and the person's words are its
// next prompt; its quest had closed, so it says what it heard and touches nothing. The turn lasts a few seconds, as an
// agent's does, so a terminal that follows the words sees the record working before it ends again.
const HEARD_FOR_MS = 3000;

async function hearAfter(sessionId, said) {
  say('heard after it ended: ' + said);
  update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'Heard after the work: ' + said } });
  await new Promise((resolve) => setTimeout(resolve, HEARD_FOR_MS));
  return 'end_turn';
}

// One tool call on the connector the protocol handed this session (ACP4), as an agent's MCP client makes it: the server spawned as
// the driver named it, with the environment it named, the handshake, the call, and its text. A connector that is not there, or
// answers nothing within a minute, fails the turn, and the driver records that.
function connector(tool, args) {
  const server = servers.find((each) => each.name === 'daoris-knowledge');
  if (!server) return Promise.reject(new Error('no daoris-knowledge server was handed to this session'));
  const env = { ...process.env };
  for (const pair of server.env ?? []) env[pair.name] = pair.value;
  return new Promise((resolve, reject) => {
    const child = spawn(server.command, server.args ?? [], { env, stdio: ['pipe', 'pipe', 'ignore'] });
    const timer = setTimeout(() => { child.kill(); reject(new Error(tool + ' was answered nothing within a minute')); }, 60000);
    const write = (frame) => child.stdin.write(JSON.stringify(frame) + '\\n');
    child.on('error', (error) => { clearTimeout(timer); reject(error); });
    let buffered = '';
    child.stdout.on('data', (chunk) => {
      buffered += chunk;
      let cut;
      while ((cut = buffered.indexOf('\\n')) >= 0) {
        const line = buffered.slice(0, cut);
        buffered = buffered.slice(cut + 1);
        let frame;
        try { frame = JSON.parse(line); } catch { continue; }
        if (frame.id === 1) {
          write({ jsonrpc: '2.0', method: 'notifications/initialized' });
          write({ jsonrpc: '2.0', id: 2, method: 'tools/call', params: { name: tool, arguments: args } });
        } else if (frame.id === 2) {
          clearTimeout(timer);
          child.stdin.end();
          // A connector that lingers once its input closed is stopped, so it never holds this process's own ending.
          setTimeout(() => child.kill(), 3000).unref();
          resolve(frame.error
            ? 'refused: ' + frame.error.message
            : (frame.result?.content ?? []).map((part) => part.text).join(' '));
        }
      }
    });
    write({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {
      protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'acp-stub', version: '0' },
    } });
  });
}

// A SET-UP STEP (REVIEWENV1h; D154, the review environment design §2.2, §2.6, §3.4): a quest titled as Daoris titles one shows its
// chain's work for the person's review. The stub drives no browser, so it shows nothing; it says what it showed as a real session
// does, through its connector's review_ready, makes no commit of its own, and closes done. Its look is the address its quest's
// body names. Resumed with the person's not yet, it says the words it heard, corrects the work in its tree with a commit, and says
// a new set-up; its quest stays done, as a closed quest's resumed run leaves it (D137 §5).
const SHOW_FOR_REVIEW = /^Show #\\S+ in \\x60[^\\x60]+\\x60 for review/;

async function showForReview(sessionId, said) {
  const look = ((process.env.DAORIS_QUEST_BODY ?? '').match(/https?:\\/\\/[^\\s\\x60,)]+/) ?? ['http://localhost/'])[0];
  const again = 'Open ' + look + ' and read the report there.';
  if (!resumed) {
    const taken = await respond('take', null);
    if (!taken.ok) { say('take refused:', taken.text); return 'end_turn'; }
    update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'showing quest ' + id + ' at ' + look } });
    const ready = await connector('review_ready', { look, shows: 'The report the work changed, at its new heading.', again });
    say('review_ready answered: ' + ready);
    const done = await respond('done', 'Shown for review at ' + look + '; it waits for the person to look.');
    say('done:', done.ok, done.ok ? '' : done.text);
    return 'end_turn';
  }

  say('the set-up step heard not yet: ' + said);
  update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'Heard your not yet: ' + said } });
  writeFileSync('review-correction-' + id + '.md', '# corrected under the words the person said\\n\\n' + said + '\\n');
  const git = 'git -c user.name="ACP Session" -c user.email="acp@example.invalid"';
  execSync(git + ' add -A', { stdio: 'ignore' });
  execSync(git + ' commit -q -m "review: correct what the person said not yet to (quest ' + id + ')"', { stdio: 'ignore' });
  const shows = ('Corrected under your words: ' + said.replace(/\\s+/g, ' ')).slice(0, 300);
  say('review_ready answered again: ' + await connector('review_ready', { look, shows, again }));
  return 'end_turn';
}

async function work(sessionId, said) {
  if ((process.env.DAORIS_QUEST_TITLE ?? '').startsWith(SET_UP)) return setUp(sessionId);
  if ((process.env.DAORIS_QUEST_TITLE ?? '').startsWith(ASKS_FIRST)) return askFirst(sessionId, said);
  if (SHOW_FOR_REVIEW.test(process.env.DAORIS_QUEST_TITLE ?? '')) return showForReview(sessionId, said);
  if (resumed) return hearAfter(sessionId, said);
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
// Whether this process resumed a conversation rather than opening one: its next prompt is then the person's answer.
let resumed = false;
// The servers the protocol handed this session (ACP4), on a new session and on a resumed one alike: its connector is among them.
let servers = [];

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
      // session/resume, in the shape the real adapter advertises it (ANSWER1b): the driver resumes only what is offered.
      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { sessionCapabilities: { resume: {} } } } });
      break;
    case 'session/resume':
      // The conversation the driver kept, resumed rather than opened (D131 §1). Nothing is replayed, as the real
      // adapter's resume replays nothing, and the transcript keeps which conversation it was.
      session = frame.params?.sessionId ?? session;
      resumed = true;
      servers = frame.params?.mcpServers ?? [];
      say('resumed conversation', session, 'on', frame.params?.cwd);
      send({ jsonrpc: '2.0', id: frame.id, result: {} });
      break;
    case 'session/new': {
      session = 'acp-session-1';
      servers = frame.params?.mcpServers ?? [];
      say('session on', frame.params?.cwd);
      // 🔴 ACP4, reported from the agent's own side. The composed target tells this session to take
      // and close its quest over a connector, and the protocol is what hands it one — so the stub
      // says what it was offered, and the gate reads it back out of the transcript. Reported rather
      // than asserted here: a stub that refused to start would tell the gate nothing about WHY.
      const offered = (frame.params?.mcpServers ?? []).map((s) => s.name).join(', ');
      say('mcp servers offered:', offered || '(none)');
      // CONNECTOR1: and what each one runs, so the deployment gate can tell WHICH connector binary the shell
      // handed over, the install's own or the home's older copy. A line each, after the names' line, which
      // the family rehearsal reads as it was.
      for (const server of frame.params?.mcpServers ?? []) say('mcp server', server.name, 'runs', server.command);
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
        const said = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\\n');
        const stopReason = await work(frame.params?.sessionId ?? session, said);
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
// stdin closed: end of input is the ending. The stub releases its input, sets its code and returns, and the
// process ends when the event loop drains, never by process.exit: on Windows, Node 24 could end in a libuv
// assertion (0xC0000409) inside process.exit while a fetch's socket was still open (STUB1). Nothing else holds
// the loop: a permission still awaited is a promise, not a handle.
lines.on('close', () => {
  say('input closed; exiting');
  process.exitCode = 0;
  process.stdin.destroy();
});
`;
