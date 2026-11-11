import type { TFunction } from 'i18next';
import type { RoleCatalogue, RoleChangePress, RoleHoldRead, RoleRow } from '../../data/wiring';
import type { AppointedRead } from './roleStructure';

/** The kinds of decision a supervisor decides, each with the acts a grant names, as the driver's vocabulary has them (`SupervisorKinds`). */
export const DECISION_KINDS: Readonly<Record<string, readonly string[]>> = {
  question: ['question.answer'],
  course: ['course.go-on'],
  limit: ['limit.move', 'limit.wait'],
  allow: ['allow.once', 'allow.reject', 'allow.standing', 'allow.steward'],
  files: ['files.allow-run', 'files.allow-copy', 'files.allow-share'],
  landing: ['landing.accept', 'landing.approve', 'landing.reject'],
  'ask-daoris': ['ask-daoris.report'],
};

export const MACHINE_ONLY: readonly string[] = ['ask-daoris'];

export const AT_ONCE: readonly string[] = ['allow', 'files'];

/** The acts each kind may hold, as the driver's vocabulary has them (`AppointedRoles`). */
export const ACTS: Readonly<Record<RoleHoldRead['kind'], readonly string[]>> = {
  supervisor: Object.values(DECISION_KINDS).flat(),
  steward: ['drive', 'undrive', 'hold', 'resume', 'stop', 'finish', 'retry', 'workflow', 'pause', 'unpause', 'done', 'delete', 'updatenow', 'updatecancel', 'land', 'clean', 'handover', 'setup'],
};

/** The holders a kind takes: the supervisor's rules alone, with a terminal or with an agent, the steward's terminal; none is a role not staffed. */
export const HOLDERS: Readonly<Record<RoleHoldRead['kind'], readonly HoldDraft['holder'][]>> = {
  supervisor: ['rules', 'terminal', 'agent'],
  steward: ['none', 'terminal'],
};

export const ordered = (hold: Pick<HoldDraft, 'kind' | 'holder'>): boolean => hold.kind === 'supervisor' && hold.holder === 'agent';

/** The bounds of the supervisor's window, in minutes. */
export const WINDOW_BOUNDS = { least: 1, most: 60 } as const;

/** An appointed role's hold as the editor keeps it; `fixed`, the machine's generic supervisor, keeps every kind for the person's rules. */
export type HoldDraft = { kind: RoleHoldRead['kind']; acts: string[]; holder: 'rules' | 'terminal' | 'agent' | 'none'; window: string; decides: string[]; fixed?: boolean };

/** The draft an appointed role's editor opens with, placed on the machine or elsewhere. */
export const holdDraftOf = (hold: RoleHoldRead, onMachine = false): HoldDraft => ({
  kind: hold.kind, acts: [...hold.acts], holder: hold.holder ?? 'none', window: String(hold.window ?? 5), decides: [...(hold.decides ?? [])],
  ...(onMachine && hold.kind === 'supervisor' && (hold.decides ?? []).length === 0 ? { fixed: true } : {}),
});

/** A new supervisor's draft, deciding <kind> with nothing granted and held by the person's rules (*Add a supervisor for…*). */
export const newSupervisorDraft = (kind: string): HoldDraft => ({ kind: 'supervisor', acts: [], holder: 'rules', window: '5', decides: [kind] });

export const decidedKinds = (decides: readonly string[]): string[] => (decides.length > 0 ? [...decides] : Object.keys(DECISION_KINDS));

/** The draft with <kind> decided or not, its acts kept to the kinds it decides. */
export function toggleKind(draft: HoldDraft, kind: string): HoldDraft {
  const decides = draft.decides.includes(kind) ? draft.decides.filter((each) => each !== kind) : [...draft.decides, kind];
  const offered = decidedKinds(decides).flatMap((each) => DECISION_KINDS[each] ?? []);
  return { ...draft, decides, acts: draft.acts.filter((act) => offered.includes(act)) };
}

/** Whether a draft is one the driver may be asked to keep: a whole window within its bounds for a supervisor. */
export const holdReads = (draft: HoldDraft): boolean => {
  if (draft.kind !== 'supervisor') return true;
  const window = Number(draft.window);
  return Number.isInteger(window) && window >= WINDOW_BOUNDS.least && window <= WINDOW_BOUNDS.most;
};

/** The appointed fields of a `SET_ROLE` press; the supervisor's window and the kinds it decides only. */
export const holdPress = (draft: HoldDraft): Pick<NonNullable<RoleChangePress['role']>, 'filled' | 'kind' | 'authority' | 'holder' | 'window' | 'decides'> => ({
  filled: 'appointed', kind: draft.kind, authority: draft.acts, holder: draft.holder === 'none' ? null : draft.holder,
  ...(draft.kind === 'supervisor' ? { window: Number(draft.window), ...(draft.decides.length > 0 ? { decides: draft.decides } : {}) } : {}),
});

/** An appointed role's folded line: what a supervisor decides, who holds it, its acts, and the supervisor's window. */
export function holdLine(t: TFunction, hold: RoleHoldRead): string {
  const holder = t(`agents.setup.roles.hold.holder.${hold.holder ?? 'none'}`);
  const acts = hold.acts.length > 0 ? hold.acts.map((act) => t(`agents.setup.roles.hold.act.${act}`)).join(t('settings.ai.role.and')) : t('agents.setup.roles.hold.noActs');
  const decides = (hold.decides ?? []).map((kind) => t(`agents.setup.roles.hold.decides.${kind}`)).join(t('settings.ai.role.and'));
  return [
    ...(hold.kind === 'supervisor' ? [t('agents.setup.roles.hold.decidesLine', { kinds: decides || t('agents.setup.roles.hold.decides.every') })] : []),
    t('agents.setup.roles.hold.heldBy', { holder }),
    acts,
    ...(hold.kind === 'supervisor' ? [t('agents.setup.roles.hold.window', { count: hold.window ?? 5 })] : []),
  ].join(' · ');
}

/** The appointed roles the catalogue holds that read, supervisor and steward alike. */
export const appointedRows = (catalogue: RoleCatalogue | null | undefined): (RoleRow & { hold: RoleHoldRead })[] =>
  (catalogue?.roles ?? []).filter((row): row is RoleRow & { hold: RoleHoldRead } => !!row.hold && (row.state === 'seed' || row.state === 'set'));

/** The diagram's appointed half, read from the catalogue: a kind is offered while a role of it stands, and a role not held draws dashed. */
export function appointedOf(catalogue: RoleCatalogue | null | undefined): AppointedRead {
  const rows = appointedRows(catalogue);
  return {
    stewardOffered: rows.some((row) => row.hold.kind === 'steward'),
    supervisorOffered: rows.some((row) => row.hold.kind === 'supervisor'),
    appointments: rows.map((row) => ({
      role: row.hold.kind,
      scope: row.scopeKind === 'workspace' || row.scopeKind === 'repository' ? `${row.scopeKind}:${row.scopeId ?? ''}` : 'machine',
      workspace: row.scopeKind === 'workspace' ? row.scopeId ?? '' : '',
      repository: row.scopeKind === 'repository' ? row.scopeId ?? '' : '',
      dormant: !row.hold.holder,
    })),
  };
}
