/**
 * Who listens on which TCP port, from `netstat -ano` (no PowerShell, no WMI) and, for their programs, the machine reader
 * (`tools/machine-read.mjs`). Windows' own listing: elsewhere the rehearsals ask `lsof`. A listener is a TCP line whose foreign address is
 * the unspecified one with port 0, not one whose state reads `LISTENING`: netstat prints the state in the display language.
 */
import { spawnSync } from 'node:child_process';
import { readProcesses } from './machine-read.mjs';

/** The longest `netstat` or the reader may take: on a fully loaded machine a read can run past thirty seconds, and naming the holder is a refusal's whole use. */
const READ_TIMEOUT_MS = 120_000;

/** The listeners among `netstat -ano`'s lines, as `{ address, port, pid }`, whatever language the state is printed in. */
function listeners(text) {
  return String(text ?? '').split(/\r?\n/).flatMap((line) => {
    const found = /^\s*TCP\s+(\S+):(\d+)\s+(?:0\.0\.0\.0|\[::\]):0\s+\S+\s+(\d+)\s*$/i.exec(line);
    return found ? [{ address: found[1], port: Number(found[2]), pid: Number(found[3]) }] : [];
  });
}

/** The pids of the processes listening on TCP `port` in `netstat -ano`'s text (IPv4 and IPv6), each once, in the order it lists them. */
export function listenersOn(port, text) {
  return [...new Set(listeners(text).filter((each) => each.port === Number(port)).map((each) => each.pid))];
}

/** The loopback ports the process `pid` listens on in `netstat -ano`'s text, in the order it lists them. */
export function loopbackPortsOf(pid, text) {
  return [...new Set(listeners(text).filter((each) => each.address === '127.0.0.1' && each.pid === Number(pid)).map((each) => each.port))];
}

/** `netstat -ano`'s text, or empty where it did not answer. `run` is a test's. */
export const listeningText = (run = spawnSync) => String(run('netstat', ['-ano'], { encoding: 'utf8', windowsHide: true, timeout: READ_TIMEOUT_MS }).stdout ?? '');

/** The listeners of `port` as `pid <id> <name> <command line>`, `; `-joined, or '' where none listens. `run` and `read` are a test's. */
export function holdersOf(port, { run = spawnSync, read = readProcesses } = {}) {
  const pids = listenersOn(port, listeningText(run));
  if (pids.length === 0) return '';
  let table = [];
  try {
    table = read({ pids, timeoutMs: READ_TIMEOUT_MS });
  } catch {
    // The pids alone still name the holders.
  }
  return pids.map((pid) => {
    const row = table.find((one) => one.pid === pid);
    return `pid ${pid} ${row?.name ?? ''} ${row?.command ?? ''}`.trim();
  }).join('; ');
}
