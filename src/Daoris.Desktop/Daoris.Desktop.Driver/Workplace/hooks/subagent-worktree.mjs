// The worktree a session's own subagent works in, which the tree guard lets it write as it lets the session write its tree.
// Claude Code makes a subagent's worktree under its repository's main checkout (`<checkout>/.claude/worktrees/<name>`), and a
// Daoris session's tree is a linked worktree elsewhere, so the subagent's worktree is never inside the session's tree.
// Zero dependencies: the guard imports it inside every session.

import path from 'node:path';

/** The text after `gitdir:` in a linked worktree's `.git` file, resolved against the worktree, or null. */
function gitDir(checkout, { paths, read }) {
  const text = read(paths.join(checkout, '.git'));
  const line = typeof text === 'string' ? /^gitdir:\s*(.+?)\s*$/m.exec(text) : null;
  return line ? paths.resolve(checkout, line[1]) : null;
}

/** The common git directory a linked worktree shares with its main checkout, or null where the checkout is not one. */
function commonDir(checkout, context) {
  const own = gitDir(checkout, context);
  if (own === null) return null;
  const text = context.read(context.paths.join(own, 'commondir'));
  return typeof text === 'string' && text.trim().length > 0 ? context.paths.resolve(own, text.trim()) : null;
}

/**
 * The root of the worktree a subagent's call stands in, where that worktree is one the harness made for a subagent of the
 * session's own repository; null for every other call. Pure: the platform and the file reads are arguments.
 *
 * Only a subagent's call (`agentId`) is answered, and only from where it stands (`cwd`), so the session's own agent never
 * writes into a subagent's worktree, and no subagent writes into another's.
 *
 * @param {{ tree: string, cwd?: string | null, agentId?: string | null, platform?: string,
 *           read: (file: string) => string | null }} call
 */
export function subagentWorktree({ tree, cwd, agentId, platform = process.platform, read }) {
  if (typeof agentId !== 'string' || agentId.length === 0 || typeof cwd !== 'string' || cwd.length === 0) return null;
  const paths = platform === 'win32' ? path.win32 : path.posix;
  const fold = platform === 'win32' ? (p) => p.toLowerCase() : (p) => p;
  const context = { paths, read };

  const common = commonDir(paths.resolve(tree), context);
  if (common === null || paths.basename(common) !== '.git') return null;

  const folder = paths.join(paths.dirname(common), '.claude', 'worktrees');
  const relative = paths.relative(fold(folder), fold(paths.resolve(cwd)));
  if (relative === '' || relative === '..' || relative.startsWith(`..${paths.sep}`) || paths.isAbsolute(relative)) return null;

  const worktree = paths.join(folder, paths.resolve(cwd).slice(folder.length + 1).split(paths.sep)[0]);
  const its = commonDir(worktree, context);
  return its !== null && fold(its) === fold(common) ? worktree : null;
}
