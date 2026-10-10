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
import { readFileSync } from 'node:fs';
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

/**
 * The guard's own exit is not the hook's: the harness reads the exit of the shell the definition names. Windows
 * PowerShell's `-Command` exits 1 when its last native command fails, whatever that command's code, and the harness
 * reads 1 as a non-blocking error: from SUBLOAD1c's first workers to WIRE1, the guard wrote its refusal and the command
 * ran (D161's guard note). So the hook is run here as the definition writes it, through its shell.
 */
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const hook = (() => {
  const definition = readFileSync(join(root, '.claude', 'agents', 'branch-worker.md'), 'utf8');
  const shell = /^\s*shell:\s*(\S+)/m.exec(definition)?.[1] ?? 'bash';
  const command = /^\s*command:\s*"((?:[^"\\]|\\.)*)"/m.exec(definition)?.[1];
  return { shell, command };
})();
const viaShell = (call) => hook.shell === 'powershell'
  ? spawnSync('powershell', ['-NoProfile', '-NonInteractive', '-Command', hook.command], { cwd: root, input: JSON.stringify(call), encoding: 'utf8' })
  : spawnSync('bash', ['-c', hook.command], { cwd: root, input: JSON.stringify(call), encoding: 'utf8' });

test('the definition\'s hook refuses through its own shell, with exit 2', { skip: hook.shell === 'powershell' && process.platform !== 'win32' }, () => {
  assert.ok(hook.command, 'the branch-worker definition names the guard as a command');
  assert.equal(viaShell(bash("sed -i 's/a/b/' src/x.ts")).status, 2, `${hook.shell}: ${hook.command}`);
  assert.equal(viaShell(bash('sed -n 1,40p A.tsx')).status, 0, 'a read still passes');
});
