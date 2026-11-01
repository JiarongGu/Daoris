import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePlaces } from '../../bridge/shell';
import type { PlaceNeed, PlaceRow, PlacesAsk, PlacesRead, StructurePlaceAsk } from '../../data/wiring';
import { Button, CheckField, Icon } from '../../design/ui';
import { cn } from '../../lib/cn';
import { PageHead, ViewMain } from '../../work/frame/ViewMain';
import { MACHINE, repositoryId, workspaceId } from '../modes';
import type { SetUpPlace } from './placePlan';
import { type PlaceDriving, PlacePageView } from './PlacePage';
import type { SourceDoor } from './PlaceRoles';
import {
  firstSection, foldedLines, headOf, needOpening, type PlaceAt, type PlaceKind, type PlaceSection, type PlaceShell, sectionsOf,
} from './placeShell';

/** A page of driven repositories, and the step "Show more" takes. */
const PAGE = 50;

export { MACHINE, repositoryId, workspaceId };

/** The place a row stands for, as the list's chosen item names it. */
export const placeIdOf = (row: PlaceRow): string =>
  row.repository ? repositoryId(row.workspace, row.repository) : row.workspace ? workspaceId(row.workspace) : MACHINE;

export type SetupState = {
  search: string;
  onSearch: (search: string) => void;
  needsLook: boolean;
  onNeedsLook: (on: boolean) => void;
  /** The workspace whose not-driven repositories are open, and the page of them. */
  fold: string | null;
  onFold: (workspace: string | null) => void;
  /** The cursor of the open fold's page; none on its first. */
  foldAfter: string | null;
  onFoldAfter: (after: string | null) => void;
  onMore: () => void;
  read: PlacesRead | null;
  /** `loading` until the first answer; `failed` where the shell does not offer places or could not read them. */
  status: 'loading' | 'failed' | 'ok';
};

/**
 * The Setup mode's places as the view holds them: the search, the filter, the open fold and the page asked, and the driver's answer
 * (AGENTSPLACE1a). The last answer stays while the next is on its way, so a press does not blank the list.
 */
export function useSetupPlaces(enabled: boolean): SetupState {
  const [search, setSearch] = useState('');
  const [needsLook, setNeedsLook] = useState(false);
  const [fold, setFold] = useState<string | null>(null);
  const [foldAfter, setFoldAfter] = useState<string | null>(null);
  const [limit, setLimit] = useState(PAGE);
  const text = search.trim();
  const ask: PlacesAsk = {
    limit,
    ...(text ? { search: text, foldSearch: text } : {}),
    ...(needsLook ? { needsLook: true } : {}),
    ...(fold ? { fold, ...(foldAfter ? { foldAfter } : {}) } : {}),
  };
  const places = usePlaces(ask, { enabled });
  const last = useRef<PlacesRead | null>(null);
  if (places.data) last.current = places.data;
  const read = places.data ?? last.current;
  const status = read ? 'ok' : places.isError || (places.isSuccess && !places.data) ? 'failed' : 'loading';
  return {
    search,
    onSearch: setSearch,
    needsLook,
    onNeedsLook: setNeedsLook,
    fold,
    onFold: (workspace) => { setFold(workspace); setFoldAfter(null); },
    foldAfter,
    onFoldAfter: setFoldAfter,
    onMore: () => setLimit((now) => now + PAGE),
    read,
    status,
  };
}

const needsOf = (needs: readonly PlaceNeed[]) => needs.map((need) => need.code);

function PlaceButton({ chosen, onPress, depth = 0, children }: { chosen: boolean; onPress: () => void; depth?: 0 | 1; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-current={chosen || undefined}
      onClick={onPress}
      className={cn(
        'block w-full min-w-0 border-l-[3px] py-1.5 pr-2.5 text-left transition-colors duration-(--speed)',
        depth ? 'pl-6' : 'pl-2.5',
        'hover:bg-accent-soft/50',
        chosen ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
      )}
    >
      {children}
    </button>
  );
}

/**
 * **The Setup mode's list** (AGENTSPLACE1e, the Agents-place design §3): *This machine*, then each workspace with its driven
 * repositories, and the rest of a workspace's imported ones folded into one row that opens a paged read. A search and *Needs a look*
 * narrow the driven ones; the driver judges the reasons from its configuration alone.
 *
 * A molecule: the driver's answer arrives, and each press goes out.
 */
export function SetupList({ state, chosen, onChoose }: { state: SetupState; chosen: string | null; onChoose: (id: string) => void }) {
  const { t } = useTranslation();
  const { read } = state;
  const line = 'm-0 px-3 py-1.5 text-small text-ink-faint';
  const reasons = (needs: readonly PlaceNeed[]) => needsOf(needs).map((code) => t(`agents.setup.need.${code}`, { defaultValue: code })).join(' · ');
  return (
    <div>
      <div className="sticky top-0 z-10 grid gap-1.5 border-b border-line bg-page px-2 pb-2 pt-1.5">
        <div className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised py-0.5 pl-2 pr-0.5 text-ink-faint focus-within:border-accent">
          <Icon name="search" size={12} className="shrink-0" />
          <input
            type="search"
            value={state.search}
            aria-label={t('agents.setup.search')}
            placeholder={t('agents.setup.search')}
            onChange={(event) => state.onSearch(event.target.value)}
            className="min-h-6 min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
          />
        </div>
        <CheckField checked={state.needsLook} onChange={state.onNeedsLook} label={t('agents.setup.needsLook')} className="px-1 text-small" />
      </div>
      {state.status === 'loading' && <p className={line}>{t('agents.setup.loading')}</p>}
      {state.status === 'failed' && <p className={line}>{t('agents.setup.failed')}</p>}
      {read && (
        <ul className="m-0 list-none p-0" aria-label={t('agents.setup.places')}>
          <li data-list-row="">
            <PlaceButton chosen={chosen === MACHINE} onPress={() => onChoose(MACHINE)}>
              <span className="block truncate text-body font-medium text-ink">{t('agents.setup.machine')}</span>
              {read.machine.needs.length > 0 && <span className="block truncate text-small text-warn">{reasons(read.machine.needs)}</span>}
            </PlaceButton>
          </li>
          {read.workspaces.map(({ place, driven, notDriven }) => {
            const workspace = place.workspace ?? '';
            const own = read.repositories.filter((row) => row.workspace === workspace);
            const open = state.fold === workspace;
            return (
              <li key={workspace} role="presentation" className="m-0 p-0">
                <ul className="m-0 list-none p-0" aria-label={workspace}>
                  <li data-list-row="">
                    <PlaceButton chosen={chosen === workspaceId(workspace)} onPress={() => onChoose(workspaceId(workspace))}>
                      <span className="block truncate text-body font-medium text-ink">{workspace}</span>
                      <span className="block truncate text-small text-ink-soft">{t('agents.setup.drivenCount', { count: driven })}</span>
                      {place.needs.length > 0 && <span className="block truncate text-small text-warn">{reasons(place.needs)}</span>}
                    </PlaceButton>
                  </li>
                  {own.map((row) => (
                    <li key={row.repository} data-list-row="">
                      <PlaceButton depth={1} chosen={chosen === placeIdOf(row)} onPress={() => onChoose(placeIdOf(row))}>
                        <span className="block truncate text-body text-ink">{row.repository}</span>
                        {row.needs.length > 0 && <span className="block truncate text-small text-warn">{reasons(row.needs)}</span>}
                      </PlaceButton>
                    </li>
                  ))}
                  {notDriven > 0 && (
                    <li data-list-row="">
                      <button
                        type="button"
                        aria-expanded={open}
                        onClick={() => state.onFold(open ? null : workspace)}
                        className="block w-full min-w-0 py-1.5 pl-6 pr-2.5 text-left text-small text-ink-soft hover:bg-accent-soft/50"
                      >
                        {t('agents.setup.imported', { count: notDriven })}
                      </button>
                    </li>
                  )}
                  {open && read.undriven && read.undriven.workspace === workspace && (
                    <>
                      {read.undriven.rows.map((row) => (
                        <li key={row.repository} data-list-row="">
                          <PlaceButton depth={1} chosen={chosen === repositoryId(row.workspace, row.repository)} onPress={() => onChoose(repositoryId(row.workspace, row.repository))}>
                            <span className="block truncate text-body text-ink-soft">{row.repository}</span>
                          </PlaceButton>
                        </li>
                      ))}
                      <li className="flex items-center gap-1.5 py-1 pl-6 pr-2.5">
                        <span className="min-w-0 flex-1 truncate text-small text-ink-faint">
                          {t('agents.setup.page', { shown: read.undriven.rows.length, total: read.undriven.total })}
                        </span>
                        {state.foldAfter && (
                          <Button variant="ghost" onClick={() => state.onFoldAfter(null)}>{t('agents.setup.first')}</Button>
                        )}
                        {read.undriven.next && (
                          <Button variant="ghost" onClick={() => state.onFoldAfter(read.undriven!.next ?? null)}>{t('agents.setup.next')}</Button>
                        )}
                      </li>
                    </>
                  )}
                </ul>
              </li>
            );
          })}
        </ul>
      )}
      {read && read.repositories.length === 0 && (state.search || state.needsLook) && <p className={line}>{t('agents.setup.nothing')}</p>}
      {read?.next && (
        <div className="px-2 py-1.5">
          <Button variant="ghost" onClick={state.onMore}>{t('agents.setup.more')}</Button>
        </div>
      )}
    </div>
  );
}

type Found =
  | { kind: 'machine'; row: PlaceRow }
  | { kind: 'workspace'; row: PlaceRow; driven: number; notDriven: number }
  | { kind: 'repository'; row: PlaceRow }
  | { kind: 'imported'; workspace: string; repository: string; checkout: boolean };

/** The place a chosen id names, from the answer in hand; none where the answer no longer holds it. */
export function findPlace(read: PlacesRead | null, id: string | null): Found | null {
  if (!read || !id) return null;
  if (id === MACHINE) return { kind: 'machine', row: read.machine };
  for (const each of read.workspaces) {
    if (id === workspaceId(each.place.workspace ?? '')) return { kind: 'workspace', row: each.place, driven: each.driven, notDriven: each.notDriven };
  }
  const driven = read.repositories.find((row) => placeIdOf(row) === id);
  if (driven) return { kind: 'repository', row: driven };
  const imported = read.undriven?.rows.find((row) => repositoryId(row.workspace, row.repository) === id);
  return imported ? { kind: 'imported', ...imported } : null;
}

/** The row an imported repository is read as until its detail arrives: not driven, with what the registry says of its checkout. */
const importedRow = (found: Extract<Found, { kind: 'imported' }>): PlaceRow => ({
  workspace: found.workspace, repository: found.repository, driven: false, held: false, trees: false, checkout: found.checkout, adopted: false,
  works: [], needs: [],
});

/** The `PLACES` row of the place a chosen id names: its own, or for an imported repository its detail once read. */
export const rowOfFound = (found: Found | null, detail: PlaceRow | null): PlaceRow | null =>
  !found ? null : found.kind === 'imported' ? detail ?? importedRow(found) : found.row;

const SECTION_TITLE: Record<PlaceSection, string> = {
  workflow: 'agents.setup.workflow.title', roles: 'agents.setup.roles.title', sessions: 'agents.setup.sessions.title',
  allRoles: 'agents.setup.allRoles.title',
};

/**
 * **A place's page** (SETUPUX1h, the setup-UX design §2.1): its head and three sections folded to the line that names their values, from
 * the place's `PLACES` row alone. The page's state (the sections open, the row a door asked for, the way back) is the application's
 * (`PlaceShell`); the sections' bodies are handed in, and a folded section mounts none, so it reads nothing.
 *
 * A molecule: the driver's answer arrives, and each press goes out.
 */
export function SetupMain({ found, state, id, row, counts, members, shell, bodies, notes, drive, setUp, onGo }: {
  found: Found | null;
  state: SetupState['status'];
  /** The chosen place's id, which the shell's state is held under. */
  id: string | null;
  /** The place's `PLACES` row; for an imported repository, its detail once read. */
  row: PlaceRow | null;
  /** A workspace's driven and not driven repositories, and those of its repositories in hand. */
  counts?: { driven: number; notDriven: number };
  members?: readonly PlaceRow[];
  shell: PlaceShell;
  /** Each section's body, an organism of the application's: Workflow and Sessions press its hooks, Roles reads the driver. */
  bodies: Partial<Record<PlaceSection, ReactNode>>;
  /** What waits on the person between the head and the sections: This machine's suggested choices and found changes. */
  notes?: ReactNode;
  /** A repository's driving for its head: the organism holds the hooks, and renders the page with what it made. */
  drive?: (row: PlaceRow, render: (driving: PlaceDriving) => ReactNode) => ReactNode;
  /**
   * The place's *Set up…* (AGENTSPLACE1j), pressed from its head: open, the page replaces the place's sections in the main area, and the
   * panel is the application's, which reads the plan and presses Apply. None for *This machine*, which has no set-up.
   */
  setUp?: { open: boolean; onOpen: () => void; panel: ReactNode };
  /** A door to another place at a section and row; the page names itself as the way back. */
  onGo: (target: string, at: PlaceAt) => void;
}) {
  const { t, i18n } = useTranslation();
  if (!found || !id || !row) {
    return (
      <ViewMain state={state === 'loading' ? 'loading' : 'none'} none={{ headline: t('agents.setup.none.headline'), body: t('agents.setup.none.body') }}>
        {null}
      </ViewMain>
    );
  }
  const kind: PlaceKind = found.kind === 'imported' ? 'repository' : found.kind;
  const name = kind === 'machine' ? t('agents.setup.machine') : kind === 'workspace' ? row.workspace ?? '' : row.repository ?? '';
  const offersSetUp = setUp !== undefined && kind !== 'machine';
  if (offersSetUp && setUp.open) {
    return <ViewMain header={<PageHead title={name} line={kind === 'repository' ? row.workspace ?? undefined : undefined} />}>{setUp.panel}</ViewMain>;
  }

  const first = firstSection(kind);
  const need = needOpening(row);
  const lines = foldedLines(row, kind, t, i18n.language);
  const facts = headOf({ kind, row, counts, members }, t);
  const back = shell.backTo(id);
  const from = (section: PlaceSection) => ({ place: id, name, section });
  const sections = sectionsOf(kind).map((section) => ({
    id: section,
    title: t(SECTION_TITLE[section]),
    summary: lines[section],
    open: shell.isOpen(id, section, section === first || need?.section === section),
    onOpenChange: (open: boolean) => shell.setOpen(id, section, open),
    body: bodies[section] ?? null,
  }));
  const workspace = row.workspace;
  const page = (driving: PlaceDriving | null) => (
    <PlacePageView
      head={{
        title: name,
        pill: facts.pill,
        facts: [...facts.facts, ...row.needs.map((each) => `⚠ ${t(`agents.setup.need.${each.code}`, { defaultValue: each.code })}${each.detail ? `: ${each.detail}` : ''}`)],
        parent: kind === 'repository' && workspace ? { name: workspace, onOpen: () => onGo(workspaceId(workspace), { section: firstSection('workspace'), back: from(first) }) } : undefined,
        setUp: offersSetUp ? { onOpen: setUp.onOpen } : undefined,
      }}
      sections={sections}
      notes={notes}
      driving={driving}
      back={back ? { place: back.name, section: back.section ? t(SECTION_TITLE[back.section]) : '', onBack: () => { shell.clearBack(); onGo(back.place, { section: back.section ?? undefined }); } } : null}
      row={shell.rowOf(id) ?? need?.row ?? null}
      onRowBrought={() => shell.rowBrought(id)}
      focusAsked={shell.focusAsked}
    />
  );
  return <ViewMain>{drive && kind === 'repository' ? drive(row, page) : page(null)}</ViewMain>;
}

/** A door from a role's source mark: the place that sets it, at the work's row of Roles, with the way back to this page. */
export function sourceDoor(
  from: { id: string; name: string }, workspace: string | null | undefined, onGo: (target: string, at: PlaceAt) => void,
): SourceDoor {
  return (scope, work) => onGo(scope.kind === 'workspace' ? workspaceId(scope.id ?? workspace ?? '') : MACHINE, {
    section: 'roles', row: work, back: { place: from.id, name: from.name, section: 'roles' },
  });
}

/** The place a *Set up…* is for, from the chosen id and the answer in hand; none for *This machine*, which has no set-up. */
export function setUpPlaceOf(found: Found | null, id: string | null, read: PlacesRead | null): SetUpPlace | null {
  if (!found || !id || found.kind === 'machine') return null;
  if (found.kind === 'workspace') {
    const workspace = found.row.workspace ?? '';
    return { id, kind: 'workspace', name: workspace, workspace, driven: (read?.repositories ?? []).filter((row) => row.workspace === workspace).map((row) => row.repository ?? '') };
  }
  return found.kind === 'repository'
    ? { id, kind: 'repository', name: found.row.repository ?? '', workspace: found.row.workspace ?? '', driven: [] }
    : { id, kind: 'repository', name: found.repository, workspace: found.workspace, driven: [] };
}

/** The place whose roles are asked: this machine's (neither), a workspace's, or a driven repository's with the workspace it sits in. */
export const placeAsk = (found: Exclude<Found, { kind: 'imported' }>): StructurePlaceAsk =>
  found.kind === 'machine' ? {}
    : found.kind === 'workspace' ? { workspace: found.row.workspace ?? '' }
      : { ...(found.row.workspace ? { workspace: found.row.workspace } : {}), repository: found.row.repository ?? '' };
