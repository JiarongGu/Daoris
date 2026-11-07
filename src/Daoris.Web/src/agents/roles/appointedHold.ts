import type { TFunction } from 'i18next';
import type { RoleCatalogue, RoleChangePress, RoleHoldRead, RoleRow } from '../../data/wiring';
import type { AppointedRead } from './roleStructure';

/** The acts each kind may hold, as the driver's vocabulary has them (`AppointedRoles`). */
export const ACTS: Readonly<Record<RoleHoldRead['kind'], readonly string[]>> = {
  supervisor: ['answer', 'defer'],
  steward: ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry', 'workflow', 'pause', 'unpause', 'done', 'delete', 'updatenow', 'updatecancel', 'land', 'clean', 'handover', 'setup'],
};

/** The holders a kind takes: the supervisor's rules alone or with a terminal, the steward's terminal; none is a role not staffed. */
export const HOLDERS: Readonly<Record<RoleHoldRead['kind'], readonly HoldDraft['holder'][]>> = {
  supervisor: ['rules', 'terminal'],
  steward: ['none', 'terminal'],
};

/** The bounds of the supervisor's window, in minutes. */
export const WINDOW_BOUNDS = { least: 1, most: 60 } as const;

/** An appointed role's hold as the editor keeps it: the window as text while typed. */
export type HoldDraft = { kind: RoleHoldRead['kind']; acts: string[]; holder: 'rules' | 'terminal' | 'none'; window: string };

/** The draft an appointed role's editor opens with. */
export const holdDraftOf = (hold: RoleHoldRead): HoldDraft => ({
  kind: hold.kind, acts: [...hold.acts], holder: hold.holder ?? 'none', window: String(hold.window ?? 5),
});

/** Whether a draft is one the driver may be asked to keep: a whole window within its bounds for a supervisor. */
export const holdReads = (draft: HoldDraft): boolean => {
  if (draft.kind !== 'supervisor') return true;
  const window = Number(draft.window);
  return Number.isInteger(window) && window >= WINDOW_BOUNDS.least && window <= WINDOW_BOUNDS.most;
};

/** The appointed fields of a `SET_ROLE` press; the supervisor's window only. */
export const holdPress = (draft: HoldDraft): Pick<NonNullable<RoleChangePress['role']>, 'filled' | 'authority' | 'holder' | 'window'> => ({
  filled: 'appointed', authority: draft.acts, holder: draft.holder === 'none' ? null : draft.holder,
  ...(draft.kind === 'supervisor' ? { window: Number(draft.window) } : {}),
});

/** An appointed role's folded line: who holds it, its acts, and the supervisor's window. */
export function holdLine(t: TFunction, hold: RoleHoldRead): string {
  const holder = t(`agents.setup.roles.hold.holder.${hold.holder ?? 'none'}`);
  const acts = hold.acts.length > 0 ? hold.acts.map((act) => t(`agents.setup.roles.hold.act.${act}`)).join(t('settings.ai.role.and')) : t('agents.setup.roles.hold.noActs');
  return [
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
