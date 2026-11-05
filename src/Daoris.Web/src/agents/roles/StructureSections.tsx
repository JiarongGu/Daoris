import type { TFunction } from 'i18next';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { StructureChoice } from '../../data/wiring';
import { Button, CheckField, Chip, Icon, SelectField } from '../../design/ui';
import { clockOf, list, moment } from '../../locales/format';
import { entryText, roleName } from './Roles';
import { RowMore } from './RowMore';
import type {
  AccountRow, RepositoryRow, RepositoryWork, RoleRef, RuleRead, Structure, StructureFilters, UsedBy, WorkspaceGroup,
} from './roleStructure';
import { NO_FILTERS, nodeOfRow, placeId } from './roleStructure';

/** Why there are no rows: not asked yet, a read that failed, or the rows. */
export type StructureRead = 'ok' | 'loading' | 'failed';

/**
 * Where a row's door leads, handed in by the application (D168 point 3): the molecules press these and know no route. A door the
 * place was not handed is not drawn.
 */
export type RoleDoors = {
  /** The Agents mode at this agent's accounts. */
  agent?: (agent: string) => void;
  /** A repository's own page at a tab. */
  repository?: (repository: string, tab: 'setup' | 'workflow') => void;
  /** A plugin in the Plugins place. */
  plugin?: (plugin: string) => void;
  /** Working roles' editor for a new role in this workspace or repository (*Add a role here*). */
  addRole?: (place: { kind: 'workspace' | 'repository'; id: string }) => void;
  /** The diagram, its row's node chosen (COMPANYVIEW1f): a row's ⋯ *On the diagram*, offered where the row is drawn. */
  diagram?: (row: string) => void;
};

const ANY = '*';

const FIELD = 'min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 text-body text-ink placeholder:italic placeholder:text-ink-faint';

export const nameOf = (t: TFunction, language: string, role: RoleRef) =>
  roleName(t, language, { id: role.id, seeded: false, name: role.name });

const workName = (t: TFunction, work: string) => t(`settings.ai.roles.work.${work}`, { defaultValue: work });

export const workList = (t: TFunction, language: string, works: readonly string[]) => list(works.map((work) => workName(t, work)), language);

/** A choice's position in its words: `1` to `4` as choice n, `before`, or a rule's agent no choice names. */
export function positionOf(t: TFunction, from: string): string {
  if (from === 'before') return t('agents.roles.position.before');
  if (from === 'rule-default') return t('agents.roles.position.ruleDefault');
  return t('agents.roles.position.n', { n: from });
}

/** A choice's state in its words with the reading's time it rests on (never a probe). */
export function stateText(t: TFunction, state: StructureChoice['state']): string {
  const when = state.at ? clockOf(state.at) : null;
  const word = ({
    ready: t('agents.roles.state.ready'),
    unknown: t('agents.roles.state.unknown'),
    'not-read': t('agents.roles.state.notRead'),
    unavailable: t('agents.roles.state.unavailable'),
    'not-offered': t('agents.roles.state.notOffered'),
    cooling: t('agents.roles.state.cooling', { when: state.until ? moment(state.until) : '' }),
    'signed-out': t('agents.roles.state.signedOut'),
    held: t('agents.roles.state.held'),
  } as Record<string, string>)[state.code] ?? state.code;
  return when && state.code !== 'not-read' ? `${word} · ${t('agents.roles.state.read', { when })}` : word;
}

/** A work's rule as one line: on, ask or off; the agents it permits; its bound; and where it was set. */
export function ruleText(t: TFunction, rule: RuleRead): string {
  if (rule.kind === 'not-a-rule') return t('agents.roles.rule.notARule');
  if (rule.kind === 'failed') return t('agents.roles.rule.failed', { why: rule.why });
  const consent = t(`agents.roles.rule.${rule.consent}`, { defaultValue: rule.consent });
  const agents = rule.agents.length > 0 ? t('agents.roles.rule.agents', { agents: rule.agents.join(', ') }) : t('agents.roles.rule.rolesDecide');
  const bound = [
    ...(rule.concurrency ? [t('agents.roles.rule.concurrent', { count: rule.concurrency })] : []),
    ...(rule.minutes ? [t('agents.roles.rule.minutes', { count: rule.minutes })] : []),
  ];
  return [consent, agents, ...bound, t(`agents.roles.rule.source.${rule.source}`, { defaultValue: rule.source })].join(' · ');
}

/**
 * **The shared filters** (COMPANYVIEW1d, the company-view design §3): the workspace whose scope Work and Accounts say, one work, one
 * agent, a name, and *Needs a look*. The mode's hook owns them; this draws them and says each change.
 */
export function StructureFilterBar({ filters, options, onFilters }: {
  filters: StructureFilters;
  options: Structure['options'];
  onFilters: (filters: StructureFilters) => void;
}) {
  const { t } = useTranslation();
  const set = (patch: Partial<StructureFilters>) => onFilters({ ...filters, ...patch });
  const touched = JSON.stringify(filters) !== JSON.stringify(NO_FILTERS);
  return (
    <form role="search" aria-label={t('agents.roles.filter.label')} className="mb-3 flex flex-wrap items-center gap-2" onSubmit={(event) => event.preventDefault()}>
      <SelectField
        ariaLabel={t('agents.roles.filter.workspace')}
        value={filters.workspace ?? ANY}
        onChange={(value) => set({ workspace: value === ANY ? null : value })}
        options={[{ value: ANY, label: t('agents.roles.filter.thisMachine') }, ...options.workspaces.map((value) => ({ value, label: value }))]}
      />
      <SelectField
        ariaLabel={t('agents.roles.filter.work')}
        value={filters.work ?? ANY}
        onChange={(value) => set({ work: value === ANY ? null : value })}
        options={[{ value: ANY, label: t('agents.roles.filter.anyWork') }, ...options.works.map((value) => ({ value, label: workName(t, value) }))]}
      />
      <SelectField
        ariaLabel={t('agents.roles.filter.agent')}
        value={filters.agent ?? ANY}
        onChange={(value) => set({ agent: value === ANY ? null : value })}
        options={[{ value: ANY, label: t('agents.roles.filter.anyAgent') }, ...options.agents.map((value) => ({ value, label: value }))]}
      />
      <input
        type="search" aria-label={t('agents.roles.filter.name')} placeholder={t('agents.roles.filter.namePlaceholder')}
        value={filters.name} onChange={(event) => set({ name: event.target.value })} className={`${FIELD} w-44`}
      />
      <CheckField checked={filters.needsLook} onChange={(needsLook) => set({ needsLook })} label={t('agents.roles.filter.needsLook')} />
      {touched && <Button variant="ghost" onClick={() => onFilters(NO_FILTERS)}>{t('agents.roles.filter.clear')}</Button>}
    </form>
  );
}

/** A row folded to its line (R1) that opens: a title, its values, and a chevron; what it says opened is its children. */
function FoldRow({ id, title, summary, below, open, onToggle, mark, onDiagram, children }: {
  id: string;
  title: string;
  summary: ReactNode;
  below?: ReactNode;
  open: boolean;
  onToggle: (id: string) => void;
  mark?: ReactNode;
  /** Its node chosen on the diagram, where the row is drawn there. */
  onDiagram?: () => void;
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <li data-structure-row={id} className="border-t border-line first:border-t-0">
      <div className="flex min-w-0 items-center gap-1">
        <button
          type="button"
          aria-expanded={open}
          aria-label={t(open ? 'agents.roles.hide' : 'agents.roles.show', { name: title })}
          onClick={() => onToggle(id)}
          className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-3 gap-y-0.5 rounded-control py-2 text-left hover:bg-accent-soft/40"
        >
          <span className="min-w-40 text-body font-medium text-ink">{title}</span>
          {/* At least 8rem, so in a narrow detail the values go under the words rather than a letter a line (R1). */}
          <span className="min-w-32 flex-1 text-body text-ink-soft [overflow-wrap:anywhere]">{summary}</span>
          {mark}
          <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} className="ml-auto shrink-0 self-center text-ink-faint" />
        </button>
        {onDiagram && <RowMore name={title} onDiagram={onDiagram} />}
      </div>
      {below && <div className="-mt-1 pb-2 text-small text-ink-faint [overflow-wrap:anywhere]">{below}</div>}
      {open && <div className="pb-3 pl-2 text-body">{children}</div>}
    </li>
  );
}

function Notice({ read, failed }: { read: StructureRead; failed?: string }) {
  const { t } = useTranslation();
  return (
    <>
      {read === 'loading' && <p className="m-0 text-body text-ink-soft">{t('agents.roles.reading')}</p>}
      {read === 'failed' && <p role="alert" className="m-0 text-body text-ink-soft">{t('agents.roles.readFailed')}</p>}
      {read === 'ok' && failed && <p role="alert" className="m-0 mb-3 text-body text-ink-soft">{t('agents.roles.catalogueFailed', { why: failed })}</p>}
    </>
  );
}

export type SectionProps = {
  structure: Structure | null;
  read: StructureRead;
  filters: StructureFilters;
  onFilters: (filters: StructureFilters) => void;
  open: ReadonlySet<string>;
  onToggle: (id: string) => void;
  doors: RoleDoors;
};

/** A row's *On the diagram*, where its door was handed in and the row is drawn there. */
const toDiagram = (structure: Structure, doors: RoleDoors, row: string) =>
  (doors.diagram && nodeOfRow(structure, row) !== null ? () => doors.diagram!(row) : undefined);

/** A section's filters, its read said, and the scope it is at; the rows, or the diagram, under them. */
export function StructureShell({ structure, read, filters, onFilters, children }: Pick<SectionProps, 'structure' | 'read' | 'filters' | 'onFilters'> & { children: ReactNode }) {
  const { t } = useTranslation();
  const ready = read === 'ok' && structure !== null;
  return (
    <div>
      {ready && <StructureFilterBar filters={filters} options={structure.options} onFilters={onFilters} />}
      <Notice read={read} failed={structure?.catalogueFailed} />
      {ready && (
        <p className="m-0 mb-2 text-small text-ink-faint">
          {filters.workspace ? t('agents.roles.scope.workspace', { workspace: filters.workspace }) : t('agents.roles.scope.machine')}
        </p>
      )}
      {ready && children}
    </div>
  );
}

const Line = ({ label, children }: { label: string; children: ReactNode }) => (
  <p className="m-0 flex flex-wrap gap-x-2 py-0.5 text-body">
    <span className="min-w-28 text-ink-soft">{label}</span>
    <span className="min-w-0 text-ink [overflow-wrap:anywhere]">{children}</span>
  </p>
);

/** ---------------------------------------------------------------- Work (§3.3) */

type RowProps = { open: boolean; onToggle: (id: string) => void; doors: RoleDoors; onDiagram?: () => void };

/** ---------------------------------------------------------------- Accounts (§3.4) */

function usedText(t: TFunction, language: string, used: UsedBy, agent: string): string {
  return t('agents.roles.accounts.use', {
    role: nameOf(t, language, used.role), works: workList(t, language, used.works), position: positionOf(t, used.from),
    choice: entryText(t, { agent, model: used.model, effort: used.effort }),
  });
}

/** One role leaning on an agent: the role, its works, its place in the order, its choice and that choice's state. */
export function UsedLine({ used, product }: { used: UsedBy; product: string }) {
  const { t, i18n } = useTranslation();
  return <>{usedText(t, i18n.language, used, product)} · {stateText(t, used.state)}</>;
}

function AccountRowView({ row, open, onToggle, doors, onDiagram }: RowProps & { row: AccountRow }) {
  const { t } = useTranslation();
  const installed = row.installed === false || row.reasons.includes('not-known') ? t('harness.absent') : null;
  const summary = [
    installed ?? row.phrase,
    row.read ? t('agents.roles.accounts.read', { when: clockOf(row.read) }) : row.tool ? t('agents.roles.accounts.neverRead') : null,
    t('agents.roles.accounts.usedBy', { count: row.usedBy.length }),
  ].filter(Boolean).join(' · ');
  return (
    <FoldRow
      id={row.id} title={row.product} summary={summary}
      below={row.plugin ? t('agents.roles.accounts.plugin', { plugin: row.plugin }) : undefined}
      open={open} onToggle={onToggle} onDiagram={onDiagram}
      mark={row.reasons.length > 0 ? <Chip accent>{t('agents.roles.filter.needsLook')}</Chip> : undefined}
    >
      {row.reasons.includes('not-known') && <p className="m-0 py-0.5 text-ink-soft">{t('agents.roles.accounts.notKnown')}</p>}
      {row.reasons.includes('not-installed') && <p className="m-0 py-0.5 text-ink-soft">{t('agents.roles.accounts.notInstalled')}</p>}
      {row.usedBy.length === 0 && <p className="m-0 py-0.5 text-ink-soft">{t('agents.roles.accounts.noUse')}</p>}
      {row.usedBy.map((used) => (
        <Line key={`${used.role.id}:${used.from}:${used.model ?? ''}`} label={t('agents.roles.accounts.usedByIs')}>
          <UsedLine used={used} product={row.product} />
        </Line>
      ))}
      <div className="mt-2 flex flex-wrap gap-2">
        {doors.agent && row.tool && (
          <Button variant="ghost" aria-label={t('agents.roles.door.open', { agent: row.product })} onClick={() => doors.agent!(row.tool!)}>
            {t('agents.roles.door.openLabel')}
          </Button>
        )}
        {doors.plugin && row.plugin && (
          <Button variant="ghost" aria-label={t('agents.roles.door.plugin', { plugin: row.plugin })} onClick={() => doors.plugin!(row.plugin!)}>
            {row.plugin}
          </Button>
        )}
      </div>
    </FoldRow>
  );
}

/** ---------------------------------------------------------------- By repository (§3.5) */

function placeText(t: TFunction, ref: RoleRef): string {
  return t(`settings.ai.roles.place.${ref.scope.kind}`, { id: ref.scope.id ?? '', defaultValue: ref.scope.kind });
}

function RepositoryWorkLine({ work }: { work: RepositoryWork }) {
  const { t, i18n } = useTranslation();
  const said = work.held
    ?? (work.role
      ? t('agents.roles.repo.role', { role: nameOf(t, i18n.language, work.role), place: placeText(t, work.role) })
      : t('agents.roles.repo.noRole'));
  const first = work.first ? ` · ${entryText(t, work.first)}${work.more > 0 ? ` ${t('settings.ai.role.more', { count: work.more })}` : ''}` : '';
  return (
    <Line label={workName(t, work.work)}>
      {said}{work.held ? '' : first}
      {work.choicesFailed && <span className="block text-ink-soft">{t('agents.roles.choices.failed', { why: work.choicesFailed })}</span>}
      {work.policyFailed && <span className="block text-ink-soft">{t('agents.roles.repo.workFailed')}: {work.policyFailed}</span>}
      {work.cannotRun && work.first && <span className="block text-ink-soft">{stateText(t, work.first.state)}</span>}
    </Line>
  );
}

function RepositoryRowView({ row, open, onToggle, doors, onDiagram }: RowProps & { row: RepositoryRow }) {
  const { t } = useTranslation();
  return (
    <FoldRow
      id={row.id} title={row.repository}
      summary={row.differs === 0 ? t('agents.roles.repo.takes') : t('agents.roles.repo.differ', { count: row.differs })}
      open={open} onToggle={onToggle} onDiagram={onDiagram}
      mark={row.reasons.length > 0 ? <Chip accent>{t('agents.roles.filter.needsLook')}</Chip> : undefined}
    >
      {row.works.map((work) => <RepositoryWorkLine key={work.work} work={work} />)}
      <div className="mt-2 border-t border-line pt-2">
        <Line label={t('agents.roles.repo.line')}>
          {row.line
            ? t('agents.roles.repo.lineIs', { branch: row.line.branch ?? '—', source: t(`agents.roles.repo.lineSource.${row.line.source}`, { defaultValue: row.line.source }) })
            : t('agents.roles.repo.lineNone')}
        </Line>
        <Line label={t('agents.roles.repo.landing')}>
          {row.landing
            ? t(row.landing.plugin ? 'agents.roles.repo.landingPlugin' : 'agents.roles.repo.landingIs', {
              form: row.landing.form, plugin: row.landing.plugin,
              source: t(`agents.roles.repo.landingSource.${row.landing.source}`, { defaultValue: row.landing.source }),
            })
            : t('agents.roles.repo.landingNone')}
        </Line>
        <Line label={t('agents.roles.repo.driving')}>
          {t(`agents.roles.repo.driving.${row.driving === 'not-driven' ? 'notDriven' : row.driving ?? 'unread'}`)}
        </Line>
        <Line label={t('agents.roles.repo.plugins')}>
          {row.plugins.length === 0
            ? t('agents.roles.repo.noPlugins')
            : row.plugins.map((plugin) => (
              <span key={plugin.id} className="mr-3 inline-flex items-center gap-1">
                {doors.plugin
                  ? (
                    <Button variant="ghost" className="!px-1 !py-0" aria-label={t('agents.roles.door.plugin', { plugin: plugin.name })} onClick={() => doors.plugin!(plugin.id)}>
                      {plugin.name}
                    </Button>
                  )
                  : plugin.name}
                <span className="text-ink-soft">
                  {t(plugin.via.length === 2 ? 'agents.roles.repo.viaBoth' : plugin.via[0] === 'landing' ? 'agents.roles.repo.viaLanding' : 'agents.roles.repo.viaAgent')}
                </span>
              </span>
            ))}
        </Line>
      </div>
      {(doors.repository || doors.addRole) && (
        <div className="mt-2 flex flex-wrap gap-2">
          {doors.addRole && (
            <Button variant="ghost" aria-label={`${t('agents.roles.door.addHere')} · ${row.repository}`} onClick={() => doors.addRole!({ kind: 'repository', id: row.repository })}>
              {t('agents.roles.door.addHere')}
            </Button>
          )}
          {doors.repository && <>
          <Button variant="ghost" aria-label={`${t('agents.roles.door.setup')} · ${row.repository}`} onClick={() => doors.repository!(row.repository, 'setup')}>
            {t('agents.roles.door.setup')}
          </Button>
          <Button variant="ghost" aria-label={`${t('agents.roles.door.workflow')} · ${row.repository}`} onClick={() => doors.repository!(row.repository, 'workflow')}>
            {t('agents.roles.door.workflow')}
          </Button>
          </>}
        </div>
      )}
    </FoldRow>
  );
}

function GroupView({ structure, group, open, onToggle, doors }: {
  structure: Structure; group: WorkspaceGroup; open: ReadonlySet<string>; onToggle: (id: string) => void; doors: RoleDoors;
}) {
  const { t } = useTranslation();
  const folded = !open.has(group.id);
  const onDiagram = toDiagram(structure, doors, group.id);
  return (
    <section aria-label={group.workspace} data-structure-row={group.id} className="mb-3">
      <div className="flex min-w-0 items-center gap-1 border-b border-line">
        <button
          type="button"
          aria-expanded={!folded}
          aria-label={t(folded ? 'agents.roles.show' : 'agents.roles.hide', { name: group.workspace })}
          onClick={() => onToggle(group.id)}
          className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-3 rounded-control py-1.5 text-left hover:bg-accent-soft/40"
        >
          <Icon name={folded ? 'chevronRight' : 'chevronDown'} size={13} className="shrink-0 self-center text-ink-faint" />
          <span className="min-w-40 text-body font-semibold text-ink">{group.workspace}</span>
          <span className="text-small text-ink-faint">
            {[t('agents.roles.repo.group', { count: group.repositories.length }), ...(group.differs > 0 ? [t('agents.roles.repo.groupDiffer', { count: group.differs })] : [])].join(' · ')}
          </span>
        </button>
        {onDiagram && <RowMore name={group.workspace} onDiagram={onDiagram} />}
      </div>
      {!folded && doors.addRole && (
        <div className="py-1 pl-4">
          <Button variant="ghost" aria-label={`${t('agents.roles.door.addHere')} · ${group.workspace}`} onClick={() => doors.addRole!({ kind: 'workspace', id: group.workspace })}>
            {t('agents.roles.door.addHere')}
          </Button>
        </div>
      )}
      {!folded && (
        <ul className="m-0 list-none p-0 pl-4">
          {group.repositories.map((row) => (
            <RepositoryRowView key={row.id} row={row} open={open.has(row.id)} onToggle={onToggle} doors={doors} onDiagram={toDiagram(structure, doors, row.id)} />
          ))}
        </ul>
      )}
    </section>
  );
}

/** ---------------------------------------------------------------- The diagram's detail (COMPANYVIEW1f, §4) */

/** A place's answer per work, in words: what choosing it lights on the diagram. */
function PlaceWorks({ title, works }: { title: string; works: readonly RepositoryWork[] }) {
  const { t } = useTranslation();
  return (
    <section aria-label={title} className="mb-3">
      <h3 className="m-0 mb-1 text-body font-semibold text-ink">{title}</h3>
      {works.length === 0
        ? <p className="m-0 text-body text-ink-soft">{t('agents.roles.diagram.noWorks')}</p>
        : works.map((work) => <RepositoryWorkLine key={work.work} work={work} />)}
    </section>
  );
}

/**
 * **A node's detail** (§4, *Moving between them*): the molecule its row is in the list, opened, its presses the list's own; a role's is
 * the catalogue's row with the same editor, handed in. A place whose row is not shown says its answer per work.
 */
export function DiagramDetail({ structure, id, open, onToggle, doors, working }: {
  structure: Structure;
  id: string;
  open: ReadonlySet<string>;
  onToggle: (id: string) => void;
  doors: RoleDoors;
  /** The role's row in Working roles, with its editor. */
  working?: ReactNode;
}) {
  const { t } = useTranslation();
  const none = <p className="m-0 text-body text-ink-soft">{t('agents.roles.diagram.notShown')}</p>;
  if (id.startsWith('role:')) return <>{working ?? none}</>;
  if (id.startsWith('agent:')) {
    const row = structure.accounts.find((each) => each.id === id);
    return row ? <ul className="m-0 list-none p-0"><AccountRowView row={row} open={open.has(id)} onToggle={onToggle} doors={doors} /></ul> : none;
  }
  const node = structure.diagram.nodes.find((each) => each.id === id);
  const works = node?.kind === 'place' ? node.works : [];
  if (id === placeId({})) return <PlaceWorks title={t('agents.roles.appointed.machine')} works={works} />;
  const group = structure.repositories.find((each) => each.id === id);
  if (group) {
    return (
      <>
        {node?.kind === 'place' && node.own && <PlaceWorks title={group.workspace} works={works} />}
        <GroupView structure={structure} group={group} open={new Set([...open, id])} onToggle={onToggle} doors={doors} />
      </>
    );
  }
  const row = structure.repositories.flatMap((each) => each.repositories).find((each) => each.id === id);
  if (row) return <ul className="m-0 list-none p-0"><RepositoryRowView row={row} open={open.has(id)} onToggle={onToggle} doors={doors} /></ul>;
  return node?.kind === 'place' ? <PlaceWorks title={node.repository ?? node.workspace ?? id} works={works} /> : none;
}
