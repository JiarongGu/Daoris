// Keeps a stdio MCP server's memory under a ceiling by starting it afresh when it has grown past it and no call is in
// flight. The harness sees one server throughout: the client's initialize is replayed to the new one and its answer swallowed.
//  node server-keeper.mjs <ceiling MB> <check every ms> <script> [arguments...]
// Newline-delimited JSON-RPC, as the MCP stdio transport writes it. Anything that is not a line of JSON passes through.
import { spawn, execFileSync } from 'node:child_process';

const [ceilingMb, everyMs, script, ...rest] = process.argv.slice(2);
const ceiling = Number(ceilingMb) * 1024 * 1024;
const REPLAY = 'server-keeper-initialize';
const REFRESH = 'server-keeper-snapshot';
const SNAPSHOT = 'browser_snapshot';
const refreshing = new Map();
let refreshes = 0;
let stale = false;

let child;
let starting = false;
let initialize = null;
let initialized = null;
const inflight = new Set();
const retired = new WeakSet();
const waiting = [];
let clientBuffer = '';
let serverBuffer = '';

function workingSet(pid) {
  try {
    if (process.platform === 'win32') {
      const out = execFileSync('tasklist', ['/FI', `PID eq ${pid}`, '/FO', 'CSV', '/NH'], { encoding: 'utf8', windowsHide: true });
      const match = /"([\d.,]+) K"\s*$/m.exec(out);
      return match ? Number(match[1].replace(/[.,]/g, '')) * 1024 : 0;
    }
    return Number(execFileSync('ps', ['-o', 'rss=', '-p', String(pid)], { encoding: 'utf8' }).trim()) * 1024;
  } catch {
    return 0;
  }
}

function start() {
  child = spawn(process.execPath, [script, ...rest], { stdio: ['pipe', 'pipe', 'inherit'], windowsHide: true });
  const mine = child;
  serverBuffer = '';
  mine.stdout.setEncoding('utf8');
  mine.stdout.on('data', chunk => fromServer(mine, chunk));
  mine.stdin.on('error', () => {});
  mine.on('exit', code => {
    if (mine === child && !retired.has(mine)) process.exit(code ?? 1);
  });
}

function fromServer(from, chunk) {
  if (from !== child) return;
  serverBuffer += chunk;
  let end;
  while ((end = serverBuffer.indexOf('\n')) >= 0) {
    const line = serverBuffer.slice(0, end);
    serverBuffer = serverBuffer.slice(end + 1);
    const message = parse(line);
    if (message?.id === REPLAY) continue;
    if (message && refreshing.has(message.id)) {
      answerRefreshed(message);
      continue;
    }
    if (message && message.method === undefined && message.id !== undefined) inflight.delete(message.id);
    process.stdout.write(line + '\n');
  }
}

function parse(line) {
  try {
    return JSON.parse(line);
  } catch {
    return null;
  }
}

function fromClient(line) {
  const message = parse(line);
  if (message?.method === 'initialize') initialize = line;
  else if (message?.method === 'notifications/initialized') initialized = line;
  else if (message?.method === 'notifications/cancelled') inflight.delete(message.params?.requestId);
  if (message && message.method !== undefined && message.id !== undefined) inflight.add(message.id);
  if (starting) waiting.push(line);
  else toServer(line);
}

// A snapshot's element references die with the server that handed them out. A call that names one before any snapshot of the new
// server is answered with a fresh snapshot, in words the agent can act on, and is not sent on.
function namesElement(message) {
  const args = message?.params?.arguments;
  return message?.method === 'tools/call' && args && typeof args === 'object' && ['ref', 'startRef', 'endRef'].some(key => key in args);
}

function toServer(line) {
  const message = parse(line);
  if (message?.method === 'tools/call' && message.params?.name === SNAPSHOT) stale = false;
  if (stale && namesElement(message)) {
    const internal = `${REFRESH}-${++refreshes}`;
    refreshing.set(internal, message.id);
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: internal, method: 'tools/call', params: { name: SNAPSHOT, arguments: {} } }) + '\n');
    return;
  }
  child.stdin.write(line + '\n');
}

function answerRefreshed(message) {
  const original = refreshing.get(message.id);
  refreshing.delete(message.id);
  stale = false;
  const snapshot = message.result?.content?.filter(part => part.type === 'text').map(part => part.text).join('\n');
  const text = snapshot
    ? `Nothing was done: the browser server restarted since that element reference was made, so the reference is out of date. The page now is below; choose the element again from it.\n\n${snapshot}`
    : 'Nothing was done: the browser server restarted since that element reference was made, so the reference is out of date. Take a snapshot with browser_snapshot and choose the element again.';
  inflight.delete(original);
  process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: original, result: { content: [{ type: 'text', text }] } }) + '\n');
}

async function restart() {
  starting = true;
  const old = child;
  retired.add(old);
  old.kill();
  await new Promise(done => (old.exitCode === null && old.signalCode === null ? old.once('exit', done) : done()));
  start();
  if (initialize) {
    const replay = parse(initialize);
    child.stdin.write(JSON.stringify({ ...replay, id: REPLAY }) + '\n');
    await new Promise(done => {
      const mine = child;
      const watch = chunk => {
        if (chunk.includes(REPLAY)) {
          mine.stdout.off('data', watch);
          done();
        }
      };
      mine.stdout.on('data', watch);
    });
    if (initialized) child.stdin.write(initialized + '\n');
  }

  starting = false;
  stale = true;
  for (const line of waiting.splice(0)) toServer(line);
}

setInterval(async () => {
  if (starting || inflight.size > 0 || !child.pid) return;
  if (workingSet(child.pid) > ceiling) await restart();
}, Number(everyMs)).unref();

process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => {
  clientBuffer += chunk;
  let end;
  while ((end = clientBuffer.indexOf('\n')) >= 0) {
    const line = clientBuffer.slice(0, end);
    clientBuffer = clientBuffer.slice(end + 1);
    if (line.trim()) fromClient(line);
  }
});
process.stdin.on('end', () => {
  child.kill();
  process.exit(0);
});
process.on('SIGTERM', () => {
  child.kill();
  process.exit(0);
});

start();
