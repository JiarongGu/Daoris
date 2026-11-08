import type { RoleChangePress, SupervisorCountRead, SupervisorRuleSetRead, SupervisorRuleWire, SupervisorRulesRead } from '../../data/wiring';

/** The supervisor's seeded id: its rules ride a press of this id (`SupervisorRuleDoor`). */
export const SUPERVISOR = 'supervisor';
export const MACHINE = 'machine';

/**
 * One rule as the editor holds it: text while typed, so a half-typed number or an IME composition survives. `roles` is the rule's
 * whole set as read, none for every role: a rule set at the terminal for two roles is written back for both.
 */
export type RuleDraft = { key: number; count: string; at: string; do: string; upTo: string; roles: readonly string[] };

/** A place's rules as the editor holds them, with the revision they were read at. */
export type RulesDraft = { place: string; revision: number; rows: RuleDraft[] };

/** The roles a rule names, none for every role. */
export const rolesOf = (rule: Pick<SupervisorRuleWire, 'roles'>): readonly string[] => rule.roles ?? [];

/** Whether two rules name the same roles, in any order. */
export const sameRoles = (a: readonly string[], b: readonly string[]): boolean => a.length === b.length && a.every((role) => b.includes(role));

/** One rule read into the editor. */
export const rowOf = (rule: SupervisorRuleWire, key: number): RuleDraft => ({
  key, count: rule.count, at: String(rule.at), do: rule.do, upTo: rule.upTo === null || rule.upTo === undefined ? '' : String(rule.upTo), roles: rolesOf(rule),
});

/** The draft a place opens with, from its set in `ROLES`; a place that holds none opens empty at revision 0. */
export function draftOf(place: string, set: SupervisorRuleSetRead | undefined): RulesDraft {
  return { place, revision: set?.revision ?? 0, rows: (set?.rules ?? []).map(rowOf) };
}

/** The place's set among `read`, if it holds one. */
export const setOf = (read: SupervisorRulesRead | undefined, place: string): SupervisorRuleSetRead | undefined => read?.sets.find((set) => set.place === place);

/** The count a new rule starts on: the first not yet ruled for every role here, else the first. */
export function nextCount(counts: readonly SupervisorCountRead[], rows: readonly RuleDraft[]): SupervisorCountRead | undefined {
  return counts.find((count) => !rows.some((row) => row.count === count.count && row.roles.length === 0)) ?? counts[0];
}

/** The action a count offers first: never `off` or `refer`, which a person chooses. */
const firstAction = (count: SupervisorCountRead): string => count.actions.find((action) => action !== 'off' && action !== 'refer') ?? count.actions[0] ?? 'hold';

export const takesMinutes = (row: Pick<RuleDraft, 'count' | 'do'>): boolean => row.do === 'wait' || (row.do === 'refer' && row.count === 'limits');

/** A rule's actions: a `refer` on a count a conversation shares only for a rule naming driven roles, or one already saying it. */
export function actionsOf(count: SupervisorCountRead, row: Pick<RuleDraft, 'do' | 'roles'>, driven: readonly string[]): string[] {
  const drivenRule = row.roles.length > 0 && row.roles.every((role) => driven.includes(role));
  return count.drivenRefer && !drivenRule ? count.actions.filter((action) => action !== 'refer' || row.do === 'refer') : count.actions;
}

/** A new rule on `count`: its least threshold and its first action. */
export function blankRule(count: SupervisorCountRead, key: number): RuleDraft {
  const action = firstAction(count);
  return { key, count: count.count, at: String(count.least), do: action, upTo: takesMinutes({ count: count.count, do: action }) ? '30' : '', roles: [] };
}

/** A rule moved to another count keeps what it can: its threshold held to the count's bounds, its action the count's first if it does not allow it. */
export function recount(row: RuleDraft, count: SupervisorCountRead): RuleDraft {
  const at = Number(row.at);
  const clamped = row.at !== '' && Number.isFinite(at) ? String(Math.min(count.most, Math.max(count.least, at))) : String(count.least);
  const action = count.actions.includes(row.do) ? row.do : firstAction(count);
  return { ...row, count: count.count, at: clamped, do: action, upTo: takesMinutes({ count: count.count, do: action }) ? row.upTo || '30' : '' };
}

/** A rule given another action: `wait` and a `refer` on limits take minutes, every other action none. */
export function reaction(row: RuleDraft, action: string): RuleDraft {
  return { ...row, do: action, upTo: takesMinutes({ count: row.count, do: action }) ? row.upTo || '30' : '' };
}

/** Whether the draft says what the stored set does, so *Save* has nothing to put to work. */
export function sameAsStored(draft: RulesDraft, set: SupervisorRuleSetRead | undefined): boolean {
  const was = draftOf(draft.place, set).rows;
  return was.length === draft.rows.length && was.every((row, at) => {
    const now = draft.rows[at]!;
    return row.count === now.count && row.at === now.at && row.do === now.do && row.upTo === now.upTo && sameRoles(row.roles, now.roles);
  });
}

/** Whether every rule reads as numbers the driver's judge can weigh; the judge itself stays the driver's. */
export function readable(draft: RulesDraft): boolean {
  return draft.rows.every((row) => /^\d+$/.test(row.at) && (!takesMinutes(row) || /^\d+$/.test(row.upTo)));
}

/** The press that writes the place's rules whole against the revision read, a rule's window and place left to the door. */
export function pressOf(draft: RulesDraft): RoleChangePress {
  const rules = draft.rows.map((row): SupervisorRuleWire => ({
    count: row.count, at: Number(row.at), do: row.do,
    ...(takesMinutes(row) ? { upTo: Number(row.upTo) } : {}),
    ...(row.roles.length > 0 ? { roles: [...row.roles] } : {}),
  }));
  return { change: 'set', id: SUPERVISOR, place: draft.place, rules, rulesRevision: draft.revision };
}

/** The place a page names: this machine, a workspace or a repository. */
export function placeKey(kind: 'machine' | 'workspace' | 'repository', id: string | undefined): string {
  return kind === 'machine' ? MACHINE : `${kind}:${id ?? ''}`;
}

/** A wider place's rule, with the place it is read from. */
export type PlacedRule = { place: string; rule: SupervisorRuleWire };

/** The places `place`'s lines read, narrow to wide: itself, its wider places as `ROLES` answers them, then this machine. */
export function chainOf(read: SupervisorRulesRead | undefined, place: string): string[] {
  return place === MACHINE ? [MACHINE] : [place, ...(read?.wider?.[place] ?? []).filter((each) => each !== place && each !== MACHINE), MACHINE];
}

type Placed = { place: string; rules: readonly SupervisorRuleWire[] };

/**
 * A line's rules per count along `chain` for `role` (null for a line with no working role), as the driver's `SupervisorRules.Effective`
 * reads them: the most specific place holding a rule on the count that applies to the role decides, its rules naming the role before
 * those naming none, and an `off` there leaves the count unruled.
 */
function effective(sets: readonly Placed[], chain: readonly string[], role: string | null): Map<string, PlacedRule[]> {
  const decided = new Map<string, PlacedRule[]>();
  for (const count of new Set(sets.flatMap((set) => set.rules.map((rule) => rule.count)))) {
    for (const place of chain) {
      const applying = sets.filter((set) => set.place === place).flatMap((set) => set.rules)
        .filter((rule) => rule.count === count && (rolesOf(rule).length === 0 || (role !== null && rolesOf(rule).includes(role))));
      if (applying.length === 0) continue;
      const named = applying.filter((rule) => rolesOf(rule).length > 0);
      const deciding = named.length > 0 ? named : applying;
      if (deciding.every((rule) => rule.do !== 'off')) decided.set(count, deciding.map((rule) => ({ place, rule })));
      break;
    }
  }
  return decided;
}

/** Every place's set with `draft` in its place's stead, as the lines would read them once saved. */
function withDraft(read: SupervisorRulesRead | undefined, draft: RulesDraft): Placed[] {
  const rules = draft.rows.map((row): SupervisorRuleWire => ({ count: row.count, at: Number(row.at), do: row.do, roles: row.roles.length > 0 ? [...row.roles] : null }));
  return [...(read?.sets ?? []).filter((set) => set.place !== draft.place), { place: draft.place, rules }];
}

/** The roles a line here may go on under: each working role, and none. */
const linesOf = (roles: readonly string[]): (string | null)[] => [...roles, null];

/**
 * The wider places' rules `row` replaces: for each role whose lines here it decides for, the rule a wider place decides by for them,
 * each once with its place.
 */
export function replaced(read: SupervisorRulesRead | undefined, draft: RulesDraft, row: RuleDraft, roles: readonly string[]): PlacedRule[] {
  const chain = chainOf(read, draft.place);
  const sets = withDraft(read, draft);
  const found: PlacedRule[] = [];
  for (const role of linesOf(roles)) {
    const here = effective(sets, chain, role).get(row.count) ?? [];
    if (!here.some((each) => each.place === draft.place && sameRoles(rolesOf(each.rule), row.roles))) continue;
    for (const wider of effective(sets, chain.slice(1), role).get(row.count) ?? []) {
      if (!found.some((each) => each.place === wider.place && each.rule === wider.rule)) found.push(wider);
    }
  }
  return found;
}

/** The wider places' rules some role's lines here still read with the draft as it stands, each once with its place. */
export function inheritedFrom(read: SupervisorRulesRead | undefined, draft: RulesDraft, roles: readonly string[]): PlacedRule[] {
  const chain = chainOf(read, draft.place);
  const sets = withDraft(read, draft);
  const found: PlacedRule[] = [];
  for (const role of linesOf(roles)) {
    for (const rules of effective(sets, chain, role).values()) {
      for (const each of rules) {
        if (each.place !== draft.place && !found.some((kept) => kept.place === each.place && kept.rule === each.rule)) found.push(each);
      }
    }
  }
  // Narrow to wide, and within a place as its set holds them.
  const order = (each: PlacedRule) => sets.find((set) => set.place === each.place)?.rules.indexOf(each.rule) ?? 0;
  return found.sort((a, b) => chain.indexOf(a.place) - chain.indexOf(b.place) || order(a) - order(b));
}

/** The rules of `role` among a set: what the role editor says of the role's moves and bound. */
export function rulesOfRole(read: SupervisorRulesRead | undefined, role: string): { place: string; rule: SupervisorRuleWire }[] {
  return (read?.sets ?? []).flatMap((set) => set.rules.filter((rule) => rule.roles?.includes(role)).map((rule) => ({ place: set.place, rule })));
}
