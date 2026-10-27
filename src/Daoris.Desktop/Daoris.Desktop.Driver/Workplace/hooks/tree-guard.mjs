// Daoris's tree guard (PERM3, D72): a PreToolUse hook that refuses a file write outside the session's
// own tree. Shipped inside the driver and written under the Daoris home; the harness runs it as
//
//   node tree-guard.mjs <tree> [<target> ...]
//
// with the call as JSON on stdin. Each <target> after the tree is a checkout the person declared this
// session's repository may also write into (D107): a write there is let through as one in the tree is.
// Zero dependencies — it runs inside every session.
//
// It judges the tools that name a file (Write, Edit, MultiEdit, NotebookEdit) by their path fields,
// resolved through links. A shell command's writes cannot be judged by reading the command, so Bash is
// not its call; there the harness's own working-directory boundary stands.
//
// 🔴 It refuses STRUCTURALLY: `permissionDecision: "deny"` on stdout, exit 0. The harness reads any exit
// but 2 as a non-blocking error, so a guard that failed by exiting would let the write through. Inside
// the tree it says NOTHING — never "allow", which would lift the harness's own asking.

import { lstatSync, readlinkSync, realpathSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Whether `target` is outside `tree`. Pure: the platform is an argument, and so is the resolution of
 * links, so the judgement is the same on any machine that tests it.
 *
 * @param {{ tree: string, target: string, cwd?: string | null, platform?: string,
 *           realpath?: (p: string) => string }} call
 */
export function judge({ tree, target, cwd = null, platform = process.platform, realpath = (p) => p }) {
  const paths = platform === 'win32' ? path.win32 : path.posix;
  const fold = platform === 'win32' ? (p) => p.toLowerCase() : (p) => p;

  const root = realpath(paths.resolve(tree));
  // A relative path is the harness's own cwd's, and only then the tree's.
  const resolved = realpath(paths.resolve(cwd ?? tree, target));
  const relative = paths.relative(fold(root), fold(resolved));

  // Inside when the way from the tree to the target never climbs out of it. On Windows another drive
  // or a UNC share answers an absolute path, which is outside by definition.
  const outside = relative === '..' || relative.startsWith(`..${paths.sep}`) || paths.isAbsolute(relative);
  return { outside, resolved };
}

/**
 * The real path of a file that may not exist yet: the deepest ancestor that does, resolved through its
 * links, with the rest put back. A write through a link inside the tree that leads out of it is outside.
 *
 * 🔴 A link whose target does not exist YET fails to resolve exactly as a missing file does — and a
 * write through it lands wherever it points, creating the target. Climbing past it as though it were
 * a plain name judged such a write inside the tree (REV3). So a component that will not resolve is
 * asked whether it is a link, and a link is followed. Too many links is a refusal, never a guess.
 */
export function resolveThroughLinks(target, depth = 0) {
  if (depth > 40) throw new Error(`too many links resolving \`${target}\``);
  const rest = [];
  let at = target;
  for (;;) {
    try {
      return path.join(realpathSync.native(at), ...rest.reverse());
    } catch {
      let link = null;
      try {
        if (lstatSync(at).isSymbolicLink()) link = readlinkSync(at);
      } catch {
        // Not there at all: an ordinary name the write would create.
      }
      if (link !== null) {
        return resolveThroughLinks(path.join(path.resolve(path.dirname(at), link), ...rest.reverse()), depth + 1);
      }

      const parent = path.dirname(at);
      if (parent === at) return target;
      rest.push(path.basename(at));
      at = parent;
    }
  }
}

/** The paths a call would write, by the fields the harness gives each tool. */
export function targets(tool, input) {
  if (!input || typeof input !== 'object') return [];
  const found = [];
  if (tool === 'NotebookEdit') found.push(input.notebook_path);
  if (tool === 'Write' || tool === 'Edit' || tool === 'MultiEdit') found.push(input.file_path);
  if (tool === 'MultiEdit' && Array.isArray(input.edits)) {
    for (const edit of input.edits) found.push(edit?.file_path);
  }
  return found.filter((one) => typeof one === 'string' && one.length > 0);
}

function deny(reason) {
  process.stdout.write(`${JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'deny',
      permissionDecisionReason: `Daoris's tree guard: ${reason}`,
    },
  })}\n`);
}

async function main() {
  let text = '';
  for await (const chunk of process.stdin) text += chunk;

  let call;
  try {
    call = JSON.parse(text);
  } catch {
    // Fail closed where this guard is the one deciding: the harness only sends it file writes.
    deny('it could not read this call, so it refuses it rather than guess.');
    return;
  }

  const paths = targets(call?.tool_name, call?.tool_input);
  if (paths.length === 0) return;

  // The session's tree, as the driver named it — then the harness's own project directory. Each argument
  // after it is a declared write target (D107).
  const tree = process.argv[2] || process.env.CLAUDE_PROJECT_DIR;
  if (!tree) {
    deny('it was not told which tree this session works in, so it refuses every write rather than guess.');
    return;
  }
  const declared = process.argv.slice(3).filter((one) => one.length > 0);

  const cwd = typeof call.cwd === 'string' && call.cwd.length > 0 ? call.cwd : null;
  for (const target of paths) {
    let judged;
    try {
      judged = [tree, ...declared].map((root) => judge({ tree: root, target, cwd, realpath: resolveThroughLinks }));
    } catch (error) {
      // Fail closed: a path this guard cannot resolve is not one it lets through.
      deny(`it could not resolve \`${target}\` (${error instanceof Error ? error.message : error}), so it refuses the write.`);
      return;
    }
    if (judged.every(({ outside }) => outside)) {
      const also = declared.length === 0 ? ''
        : ` and the checkouts its repository may also write into (${declared.map((one) => `\`${one}\``).join(', ')})`;
      deny(`\`${judged[0].resolved}\` is outside this session's tree \`${tree}\`${also}. A session writes only inside `
        + 'its own tree; a change needed elsewhere is a quest to whoever owns it.');
      return;
    }
  }
}

// Run only as the hook — imported, it is the judgement alone.
const same = (a, b) => (process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b);
if (process.argv[1] && same(path.resolve(process.argv[1]), fileURLToPath(import.meta.url))) {
  await main();
}
