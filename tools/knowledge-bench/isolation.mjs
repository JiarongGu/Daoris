import { existsSync } from 'node:fs';
import { dirname, join, parse, resolve } from 'node:path';
import { ALLOWED_TOOLS, DISALLOWED_TOOLS, WINDOW_CEILING } from './design.mjs';

const INSTRUCTION_FILES = ['CLAUDE.md', 'CLAUDE.local.md', 'AGENTS.md', '.claude/CLAUDE.md'];

/** Refuse a root with an instruction file in it or in any folder above it: the harness would load that file. */
export function assertIsolatedRoot(root, { exists = existsSync } = {}) {
  if (!root) throw new Error('--root is required: a folder under the OS temp folder, with no CLAUDE.md or AGENTS.md above it');
  let dir = resolve(root);
  for (;;) {
    for (const f of INSTRUCTION_FILES) {
      const p = join(dir, f);
      if (exists(p)) throw new Error(`refusing --root ${root}: ${p.replace(/\\/g, '/')} would be loaded by every run`);
    }
    const up = dirname(dir);
    if (up === dir) return;
    dir = up;
  }
}

/** The arguments of one run. The brief's flags, plus `--tools` (the exact set), hook events and no persistence. */
export function claudeArgs(prompt, { maxTurns = 20 } = {}) {
  return [
    '-p', prompt,
    '--output-format', 'stream-json', '--verbose',
    '--setting-sources', 'project',
    '--strict-mcp-config', '--mcp-config', '{"mcpServers":{}}',
    '--tools', ALLOWED_TOOLS,
    '--allowedTools', ALLOWED_TOOLS,
    '--disallowedTools', DISALLOWED_TOOLS,
    '--max-turns', String(maxTurns),
    '--include-hook-events',
    '--no-session-persistence',
  ];
}

/** The stream's lines, parsed; a line that is not JSON is kept as `{ type: 'unparsed', line }`. */
export function parseStream(text) {
  return String(text).split(/\r?\n/).filter((l) => l.trim()).map((line) => {
    try { return JSON.parse(line); } catch { return { type: 'unparsed', line }; }
  });
}

export const isHook = (e) => e.type === 'system' && String(e.subtype ?? '').startsWith('hook_');
const hookText = (e) => [e.output, e.stdout, e.hook_output, e.additionalContext].filter((x) => typeof x === 'string').join('\n');

/**
 * Why a canary's stream does not prove isolation, as sentences; empty when it does. The tools are exactly the
 * allowed four, no MCP server, only the harness's built-in plugins, no skill beyond the harness's own (and C's
 * documents), and no hook but G's own ranker, which G must show firing.
 */
export function isolationProblems(events, { design, baselineSkills, corpusNames = [], fixtureDir } = {}) {
  const problems = [];
  const init = events.find((e) => e.type === 'system' && e.subtype === 'init');
  if (!init) return ['no init event in the stream'];
  const tools = [...(init.tools ?? [])].sort();
  if (tools.join(',') !== ALLOWED_TOOLS.split(',').sort().join(',')) problems.push(`tools are ${tools.join(', ')}, not exactly ${ALLOWED_TOOLS}`);
  if ((init.mcp_servers ?? []).length) problems.push(`MCP servers present: ${init.mcp_servers.map((s) => s.name ?? s).join(', ')}`);
  for (const p of init.plugins ?? []) {
    if (p.path !== 'builtin') problems.push(`plugin ${p.name} is not the harness's own (${p.source ?? p.path})`);
  }
  const allowed = new Set([...(baselineSkills ?? []), ...(design === 'C' ? corpusNames : [])]);
  const extra = (init.skills ?? []).filter((s) => !allowed.has(s));
  if (extra.length) problems.push(`skill(s) beyond the harness's own: ${extra.join(', ')}`);
  if (design === 'C') {
    const missing = corpusNames.filter((n) => !(init.skills ?? []).includes(n));
    if (missing.length) problems.push(`${missing.length} corpus skill(s) not listed, e.g. ${missing.slice(0, 3).join(', ')}`);
  }
  if (fixtureDir && resolve(init.cwd ?? '') !== resolve(fixtureDir)) problems.push(`ran in ${init.cwd}, not the fixture`);
  const hooks = events.filter(isHook);
  if (design === 'G') {
    const ours = hooks.filter((e) => /local search ranked/.test(hookText(e)));
    if (!ours.length) problems.push('the ranker hook did not fire, or printed nothing');
    const others = hooks.filter((e) => !/UserPromptSubmit/.test(`${e.hook_event ?? ''} ${e.hook_name ?? ''}`));
    if (others.length) problems.push(`hook events other than the ranker's: ${others.map((e) => e.hook_name ?? e.subtype).join(', ')}`);
  } else if (hooks.length) {
    problems.push(`hook events in a design with no hook: ${hooks.map((e) => `${e.subtype} ${e.hook_name ?? ''}`).join(', ')}`);
  }
  return problems;
}

/** A sentence when the stream shows a rate or spend limit, or a window past the ceiling; else null. */
export function limitReached(events, ceiling = WINDOW_CEILING) {
  for (const e of events) {
    if (e.type === 'rate_limit_event') {
      const info = e.rate_limit_info ?? {};
      if (info.status && !String(info.status).startsWith('allowed')) return `rate limit ${info.status} (${info.rateLimitType ?? 'window'})`;
      for (const [window, w] of Object.entries(info.unifiedWindows ?? {})) {
        if ((w?.utilization ?? 0) >= ceiling) return `${window} window at ${Math.round(w.utilization * 100)}%, past the bench's ${Math.round(ceiling * 100)}% ceiling`;
      }
    }
    if (e.type === 'result' && e.is_error && /limit|quota|credit|spend|budget|429/i.test(`${e.result ?? ''} ${e.api_error_status ?? ''}`)) {
      return `the run ended on a limit: ${String(e.result ?? e.api_error_status).slice(0, 160)}`;
    }
  }
  return null;
}

// ---------------------------------------------------------------------------------------------------------
// Scoring
