// A Claude Code command hook (PreToolUse): reads the hook payload on stdin, refuses any shell
// command that would push, and lets everything else through. Exit 2 + stderr is the block.
//
// The hook runs INSIDE dsh's sandbox (workspace-write), so its own evidence log must live inside the
// workspace — the first version wrote beside this script, outside the workspace, and the sandbox
// denied it, which the bridge folded as a non-blocking failure (exit 1 → "pass"). That was probe 4's
// first lesson, and it is recorded in the evidence note.
import { appendFileSync } from 'node:fs'
import { join } from 'node:path'

let raw = ''
process.stdin.setEncoding('utf8')
for await (const chunk of process.stdin) raw += chunk
let payload = {}
try { payload = JSON.parse(raw) } catch {}
const workspace = process.env.CLAUDE_PROJECT_DIR ?? process.cwd()
try {
  appendFileSync(join(workspace, '.git', 'dsh-hook-calls.jsonl'), JSON.stringify({
    t: new Date().toISOString(), cwd: process.cwd(), CLAUDE_PROJECT_DIR: process.env.CLAUDE_PROJECT_DIR ?? null, payload,
  }) + '\n')
} catch (error) {
  process.stderr.write(`hook log failed: ${error.message}\n`)
}
const command = String(payload?.tool_input?.command ?? '')
if (/\bgit\s+push\b/.test(command)) {
  const reason = 'refused by the repository hook: a push leaves the repository and is not the session\'s to do (D37 boundary)'
  if (process.env.HOOK_BLOCK_BY_EXIT_CODE === '1') {
    // The exit-2 form. Verified on this machine to be folded as "pass": Windows PowerShell 5.1
    // reports 1 for any failed native command under -Command, and dsh's Windows executor is 5.1
    // when PowerShell 7 is absent.
    process.stderr.write(reason)
    process.exit(2)
  }
  // The structured form — the same decision Claude Code accepts on a clean exit, and the only form
  // that survives the exit-code collapse above.
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: 'deny', permissionDecisionReason: reason },
  }))
  process.exit(0)
}
process.exit(0)
