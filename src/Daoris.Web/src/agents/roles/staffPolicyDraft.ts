import type { RolePolicyRow, StaffPolicy, StaffPolicyChoice } from '../../data/wiring';

/** The presets the doors take (STAFFPOLICY1, the staff policy design §2.4); a preset fills the form and never saves. */
export type Preset = 'first' | 'even' | 'weighted';

/** A choice as the editor holds it for the policy: text while typed, so a half-typed number or an IME composition survives. */
export type PolicyFields = {
  /** 0 to 10 under a weight rule. */
  weight?: string;
  /** 1 to 99, at most this share of the window. */
  share?: string;
  budgetKind?: 'starts' | 'tokens';
  budget?: string;
  /** The works the choice serves; null or absent, every work of the role. */
  works?: string[] | null;
};

/** A role's policy as the editor holds it: the rule it was opened on. Its moves to another choice and their bound are the supervisor's rules. */
export type PolicyDraft = {
  state: RolePolicyRow['policyState'];
  /** The policy revision read when the editor opened; a save against a moved one is refused. */
  revision: number | null;
  why: string | null;
  preset: Preset;
};

/** A conversation's work refuses a limit that waits or asks (`StaffCapabilities.Unenforced`). */
export const conversational = (works: readonly string[]): boolean => works.some((work) => work === 'chat' || work === 'ask-daoris');

/** The works a choice may be given: the role's, and design where it takes build. */
export function worksOf(takes: readonly string[]): string[] {
  return takes.includes('build') && !takes.includes('design') ? [...takes, 'design'] : [...takes];
}

const same = (entry: StaffPolicyChoice, agent: string, model: string, effort: string) =>
  entry.agent === agent && (entry.model ?? '') === model && (entry.effort ?? '') === effort;

const text = (value: number | null | undefined) => (value === null || value === undefined ? '' : String(value));

/** A choice's policy fields from the record entry that names its tuple; a choice the record does not list is weight 1 and no limits. */
export function fieldsOf(policy: StaffPolicy | null | undefined, agent: string, model: string, effort: string): PolicyFields {
  const entry = policy?.choices.find((each) => same(each, agent, model, effort));
  const budget = entry?.budget;
  return {
    weight: text(entry?.weight ?? 1), share: text(entry?.share),
    ...(budget?.starts !== undefined ? { budgetKind: 'starts' as const, budget: text(budget.starts) } : budget?.tokens !== undefined ? { budgetKind: 'tokens' as const, budget: text(budget.tokens) } : {}),
    works: entry?.takes ?? null,
  };
}

/** The rule the record reads as: the order, an even spread (every weight 1) or the person's weights. */
function presetOf(policy: StaffPolicy): Preset {
  if (policy.rule !== 'weight') return 'first';
  return policy.choices.every((choice) => (choice.weight ?? 1) === 1) ? 'even' : 'weighted';
}

/** The policy draft a role opens with, from its row in `ROLES`; a role with no row opens on *First that can run*. */
export function policyDraftOf(read: RolePolicyRow): PolicyDraft {
  const policy = read.staffPolicy;
  return { state: read.policyState, revision: read.revision, why: read.why ?? null, preset: policy ? presetOf(policy) : 'first' };
}

type WithPolicy = { drafts: { weight?: string; share?: string; budgetKind?: 'starts' | 'tokens'; budget?: string; works?: string[] | null }[]; policy?: PolicyDraft | null };

/** Fills the form from a preset (§2.4): *First that can run* is the order and nothing else; the spreads keep the limits and set the weights. */
export function withPreset<T extends WithPolicy>(editing: T, preset: Preset): T {
  const was = editing.policy;
  if (!was) return editing;
  if (preset === 'first') {
    return { ...editing, drafts: editing.drafts.map((draft) => ({ ...draft, weight: '1', share: '', budget: '', budgetKind: undefined, works: null })), policy: { ...was, preset } };
  }

  return { ...editing, drafts: preset === 'even' ? editing.drafts.map((draft) => ({ ...draft, weight: '1' })) : editing.drafts, policy: { ...was, preset } };
}

const whole = (value: string | undefined, otherwise: number): number => (value === undefined || value.trim() === '' ? otherwise : Number(value));

/** The aim of choice `index` among `weights`: its share of the new starts, as `[its weight, the sum]`; null where weight 0 or no weight above 0. */
export function aimOf(weights: readonly (string | undefined)[], index: number): [number, number] | null {
  const numbers = weights.map((each) => whole(each, 1));
  const sum = numbers.reduce((total, each) => total + (Number.isFinite(each) ? each : 0), 0);
  const own = numbers[index] ?? 0;
  return own > 0 && sum > 0 ? [own, sum] : null;
}

type Tuple = { agent: string; model: string; effort: string } & PolicyFields;

/** Whether the draft says nothing a role without a row does not already: the order and no limits. */
export function policyIsDefault(drafts: readonly PolicyFields[], policy: PolicyDraft): boolean {
  return policy.preset === 'first'
    && drafts.every((draft) => !draft.share?.trim() && !draft.budget?.trim() && !draft.works);
}

/** The record to save for the drafts and the policy draft, as `SET_ROLE` takes it: the rule, the choices and their limits, never a move. */
export function recordOf(drafts: readonly Tuple[], policy: PolicyDraft): StaffPolicy {
  const weighted = policy.preset !== 'first';
  const choices = drafts.map((draft): StaffPolicyChoice => {
    const budget = draft.budget?.trim() ? { [draft.budgetKind ?? 'starts']: Number(draft.budget) } : null;
    return {
      agent: draft.agent.trim(), ...(draft.model.trim() ? { model: draft.model.trim() } : {}), ...(draft.effort.trim() ? { effort: draft.effort.trim() } : {}),
      weight: weighted ? whole(draft.weight, 1) : 1,
      ...(draft.share?.trim() ? { share: Number(draft.share) } : {}), ...(budget ? { budget } : {}), ...(draft.works ? { takes: draft.works } : {}),
    };
  });
  return { rule: weighted ? 'weight' : 'order', choices };
}

/** Keeps the digits of what was typed or composed, full-width digits as ASCII ones. */
export const digits = (typed: string): string => typed.normalize('NFKC').replace(/\D+/g, '');

/** A weight stepped by one, held to 0 to 10. */
export const stepped = (weight: string | undefined, by: 1 | -1): string => String(Math.min(10, Math.max(0, whole(weight, 1) + by)));
