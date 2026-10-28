// Daoris's lineage hook (FABRIC-C1a, D173): what Claude Code says of the agent behind each call to Daoris's connector, and of
// each subagent's start and stop, appended to the session's lineage file for the driver to bind its native children to the
// part. Shipped inside the driver and written under the Daoris home; the harness runs it as
//
//   node lineage-hook.mjs <lineage file>
//
// on `PreToolUse` for the connector's tools, `SubagentStart` and `SubagentStop`, with the hook's input as JSON on stdin.
// Zero dependencies: it runs inside every session that is handed it.
//
// 🔴 Ids only. The input also names the working directory and transcript paths; none of them is read. A value is kept only
// where it is shaped as an id (the connector's `CallLineage.Id`, the driver's `ChildBindings` twin), so no path reaches the
// file (D46, D47).
//
// 🔴 It decides nothing: nothing on stdout, exit 0, whatever happens. A hook that fails is a non-blocking error, and the call
// goes on as the session's rules say. A line it cannot write is a lineage the driver says it does not know.
//
// The file is shared with the connector, which appends holding it alone: a write refused while the connector holds it is
// tried again for about two seconds.

import { appendFileSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ID = /^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$/;
const EVENTS = { PreToolUse: 'call', SubagentStart: 'started', SubagentStop: 'stopped' };
const HELD = new Set(['EBUSY', 'EPERM', 'EACCES']);

const id = (value) => (typeof value === 'string' && ID.test(value) ? value : undefined);

/** The line one hook input becomes, or null for an event this hook does not record. */
export function lineageLine(input, at = new Date()) {
  const event = EVENTS[input?.hook_event_name];
  if (!event) return null;
  const line = { at: at.toISOString(), by: 'hook', event };
  const put = (name, value) => {
    if (value !== undefined) line[name] = value;
  };
  put('agent', id(input.agent_id));
  put('type', id(input.agent_type));
  if (event === 'call') {
    put('toolUse', id(input.tool_use_id));
    put('tool', id(input.tool_name));
  }
  put('session', id(input.session_id));
  return JSON.stringify(line);
}

function append(file, text) {
  mkdirSync(path.dirname(file), { recursive: true });
  for (let attempt = 1; ; attempt++) {
    try {
      appendFileSync(file, `${text}\n`);
      return;
    } catch (error) {
      if (attempt >= 80 || !HELD.has(error?.code)) throw error;
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 25);
    }
  }
}

function main() {
  const file = process.argv[2];
  if (!file) return;
  let input;
  try {
    input = JSON.parse(readFileSync(0, 'utf8'));
  } catch {
    return;
  }
  const line = lineageLine(input);
  if (line !== null) append(file, line);
}

// Run only as the hook — imported, it is the line alone.
const same = (a, b) => (process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b);
if (process.argv[1] && same(path.resolve(process.argv[1]), fileURLToPath(import.meta.url))) {
  try {
    main();
  } catch {
    // Never a decision, never a failure the harness would show: the line is lost, and the lineage is unknown.
  }
  process.exit(0);
}
