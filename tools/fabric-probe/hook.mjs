/**
 * The fabric probe's hook logger (FABRIC-PROBE1; the C0 audit §2).
 *
 *   node tools/fabric-probe/hook.mjs <log>
 *
 * A launcher hands it to Claude Code as an exec-form hook on `PreToolUse` (every tool), `SubagentStart` and
 * `SubagentStop`. It appends the input the harness handed it, whole, to the log as one JSON object a line, and exits 0
 * with nothing on stdout: no decision, so the harness's own rules decide as they would without it. Written
 * synchronously, before it exits, so a kill of the harness right after loses nothing the hook was told.
 */
import { appendFileSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { isMain } from '../fsx.mjs';

/** The line one hook input becomes: the input parsed where it parses, and kept as text where it does not. */
export function hookLine(text, at = new Date()) {
  let input;
  try {
    input = JSON.parse(text);
  } catch {
    return { at: at.toISOString(), pid: process.pid, unreadable: text };
  }
  return { at: at.toISOString(), pid: process.pid, input };
}

if (isMain(import.meta.url)) {
  const log = process.argv[2] ?? process.env.FABRIC_PROBE_HOOK_LOG;
  let text = '';
  try {
    text = readFileSync(0, 'utf8');
  } catch {
    // No input is a line that says so.
  }
  if (log) {
    try {
      mkdirSync(dirname(log), { recursive: true });
      appendFileSync(log, `${JSON.stringify(hookLine(text))}\n`);
    } catch (error) {
      // Never a decision: a hook that fails is a non-blocking error, and the call goes on as its rules say.
      process.stderr.write(`fabric-probe hook: the log would not take a line: ${error.message}\n`);
    }
  }
  process.exit(0);
}
