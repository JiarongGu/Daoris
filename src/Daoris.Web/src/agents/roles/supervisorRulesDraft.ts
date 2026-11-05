import type { RoleChangePress, SupervisorCountRead, SupervisorRuleSetRead, SupervisorRuleWire, SupervisorRulesRead } from '../../data/wiring';

/** The supervisor's seeded id: its rules ride a press of this id (`SupervisorRuleDoor`). */
export const SUPERVISOR = 'supervisor';
export const MACHINE = 'machine';

/** One rule as the editor holds it: text while typed, so a half-typed number or an IME composition survives. */
export type RuleDraft = { key: number; count: string; at: string; do: string; upTo: string; role: string };

/** A place's rules as the editor holds them, with the revision they were read at. */
export type RulesDraft = { place: string; revision: number; rows: RuleDraft[] };

/** The one role a rule names, or none for every role. */
const roleOf = (rule: SupervisorRuleWire): string => rule.roles?.[0] ?? '';

/** The draft a place opens with, from its set in `ROLES`; a place that holds none opens empty at revision 0. */
export function draftOf(place: string, set: SupervisorRuleSetRead | undefined): RulesDraft {
  return {
    place, revision: set?.revision ?? 0,
    rows: (set?.rules ?? []).map((rule, key) => ({
      key, count: rule.count, at: String(rule.at), do: rule.do, upTo: rule.upTo === null || rule.upTo === undefined ? '' : String(rule.upTo), role: roleOf(rule),
    })),
  };
}

/** The place's set among `read`, if it holds one. */
export const setOf = (read: SupervisorRulesRead | undefined, place: string): SupervisorRuleSetRead | undefined => read?.sets.find((set) => set.place === place);

/** The count a new rule starts on: the first not yet ruled for every role here, else the first. */
export function nextCount(counts: readonly SupervisorCountRead[], rows: readonly RuleDraft[]): SupervisorCountRead | undefined {
  return counts.find((count) => !rows.some((row) => row.count === count.count && row.role === '')) ?? counts[0];
}

/** The action a count offers first: never `off` or `refer`, which a person chooses. */
const firstAction = (count: SupervisorCountRead): string => count.actions.find((action) => action !== 'off' && action !== 'refer') ?? count.actions[0] ?? 'hold';

/** A new rule on `count`: its least threshold and its first action. */
export function blankRule(count: SupervisorCountRead, key: number): RuleDraft {
  const action = firstAction(count);
  return { key, count: count.count, at: String(count.least), do: action, upTo: action === 'wait' ? '30' : '', role: '' };
}

/** A rule moved to another count keeps what it can: its threshold held to the count's bounds, its action the count's first if it does not allow it. */
export function recount(row: RuleDraft, count: SupervisorCountRead): RuleDraft {
  const at = Number(row.at);
  const clamped = row.at !== '' && Number.isFinite(at) ? String(Math.min(count.most, Math.max(count.least, at))) : String(count.least);
  const action = count.actions.includes(row.do) ? row.do : firstAction(count);
  return { ...row, count: count.count, at: clamped, do: action, upTo: action === 'wait' ? row.upTo || '30' : '' };
}

/** A rule given another action: `wait` takes minutes, every other action none. */
export function reaction(row: RuleDraft, action: string): RuleDraft {
  return { ...row, do: action, upTo: action === 'wait' ? row.upTo || '30' : '' };
}

/** Whether the draft says what the stored set does, so *Save* has nothing to put to work. */
export function sameAsStored(draft: RulesDraft, set: SupervisorRuleSetRead | undefined): boolean {
  const was = draftOf(draft.place, set).rows;
  return was.length === draft.rows.length && was.every((row, at) => {
    const now = draft.rows[at]!;
    return row.count === now.count && row.at === now.at && row.do === now.do && row.upTo === now.upTo && row.role === now.role;
  });
}

/** Whether every rule reads as numbers the driver's judge can weigh; the judge itself stays the driver's. */
export function readable(draft: RulesDraft): boolean {
  return draft.rows.every((row) => /^\d+$/.test(row.at) && (row.do !== 'wait' || /^\d+$/.test(row.upTo)));
}

/** The press that writes the place's rules whole against the revision read, a rule's window and place left to the door. */
export function pressOf(draft: RulesDraft): RoleChangePress {
  const rules = draft.rows.map((row): SupervisorRuleWire => ({
    count: row.count, at: Number(row.at), do: row.do,
    ...(row.do === 'wait' ? { upTo: Number(row.upTo) } : {}),
    ...(row.role ? { roles: [row.role] } : {}),
  }));
  return { change: 'set', id: SUPERVISOR, place: draft.place, rules, rulesRevision: draft.revision };
}

/** The place a page names: this machine, a workspace or a repository. */
export function placeKey(kind: 'machine' | 'workspace' | 'repository', id: string | undefined): string {
  return kind === 'machine' ? MACHINE : `${kind}:${id ?? ''}`;
}

/** The wider place's rules on the count and role of `row`, which this place's own rule replaces. */
export function replaced(row: RuleDraft, wider: readonly SupervisorRuleWire[]): SupervisorRuleWire[] {
  return wider.filter((rule) => rule.count === row.count && (rule.roles?.[0] ?? '') === row.role);
}

/** The machine's rules a repository or workspace reads on the counts it holds none of its own. */
export function inheritedFrom(read: SupervisorRulesRead | undefined, place: string, rows: readonly RuleDraft[]): SupervisorRuleWire[] {
  if (place === MACHINE) return [];
  const machine = setOf(read, MACHINE)?.rules ?? [];
  return machine.filter((rule) => !rows.some((row) => row.count === rule.count));
}

/** The rules of `role` among a set: what the role editor says of the role's moves and bound. */
export function rulesOfRole(read: SupervisorRulesRead | undefined, role: string): { place: string; rule: SupervisorRuleWire }[] {
  return (read?.sets ?? []).flatMap((set) => set.rules.filter((rule) => rule.roles?.includes(role)).map((rule) => ({ place: set.place, rule })));
}
