import { appendFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { spawnGit } from '../fsx.mjs';
import { removeWorktree } from '../worktree-remove.mjs';

export const STARTUP = 'local/metrics/startup.jsonl';
export const PROMPT = 'Reply with the single word: ok.';
export const MODELS = ['sonnet', 'opus'];

const SKILLS = '.claude/skills';
const AGENTS = '.claude/agents';
const SETTINGS = '.claude/settings.json';

/** Each repository source a session starts with, and what is taken away to see its share. `removeAll` is the floor: no repository source. */
export const SOURCES = [
  { id: 'brief', remove: ['AGENTS.md'] },
  { id: 'claude-md', remove: ['CLAUDE.md'] },
  { id: 'skills', remove: [SKILLS] },
  { id: 'agents', remove: [AGENTS] },
  { id: 'mcp', remove: ['.mcp.json'] },
  { id: 'plugins', plugins: false },
];

export const VARIANTS = [
  { id: 'all', remove: [], plugins: true },
  ...SOURCES.map(s => ({ id: `without-${s.id}`, remove: s.remove ?? [], plugins: s.plugins ?? true })),
  { id: 'floor', remove: SOURCES.flatMap(s => s.remove ?? []), plugins: false },
];

/** The runs to make: every variant under every model. */
export function planOf(models = MODELS) {
  return models.flatMap(model => VARIANTS.map(variant => ({ model, variant: variant.id })));
}

/** One headless run's JSON result to the first turn's input: sent new, written to the cache, read from it. */
export function usageOf(text) {
  let result;
  try { result = JSON.parse(text); } catch { return null; }
  const u = result?.usage;
  if (!u || result.is_error) return null;
  const input = u.input_tokens ?? 0, cacheWrite = u.cache_creation_input_tokens ?? 0, cacheRead = u.cache_read_input_tokens ?? 0;
  return { input, cacheWrite, cacheRead, output: u.output_tokens ?? 0, total: input + cacheWrite + cacheRead, turns: result.num_turns ?? null };
}

/** The settings text with every plugin switched off. */
export function pluginsOff(text) {
  const settings = JSON.parse(text);
  settings.enabledPlugins = Object.fromEntries(Object.keys(settings.enabledPlugins ?? {}).map(k => [k, false]));
  return `${JSON.stringify(settings, null, 2)}\n`;
}

function git(repo, args) {
  const ran = spawnGit(['-C', repo, ...args], { encoding: 'utf8' });
  if (ran.status !== 0) throw new Error(`git ${args[0]} failed: ${String(ran.stderr).trim()}`);
  return ran.stdout;
}

function applyVariant(tree, variant) {
  for (const path of variant.remove) rmSync(join(tree, path), { recursive: true, force: true });
  if (!variant.plugins && existsSync(join(tree, SETTINGS))) writeFileSync(join(tree, SETTINGS), pluginsOff(readFileSync(join(tree, SETTINGS), 'utf8')));
}

function runOnce(tree, model) {
  const ran = spawnSync('claude', ['-p', PROMPT, '--model', model, '--output-format', 'json', '--no-session-persistence'],
    { cwd: tree, encoding: 'utf8', windowsHide: true, timeout: 240_000, input: '' });
  return ran.status === 0 ? usageOf(ran.stdout) : null;
}

/** The runs, in a throwaway worktree beside the checkout (never inside it, where the checkout's own brief would load too), one at a time; the worktree goes through the removal tool. Rows are appended to the machine-local log. */
export function runStartup(home, models = MODELS) {
  const tree = resolve(dirname(home), `.daoris-startup-${Date.now()}`);
  const log = join(home, STARTUP);
  mkdirSync(dirname(log), { recursive: true });
  const rows = [];
  git(home, ['worktree', 'add', '--detach', tree, 'HEAD']);
  try {
    const setup = spawnSync('node', ['tools/worktree-setup.mjs'], { cwd: tree, stdio: 'inherit', windowsHide: true, timeout: 900_000 });
    if (setup.status !== 0) throw new Error('worktree-setup failed in the throwaway worktree');
    for (const { model, variant } of planOf(models)) {
      git(tree, ['checkout', '--', '.']);
      applyVariant(tree, VARIANTS.find(v => v.id === variant));
      const usage = runOnce(tree, model);
      const row = { schema: 1, at: new Date().toISOString(), commit: git(tree, ['rev-parse', 'HEAD']).trim(), model, variant, ...(usage ?? { failed: true }) };
      appendFileSync(log, `${JSON.stringify(row)}\n`);
      rows.push(row);
    }
  } finally {
    removeWorktree(home, tree, { force: true });
  }
  return rows;
}

/** Per model: the floor (every repository source removed), and each source's share as the tokens the full start has over the start without it. */
export function sharesOf(rows) {
  const models = [...new Set(rows.map(r => r.model))];
  return models.map(model => {
    const at = id => rows.filter(r => r.model === model && r.variant === id && !r.failed).at(-1)?.total ?? null;
    const all = at('all'), floor = at('floor');
    return {
      model, all, floor, repository: all !== null && floor !== null ? all - floor : null,
      sources: Object.fromEntries(SOURCES.map(s => { const without = at(`without-${s.id}`); return [s.id, all !== null && without !== null ? all - without : null]; })),
    };
  });
}
