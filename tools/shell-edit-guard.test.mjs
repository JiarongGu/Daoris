/**
 * D161: a branch-worker's edit through the shell is refused before it runs, by the same pattern that counts one.
 *
 *   node --test tools/shell-edit-guard.test.mjs
 *
 * Run by `npm run verify`. The guard is a PreToolUse hook in `.claude/agents/branch-worker.md`: the harness hands it
 * the tool call as JSON on stdin, and an exit of 2 refuses the call with what it wrote to stderr.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const guard = join(dirname(fileURLToPath(import.meta.url)), 'shell-edit-guard.mjs');
const run = (call) => spawnSync(process.execPath, [guard], { input: JSON.stringify(call), encoding: 'utf8' });
const bash = (command) => ({ tool_name: 'Bash', tool_input: { command } });

test('an edit through the shell is refused, and the refusal names the tools that edit', () => {
  for (const command of [
    "cd web && sed -i '74,76d' help/history.ts",
    "python - <<'E'\nopen(p,'w').write(s)\nE",
    "cat >> src/work/TrustAsk.test.tsx <<'EOF'\nit('x')\nEOF",
  ]) {
    const answered = run(bash(command));
    assert.equal(answered.status, 2, command);
    assert.match(answered.stderr, /Edit or Write/);
    assert.match(answered.stderr, /file-tool-discipline/);
  }
  assert.equal(run({ tool_name: 'PowerShell', tool_input: { command: 'Set-Content -Path a.ts -Value x' } }).status, 2);
});

test('reading, searching, gates and scratch pass', () => {
  for (const command of [
    'sed -n 1,40p A.tsx',
    'grep -c "writeFileSync(" tools/*.mjs',
    'npm run verify > local-verify.log 2>&1',
    "node -e \"fs.writeFileSync('local/scratch/x.json', s)\"",
    'git commit -q -m "x"',
  ]) assert.equal(run(bash(command)).status, 0, command);
  assert.equal(run({ tool_name: 'Edit', tool_input: { file_path: 'a.ts' } }).status, 0, 'the edit tools themselves pass');
});

test('a call it cannot read passes, so a broken input never blocks the work', () => {
  const answered = spawnSync(process.execPath, [guard], { input: 'not json', encoding: 'utf8' });
  assert.equal(answered.status, 0);
});
