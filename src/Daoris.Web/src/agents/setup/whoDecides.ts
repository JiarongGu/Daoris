import type { TFunction } from 'i18next';
import type { DeciderLine, DecidersRead, RoleCatalogue, StructurePlaceAsk } from '../../data/wiring';
import { appointedRows, DECISION_KINDS, MACHINE_ONLY } from '../roles/appointedHold';
import { placeKey } from '../roles/supervisorRulesDraft';

/** The place's own scope, as a role placed here names it. */
export const scopeOf = (place: StructurePlaceAsk): { kind: 'machine' | 'workspace' | 'repository'; id?: string } =>
  place.repository ? { kind: 'repository', id: place.repository } : place.workspace ? { kind: 'workspace', id: place.workspace } : { kind: 'machine' };

/**
 * The driver's line per kind for <place>: its own where `deciders` names it; a repository it does not name reads `elsewhere`, and a
 * workspace it does not name the machine's, since no supervisor is placed there.
 */
export function decidersAt(deciders: DecidersRead, place: StructurePlaceAsk): DeciderLine[] {
  const scope = scopeOf(place);
  const named = deciders.places.find((each) => each.place === placeKey(scope.kind, scope.id));
  if (named) return named.kinds;
  const lines = scope.kind === 'repository' ? deciders.elsewhere : deciders.places.find((each) => each.place === 'machine')?.kinds ?? [];
  return scope.kind === 'machine' ? lines : lines.filter((line) => !MACHINE_ONLY.includes(line.kind));
}

/** The kinds no supervisor placed at <scope> decides, in the editor's order: what *Add a supervisor for…* offers; below the machine, none of the machine's alone. */
export function offeredKinds(catalogue: RoleCatalogue, scope: { kind: string; id?: string }): string[] {
  const placed = appointedRows(catalogue).filter((row) =>
    row.hold.kind === 'supervisor' && (row.scopeKind ?? 'machine') === scope.kind && (scope.kind === 'machine' || row.scopeId === scope.id));
  const decided = new Set(placed.flatMap((row) => row.hold.decides ?? []));
  return Object.keys(DECISION_KINDS).filter((kind) => !decided.has(kind) && (scope.kind === 'machine' || !MACHINE_ONLY.includes(kind)));
}

/** Where a supervisor is set, in the line's words: this machine, or the workspace or repository by its id. */
const scopeWords = (t: TFunction, scope: string | null | undefined): string => {
  const [kind, ...id] = (scope ?? 'machine').split(':');
  return kind === 'workspace' || kind === 'repository' ? t(`agents.setup.roles.decides.at.${kind}`, { id: id.join(':') }) : t('agents.setup.roles.decides.at.machine');
};

/**
 * One kind's line of *Who decides here*: the supervisor and where it is set; or *you*, with why: held by the person's rules, granted
 * none of the kind's acts, or the driver's own sentence where no supervisor can be read.
 */
export function deciderText(t: TFunction, line: DeciderLine, nameOf: (id: string) => string): string {
  const kind = t(`agents.setup.roles.hold.decides.${line.kind}`);
  if (!line.role) return t('agents.setup.roles.decides.line', { kind, who: t('agents.setup.roles.decides.yours', { why: line.held ?? '' }) });
  const role = nameOf(line.role);
  if (line.kind === 'ask-daoris') {
    const who = line.holder === 'rules' ? t('agents.setup.roles.decides.unread', { role })
      : line.granted.length === 0 ? t('agents.setup.roles.decides.mayNot.ask-daoris', { role }) : t('agents.setup.roles.decides.read', { role });
    return t('agents.setup.roles.decides.line', { kind, who });
  }
  const at = scopeWords(t, line.scope);
  const who = line.holder !== 'terminal'
    ? t('agents.setup.roles.decides.byRules', { role, at })
    : line.granted.length === 0 ? t(`agents.setup.roles.decides.mayNot.${line.kind}`, { role }) : t('agents.setup.roles.decides.by', { role, at });
  return t('agents.setup.roles.decides.line', { kind, who });
}
