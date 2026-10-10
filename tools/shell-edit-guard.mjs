#!/usr/bin/env node
/**
 * Refuses a branch-worker's edit through the shell before it runs (D161).
 *
 * A PreToolUse hook in `.claude/agents/branch-worker.md`: the harness hands it the tool call as JSON on stdin, and an
 * exit of 2 refuses the call, its stderr being what the worker reads. A shell command that writes a file of the work
 * (`sed -i`, a script's write, a redirect into a source or record file) is refused; a read, a search, a gate, a log and
 * scratch pass. The pattern is `subagent-usage.mjs`'s, so what is refused is what is counted.
 *
 * ## Why
 *
 * `file-tool-discipline` is always loaded and every worker's prompt restated it, and still 6 of 14 Sonnet workers edited
 * through the shell, one by deleting lines at computed offsets (D160's second SUBLOAD1c note). A rule an agent drops,
 * a transcript shows it dropping, belongs in a mechanism, not a sentence (D161). A call the guard cannot read passes, so
 * a broken input never blocks the work.
 */
import { readFileSync } from 'node:fs';
import { isMain } from './fsx.mjs';
import { editsThroughShell } from './subagent-usage.mjs';

/** The exit and the sentence for one tool call: 2 refuses it, 0 lets it run. */
export function judge(call) {
  const name = call?.tool_name;
  const command = String(call?.tool_input?.command ?? '');
  if ((name !== 'Bash' && name !== 'PowerShell') || !editsThroughShell(command)) return { exit: 0 };
  return {
    exit: 2,
    says: 'Refused (file-tool-discipline, D161): this command writes a file through the shell. Change the file with the '
      + 'Edit or Write tool instead: a scripted edit passes your text through another language\'s escaping, and a pattern '
      + 'that stops matching changes nothing and says nothing. Reading, searching, gates, logs and local/scratch stay open.',
  };
}

if (isMain(import.meta.url)) {
  let call = null;
  try { call = JSON.parse(readFileSync(0, 'utf8')); } catch { /* unreadable: let it run */ }
  const { exit, says } = judge(call);
  if (says) process.stderr.write(`${says}\n`);
  process.exit(exit);
}
