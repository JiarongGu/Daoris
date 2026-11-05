// MCPMEM1: keeps a stdio MCP server's memory under a ceiling by starting it afresh when it has grown past it and no call is in
// flight. The harness sees one server throughout: the client's initialize is replayed to the new one and its answer swallowed.
//
//   node server-keeper.mjs <ceiling MB> <check every ms> <script> [arguments...]
//
// Newline-delimited JSON-RPC, as the MCP stdio transport writes it. Anything that is not a line of JSON passes through.
import { spawn, execFileSync } from 'node:child_process';

const [ceilingMb, everyMs, script, ...rest] = process.argv.slice(2);
const ceiling = Number(ceilingMb) * 1024 * 1024;
const REPLAY = 'server-keeper-initialize';

let child;
let starting = false;
let initialize = null;
let initialized = null;
const inflight = new Set();
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
  mine.stdout.on('data', chunk => fromServer(mine, chunk));
  mine.stdin.on('error', () => {});
  mine.on('exit', code => {
    if (mine === child && !starting) process.exit(code ?? 1);
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
  if (message && message.method !== undefined && message.id !== undefined) inflight.add(message.id);
  if (starting) waiting.push(line);
  else child.stdin.write(line + '\n');
}

async function restart() {
  starting = true;
  const old = child;
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
  for (const line of waiting.splice(0)) child.stdin.write(line + '\n');
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
