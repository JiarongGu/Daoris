import type { TFunction, i18n as I18n } from 'i18next';

/**
 * Daoris's own roles a step may name, in the editor's order: the driver's `StepRoles.All`, which the palette carries as `roles`; this list
 * stands in only where a palette is not read, and each is worded as `workflow.role.<id>` in both catalogues.
 */
export const STEP_ROLES = [
  'design', 'opinion', 'tester', 'acceptor', 'ask-reader', 'approver', 'release-manager', 'operations', 'config-changer', 'credentials-keeper', 'owner',
] as const;

const record = (value: unknown): Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};

/**
 * The role a step's JSON names itself: its `role`, else the `role` option an agent step named it by before the field (read, never
 * written); null where it names none, and its kind's is read. The one reader of that option on this page, as `StepRoles.Named` is the
 * driver's: both end when no kept version names a role by its option (the roles-since floor).
 */
export function namedRole(node: Record<string, unknown>): string | null {
  if (typeof node.role === 'string' && node.role) return node.role;
  const option = record(node.options).role;
  return typeof option === 'string' && option ? option : null;
}

/** The role that takes a step: the one it names, else its kind's (`kindRole`, the palette's), else none known. */
export const roleOf = (node: Record<string, unknown>, kindRole: string | null | undefined): string | null => namedRole(node) ?? kindRole ?? null;

/** A role in the window's words: Daoris's own by the catalogue, a role of the home's catalogue by its id as written. */
export const roleSaid = (t: TFunction, i18n: I18n, role: string): string =>
  i18n.exists(`workflow.role.${role}`) ? t(`workflow.role.${role}`) : role;
