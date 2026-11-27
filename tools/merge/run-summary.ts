/**
 * The run's summary, one record built by code from what git, the code map, the area documents and the receipts say:
 * what changed (by module and by rule), what is verified (each unit with its receipt), and what is left for judgement. Nothing in it
 * is an agent's word. Its readers are the same record: the second opinion reads it instead of finding the change again, the window's
 * Outputs panel draws it, and the landing record keeps it.
 */
import { coverage, type GateReceipt, type Required } from './receipts.ts';

export type Change = { path: string; added: number; removed: number };
export type Module = { id: string; path: string };
/** An area document before and after the change, where it changed; null where it did not exist on that side. */
export type AreaDocument = { path: string; before: string | null; after: string | null };
export type Deferred = { name: string; why: string };

export type SummaryInput = {
  branch: string; tip: string; base: string; changes: Change[]; modules: Module[]; areas: AreaDocument[];
  receipts: GateReceipt[]; required: Required[]; deferred: Deferred[];
};

export type RuleChange = { area: string; section: string; rule: string; change: 'added' | 'removed' | 'rewritten'; heading: string };
export type Judgement = { kind: 'decision' | 'rule' | 'deferred' | 'failed' | 'unverified'; what: string };

export type RunSummary = {
  schema: 1; branch: string; tip: string; base: string;
  changed: {
    files: number; added: number; removed: number;
    modules: { id: string; files: number; added: number; removed: number }[];
    unplaced: string[];
    rules: RuleChange[];
  };
  verified: {
    passed: number; failed: number;
    units: { unit: string; command: string; verdict: string; replayed: boolean; ms: number; at: string; note?: string }[];
    missing: Required[];
  };
  judgement: Judgement[];
};

const RULE = /^(\d+[a-z]?)\.\s+(.*)$/;
const compare = (a: string, b: string): number => {
  const [na, nb] = [Number.parseInt(a, 10), Number.parseInt(b, 10)];
  return na === nb ? a.localeCompare(b) : na - nb;
};

/** A document's numbered rules by section and number, each with the lines that run on under it, up to the next rule or heading: a list that starts at 1 again under another heading is another set. */
function rulesIn(text: string | null): Map<string, { section: string; rule: string; heading: string; body: string }> {
  const found = new Map<string, { section: string; rule: string; heading: string; lines: string[] }>();
  let section = '';
  let current: { heading: string; lines: string[] } | null = null;
  for (const line of (text ?? '').split(/\r?\n/)) {
    const rule = RULE.exec(line);
    const title = /^#{1,6}\s+(.*)$/.exec(line);
    if (rule) {
      current = { heading: (rule[2] ?? '').slice(0, 80), lines: [line] };
      found.set(`${section}|${rule[1]}`, { section, rule: rule[1] as string, ...current });
    } else if (title) {
      section = (title[1] ?? '').trim();
      current = null;
    } else if (current) {
      current.lines.push(line);
    }
  }
  return new Map([...found].map(([key, each]) => [key, { section: each.section, rule: each.rule, heading: each.heading, body: each.lines.join('\n').trim() }]));
}

/** The rules of a document that were added, removed or rewritten, in rule order: a sentence the change made false is found here, not remembered. */
export function rulesOf(path: string, before: string | null, after: string | null): RuleChange[] {
  const [was, is] = [rulesIn(before), rulesIn(after)];
  const out: RuleChange[] = [];
  for (const [key, now] of is) {
    const then = was.get(key);
    if (!then) out.push({ area: path, section: now.section, rule: now.rule, change: 'added', heading: now.heading });
    else if (then.body !== now.body) out.push({ area: path, section: now.section, rule: now.rule, change: 'rewritten', heading: now.heading });
  }
  for (const [key, then] of was) if (!is.has(key)) out.push({ area: path, section: then.section, rule: then.rule, change: 'removed', heading: then.heading });
  return out.sort((a, b) => a.section.localeCompare(b.section) || compare(a.rule, b.rule));
}

/** The module a path belongs to: the one whose path is its longest prefix. */
const moduleOf = (path: string, modules: Module[]): Module | undefined =>
  modules.filter((each) => path === each.path || path.startsWith(`${each.path}/`)).sort((a, b) => b.path.length - a.path.length)[0];

export function buildSummary(input: SummaryInput): RunSummary {
  const by = new Map<string, { id: string; files: number; added: number; removed: number }>();
  const unplaced: string[] = [];
  for (const change of input.changes) {
    const module = moduleOf(change.path, input.modules);
    if (!module) {
      unplaced.push(change.path);
      continue;
    }
    const entry = by.get(module.id) ?? { id: module.id, files: 0, added: 0, removed: 0 };
    entry.files += 1;
    entry.added += change.added;
    entry.removed += change.removed;
    by.set(module.id, entry);
  }
  const rules = input.areas.flatMap((area) => rulesOf(area.path, area.before, area.after));
  const seen = coverage(input.required, input.receipts);
  const missing = seen.missing;
  const failed = input.receipts.filter((each) => seen.failed.some((unit) => unit.unit === each.unit));
  const judgement: Judgement[] = [
    ...input.changes.filter((each) => each.path.startsWith('docs/decisions/')).map((each) => ({ kind: 'decision' as const, what: `${each.path}: a choice that rejected an alternative; read it` })),
    ...rules.map((each) => ({ kind: 'rule' as const, what: `${each.area} ${each.section ? `(${each.section}) ` : ''}rule ${each.rule} ${each.change}: ${each.heading}` })),
    ...input.deferred.map((each) => ({ kind: 'deferred' as const, what: `${each.name}: ${each.why}` })),
    ...failed.map((each) => ({ kind: 'failed' as const, what: `${each.unit}: \`${each.command}\` failed${each.note ? `: ${each.note}` : ''}` })),
    ...missing.map((each) => ({ kind: 'unverified' as const, what: `${each.unit}: \`${each.command}\` has no pass at this tip` })),
  ];
  return {
    schema: 1, branch: input.branch, tip: input.tip, base: input.base,
    changed: {
      files: input.changes.length,
      added: input.changes.reduce((sum, each) => sum + each.added, 0),
      removed: input.changes.reduce((sum, each) => sum + each.removed, 0),
      modules: [...by.values()].sort((a, b) => b.files - a.files || a.id.localeCompare(b.id)),
      unplaced, rules,
    },
    verified: {
      passed: input.receipts.filter((each) => each.verdict === 'pass').length,
      failed: failed.length,
      units: input.receipts.map((each) => ({ unit: each.unit, command: each.command, verdict: each.verdict, replayed: each.replayed, ms: each.ms, at: each.at, ...(each.note ? { note: each.note } : {}) })),
      missing,
    },
    judgement,
  };
}

const minutes = (ms: number): string => `${Math.max(0, Math.round(ms / 60_000))} min`;

/** The record as the lines a terminal or a brief shows: the three parts, in order. */
export function summaryLines(summary: RunSummary): string[] {
  const { changed, verified } = summary;
  const lines = [`Changed: ${changed.files} files, +${changed.added} −${changed.removed}, ${summary.tip.slice(0, 7)} against ${summary.base.slice(0, 7)}`];
  for (const each of changed.modules) lines.push(`  ${each.id}: ${each.files} file${each.files === 1 ? '' : 's'}, +${each.added} −${each.removed}`);
  if (changed.unplaced.length) lines.push(`  in no module: ${changed.unplaced.join(', ')}`);
  for (const each of changed.rules) lines.push(`  ${each.area} ${each.section ? `(${each.section}) ` : ''}rule ${each.rule} ${each.change}: ${each.heading}`);
  lines.push(`Verified: ${verified.passed} passed, ${verified.failed} failed, ${verified.missing.length} without a receipt`);
  for (const each of verified.units) lines.push(`  ${each.unit}: ${each.verdict}${each.replayed ? ', replayed from a standing pass' : ` (${minutes(each.ms)})`}${each.note ? `: ${each.note}` : ''}`);
  lines.push(summary.judgement.length ? `Left for judgement: ${summary.judgement.length}` : 'Left for judgement: nothing');
  for (const each of summary.judgement) lines.push(`  ${each.kind}: ${each.what}`);
  return lines;
}
