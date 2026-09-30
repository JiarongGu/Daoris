import { describe, expect, it } from 'vitest';
import barrel from '../shell.ts?raw';
import * as shell from '../shell';

/**
 * The bridge by domain (MOD3): each domain's types and calls live in `bridge/<domain>.ts`, and `shell.ts`
 * is only the barrel re-exporting them. One file held every call and type the page asks the shell, and
 * ten of eighteen branches in three days edited it (`docs/2026-09-30-parallel-development-design.md`).
 *
 * The rule is held here because it breaks one convenient line at a time: a hook added to the barrel
 * "for now" is how the god file comes back. A new domain is a new file and one line in the barrel.
 */

const sources = import.meta.glob('./*.ts', { eager: true, query: '?raw', import: 'default' }) as Record<string, string>;
const modules = import.meta.glob(['./*.ts', '!./*.test.ts'], { eager: true }) as Record<string, Record<string, unknown>>;

/** The domains' own helper — a call onto a route by name — shared by the domains and re-exported by none. */
const INTERNAL = new Set(['call']);

const nameOf = (path: string) => path.replace(/^\.\/(.+)\.ts$/, '$1');
const domains = Object.keys(sources).filter((path) => !/\.test\.ts$/.test(path)).map(nameOf).sort();

/** What is wrong with a barrel: code of its own, a domain it leaves out, or the helper it must not export. */
export function barrelProblems(source: string, names: readonly string[]): string[] {
  const problems: string[] = [];
  const exported = new Set<string>();
  for (const line of source.split('\n')) {
    const text = line.trim();
    if (text === '' || text.startsWith('//')) continue;
    const reexport = /^export \* from '\.\/bridge\/([\w-]+)';$/.exec(text);
    if (!reexport) {
      problems.push(`shell.ts holds more than re-exports: ${text}`);
      continue;
    }
    exported.add(reexport[1]!);
  }
  for (const name of names) {
    if (INTERNAL.has(name) && exported.has(name)) problems.push(`shell.ts re-exports bridge/${name}.ts, which is the domains' own`);
    if (!INTERNAL.has(name) && !exported.has(name)) problems.push(`shell.ts leaves out bridge/${name}.ts`);
  }
  for (const name of exported) if (!names.includes(name)) problems.push(`shell.ts re-exports bridge/${name}.ts, which does not exist`);
  return problems;
}

describe('the bridge by domain', () => {
  it('catches what it is meant to catch — the check itself, sabotaged', () => {
    const names = ['call', 'driver', 'plugins'];
    const sound = "// the barrel\nexport * from './bridge/driver';\nexport * from './bridge/plugins';\n";
    expect(barrelProblems(sound, names)).toEqual([]);
    // A hook written into the barrel "for now".
    expect(barrelProblems(`${sound}export const useThing = () => null;\n`, names))
      .toEqual(['shell.ts holds more than re-exports: export const useThing = () => null;']);
    // A new domain nobody re-exported, and the helper exported by mistake.
    expect(barrelProblems("export * from './bridge/driver';\nexport * from './bridge/call';\n", names))
      .toEqual(["shell.ts re-exports bridge/call.ts, which is the domains' own", 'shell.ts leaves out bridge/plugins.ts']);
  });

  it('is looking at files at all', () => {
    expect(domains.length).toBeGreaterThan(10);
    expect(domains).toContain('call');
  });

  it('holds: shell.ts re-exports every domain and holds nothing of its own', () => {
    expect(barrelProblems(barrel, domains)).toEqual([]);
  });

  it('serves every export of every domain through the barrel, the same object', () => {
    // `export *` drops a name two domains both export, without a word at runtime.
    expect(Object.keys(modules).length).toBe(domains.length);
    const lost = Object.entries(modules)
      .filter(([path]) => !INTERNAL.has(nameOf(path)))
      .flatMap(([path, module]) => Object.entries(module)
        .filter(([name, value]) => (shell as Record<string, unknown>)[name] !== value)
        .map(([name]) => `${nameOf(path)}.${name}`));
    expect(lost).toEqual([]);
  });

  it('keeps the route helper out of the page: nothing outside the bridge imports it', () => {
    const everywhere = import.meta.glob(['../**/*.{ts,tsx}', '!./**'], { eager: true, query: '?raw', import: 'default' }) as Record<string, string>;
    const reaching = Object.entries(everywhere)
      .filter(([, source]) => /from\s+'(?:\.\.?\/)+bridge\/call'/.test(source))
      .map(([path]) => path);
    expect(Object.keys(everywhere).length).toBeGreaterThan(50);
    expect(reaching).toEqual([]);
  });

  it('never imports the barrel from a domain, which would make every domain load every other', () => {
    const circular = Object.entries(sources)
      .filter(([path, source]) => !/\.test\.ts$/.test(path) && /from\s+'\.\.\/shell'/.test(source))
      .map(([path]) => path);
    expect(circular).toEqual([]);
  });
});
